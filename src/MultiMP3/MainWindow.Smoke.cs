using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MultiMP3.Core;
namespace MultiMP3;
public partial class MainWindow
{
    // Opt-in integration harness. Always run with MULTIMP3_DATA_DIR set to a disposable folder.
    private async Task RunSmoke(string directory)
    {
        Directory.CreateDirectory(directory);
        try
        {
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("MULTIMP3_DATA_DIR"))) throw new InvalidOperationException("UI smoke test requires isolated data storage.");
            void Assert(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
            async Task Capture(string name)
            {
                await Task.Delay(150); UpdateLayout();
                var bitmap = new RenderTargetBitmap((int)ActualWidth, (int)ActualHeight, 96, 96, PixelFormats.Pbgra32); bitmap.Render(this);
                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using var file = File.Create(Path.Combine(directory, name + ".png")); encoder.Save(file);
            }
            await Capture("01-empty");
            ShowPage("About");
            Assert(AboutPage.Visibility == Visibility.Visible && DownloadsPage.Visibility == Visibility.Collapsed && AboutVersion.Text.Contains(ApplicationVersion), "About navigation or metadata version failed");
            ShowPage("Downloads");
            state.Settings.CheckDuplicates = false;
            var album = new Album { Name = "Night Drive" }; state.Albums.Add(album); RefreshAlbums(); AlbumCombo.SelectedItem = album;
            LinksInput.Text = "https://youtu.be/abcdefghijk\ninvalid-link\nhttps://www.youtube.com/watch?v=lmnopqrstuv";
            await AddLinksAsync();
            Assert(state.Tracks.Count == 2 && state.Errors.Count == 1, "URL entry did not isolate an invalid link");
            Assert(state.Tracks.All(t => t.Destination.EndsWith("Night Drive") && t.AlbumId == album.Id), "Album destinations incorrect");
            QualityCombo.SelectedItem = "192 kbps";
            Assert(state.Tracks.All(t => t.Quality == "192 kbps"), "Quality did not update queue");
            await Capture("02-queue");
            ShowPage("Albums"); await Capture("03-albums"); ShowPage("Error Log"); await Capture("04-errors");
            ShowPage("Settings"); SettingsQuality.SelectedItem = "256 kbps"; ConcurrencyCombo.SelectedItem = 3; SaveSettings(this, new RoutedEventArgs());
            var saved = store.Load(); Assert(saved.Settings.Quality == "256 kbps" && saved.Settings.Concurrency == 3 && saved.Albums.Count == 1, "Settings or album persistence failed");
            await Capture("05-settings"); ShowPage("Downloads"); ClearQueue(this, new RoutedEventArgs()); Assert(state.Tracks.Count == 0, "Clear queue failed");
            const string albumLink = "https://music.youtube.com/playlist?list=OLAK5uy_exampleAlbum&si=test";
            LinksInput.Text = albumLink;
            await AddLinksAsync((_, _) => Task.FromResult(MultiMP3.Services.AlbumService.Parse("""
                {"id":"OLAK5uy_test","title":"Album - Imported Album","entries":[{"id":"abcdefghijk","title":"First song"},{"id":"lmnopqrstuv","title":"Second song"}]}
                """, albumLink)));
            Assert(state.Tracks.Count == 2 && state.Tracks[0].Title == "First song" && state.Albums.Last().Name == "Imported Album", "Album UI import failed");
            Assert(AddLinksButton.IsEnabled && CancelButton.IsEnabled == false, "Import controls did not recover");
            await Capture("06-imported-album");
            var before = state.Tracks.Count;
            LinksInput.Text = albumLink;
            var pending = AddLinksAsync(async (_, token) => { await Task.Delay(30000, token); throw new InvalidOperationException(); });
            Assert(!AddLinksButton.IsEnabled && CancelButton.IsEnabled, "Import must expose cancel and prevent repeated submissions");
            CancelDownloads(this, new RoutedEventArgs()); await pending;
            Assert(state.Tracks.Count == before && AddLinksButton.IsEnabled, "Canceled import changed queue or left controls disabled");
            if (Environment.GetEnvironmentVariable("MULTIMP3_ALBUM_TEST_URL") is { } liveAlbum)
            {
                ClearQueue(this, new RoutedEventArgs()); LinksInput.Text = liveAlbum;
                await AddLinksAsync();
                Assert(state.Tracks.Count > 0, "Live example album UI import failed");
                Assert(store.Load().Tracks.Count == state.Tracks.Count, "Live album queue did not persist");
                await Capture("07-live-album");
            }
            if (Environment.GetEnvironmentVariable("MULTIMP3_PLAYLIST_TEST_URL") is { } livePlaylist)
            {
                ClearQueue(this, new RoutedEventArgs()); LinksInput.Text = livePlaylist;
                await AddLinksAsync();
                Assert(state.Tracks.Count > 0, "Live playlist UI import failed");
                Assert(store.Load().Tracks.Count == state.Tracks.Count && state.Albums.Last().Kind == "Playlist", "Playlist identity and queue did not persist");
                OpenAlbum(new Button { Tag = state.Albums.Last() }, new RoutedEventArgs());
                Assert((string)DownloadButton.Content == "↓  Download Playlist", "Playlist download button missing");
                await Capture("08-live-playlist");
            }
            var sourceFolder = Path.Combine(directory, "purchased-mp3s"); Directory.CreateDirectory(sourceFolder);
            state.Settings.DownloadFolder = Path.Combine(directory, "organized");
            var sourceMp3 = Path.Combine(sourceFolder, "purchase.mp3");
            await MultiMP3.Services.ProcessRunner.Run(dependencies.FFmpeg!, ["-f", "lavfi", "-i", "sine=frequency=440:duration=1", "-metadata", "title=Evening Walk", "-metadata", "album=Purchased Collection", "-metadata", "artist=Example Artist", sourceMp3], null, default);
            var originalBytes = File.ReadAllBytes(sourceMp3);
            ModeCombo.SelectedItem = "Amazon Music";
            Assert(AmazonInputPanel.Visibility == Visibility.Visible && YouTubeInputPanel.Visibility == Visibility.Collapsed, "Amazon mode did not switch input panels");
            Assert(AutomaticAlbumLabel.Visibility == Visibility.Visible && AlbumCombo.Visibility == Visibility.Collapsed, "Automatic grouping must hide manual album selection");
            await ImportLocalFilesAsync([], sourceFolder);
            Assert(VisibleTracks.Count() == 1 && VisibleTracks.Single().Title == "Evening Walk", "Folder scan did not queue purchased MP3");
            Assert((string)DownloadButton.Content == "↓  Import All" && DownloadButton.IsEnabled, "Amazon import action unavailable");
            Assert(Summary.Text.Contains("1 queued"), "Mode counters include tracks hidden in another source mode");
            await Capture("09-amazon-queued");
            await queue.Run(VisibleTracks.ToArray());
            var imported = VisibleTracks.Single();
            Assert(imported.Status == "Completed" && File.ReadAllBytes(imported.Destination).SequenceEqual(originalBytes) && File.ReadAllBytes(sourceMp3).SequenceEqual(originalBytes), "Amazon UI import changed original bytes or did not complete");
            Assert(store.Load().Settings.SourceMode == "Amazon Music", "Source mode did not persist");
            await Capture("10-amazon-imported");
            ModeCombo.SelectedItem = "YouTube";
            Assert(VisibleTracks.All(t => !t.IsLocal) && YouTubeInputPanel.Visibility == Visibility.Visible, "YouTube mode did not restore its queue");
            File.WriteAllText(Path.Combine(directory, "result.txt"), "PASS: UI launch, URL entry, collection import, cancellation, persistence, source-mode switching, purchased MP3 folder scanning, exact-copy import, source preservation and queue filtering; screenshots captured.");
        }
        catch (Exception ex) { File.WriteAllText(Path.Combine(directory, "result.txt"), "FAIL: " + ex); }
        finally { Close(); }
    }
}
