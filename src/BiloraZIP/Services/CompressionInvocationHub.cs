using System.IO;
using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace BiloraZIP.Services;

// Explorer's classic %1 verb launches one process per selected entry. Merge those
// invocations into one 1.5-second burst before showing a single options window.
// The pipe is session/user-scoped, short-lived and only transfers path strings.
internal static class CompressionInvocationHub
{
    private static readonly string Id = Convert.ToHexString(
        SHA256.HashData(Encoding.UTF8.GetBytes(Environment.UserDomainName + "\\" + Environment.UserName)))[..16];
    private static readonly string PipeName = "BiloraZIP_Compress_091_" + Id;
    private static readonly string MutexName = @"Local\BiloraZIP_Compress_091_" + Id;

    // null means the request was delivered to the collector and this helper process may exit.
    public static async Task<string[]?> CollectAsync(IReadOnlyList<string> inputs)
    {
        if (inputs.Count == 0) return [];
        using var group = new Mutex(false, MutexName, out bool first);
        if (!first)
        {
            for (int retry = 0; retry < 4; retry++)
            {
                try
                {
                    using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out,
                        PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                    await client.ConnectAsync(1200);
                    byte[] payload = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(inputs));
                    if (payload.Length > 60_000) break;
                    await client.WriteAsync(payload);
                    await client.FlushAsync();
                    return null;
                }
                catch (TimeoutException) { await Task.Delay(125); }
                catch (IOException) { await Task.Delay(125); }
            }
            // IPC not available: opening just this selection is preferable to dropping a file.
            return inputs.ToArray();
        }
        var collected = new HashSet<string>(inputs, StringComparer.OrdinalIgnoreCase);
        using var timeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(1550));
        while (!timeout.IsCancellationRequested)
        {
            try
            {
                using var server = new NamedPipeServerStream(PipeName, PipeDirection.In, 1,
                    PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await server.WaitForConnectionAsync(timeout.Token);
                using var data = new MemoryStream();
                var chunk = new byte[4096];
                while (data.Length < 60_000)
                {
                    int n = await server.ReadAsync(chunk, timeout.Token);
                    if (n == 0) break;
                    data.Write(chunk, 0, n);
                }
                if (data.Length >= 60_000) continue;
                string[]? paths = JsonSerializer.Deserialize<string[]>(data.ToArray());
                if (paths is not null)
                    foreach (string path in paths) if (!string.IsNullOrWhiteSpace(path)) collected.Add(path);
            }
            catch (OperationCanceledException) { break; }
            catch (IOException) { if (!timeout.IsCancellationRequested) await Task.Delay(20); }
        }
        return collected.ToArray();
    }
}
