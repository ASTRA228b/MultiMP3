using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using MultiMP3.Core;
using MultiMP3.Services;

namespace MultiMP3.Mac;

internal sealed class MainWindow : Window
{
    private readonly StateStore store = new();
    private readonly ProjectState state;
    private readonly TextBlock destination = new() { Foreground = Brushes.LightGray, TextTrimming = TextTrimming.CharacterEllipsis };
    private readonly TextBlock status = new() { Text = "Ready", Foreground = new SolidColorBrush(Color.Parse("#B9C0CE")) };
    private readonly StackPanel library = new() { Spacing = 7 };
    private readonly Button import = new() { Content = "Import purchased MP3s", MinHeight = 46, FontWeight = FontWeight.SemiBold };

    public MainWindow()
    {
        state = store.Load();
        Title = "MultiMP3 for Mac";
        Width = 660;
        Height = 520;
        MinWidth = 520;
        MinHeight = 420;
        Background = new SolidColorBrush(Color.Parse("#101018"));
        Content = BuildUi();
        destination.Text = state.Settings.DownloadFolder;
        import.Click += async (_, _) => await ImportAsync();
        RefreshLibrary();
    }

    private Control BuildUi()
    {
        var title = new TextBlock { Text = "MultiMP3", FontSize = 28, FontWeight = FontWeight.Bold, Foreground = Brushes.White };
        var badge = new TextBlock
        {
            Text = "MAC · LOCAL ORGANIZER", FontSize = 11, FontWeight = FontWeight.SemiBold,
            Foreground = new SolidColorBrush(Color.Parse("#A997FF")), VerticalAlignment = VerticalAlignment.Center
        };
        var heading = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = 12 };
        Grid.SetColumn(title, 0); Grid.SetColumn(badge, 1); heading.Children.Add(title); heading.Children.Add(badge);

        import.Background = new SolidColorBrush(Color.Parse("#765BFF"));
        import.Foreground = Brushes.White;
        var choose = new Button { Content = "Choose destination" };
        choose.Click += async (_, _) => await ChooseDestinationAsync();
        var actions = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 10 };
        Grid.SetColumn(import, 0); Grid.SetColumn(choose, 1); actions.Children.Add(import); actions.Children.Add(choose);

        var destinationCard = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#1B1B27")), CornerRadius = new CornerRadius(10), Padding = new Thickness(12),
            Child = new StackPanel { Spacing = 3, Children = { new TextBlock { Text = "ORGANIZED MUSIC FOLDER", FontSize = 10, Foreground = Brushes.Gray }, destination } }
        };
        var listCard = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#171720")), CornerRadius = new CornerRadius(12), Padding = new Thickness(12),
            Child = new ScrollViewer { Content = library }
        };
        return new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto,*"), RowSpacing = 12, Margin = new Thickness(22),
            Children =
            {
                Place(heading, 0), Place(new TextBlock { Text = "Organize MP3 files you own. Originals are never changed or removed.", Foreground = Brushes.LightGray }, 1),
                Place(actions, 2), Place(new StackPanel { Spacing = 8, Children = { destinationCard, status } }, 3), Place(listCard, 4)
            }
        };
    }

    private static Control Place(Control control, int row) { Grid.SetRow(control, row); return control; }

    private async Task ChooseDestinationAsync()
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = "Choose your organized music folder", AllowMultiple = false });
        string? path = folders.FirstOrDefault()?.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path)) return;
        state.Settings.DownloadFolder = path;
        destination.Text = path;
        store.Save(state);
        status.Text = "Destination saved";
    }

    private async Task ImportAsync()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Choose purchased or owned MP3 files", AllowMultiple = true,
            FileTypeFilter = [new FilePickerFileType("MP3 audio") { Patterns = ["*.mp3"], AppleUniformTypeIdentifiers = ["public.mp3"] }]
        });
        string[] paths = files.Select(file => file.TryGetLocalPath()).Where(path => !string.IsNullOrWhiteSpace(path)).Cast<string>().ToArray();
        if (paths.Length == 0) return;
        import.IsEnabled = false;
        var dependencies = Dependencies.Find();
        var service = new LocalMusicService(dependencies.FFprobe);
        int imported = 0, skipped = 0, failed = 0;
        try
        {
            for (int index = 0; index < paths.Length; index++)
            {
                string path = paths[index];
                status.Text = $"Reading {index + 1} of {paths.Length} · {Path.GetFileName(path)}";
                try
                {
                    if (LocalMusicImporter.IsDuplicate(state, path)) { skipped++; continue; }
                    LocalSong song = await service.Inspect(path, CancellationToken.None);
                    Track track = LocalMusicImporter.Add(state, song, groupByAlbum: true, chosenAlbum: null);
                    Album? album = state.Albums.FirstOrDefault(item => item.Id == track.AlbumId);
                    track.Status = "Copying";
                    track.Destination = await LocalMusicService.Copy(track, album, state.Settings, (_, label) => track.Status = label, CancellationToken.None);
                    track.Status = "Completed";
                    state.History.Insert(0, new(DateTime.Now, track.Title, track.Url, track.Destination, "Original MP3"));
                    imported++;
                }
                catch (Exception error)
                {
                    failed++;
                    state.Errors.Insert(0, new(DateTime.Now, Path.GetFileName(path), path, error.Message, error.ToString()));
                }
                store.Save(state);
                RefreshLibrary();
            }
            status.Text = $"Finished · {imported} organized · {skipped} already present · {failed} failed";
        }
        finally { import.IsEnabled = true; }
    }

    private void RefreshLibrary()
    {
        library.Children.Clear();
        if (state.History.Count == 0)
        {
            library.Children.Add(new TextBlock { Text = "Your organized music will appear here.", Foreground = Brushes.Gray, Margin = new Thickness(4, 10) });
            return;
        }
        foreach (DownloadRecord item in state.History.Take(100))
        {
            library.Children.Add(new Border
            {
                Background = new SolidColorBrush(Color.Parse("#222230")), CornerRadius = new CornerRadius(8), Padding = new Thickness(11, 8),
                Child = new StackPanel { Spacing = 2, Children = { new TextBlock { Text = item.Title, FontWeight = FontWeight.SemiBold }, new TextBlock { Text = item.Path, FontSize = 11, Foreground = Brushes.Gray, TextTrimming = TextTrimming.CharacterEllipsis } } }
            });
        }
    }
}
