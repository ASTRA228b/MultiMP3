using MultiMP3.Core;
using MultiMP3.Services;
var root = Path.Combine(Path.GetTempPath(), "MultiMP3-test-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
int passed = 0;
void Check(bool condition, string name) { if (!condition) throw new Exception("FAILED: " + name); Console.WriteLine("PASS " + name); passed++; }
AlbumTests.Run(Check, root);
Check(FileManager.TryUrl("https://youtu.be/abcdefghijk?t=12", out var canonical) && canonical == "https://www.youtube.com/watch?v=abcdefghijk", "Normalize duplicate video links");
foreach (var bad in new[] { "hello", "file:///C:/secret", "https://youtube.com.evil.test/watch?v=abcdefghijk", "https://youtube.com/watch?v=bad", "http://youtu.be/abcdefghijk", "https://user:pass@youtu.be/abcdefghijk" }) Check(!FileManager.TryUrl(bad, out _), "Reject invalid URL: " + bad);
Check(FileManager.SafeName("CON.mp3") == "_CON.mp3" && FileManager.SafeName("../bad:name? ") == ".._bad_name_", "Sanitize reserved and invalid Windows names");
var album = new Album { Name = "Night Drive" };
var folder = FileManager.AlbumFolder(root, album);
Check(folder == Path.Combine(root, "Night Drive"), "Album folder stays under destination");
var source = Path.Combine(root, "source.tmp"); File.WriteAllText(source, "original");
var first = FileManager.CommitUnique(source, folder, "Track"); File.WriteAllText(source, "new"); var second = FileManager.CommitUnique(source, folder, "Track");
Check(first != second && File.ReadAllText(first) == "original" && File.ReadAllText(second) == "new", "Duplicate filenames never overwrite");
var state = new ProjectState(); state.Settings.DownloadFolder = root; state.Settings.Concurrency = 1; state.Settings.AutoRetry = false; state.Albums.Add(album);
state.Tracks.Add(new() { Url = "fail", AlbumId = album.Id }); state.Tracks.Add(new() { Url = "success", AlbumId = album.Id });
var fake = new FakeMedia(); var queue = new QueueManager(state, fake, a => a()); await queue.Run(state.Tracks);
Check(state.Tracks[0].Status == "Failed" && state.Tracks[1].Status == "Completed" && state.History.Count == 1 && state.Errors.Count == 1, "Failed item does not stop queue");
Check(fake.LastAlbum == album, "Queue passes album to download engine");
var store = new StateStore(Path.Combine(root, "settings")); store.Save(state); var restored = store.Load();
Check(restored.Settings.DownloadFolder == root && restored.Albums[0].Name == album.Name && restored.History.Count == 1, "Settings, albums, and history persist");
restored.Tracks[0].Status = "Downloading"; store.Save(restored); Check(store.Load().Tracks[0].Status == "Queued", "Interrupted work recovers to queue");
File.WriteAllText(Path.Combine(root, "settings", "library.json"), "broken"); Check(store.Load().History.Count == 1 && store.Warning != null, "Corrupt library recovers backup");
var retryState = new ProjectState(); retryState.Settings.Concurrency = 1; retryState.Settings.MaxRetries = 1; retryState.Tracks.Add(new() { Url = "retry" });
var retryMedia = new FakeMedia { FailOnce = true }; await new QueueManager(retryState, retryMedia, a => a()).Run(retryState.Tracks);
Check(retryMedia.Attempts == 2 && retryState.Tracks[0].Status == "Completed", "Automatic retry recovers transient failure");
var cancelState = new ProjectState(); cancelState.Tracks.Add(new() { Url = "slow" }); var cancelQueue = new QueueManager(cancelState, new FakeMedia { Slow = true }, a => a());
var running = cancelQueue.Run(cancelState.Tracks); await Task.Delay(50); cancelQueue.Cancel(); await running;
Check(!cancelQueue.Running && cancelState.Tracks[0].Status == "Queued", "Cancel safely returns unfinished work to queue");
if (args.Length > 0)
{
    await LocalMusicTests.Run(Check, root, args[0]);
    var ffmpeg = Path.Combine(args[0], "ffmpeg.exe"); var ffprobe = Path.Combine(args[0], "ffprobe.exe");
    var wav = Path.Combine(root, "test.wav"); var mp3 = Path.Combine(root, "test.mp3");
    await ProcessRunner.Run(ffmpeg, ["-f", "lavfi", "-i", "sine=frequency=440:duration=1", wav], null, default);
    await AudioConverter.Convert(ffmpeg, wav, mp3, null, new() { Title = "Test song", Artist = "MultiMP3", Quality = "192 kbps" }, album, state.Settings, default);
    var probe = await ProcessRunner.Run(ffprobe, ["-v", "quiet", "-show_format", "-show_streams", "-of", "json", mp3], null, default);
    Check(probe.Contains("mp3") && probe.Contains("Night Drive") && probe.Contains("Test song"), "Real FFmpeg conversion and album metadata");
    var original = File.ReadAllBytes(mp3);
    try { await AudioConverter.Convert(ffmpeg, wav, mp3, null, new(), null, state.Settings, default); throw new Exception("Overwrote output"); } catch (IOException) { Check(File.ReadAllBytes(mp3).SequenceEqual(original), "Conversion refuses overwriting existing output"); }
}
Console.WriteLine($"{passed} checks passed. Test artifacts: {root}");
if (args.Length > 2 && args[1] is "--album" or "--collection")
{
    var tools = Path.GetFullPath(args[0]);
    var dependencies = new Dependencies(Path.Combine(tools, "yt-dlp.exe"), Path.Combine(tools, "ffmpeg.exe"), Path.Combine(tools, "ffprobe.exe"), Path.Combine(tools, "node.exe"));
    var info = await new AlbumService(dependencies).Read(args[2], default);
    var albumState = new ProjectState(); albumState.Settings.DownloadFolder = root; albumState.Settings.AutoRetry = false;
    var imported = AlbumImporter.Add(albumState, info, "192 kbps", false);
    Check(imported.Added == info.Songs.Count && albumState.Tracks.Select(t => t.Url).SequenceEqual(info.Songs.Select(t => t.Url)), "Live album fully imported in source order");
    Console.WriteLine($"Imported {info.Title}: {imported.Added} songs");
    if (args.Contains("--download-first"))
    {
        await new QueueManager(albumState, new MediaService(dependencies), a => a()).Run(albumState.Tracks.Take(1));
        foreach (var error in albumState.Errors) Console.WriteLine(error.Details);
        Check(albumState.Tracks[0].Status == "Completed" && File.Exists(albumState.Tracks[0].Destination), "Download an imported album song through the normal queue");
    }
}
if (args.Length > 2 && args[1] == "--live")
{
    if (!FileManager.TryUrl(args[2], out var liveUrl)) throw new ArgumentException("Invalid live test URL");
    var tools = Path.GetFullPath(args[0]);
    var dependencies = new Dependencies(Path.Combine(tools, "yt-dlp.exe"), Path.Combine(tools, "ffmpeg.exe"), Path.Combine(tools, "ffprobe.exe"), Path.Combine(tools, "node.exe"));
    await dependencies.Validate(default);
    var liveState = new ProjectState(); liveState.Settings.DownloadFolder = root; liveState.Settings.Concurrency = 1; liveState.Settings.AutoRetry = false; liveState.Settings.EmbedThumbnail = true;
    liveState.Albums.Add(album); liveState.Tracks.Add(new() { Url = "https://www.youtube.com/watch?v=BaW_jenozKc", AlbumId = album.Id }); liveState.Tracks.Add(new() { Url = liveUrl, AlbumId = album.Id });
    var liveQueue = new QueueManager(liveState, new MediaService(dependencies), a => a());
    var last = ""; liveQueue.Changed += () => { var status = string.Join(" | ", liveState.Tracks.Select(t => t.Status)); if (status != last) { Console.WriteLine(status); last = status; } };
    await liveQueue.Run(liveState.Tracks);
    foreach (var error in liveState.Errors) Console.WriteLine(error.Details);
    Check(liveState.Tracks[0].Status == "Failed" && liveState.Tracks[1].Status == "Completed" && File.Exists(liveState.Tracks[1].Destination), "Live unavailable-video isolation and YouTube album download with cover art");
    Console.WriteLine("Live MP3: " + liveState.Tracks[1].Destination);
}
class FakeMedia : IMediaService
{
    public Album? LastAlbum; public bool FailOnce; public bool Slow; public int Attempts;
    public async Task ReadInfo(Track track, CancellationToken token) { Attempts++; if (Slow) await Task.Delay(10000, token); if (track.Url == "fail" || (FailOnce && Attempts == 1)) throw new IOException("Simulated failure"); track.Title = "Test"; }
    public Task<string> Download(Track track, Album? album, Settings settings, Action<double,string> progress, CancellationToken token) { LastAlbum = album; progress(90, "Converting"); return Task.FromResult(Path.Combine(settings.DownloadFolder, "test.mp3")); }
}
