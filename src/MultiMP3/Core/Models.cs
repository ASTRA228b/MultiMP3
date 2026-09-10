using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
namespace MultiMP3.Core;

public class Observable : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    protected void Set<T>(ref T field, T value, [CallerMemberName] string? name = null) { field = value; PropertyChanged?.Invoke(this, new(name)); }
    public void Refresh() => PropertyChanged?.Invoke(this, new(null));
}
public class Track : Observable
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Url { get; set; } = "";
    public string? LocalSourcePath { get; set; }
    [JsonIgnore] public bool IsLocal => !string.IsNullOrEmpty(LocalSourcePath);
    public string? AlbumId { get; set; }
    private string title = "Ready to read media info";
    public string Title { get => title; set => Set(ref title, value); }
    private string status = "Queued";
    public string Status { get => status; set { Set(ref status, value); Refresh(); } }
    private double progress;
    public double Progress { get => progress; set => Set(ref progress, value); }
    private string thumbnail = "";
    public string Thumbnail { get => thumbnail; set => Set(ref thumbnail, value); }
    public string Quality { get; set; } = "320 kbps";
    private string destination = "";
    public string Destination { get => destination; set => Set(ref destination, value); }
    public string Artist { get; set; } = "";
    [JsonIgnore] public bool IsActive => Status is "Reading Info" or "Downloading" or "Converting" or "Copying";
    [JsonIgnore] public string StatusColor => Status switch { "Completed" => "#85D9B4", "Failed" => "#F092A5", "Skipped" => "#D8BC83", _ => "#B794F6" };
}
public class Album
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public string? SourceUrl { get; set; }
    [JsonIgnore] public string Kind => SourceUrl is not null && FileManager.TryCollectionUrl(SourceUrl, out _) && !FileManager.TryAlbumUrl(SourceUrl, out _) ? "Playlist" : "Album";
    public override string ToString() => Name;
}
public record DownloadRecord(DateTime Time, string Title, string Url, string Path, string Quality);
public record ErrorRecord(DateTime Time, string Title, string Url, string Reason, string Details);
public class Settings
{
    public string SourceMode { get; set; } = "YouTube";
    public string DownloadFolder { get; set; } = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyMusic), "MultiMP3");
    public string Quality { get; set; } = "320 kbps";
    public int Concurrency { get; set; } = 2;
    public bool OpenFolder { get; set; }
    public bool AutoRetry { get; set; } = true;
    public int MaxRetries { get; set; } = 1;
    public bool CheckDuplicates { get; set; } = true;
    public bool EmbedMetadata { get; set; } = true;
    public bool EmbedThumbnail { get; set; }
    public double Width { get; set; } = 1280;
    public double Height { get; set; } = 860;
}
public class ProjectState
{
    public Settings Settings { get; set; } = new();
    public ObservableCollection<Track> Tracks { get; set; } = new();
    public ObservableCollection<Album> Albums { get; set; } = new();
    public ObservableCollection<DownloadRecord> History { get; set; } = new();
    public ObservableCollection<ErrorRecord> Errors { get; set; } = new();
}
