using MultiMP3.Services;
namespace MultiMP3.Core;
public static class LocalMusicImporter
{
    public static bool IsDuplicate(ProjectState state, string path)
    {
        var uri = new Uri(System.IO.Path.GetFullPath(path)).AbsoluteUri;
        return state.Tracks.Any(t => t.Url.Equals(uri, StringComparison.OrdinalIgnoreCase)) || state.History.Any(h => h.Url.Equals(uri, StringComparison.OrdinalIgnoreCase));
    }
    public static Track Add(ProjectState state, LocalSong song, bool groupByAlbum, Album? chosenAlbum)
    {
        var album = chosenAlbum;
        if (groupByAlbum)
        {
            var title = string.IsNullOrWhiteSpace(song.Album) ? "Amazon Purchases" : song.Album;
            var artist = string.IsNullOrWhiteSpace(song.AlbumArtist) ? song.Artist : song.AlbumArtist;
            var identity = "amazon-purchases:" + Uri.EscapeDataString(artist) + "/" + Uri.EscapeDataString(title);
            album = state.Albums.FirstOrDefault(a => a.SourceUrl == identity);
            if (album is null)
            {
                var name = FileManager.SafeName(title); var candidate = name; var suffix = 2;
                while (state.Albums.Any(a => a.Name.Equals(candidate, StringComparison.OrdinalIgnoreCase))) candidate = name[..Math.Min(name.Length, 85)] + $" ({suffix++})";
                album = new() { Name = candidate, SourceUrl = identity }; state.Albums.Add(album);
            }
        }
        var track = new Track { LocalSourcePath = song.Path, Url = new Uri(song.Path).AbsoluteUri, Title = song.Title, Artist = song.Artist, AlbumId = album?.Id, Quality = "Original MP3", Destination = FileManager.AlbumFolder(state.Settings.DownloadFolder, album) };
        state.Tracks.Add(track); return track;
    }
}
