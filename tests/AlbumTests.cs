using MultiMP3.Core;
using MultiMP3.Services;
public static class AlbumTests
{
    public static void Run(Action<bool, string> check, string root)
    {
        const string share = "https://music.youtube.com/playlist?list=OLAK5uy_exampleAlbum&si=example";
        check(FileManager.TryAlbumUrl(share, out var canonical) && !canonical.Contains("&si="), "Recognize user album share link and remove tracking parameters");
        check(FileManager.TryAlbumUrl("https://music.youtube.com/browse/MPREb_gTAcphH99wE?si=test", out _), "Recognize album browse links");
        const string playlistLink = "https://music.youtube.com/playlist?list=PLexamplePlaylist&si=example";
        check(FileManager.TryCollectionUrl(playlistLink, out var playlistUrl) && playlistUrl.EndsWith("PLexamplePlaylist"), "Recognize playlist and remove share tracking");
        check(FileManager.TryCollectionUrl(playlistLink.Replace("music.youtube.com", "www.youtube.com"), out var desktopUrl) && desktopUrl == playlistUrl, "Normalize YouTube and Music playlist aliases to the same identity");
        check(!FileManager.TryCollectionUrl("https://music.youtube.com/watch?v=abcdefghijk&list=PLtest", out _) && !FileManager.TryCollectionUrl("https://music.youtube.com/playlist?list=RDAMVMabcdefghijk", out _) && !FileManager.TryCollectionUrl("https://music.youtube.com.evil.test/playlist?list=PLtest", out _), "Keep single song links, infinite radio mixes and spoofed hosts out of playlist imports");
        var playlistJson = System.Text.Json.JsonSerializer.Serialize(new { id = "PLexamplePlaylist", title = "Album - My playlist", entries = Enumerable.Range(1, 135).Select(i => new { id = i.ToString("D11"), title = "Song " + i }).ToArray() });
        var playlist = AlbumService.Parse(playlistJson, playlistUrl);
        check(playlist.Songs.Count == 135 && playlist.Songs.Last().Title == "Song 135" && playlist.Title == "Album - My playlist", "Preserve a long playlist and its literal title without truncation");
        var playlistState = new ProjectState(); playlistState.Settings.DownloadFolder = root;
        var playlistImport = AlbumImporter.Add(playlistState, playlist, "192 kbps", false);
        check(playlistImport.Added == 135 && playlistImport.Album.Kind == "Playlist" && playlistState.Tracks.All(t => t.AlbumId == playlistImport.Album.Id), "Import every playlist track into one named collection");
        check(AlbumImporter.Add(playlistState, playlist, "192 kbps", false).Skipped == 135, "Reimport a playlist without duplicate downloads");
        foreach (var invalid in new[] { "https://music.youtube.com.evil.test/playlist?list=OLAK5uy_test", "https://music.youtube.com/browse/UCartist", "https://music.youtube.com/playlist?list=RDAMVMabcdefghijk", "https://music.youtube.com/watch?v=abcdefghijk&list=OLAK5uy_test", "https://user:pass@music.youtube.com/playlist?list=OLAK5uy_test" })
            check(!FileManager.TryAlbumUrl(invalid, out _), "Do not expand non-album or spoofed links");
        const string json = """
        {"id":"OLAK5uy_test","title":"Album - Night: Drive","entries":[
        {"id":"abcdefghijk","title":"First song","uploader":"Artist","thumbnails":[{"url":"https://example.org/image.jpg"}]},
        null,{"id":"lmnopqrstuv","title":"[Private video]"},
        {"id":"12345678901","title":"Last song"}]}
        """;
        var info = AlbumService.Parse(json, canonical);
        check(info.Title == "Night: Drive" && info.Songs.Count == 3 && info.Songs[0].Title == "First song" && info.Songs[2].Title == "Last song", "Extract titles and preserve album track order");
        check(info.Warnings.Count == 1 && info.Songs[1].Title == "[Private video]", "Report missing entries and retain unavailable URLs for queue error handling");
        var state = new ProjectState(); state.Settings.DownloadFolder = root; state.Albums.Add(new() { Name = "Night_ Drive" });
        var result = AlbumImporter.Add(state, info, "256 kbps", false);
        check(result.Added == 3 && result.Album.Name == "Night_ Drive (2)" && state.Tracks.All(t => t.AlbumId == result.Album.Id && t.Quality == "256 kbps" && t.Destination.EndsWith("Night_ Drive (2)")), "Create safe separate album folder with selected quality");
        check(AlbumImporter.DuplicateCount(state, info) == 3, "Detect duplicates once for the whole album");
        var repeat = AlbumImporter.Add(state, info, "256 kbps", false);
        check(repeat.Added == 0 && repeat.Skipped == 3 && state.Albums.Count == 2, "Repeat imports reuse album and skip existing tracks");
        check(AlbumImporter.Add(state, info, "192 kbps", true).Added == 3, "Allow explicitly requested duplicate album tracks");
        var store = new StateStore(Path.Combine(root, "album-state")); store.Save(state);
        check(store.Load().Albums[1].SourceUrl == info.SourceUrl, "Persist imported album identity for future reimports");
        try { AlbumService.Parse("{\"entries\":[]}", canonical); check(false, "Empty album should fail"); } catch (InvalidDataException) { check(true, "Empty albums produce useful errors"); }
    }
}
