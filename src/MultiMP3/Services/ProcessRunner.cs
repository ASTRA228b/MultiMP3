using System.Diagnostics;
using System.Text;
namespace MultiMP3.Services;
public static class ProcessRunner
{
    public static async Task<string> Run(string executable, IEnumerable<string> args, Action<string>? output, CancellationToken token)
    {
        var start = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in args) start.ArgumentList.Add(arg);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start " + executable);
        using var registration = token.Register(() => { try { process.Kill(true); } catch (InvalidOperationException) { } });
        var log = new StringBuilder();
        async Task Read(System.IO.StreamReader reader)
        {
            while (await reader.ReadLineAsync() is { } line)
            {
                lock (log) { if (log.Length < 2000000) log.AppendLine(line); }
                output?.Invoke(line);
            }
        }
        await Task.WhenAll(Read(process.StandardOutput), Read(process.StandardError), process.WaitForExitAsync());
        token.ThrowIfCancellationRequested();
        if (process.ExitCode != 0) throw new InvalidOperationException(log.ToString().Trim());
        return log.ToString();
    }
}
