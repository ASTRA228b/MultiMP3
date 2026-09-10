using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using MultiMP3.Core;
using MultiMP3.Services;
namespace MultiMP3;
public partial class MainWindow
{
    private void LocalGroupingChanged(object sender, RoutedEventArgs e) { if (ready) Refresh(false); }
    private void ModeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!ready || queue.Running || linkImport is not null) return;
        state.Settings.SourceMode = ModeCombo.SelectedItem as string ?? "YouTube";
        ShowPage("Downloads");
        Notice.Text = AmazonMode ? "Choose purchased MP3 files or a folder, then Import All to save organized copies." : dependencies.Message;
        Persist();
    }
    private async void ChooseAmazonFiles(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Title = "Choose purchased Amazon MP3 files", Filter = "MP3 audio (*.mp3)|*.mp3", Multiselect = true };
        if (dialog.ShowDialog(this) == true) await ImportLocalFilesAsync(dialog.FileNames);
    }
    private async void ChooseAmazonFolder(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "Choose a folder of purchased MP3s" };
        if (dialog.ShowDialog(this) != true) return;
        // Enumeration runs with the same cancellation and error reporting as file inspection.
        await ImportLocalFilesAsync([], dialog.FolderName);
    }
    private async Task ImportLocalFilesAsync(IEnumerable<string> selectedPaths, string? folder = null)
    {
        if (queue.Running || linkImport is not null) return;
        linkImport = new(); var token = linkImport.Token;
        var groupByAlbum = GroupLocalAlbums.IsChecked == true;
        var chosenAlbum = AlbumCombo.SelectedItem is Album { Id.Length: > 0 } selected ? selected : null;
        var added = 0; var skipped = 0; var failed = 0;
        Refresh(false);
        try
        {
            Notice.Text = "Reading purchased MP3 files… Cancel to stop.";
            var paths = await Task.Run(() =>
            {
                var source = folder is null ? selectedPaths : Directory.EnumerateFiles(folder, "*.mp3", new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = false, AttributesToSkip = FileAttributes.ReparsePoint });
                return source.Select(p => { token.ThrowIfCancellationRequested(); return Path.GetFullPath(p); }).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            }, token);
            bool allowDuplicates = !state.Settings.CheckDuplicates;
            var duplicateCount = paths.Count(p => LocalMusicImporter.IsDuplicate(state, p));
            if (!allowDuplicates && duplicateCount > 0)
                allowDuplicates = MessageBox.Show(this, $"{duplicateCount} files are already in your queue or history.\n\nAdd those files again? No adds only new files.", "Duplicate MP3 files", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
            var service = new LocalMusicService(dependencies.FFprobe);
            var songs = new List<LocalSong>();
            for (var i = 0; i < paths.Length; i++)
            {
                token.ThrowIfCancellationRequested();
                if (!allowDuplicates && LocalMusicImporter.IsDuplicate(state, paths[i])) { skipped++; continue; }
                Notice.Text = $"Reading MP3 {i + 1} of {paths.Length} · {Path.GetFileName(paths[i])}";
                try { songs.Add(await service.Inspect(paths[i], token)); }
                catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
                catch (Exception ex) { failed++; state.Errors.Insert(0, new(DateTime.Now, Path.GetFileName(paths[i]), paths[i], "Could not read this MP3. See details.", ex.ToString())); }
            }
            token.ThrowIfCancellationRequested();
            foreach (var song in songs.OrderBy(s => s.Album, StringComparer.OrdinalIgnoreCase).ThenBy(s => s.TrackNumber == 0 ? int.MaxValue : s.TrackNumber).ThenBy(s => s.Path, StringComparer.OrdinalIgnoreCase))
            { LocalMusicImporter.Add(state, song, groupByAlbum, chosenAlbum); added++; }
            RefreshAlbums(); ShowPage("Downloads");
            Notice.Text = $"{added} MP3s queued · {skipped} duplicates skipped · {failed} files could not be read. " + (added > 0 ? "Choose Import All to save copies." : "Choose MP3 files or another folder.");
        }
        catch (OperationCanceledException) { Notice.Text = "File scan canceled. No files from this scan were added."; }
        catch (Exception ex) { Notice.Text = "Could not scan files: " + ex.Message; state.Errors.Insert(0, new(DateTime.Now, "Amazon MP3 import", folder ?? "Selected files", "Could not scan purchased MP3s.", ex.ToString())); }
        finally { linkImport.Dispose(); linkImport = null; Refresh(); Persist(); }
    }
}
