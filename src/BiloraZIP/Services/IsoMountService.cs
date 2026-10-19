using System.Diagnostics;
using System.IO;
using System.Text;

namespace BiloraZIP.Services;

/// <summary>Uses Windows' built-in, read-only ISO virtual drive support. Never executes ISO contents.</summary>
public static class IsoMountService
{
    public static async Task<string> MountAsync(string path, CancellationToken token = default)
        => await RunAsync(path, true, token);

    public static async Task<string> UnmountAsync(string path, CancellationToken token = default)
        => await RunAsync(path, false, token);

    private static async Task<string> RunAsync(string path, bool mount, CancellationToken token)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("ISO mounting requires Windows.");
        string full = Path.GetFullPath(path);
        if (!Path.GetExtension(full).Equals(".iso", StringComparison.OrdinalIgnoreCase))
            throw new NotSupportedException("Only .iso images are supported by this command.");
        if (!File.Exists(full)) throw new FileNotFoundException("ISO image not found.", full);
        // A single-quoted PowerShell literal; embedded single quotes are doubled.
        string quoted = "'" + full.Replace("'", "''") + "'";
        string command = mount
            ? $"$ErrorActionPreference='Stop'; $image=Mount-DiskImage -ImagePath {quoted} -StorageType ISO -Access ReadOnly -PassThru; ($image | Get-Volume | Select-Object -ExpandProperty DriveLetter)"
            : $"$ErrorActionPreference='Stop'; Dismount-DiskImage -ImagePath {quoted} -ErrorAction Stop; 'Unmounted'";
        var start = new ProcessStartInfo
        {
            FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),
                "WindowsPowerShell", "v1.0", "powershell.exe"),
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        start.ArgumentList.Add("-NoProfile");
        start.ArgumentList.Add("-NonInteractive");
        start.ArgumentList.Add("-Command");
        start.ArgumentList.Add(command);
        using var process = Process.Start(start) ?? throw new IOException("Unable to start Windows ISO mount service.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(90));
        Task<string> stdout = process.StandardOutput.ReadToEndAsync(timeout.Token);
        Task<string> stderr = process.StandardError.ReadToEndAsync(timeout.Token);
        try { await process.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException)
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            throw;
        }
        string output = (await stdout).Trim();
        string error = (await stderr).Trim();
        if (process.ExitCode != 0) throw new IOException("Windows ISO operation failed: " + error);
        return output;
    }
}
