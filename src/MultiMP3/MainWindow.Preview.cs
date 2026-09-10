using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MultiMP3.Core;
namespace MultiMP3;

public partial class MainWindow
{
    // Documentation captures use synthetic display data and never read a real library.
    private async Task RunPreview(string directory)
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("MULTIMP3_DATA_DIR"))) throw new InvalidOperationException("Preview requires isolated storage.");
        Directory.CreateDirectory(directory);
        Width = 1280; Height = 860;
        state.Tracks.Clear(); state.Albums.Clear(); state.History.Clear(); state.Errors.Clear();
        var album = new Album { Name = "Open Audio Collection" }; state.Albums.Add(album); RefreshAlbums();
        state.Tracks.Add(new() { Title = "Example track 01", Url = "https://www.youtube.com/watch?v=abcdefghijk", AlbumId = album.Id, Quality = "320 kbps", Destination = "Music / MultiMP3 / Open Audio Collection" });
        state.Tracks.Add(new() { Title = "Example track 02", Url = "https://www.youtube.com/watch?v=lmnopqrstuv", AlbumId = album.Id, Quality = "320 kbps", Destination = "Music / MultiMP3 / Open Audio Collection" });
        async Task Capture(string name)
        {
            Refresh(); FolderLabel.Text = "Music / MultiMP3"; Notice.Text = "Example collection · ready to organize your audio";
            await Task.Delay(150); UpdateLayout();
            var bitmap = new RenderTargetBitmap((int)ActualWidth, (int)ActualHeight, 96, 96, PixelFormats.Pbgra32); bitmap.Render(this);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var file = File.Create(Path.Combine(directory, name)); encoder.Save(file);
        }
        ModeCombo.SelectedItem = "YouTube"; ShowPage("Downloads"); AlbumCombo.SelectedItem = album;
        await Capture("downloads.png");
        ShowPage("About"); await Capture("about.png");
        Width = MinWidth; Height = MinHeight;
        ShowPage("Downloads"); await Capture("downloads-narrow.png");
        ShowPage("About"); await Capture("about-narrow.png");
        Width = 1280; Height = 860;
        ShowPage("Albums"); await Capture("albums.png");
        state.Tracks.Add(new() { Title = "Example purchased MP3", Url = "Purchased Music / Example.mp3", LocalSourcePath = "Example.mp3", AlbumId = album.Id, Quality = "Original MP3", Destination = "Music / MultiMP3 / Open Audio Collection" });
        ModeCombo.SelectedItem = "Amazon Music"; ShowPage("Downloads"); await Capture("amazon-import.png");
        Close();
    }
}
