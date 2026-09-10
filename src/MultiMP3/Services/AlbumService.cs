using System.IO;
using System.Text.Json;
using MultiMP3.Core;
namespace MultiMP3.Services;

public record AlbumSong(string Url, string Title, string Artist, string Thumbnail);
public record AlbumInfo(string SourceUrl, string Title, IReadOnlyList<AlbumSong> Songs, IReadOnlyList<string> Warnings);

public class AlbumService(Dependencies dependencies)
{
    public async Task<AlbumInfo> Read(string link, CancellationToken token)
    {
        if (!FileManager.TryCollectionUrl(link, out var url)) throw new ArgumentException("Use a public YouTube album or playlist share link.");
        if (dependencies.YtDlp is null) throw new InvalidOperationException(dependencies.Message);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromMinutes(10));
        string? json = null;
        var warnings = new List<string>();
        await ProcessRunner.Run(dependencies.YtDlp,
            ["--ignore-config", "--yes-playlist", "--flat-playlist", "--skip-download", "--dump-single-json", "--ignore-errors", "--no-colors", "--socket-timeout", "20", "--retries", "2", "--extractor-retries", "1", "--", url],
            line => { if (line.StartsWith('{')) json = line; else if ((line.StartsWith("WARNING:") || line.StartsWith("ERROR:")) && !line.Contains("YouTube Music is not directly supported. Redirecting to")) { lock (warnings) warnings.Add(line); } }, timeout.Token);
        token.ThrowIfCancellationRequested();
        var info = Parse(json ?? throw new InvalidDataException("The album or playlist could not be read. It may be unavailable or require access."), url);
        return info with { Warnings = info.Warnings.Concat(warnings).ToArray() };
    }

    public static AlbumInfo Parse(string json, string sourceUrl)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (!root.TryGetProperty("entries", out var entries) || entries.ValueKind != JsonValueKind.Array) throw new InvalidDataException("This link did not return an album or playlist track listing.");
        static string Get(JsonElement item, string key) => item.ValueKind == JsonValueKind.Object && item.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : "";
        var title = Get(root, "title");
        var isAlbum = FileManager.TryAlbumUrl(sourceUrl, out _) || FileManager.TryAlbumUrl("https://music.youtube.com/playlist?list=" + Get(root, "id"), out _);
        if (isAlbum && title.StartsWith("Album - ", StringComparison.OrdinalIgnoreCase)) title = title[8..];
        if (string.IsNullOrWhiteSpace(title)) title = isAlbum ? "YouTube Music Album" : "YouTube Playlist";
        // Resolved playlist identity makes browse and share links refer to the same saved album.
        if (FileManager.TryCollectionUrl("https://music.youtube.com/playlist?list=" + Get(root, "id"), out var resolved)) sourceUrl = resolved;
        var songs = new List<AlbumSong>(); var warnings = new List<string>(); var index = 0;
        foreach (var item in entries.EnumerateArray())
        {
            index++;
            var id = Get(item, "id");
            if (!FileManager.TryUrl("https://www.youtube.com/watch?v=" + id, out var url) && !FileManager.TryUrl(Get(item, "url"), out url))
            { warnings.Add($"Track {index} has no available video link and was not imported."); continue; }
            var songTitle = Get(item, "title");
            if (string.IsNullOrWhiteSpace(songTitle)) songTitle = $"Track {index}";
            var thumbnail = "";
            if (item.TryGetProperty("thumbnails", out var thumbs) && thumbs.ValueKind == JsonValueKind.Array)
                thumbnail = thumbs.EnumerateArray().Select(t => Get(t, "url")).LastOrDefault(u => Uri.TryCreate(u, UriKind.Absolute, out var image) && image.Scheme == "https") ?? "";
            songs.Add(new(url, songTitle, Get(item, "uploader"), thumbnail));
        }
        if (songs.Count == 0) throw new InvalidDataException("No tracks are available in this album or playlist. It may be empty, private, or unavailable.");
        return new(sourceUrl, title, songs, warnings);
    }
}
