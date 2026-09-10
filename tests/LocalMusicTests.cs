using MultiMP3.Core;
using MultiMP3.Services;
public static class LocalMusicTests
{
    public static async Task Run(Action<bool, string> check, string root, string tools)
    {
        var ffmpeg = Path.Combine(tools, "ffmpeg.exe"); var ffprobe = Path.Combine(tools, "ffprobe.exe");
        var source = Path.Combine(root, "purchases"); Directory.CreateDirectory(source);
        var path = Path.Combine(source, "purchase.mp3");
        await ProcessRunner.Run(ffmpeg, ["-f", "lavfi", "-i", "sine=frequency=440:duration=1", "-metadata", "title=Purchased Song", "-metadata", "artist=Test Artist", "-metadata", "album=Purchased Album", "-metadata", "album_artist=Test Artist", "-metadata", "track=2/10", "-b:a", "192k", path], null, default);
        var original = File.ReadAllBytes(path);
        var local = new LocalMusicService(ffprobe); var song = await local.Inspect(path, default);
        check(song.Title == "Purchased Song" && song.Album == "Purchased Album" && song.TrackNumber == 2, "Read purchased MP3 tags and track number");
        var state = new ProjectState(); state.Settings.DownloadFolder = Path.Combine(root, "organized"); state.Settings.AutoRetry = false;
        var track = LocalMusicImporter.Add(state, song, true, null);
        check(track.IsLocal && track.Quality == "Original MP3" && track.Destination.EndsWith("Purchased Album"), "Queue purchased MP3 in its tagged album folder");
        check(LocalMusicImporter.IsDuplicate(state, path.ToUpperInvariant()), "Detect duplicate source paths case-insensitively");
        var deps = new Dependencies(null, null, ffprobe, null);
        await new QueueManager(state, new MediaService(deps), a => a()).Run(state.Tracks);
        check(track.Status == "Completed" && state.History.Count == 1 && File.ReadAllBytes(track.Destination).SequenceEqual(original) && File.ReadAllBytes(path).SequenceEqual(original), "Import exact MP3 bytes with history; preserve the source without YouTube dependencies");
        var duplicate = LocalMusicImporter.Add(state, song, true, null);
        await new QueueManager(state, new MediaService(deps), a => a()).Run([duplicate]);
        check(duplicate.Destination != track.Destination && File.ReadAllBytes(track.Destination).SequenceEqual(original), "Repeated imports never overwrite an existing MP3");
        var manualAlbum = new Album { Name = "Manual Collection" }; state.Albums.Add(manualAlbum);
        var manual = LocalMusicImporter.Add(state, song, false, manualAlbum);
        check(manual.AlbumId == manualAlbum.Id, "Respect manually selected destination album");
        var missing = new Track { Url = "file:///missing.mp3", LocalSourcePath = Path.Combine(root, "missing.mp3") }; state.Tracks.Add(missing);
        await new QueueManager(state, new MediaService(deps), a => a()).Run([missing, manual]);
        check(missing.Status == "Failed" && manual.Status == "Completed", "Missing source fails independently and next local MP3 imports");
        var invalid = Path.Combine(source, "invalid.mp3"); File.WriteAllText(invalid, "not audio");
        try { await local.Inspect(invalid, default); check(false, "Reject corrupt MP3"); } catch (InvalidOperationException) { check(true, "Reject corrupt MP3 without queuing it"); }
        var store = new StateStore(Path.Combine(root, "local-state")); state.Settings.SourceMode = "Amazon Music"; store.Save(state);
        check(store.Load().Tracks[0].LocalSourcePath == path && store.Load().Settings.SourceMode == "Amazon Music", "Persist Amazon source mode and local source paths");
        using var cancel = new CancellationTokenSource(); cancel.Cancel();
        try { await LocalMusicService.Copy(track, null, state.Settings, (_, _) => { }, cancel.Token); check(false, "Cancel local copy"); } catch (OperationCanceledException) { check(!Directory.EnumerateFiles(state.Settings.DownloadFolder, "*.tmp", SearchOption.AllDirectories).Any() && File.ReadAllBytes(path).SequenceEqual(original), "Canceled copy removes staging file and leaves source untouched"); }
    }
}
