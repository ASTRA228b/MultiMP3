using System.IO;
namespace MultiMP3.Services;
public record Dependencies(string? YtDlp, string? FFmpeg, string? FFprobe, string? Node)
{
    public bool Ready => YtDlp is not null && FFmpeg is not null && FFprobe is not null && Node is not null;
    public string Message => Ready ? "Download engine ready · yt-dlp + FFmpeg + Node.js" : "Setup needed: " + string.Join(", ", new[] { YtDlp is null ? "yt-dlp.exe" : null, FFmpeg is null ? "ffmpeg.exe" : null, FFprobe is null ? "ffprobe.exe" : null, Node is null ? "Node.js 22+" : null }.Where(x => x is not null)) + ". Open Settings for setup instructions.";
    public static Dependencies Find()
    {
        string? FindExe(string name)
        {
            var dirs = new[] { Path.Combine(AppContext.BaseDirectory, "tools"), AppContext.BaseDirectory }.Concat((Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator));
            return dirs.Select(d => Path.Combine(d, name + ".exe")).FirstOrDefault(File.Exists);
        }
        return new(FindExe("yt-dlp"), FindExe("ffmpeg"), FindExe("ffprobe"), FindExe("node"));
    }
    public async Task Validate(CancellationToken token)
    {
        if (!Ready) throw new InvalidOperationException(Message);
        await Task.WhenAll(ProcessRunner.Run(YtDlp!, ["--version"], null, token), ProcessRunner.Run(FFmpeg!, ["-version"], null, token), ProcessRunner.Run(FFprobe!, ["-version"], null, token));
        var version = (await ProcessRunner.Run(Node!, ["--version"], null, token)).Trim().TrimStart('v');
        if (!Version.TryParse(version, out var nodeVersion) || nodeVersion.Major < 22) throw new InvalidOperationException("Node.js 22 or newer is required. Run Setup-Engine.ps1 to install the bundled runtime.");
    }
}
