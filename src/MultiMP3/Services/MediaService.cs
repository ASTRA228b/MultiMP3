using System.IO;
using System.Text.Json;
using System.Globalization;
using System.Net.Http;
using MultiMP3.Core;
namespace MultiMP3.Services;
public interface IMediaService
{
    Task ReadInfo(Track track, CancellationToken token);
    Task<string> Download(Track track, Album? album, Settings settings, Action<double, string> progress, CancellationToken token);
}
public class MediaService(Dependencies dependencies) : IMediaService
{
    private string Executable => dependencies.YtDlp ?? throw new InvalidOperationException(dependencies.Message);
    private string[] Common => ["--ignore-config", "--no-playlist", "--no-colors", "--js-runtimes", "node:" + dependencies.Node, "--socket-timeout", "20", "--retries", "2", "--extractor-retries", "1"];
    public async Task ReadInfo(Track track, CancellationToken token)
    {
        if (track.IsLocal) { await new LocalMusicService(dependencies.FFprobe).ReadInfo(track, token); return; }
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromMinutes(2));
        string? json = null;
        await ProcessRunner.Run(Executable, Common.Concat(["--skip-download", "--dump-single-json", "--", track.Url]), line => { if (line.StartsWith('{')) json = line; }, timeout.Token);
        using var doc = JsonDocument.Parse(json ?? throw new InvalidOperationException("No media information was returned."));
        var root = doc.RootElement;
        string Get(string name) => root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : "";
        if (root.TryGetProperty("is_live", out var live) && live.ValueKind == JsonValueKind.True) throw new InvalidOperationException("Live broadcasts are not supported. Try the recording after the stream ends.");
        if (Get("availability") is "private" or "premium_only" or "subscriber_only" or "needs_auth") throw new InvalidOperationException("This media requires access or authentication and cannot be downloaded.");
        track.Title = Get("title") is { Length: > 0 } title ? title : "Untitled";
        track.Artist = Get("uploader");
        var thumbnail = Get("thumbnail");
        if (Uri.TryCreate(thumbnail, UriKind.Absolute, out var thumb) && thumb.Scheme == "https") track.Thumbnail = thumbnail;
    }
    public async Task<string> Download(Track track, Album? album, Settings settings, Action<double, string> progress, CancellationToken token)
    {
        if (track.IsLocal) return await LocalMusicService.Copy(track, album, settings, progress, token);
        var folder = FileManager.AlbumFolder(settings.DownloadFolder, album);
        Directory.CreateDirectory(folder);
        var staging = Path.Combine(folder, ".multimp3-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staging);
        try
        {
            var args = Common.Concat(new[] { "--no-overwrites", "--newline", "--progress", "--progress-template", "download:MP3PROGRESS:%(progress._percent_str)s", "-f", "bestaudio/best", "-o", Path.Combine(staging, "audio.%(ext)s"), "--ffmpeg-location", Path.GetDirectoryName(dependencies.FFmpeg!)!, "--" , track.Url });
            await ProcessRunner.Run(Executable, args, line =>
            {
                if (line.StartsWith("MP3PROGRESS:") && double.TryParse(line[12..].Trim().TrimEnd('%'), NumberStyles.Float, CultureInfo.InvariantCulture, out var pct)) progress(pct * .85, "Downloading");
            }, token);
            var input = Directory.EnumerateFiles(staging, "audio.*").FirstOrDefault(p => !p.EndsWith(".part") && !p.EndsWith(".ytdl")) ?? throw new IOException("Download completed without a media file.");
            string? cover = null;
            if (settings.EmbedThumbnail && track.Thumbnail.Length > 0)
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
                using var response = await client.GetAsync(track.Thumbnail, HttpCompletionOption.ResponseHeadersRead, token);
                response.EnsureSuccessStatusCode();
                await using var stream = await response.Content.ReadAsStreamAsync(token);
                cover = Path.Combine(staging, "cover.image");
                await using var file = File.Create(cover);
                var buffer = new byte[8192]; var total = 0; int read;
                while ((read = await stream.ReadAsync(buffer, token)) > 0) { total += read; if (total > 10_000_000) throw new IOException("Cover image exceeds 10 MB."); await file.WriteAsync(buffer.AsMemory(0, read), token); }
            }
            progress(88, "Converting");
            var output = Path.Combine(staging, "converted.mp3");
            await AudioConverter.Convert(dependencies.FFmpeg!, input, output, cover, track, album, settings, token);
            token.ThrowIfCancellationRequested();
            return FileManager.CommitUnique(output, folder, track.Title);
        }
        finally { try { Directory.Delete(staging, true); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
    }
}
