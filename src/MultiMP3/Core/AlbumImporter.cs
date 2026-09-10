using MultiMP3.Services;
namespace MultiMP3.Core;
public record AlbumImportResult(Album Album, int Added, int Skipped);
public static class AlbumImporter
{
    public static int DuplicateCount(ProjectState state, AlbumInfo info)
    {
        var known = state.Tracks.Select(t => t.Url).Concat(state.History.Select(h => h.Url)).ToHashSet(StringComparer.Ordinal);
        return info.Songs.Count(song => !known.Add(song.Url));
    }
    public static AlbumImportResult Add(ProjectState state, AlbumInfo info, string quality, bool allowDuplicates)
    {
        var album = state.Albums.FirstOrDefault(a => a.SourceUrl == info.SourceUrl);
        if (album is null)
        {
            var name = FileManager.SafeName(info.Title); var candidate = name; var suffix = 2;
            while (state.Albums.Any(a => a.Name.Equals(candidate, StringComparison.OrdinalIgnoreCase))) candidate = name[..Math.Min(name.Length, 85)] + $" ({suffix++})";
            album = new() { Name = candidate, SourceUrl = info.SourceUrl };
        }
        var destination = FileManager.AlbumFolder(state.Settings.DownloadFolder, album);
        if (!state.Albums.Contains(album)) state.Albums.Add(album);
        var known = state.Tracks.Select(t => t.Url).Concat(state.History.Select(h => h.Url)).ToHashSet(StringComparer.Ordinal);
        var added = 0; var skipped = 0;
        foreach (var song in info.Songs)
        {
            if (!known.Add(song.Url) && !allowDuplicates) { skipped++; continue; }
            state.Tracks.Add(new() { Url = song.Url, Title = song.Title, Artist = song.Artist, Thumbnail = song.Thumbnail, AlbumId = album.Id, Quality = quality, Destination = destination });
            added++;
        }
        foreach (var warning in info.Warnings) state.Errors.Insert(0, new(DateTime.Now, info.Title, info.SourceUrl, "Collection import notice — see details.", warning));
        return new(album, added, skipped);
    }
}
