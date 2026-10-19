using System.Diagnostics;
using System.IO;
using System.Text;

namespace BiloraZIP.Services;

// This invokes the platform antivirus; it is not an antivirus engine.
public static class DefenderScanService
{
    public static async Task ScanAsync(string file)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Windows only.");
        if (!File.Exists(file)) throw new FileNotFoundException("File not found.", file);
        string safePath = file.Replace("'", "''");
        string script = "$ErrorActionPreference='Stop'; Import-Module Defender -ErrorAction Stop; Start-MpScan -ScanType CustomScan -ScanPath '" + safePath + "' -ErrorAction Stop";
        string encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
        var info = new ProcessStartInfo("powershell.exe")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true
        };
        info.ArgumentList.Add("-NoProfile");
        info.ArgumentList.Add("-NonInteractive");
        info.ArgumentList.Add("-EncodedCommand");
        info.ArgumentList.Add(encoded);
        using var process = Process.Start(info) ?? throw new IOException("Could not start Windows PowerShell.");
        Task<string> errors = process.StandardError.ReadToEndAsync();
        Task<string> output = process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();
        if (process.ExitCode != 0)
            throw new InvalidOperationException("Microsoft Defender scan could not complete: " + await errors);
        _ = await output;
    }
}
