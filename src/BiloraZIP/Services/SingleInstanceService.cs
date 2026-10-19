using System.IO;
using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace BiloraZIP.Services;

// One persistent GUI per Windows logon. Secondary launches forward their
// arguments over a current-user-only pipe instead of creating another tray icon.
internal static class SingleInstanceService
{
    private static readonly string Identity = Convert.ToHexString(
        SHA256.HashData(Encoding.UTF8.GetBytes(Environment.UserDomainName + "\\" + Environment.UserName)))[..16];
    public static readonly string MutexName = @"Local\BiloraZIP_Main_093_" + Identity;
    private static readonly string PipeName = "BiloraZIP_Main_093_" + Identity;
    private const int MaxMessageBytes = 64 * 1024;

    public static async Task<bool> ForwardAsync(string[] args)
    {
        byte[] content = JsonSerializer.SerializeToUtf8Bytes(args);
        if (content.Length > MaxMessageBytes) return false;
        for (int attempt = 0; attempt < 15; attempt++)
        {
            try
            {
                using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out,
                    PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await client.ConnectAsync(350);
                // length-prefixed to avoid ambiguity in arbitrary Windows paths
                byte[] length = BitConverter.GetBytes(content.Length);
                await client.WriteAsync(length);
                await client.WriteAsync(content);
                await client.FlushAsync();
                return true;
            }
            catch (Exception ex) when (ex is TimeoutException or IOException)
            {
                await Task.Delay(120);
            }
        }
        return false;
    }

    public static async Task ListenAsync(Action<string[]> onMessage, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                using var server = new NamedPipeServerStream(PipeName, PipeDirection.In, 1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await server.WaitForConnectionAsync(token);
                byte[] lenBytes = new byte[4];
                await server.ReadExactlyAsync(lenBytes, token);
                int size = BitConverter.ToInt32(lenBytes, 0);
                if (size is < 2 or > MaxMessageBytes) continue;
                byte[] payload = new byte[size];
                await server.ReadExactlyAsync(payload, token);
                string[]? args = JsonSerializer.Deserialize<string[]>(payload);
                if (args is not null && args.Length <= 4096 && args.All(a => a is not null && a.Length < 32768))
                    onMessage(args);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { break; }
            catch (Exception ex) when (ex is IOException or JsonException or EndOfStreamException)
            {
                if (!token.IsCancellationRequested) await Task.Delay(100, token);
            }
        }
    }
}
