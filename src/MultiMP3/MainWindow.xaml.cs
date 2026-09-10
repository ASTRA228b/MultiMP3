using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using MultiMP3.Core;
using MultiMP3.Services;
namespace MultiMP3;
public partial class MainWindow : Window
{
    private readonly StateStore store = new();
    private ProjectState state = new();
    private QueueManager queue = null!;
    private Dependencies dependencies = Dependencies.Find();
    private readonly Album inbox = new() { Id = "", Name = "Unsorted" };
    private bool ready;
    private bool engineHealthy;
    private CancellationTokenSource? linkImport;
    private string page = "Downloads";
    private Point dragStart;
    private Track? dragTrack;
    private string? selectedAlbumId;
    private static readonly string[] Qualities = ["128 kbps", "192 kbps", "256 kbps", "320 kbps", "Best Available"];
    public MainWindow()
    {
        InitializeComponent();
        SidebarVersion.Text = "MULTIMP3 / " + ApplicationVersion;
        AboutVersion.Text = "VERSION " + ApplicationVersion + "  /  WINDOWS X64";
        SizeChanged += (_, _) =>
        {
            var compact = ActualWidth < 1150 || ActualHeight < 780;
            LinksInput.Height = compact ? 48 : 80;
            AudioInputCard.Padding = new Thickness(compact ? 16 : 22);
            AudioOptions.Margin = compact ? new Thickness(0, 12, 0, 12) : new Thickness(0, 18, 0, 20);
        };
        try { state = store.Load(); } catch (Exception ex) { Notice.Text = "Could not load library: " + ex.Message; }
        Width = Math.Clamp(state.Settings.Width, MinWidth, Math.Max(MinWidth, SystemParameters.WorkArea.Width));
        Height = Math.Clamp(state.Settings.Height, MinHeight, Math.Max(MinHeight, SystemParameters.WorkArea.Height));
        QualityCombo.ItemsSource = Qualities; QualityCombo.SelectedItem = state.Settings.Quality;
        ModeCombo.ItemsSource = new[] { "YouTube", "Amazon Music" };
        ModeCombo.SelectedItem = state.Settings.SourceMode == "Amazon Music" ? "Amazon Music" : "YouTube";
        if (QualityCombo.SelectedIndex < 0) QualityCombo.SelectedIndex = 3;
        SettingsQuality.ItemsSource = Qualities; ConcurrencyCombo.ItemsSource = Enumerable.Range(1, 4); RetriesCombo.ItemsSource = Enumerable.Range(0, 6);
        HistoryList.ItemsSource = state.History; ErrorsList.ItemsSource = state.Errors;
        RefreshAlbums(); LoadSettings(); CreateQueue();
        ready = true; Refresh();
        Notice.Text = store.Warning ?? dependencies.Message;
        Loaded += async (_, _) =>
        {
            await CheckDependencies();
#if UI_TESTS
            if (Environment.GetEnvironmentVariable("MULTIMP3_SMOKE_DIR") is { } smoke) await RunSmoke(smoke);
            else if (Environment.GetEnvironmentVariable("MULTIMP3_PREVIEW_DIR") is { } preview) await RunPreview(preview);
#endif
        };
        var icon = Path.Combine(AppContext.BaseDirectory, "Assets", "MultiMP3.ico");
        if (File.Exists(icon)) Icon = System.Windows.Media.Imaging.BitmapFrame.Create(new Uri(icon));
    }
    private void CreateQueue()
    {
        queue = new QueueManager(state, new MediaService(dependencies), a => Dispatcher.Invoke(a));
        var historyCount = state.History.Count; var errorCount = state.Errors.Count;
        queue.Changed += () => Dispatcher.Invoke(() =>
        {
            Refresh(false);
            if (!queue.Running || historyCount != state.History.Count || errorCount != state.Errors.Count) { Persist(); historyCount = state.History.Count; errorCount = state.Errors.Count; }
        });
    }
    private bool AmazonMode => state.Settings.SourceMode == "Amazon Music";
    private IEnumerable<Track> VisibleTracks => state.Tracks.Where(t => t.IsLocal == AmazonMode && (selectedAlbumId is null || t.AlbumId == selectedAlbumId));
    private void Refresh(bool items = true)
    {
        if (!ready) return;
        var visible = VisibleTracks.ToArray();
        if (items) QueueList.ItemsSource = visible;
        EmptyQueue.Visibility = visible.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        EmptyHistory.Visibility = state.History.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        EmptyErrors.Visibility = state.Errors.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        QueueHeading.Text = $"{(AmazonMode ? "Import" : "Download")} queue  ·  {visible.Length}";
        Summary.Text = $"{visible.Count(t => t.Status == "Completed")} completed  ·  {visible.Count(t => t.IsActive)} active  ·  {visible.Count(t => t.Status == "Queued")} queued  ·  {visible.Count(t => t.Status == "Failed")} failed";
        BatchProgress.Value = visible.Length == 0 ? 0 : visible.Average(t => t.Status is "Failed" or "Skipped" ? 100 : t.Progress);
        BatchLabel.Text = linkImport is not null ? AmazonMode ? "Reading purchased MP3 files…" : "Reading links · tracks are added together" : queue.Running ? queue.Paused ? "Paused · active items finish; new items wait" : "Working on your collection…" : "Ready when you are";
        PauseButton.IsEnabled = queue.Running;
        CancelButton.IsEnabled = queue.Running || linkImport is not null;
        PauseButton.Content = queue.Paused ? "Resume" : "Pause";
        DownloadButton.IsEnabled = !queue.Running && linkImport is null && (AmazonMode ? dependencies.FFprobe is not null : engineHealthy) && visible.Any(t => t.Status == "Queued");
        DownloadButton.Content = AmazonMode ? "↓  Import All" : selectedAlbumId is null ? "↓  Download All" : "↓  Download " + (state.Albums.FirstOrDefault(a => a.Id == selectedAlbumId)?.Kind ?? "Album");
        SaveSettingsButton.IsEnabled = !queue.Running && linkImport is null;
        QualityCombo.IsEnabled = !queue.Running && linkImport is null;
        AddLinksButton.IsEnabled = linkImport is null;
        AddLinksButton.Content = linkImport is null ? "＋  Add Links" : "Reading links…";
        LinksInput.IsEnabled = linkImport is null;
        ModeCombo.IsEnabled = !queue.Running && linkImport is null;
        AddMp3Button.IsEnabled = AddLocalFolderButton.IsEnabled = !queue.Running && linkImport is null;
        GroupLocalAlbums.IsEnabled = !queue.Running && linkImport is null;
        YouTubeInputPanel.Visibility = AmazonMode ? Visibility.Collapsed : Visibility.Visible;
        AmazonInputPanel.Visibility = AmazonMode ? Visibility.Visible : Visibility.Collapsed;
        QualityCombo.Visibility = AmazonMode ? Visibility.Collapsed : Visibility.Visible;
        OriginalQualityLabel.Visibility = AmazonMode ? Visibility.Visible : Visibility.Collapsed;
        var automaticAlbum = AmazonMode && GroupLocalAlbums.IsChecked == true;
        AlbumCombo.Visibility = automaticAlbum ? Visibility.Collapsed : Visibility.Visible;
        AutomaticAlbumLabel.Visibility = automaticAlbum ? Visibility.Visible : Visibility.Collapsed;
        EmptyQueueTitle.Text = AmazonMode ? "Bring your purchased music together" : "Your next playlist starts here";
        EmptyQueueHint.Text = AmazonMode ? "Choose MP3s or a folder above, then Import All." : "Add some links above. We’ll take it from there.";
        FolderLabel.Text = state.Settings.DownloadFolder;
    }
    private void Persist()
    {
        try { store.Save(state); } catch (Exception ex) { Notice.Text = "Could not save library: " + ex.Message; }
    }
    private void Navigate(object sender, RoutedEventArgs e) => ShowPage((string)((Button)sender).Tag);
    private void ShowPage(string name)
    {
        page = name;
        foreach (var nav in new[] { DownloadsNav }.Concat(FindNavButtons((DependencyObject)Content)))
            nav.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString((string?)nav.Tag == name ? "#342744" : "Transparent"));
        DownloadsPage.Visibility = name == "Downloads" ? Visibility.Visible : Visibility.Collapsed;
        AlbumsPage.Visibility = name == "Albums" ? Visibility.Visible : Visibility.Collapsed;
        HistoryPage.Visibility = name == "History" ? Visibility.Visible : Visibility.Collapsed;
        SettingsPage.Visibility = name == "Settings" ? Visibility.Visible : Visibility.Collapsed;
        ErrorsPage.Visibility = name == "Error Log" ? Visibility.Visible : Visibility.Collapsed;
        AboutPage.Visibility = name == "About" ? Visibility.Visible : Visibility.Collapsed;
        QueueFooter.Visibility = NoticePanel.Visibility = name == "About" ? Visibility.Collapsed : Visibility.Visible;
        if (name == "Downloads") { selectedAlbumId = null; PageSubtitle.Text = "A better home for everything you listen to."; }
        else PageSubtitle.Text = name switch { "Albums" => "Collections for late nights, long drives, and everything between.", "History" => "Your completed downloads, right where you left them.", "Settings" => "Make this workspace yours.", "About" => "MultiMP3 · A ChudGPT App", _ => "A clear view of what needs another try." };
        PageTitle.Text = name;
        if (name == "Settings") LoadSettings();
        Refresh();
    }
    private async void AddLinks(object sender, RoutedEventArgs e) => await AddLinksAsync();
    private async Task AddLinksAsync(Func<string, CancellationToken, Task<AlbumInfo>>? readAlbum = null)
    {
        if (linkImport is not null) return;
        var links = LinksInput.Text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        var added = 0; var invalid = 0; var skipped = 0;
        var importedAlbums = 0;
        var quality = (string)QualityCombo.SelectedItem;
        var targetAlbum = AlbumCombo.SelectedItem as Album;
        linkImport = new(); var token = linkImport.Token;
        readAlbum ??= new AlbumService(dependencies).Read;
        Refresh(false);
        try
        {
            foreach (var link in links)
            {
                token.ThrowIfCancellationRequested();
                try
                {
                    if (FileManager.TryCollectionUrl(link, out var albumUrl))
                    {
                        Notice.Text = "Reading album or playlist… All available tracks will be added in source order. Cancel to stop.";
                        var info = await readAlbum(albumUrl, token);
                        token.ThrowIfCancellationRequested();
                        var allowDuplicates = !state.Settings.CheckDuplicates;
                        var duplicateCount = AlbumImporter.DuplicateCount(state, info);
                        if (!allowDuplicates && duplicateCount > 0)
                            allowDuplicates = MessageBox.Show(this, $"{duplicateCount} tracks in '{info.Title}' are already in your queue or history.\n\nAdd those tracks again? Choose No to add only new tracks.", "Collection duplicates", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
                        var result = AlbumImporter.Add(state, info, quality, allowDuplicates);
                        added += result.Added; skipped += result.Skipped; importedAlbums++;
                        RefreshAlbums(); AlbumCombo.SelectedItem = result.Album;
                        ShowPage("Downloads"); Persist();
                        continue;
                    }
                    if (!FileManager.TryUrl(link, out var url)) throw new ArgumentException("Use a public YouTube video, album, or playlist share link.");
                    if (state.Settings.CheckDuplicates && (state.Tracks.Any(t => t.Url == url) || state.History.Any(h => h.Url == url)))
                    {
                        if (MessageBox.Show(this, "This link is already in your queue or history:\n" + url + "\n\nAdd another copy? Existing files will be preserved.", "Duplicate link", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) { skipped++; continue; }
                    }
                    state.Tracks.Add(new() { Url = url, AlbumId = string.IsNullOrEmpty(targetAlbum?.Id) ? null : targetAlbum.Id, Quality = quality, Destination = FileManager.AlbumFolder(state.Settings.DownloadFolder, targetAlbum?.Id == "" ? null : targetAlbum) });
                    added++;
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
                catch (Exception ex)
                {
                    var reason = ex is OperationCanceledException ? "Album lookup timed out. Try again when your connection is available." : ex.Message;
                    state.Errors.Insert(0, new(DateTime.Now, "Could not add link", link, reason.Length > 260 ? reason[..260] : reason, ex.ToString())); invalid++;
                }
            }
            LinksInput.Clear();
            Notice.Text = $"{added} tracks added" + (importedAlbums > 0 ? $" · {importedAlbums} collection(s) imported" : "") + (skipped > 0 ? $" · {skipped} duplicates skipped" : "") + (invalid > 0 ? $" · {invalid} links could not be read; see Error Log" : "") + ". Choose Download All to start.";
        }
        catch (OperationCanceledException) { Notice.Text = $"Import canceled. {added} tracks already added were kept; the current collection was not imported."; }
        finally { linkImport.Dispose(); linkImport = null; Refresh(); Persist(); }
    }
    private static IEnumerable<Button> FindNavButtons(DependencyObject parent)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is Button b && b.Tag is string tag && tag is "Downloads" or "Albums" or "History" or "Settings" or "Error Log" or "About") yield return b;
            foreach (var nested in FindNavButtons(child)) yield return nested;
        }
    }
    private void RemoveTrack(object sender, RoutedEventArgs e)
    {
        if (queue.Running) { Notice.Text = "Wait for the batch to finish or cancel before removing tracks."; return; }
        state.Tracks.Remove((Track)((Button)sender).Tag); Refresh(); Persist();
    }
    private void ClearQueue(object sender, RoutedEventArgs e)
    {
        if (queue.Running) { Notice.Text = "Cancel the active batch before clearing the queue."; return; }
        foreach (var track in VisibleTracks.ToArray()) state.Tracks.Remove(track);
        Refresh(); Persist();
    }
    private async void DownloadAll(object sender, RoutedEventArgs e)
    {
        if (queue.Running || linkImport is not null) return;
        try
        {
            Notice.Text = AmazonMode ? "Importing purchased MP3s · original files are preserved." : "Downloading public media · files are saved locally.";
            var batch = VisibleTracks.Where(t => t.Status == "Queued").ToArray();
            await queue.Run(batch);
            Persist(); Refresh();
            Notice.Text = $"Batch finished · {batch.Count(t => t.Status == "Completed")} completed, {batch.Count(t => t.Status == "Failed")} failed, {batch.Count(t => t.Status == "Queued")} remaining.";
            if (state.Settings.OpenFolder && batch.Any(t => t.Status == "Completed")) OpenFolder(state.Settings.DownloadFolder);
        }
        catch (Exception ex) { Notice.Text = ex.Message; }
    }
    private void PauseDownloads(object sender, RoutedEventArgs e) => queue.TogglePause();
    private void CancelDownloads(object sender, RoutedEventArgs e) { if (linkImport is not null) { linkImport.Cancel(); Notice.Text = "Canceling import scan…"; return; } queue.Cancel(); Notice.Text = "Canceling active work. Unfinished items return to the queue."; }
    private void RetryFailed(object sender, RoutedEventArgs e)
    {
        if (queue.Running) { Notice.Text = "Retry is available after the active batch finishes."; return; }
        foreach (var t in VisibleTracks.Where(t => t.Status == "Failed")) { t.Status = "Queued"; t.Progress = 0; }
        ShowPage("Downloads"); Persist(); Notice.Text = "Failed items are queued again. Click Download All to retry.";
    }
    private void SelectFolder(object sender, RoutedEventArgs e)
    {
        if (queue.Running || linkImport is not null) { Notice.Text = "Wait for downloads or album import before changing folders."; return; }
        var dialog = new OpenFolderDialog { Title = "Choose your MultiMP3 download folder" };
        if (dialog.ShowDialog(this) == true) { state.Settings.DownloadFolder = dialog.FolderName; UpdateDestinations(); Refresh(); Persist(); }
    }
    private void UpdateDestinations() { foreach (var t in state.Tracks.Where(t => t.Status != "Completed")) t.Destination = FileManager.AlbumFolder(state.Settings.DownloadFolder, state.Albums.FirstOrDefault(a => a.Id == t.AlbumId)); }
    private void QualityChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!ready || queue.Running || QualityCombo.SelectedItem is not string quality) return;
        state.Settings.Quality = quality;
        foreach (var t in VisibleTracks.Where(t => !t.IsLocal && (t.Status is "Queued" or "Failed"))) { t.Quality = quality; t.Refresh(); }
        Persist();
    }
    private void AlbumChanged(object sender, SelectionChangedEventArgs e) { }
    private void RefreshAlbums()
    {
        var selected = (AlbumCombo.SelectedItem as Album)?.Id;
        AlbumCombo.ItemsSource = new[] { inbox }.Concat(state.Albums).ToArray();
        AlbumCombo.SelectedItem = state.Albums.FirstOrDefault(a => a.Id == selected) ?? inbox;
        AlbumsList.ItemsSource = state.Albums; AlbumsList.Items.Refresh();
    }
    private string? AskName(string title, string initial = "")
    {
        var box = new TextBox { Text = initial, Margin = new Thickness(0, 14, 0, 20) };
        var panel = new StackPanel { Margin = new Thickness(24) };
        var dialog = new Window { Title = title, Owner = this, Width = 430, Height = 225, ResizeMode = ResizeMode.NoResize, WindowStartupLocation = WindowStartupLocation.CenterOwner, Background = Background, Content = panel };
        panel.Children.Add(new TextBlock { Text = title, FontSize = 20 }); panel.Children.Add(box);
        var button = new Button { Content = "Save Album", IsDefault = true, HorizontalAlignment = HorizontalAlignment.Right };
        button.Click += (_, _) => { if (!string.IsNullOrWhiteSpace(box.Text)) dialog.DialogResult = true; };
        panel.Children.Add(button); dialog.Loaded += (_, _) => { box.Focus(); box.SelectAll(); };
        return dialog.ShowDialog() == true ? FileManager.SafeName(box.Text.Trim()) : null;
    }
    private void AddAlbum(object sender, RoutedEventArgs e)
    {
        var name = AskName("New album"); if (name is null) return;
        if (state.Albums.Any(a => a.Name.Equals(name, StringComparison.OrdinalIgnoreCase))) { Notice.Text = "An album with that name already exists."; return; }
        state.Albums.Add(new() { Name = name }); RefreshAlbums(); Persist();
    }
    private void OpenAlbum(object sender, RoutedEventArgs e)
    {
        var album = (Album)((Button)sender).Tag;
        if (!queue.Running && linkImport is null)
        {
            var tracks = state.Tracks.Where(t => t.AlbumId == album.Id).ToArray();
            ModeCombo.SelectedItem = album.SourceUrl?.StartsWith("amazon-purchases:") == true || (tracks.Length > 0 && tracks.All(t => t.IsLocal)) ? "Amazon Music" : "YouTube";
        }
        ShowPage("Downloads"); selectedAlbumId = album.Id; AlbumCombo.SelectedItem = album; PageTitle.Text = album.Name; PageSubtitle.Text = album.Kind + " collection · files are saved in this collection’s folder."; Refresh();
    }
    private void RenameAlbum(object sender, RoutedEventArgs e)
    {
        if (queue.Running || linkImport is not null) { Notice.Text = "Wait for downloads or album import before renaming albums."; return; }
        var album = (Album)((Button)sender).Tag; var name = AskName("Rename album", album.Name); if (name is null) return;
        if (state.Albums.Any(a => a.Id != album.Id && a.Name.Equals(name, StringComparison.OrdinalIgnoreCase))) { Notice.Text = "An album with that name already exists."; return; }
        album.Name = name; UpdateDestinations(); RefreshAlbums(); Persist(); Notice.Text = "Album renamed. Previously downloaded files remain in their original folder.";
    }
    private void DeleteAlbum(object sender, RoutedEventArgs e)
    {
        if (queue.Running || linkImport is not null) { Notice.Text = "Wait for downloads or album import before deleting albums."; return; }
        var album = (Album)((Button)sender).Tag;
        if (MessageBox.Show(this, $"Delete '{album.Name}' from your library?\nTracks will move to Unsorted. Downloaded files will stay on disk.", "Delete album", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
        foreach (var t in state.Tracks.Where(t => t.AlbumId == album.Id)) t.AlbumId = null;
        state.Albums.Remove(album); UpdateDestinations(); RefreshAlbums(); Refresh(); Persist();
    }
    private void LoadSettings()
    {
        var s = state.Settings; SettingsQuality.SelectedItem = s.Quality; SettingsFolder.Text = s.DownloadFolder; ConcurrencyCombo.SelectedItem = s.Concurrency; RetriesCombo.SelectedItem = s.MaxRetries;
        OpenFolderCheck.IsChecked = s.OpenFolder; DuplicateCheck.IsChecked = s.CheckDuplicates; RetryCheck.IsChecked = s.AutoRetry; MetadataCheck.IsChecked = s.EmbedMetadata; ThumbnailCheck.IsChecked = s.EmbedThumbnail;
        StorageLabel.Text = "Library and settings: " + store.DirectoryPath;
    }
    private void SaveSettings(object sender, RoutedEventArgs e)
    {
        if (queue.Running || linkImport is not null) return;
        try
        {
            if (!Path.IsPathFullyQualified(SettingsFolder.Text.Trim())) throw new ArgumentException("Choose an absolute download folder path.");
            var s = state.Settings; s.DownloadFolder = Path.GetFullPath(SettingsFolder.Text.Trim());
            s.Quality = SettingsQuality.SelectedItem as string ?? "320 kbps"; s.Concurrency = (int)(ConcurrencyCombo.SelectedItem ?? 2); s.MaxRetries = (int)(RetriesCombo.SelectedItem ?? 1);
            s.OpenFolder = OpenFolderCheck.IsChecked == true; s.CheckDuplicates = DuplicateCheck.IsChecked == true; s.AutoRetry = RetryCheck.IsChecked == true; s.EmbedMetadata = MetadataCheck.IsChecked == true; s.EmbedThumbnail = ThumbnailCheck.IsChecked == true;
            QualityCombo.SelectedItem = s.Quality; UpdateDestinations(); Persist(); Refresh(); Notice.Text = "Preferences saved.";
        }
        catch (Exception ex) { Notice.Text = "Could not save preferences: " + ex.Message; }
    }
    private async Task CheckDependencies()
    {
        if (queue.Running || linkImport is not null) return;
        engineHealthy = false; dependencies = Dependencies.Find(); CreateQueue(); Notice.Text = dependencies.Message; Refresh();
        if (!dependencies.Ready) return;
        try { using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20)); await dependencies.Validate(timeout.Token); engineHealthy = true; Notice.Text = store.Warning ?? dependencies.Message; Refresh(); }
        catch (Exception ex) { Notice.Text = "Download engine check failed: " + ex.Message; DownloadButton.IsEnabled = false; }
    }
    private async void RecheckDependencies(object sender, RoutedEventArgs e) => await CheckDependencies();
    private void ShowHistoryFolder(object sender, RoutedEventArgs e) => OpenFolder(Path.GetDirectoryName((string)((Button)sender).Tag)!);
    private void OpenFolder(string path) { try { if (!Directory.Exists(path)) throw new DirectoryNotFoundException("The folder is no longer available."); Process.Start(new ProcessStartInfo("explorer.exe") { ArgumentList = { path }, UseShellExecute = false }); } catch (Exception ex) { Notice.Text = ex.Message; } }
    private void ClearErrors(object sender, RoutedEventArgs e) { state.Errors.Clear(); Refresh(); Persist(); }
    private void CopyError(object sender, RoutedEventArgs e) { try { var error = (ErrorRecord)((Button)sender).Tag; Clipboard.SetText($"{error.Time:g}\n{error.Title}\n{error.Url}\n{error.Reason}\n\n{error.Details}"); Notice.Text = "Error copied."; } catch (Exception ex) { Notice.Text = "Clipboard unavailable: " + ex.Message; } }
    private void QueueMouseDown(object sender, MouseButtonEventArgs e)
    {
        dragStart = e.GetPosition(QueueList); dragTrack = (ItemsControl.ContainerFromElement(QueueList, e.OriginalSource as DependencyObject) as ListBoxItem)?.DataContext as Track;
    }
    private void QueueMouseMove(object sender, MouseEventArgs e)
    {
        if (queue.Running || dragTrack is null || e.LeftButton != MouseButtonState.Pressed) return;
        if ((e.GetPosition(QueueList) - dragStart).Length > 10) { var track = dragTrack; dragTrack = null; DragDrop.DoDragDrop(QueueList, track, DragDropEffects.Move); }
    }
    private void QueueDrop(object sender, DragEventArgs e)
    {
        if (queue.Running || e.Data.GetData(typeof(Track)) is not Track source) return;
        var target = (ItemsControl.ContainerFromElement(QueueList, e.OriginalSource as DependencyObject) as ListBoxItem)?.DataContext as Track;
        if (target is not null && target != source) { state.Tracks.Move(state.Tracks.IndexOf(source), state.Tracks.IndexOf(target)); Refresh(); Persist(); }
    }
    private async void WindowClosing(object? sender, CancelEventArgs e)
    {
        if (linkImport is not null) { e.Cancel = true; linkImport.Cancel(); while (linkImport is not null) await Task.Delay(100); Close(); return; }
        if (queue.Running) { e.Cancel = true; queue.Cancel(); Notice.Text = "Stopping downloads before closing…"; while (queue.Running) await Task.Delay(100); Close(); return; }
        state.Settings.Width = RestoreBounds.Width; state.Settings.Height = RestoreBounds.Height; Persist();
    }
}
