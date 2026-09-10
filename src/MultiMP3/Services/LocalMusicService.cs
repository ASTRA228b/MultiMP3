using System.IO;
using System.Text.Json;
using MultiMP3.Core;
namespace MultiMP3.Services;

public record LocalSong(string Path, string Title, string Artist, string Album, string AlbumArtist, int TrackNumber);

public class LocalMusicService(string? ffprobe)
{
    public async Task<LocalSong> Inspect(string path, CancellationToken token)
    {
        path = System.IO.Path.GetFullPath(path);
        if (!System.IO.Path.GetExtension(path).Equals(".mp3", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Choose an MP3 file downloaded from your purchased music library.");
        if (!File.Exists(path)) throw new FileNotFoundException("The source MP3 is no longer available.", path);
        if (ffprobe is null) throw new InvalidOperationException("FFprobe is missing. Run Setup-Engine.ps1 or recheck dependencies in Settings.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token); timeout.CancelAfter(TimeSpan.FromSeconds(30));
        var json = await ProcessRunner.Run(ffprobe, ["-v", "error", "-show_entries", "stream=codec_name,codec_type:format_tags", "-of", "json", path], null, timeout.Token);
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("streams", out var streams) || !streams.EnumerateArray().Any(s => s.TryGetProperty("codec_name", out var codec) && codec.GetString() == "mp3")) throw new InvalidDataException("This file does not contain MP3 audio.");
        var tags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (doc.RootElement.TryGetProperty("format", out var format) && format.TryGetProperty("tags", out var metadata))
            foreach (var tag in metadata.EnumerateObject()) if (tag.Value.ValueKind == JsonValueKind.String) tags[tag.Name] = tag.Value.GetString() ?? "";
        string Get(string name) => tags.GetValueOrDefault(name, "").Trim();
        var title = Get("title"); if (title.Length == 0) title = System.IO.Path.GetFileNameWithoutExtension(path);
        int.TryParse(Get("track").Split('/')[0], out var number);
        return new(path, title, Get("artist"), Get("album"), Get("album_artist"), number);
    }
    public async Task ReadInfo(Track track, CancellationToken token)
    {
        var song = await Inspect(track.LocalSourcePath!, token);
        track.Title = song.Title; track.Artist = song.Artist;
    }
    public static async Task<string> Copy(Track track, Album? album, Settings settings, Action<double, string> progress, CancellationToken token)
    {
        var folder = FileManager.AlbumFolder(settings.DownloadFolder, album);
        Directory.CreateDirectory(folder);
        var temp = System.IO.Path.Combine(folder, ".multimp3-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            await using (var input = new FileStream(track.LocalSourcePath!, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true))
            await using (var output = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true))
            {
                var buffer = new byte[81920]; long copied = 0; int read;
                while ((read = await input.ReadAsync(buffer, token)) > 0)
                {
                    await output.WriteAsync(buffer.AsMemory(0, read), token); copied += read;
                    progress(input.Length == 0 ? 0 : copied * 99d / input.Length, "Copying");
                }
                await output.FlushAsync(token);
            }
            token.ThrowIfCancellationRequested();
            return FileManager.CommitUnique(temp, folder, track.Title);
        }
        finally { try { File.Delete(temp); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
    }
}
