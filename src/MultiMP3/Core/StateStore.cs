using System.IO;
using System.Text.Json;
namespace MultiMP3.Core;
public class StateStore
{
    public string DirectoryPath { get; }
    public string? Warning { get; private set; }
    private string FilePath => Path.Combine(DirectoryPath, "library.json");
    public StateStore(string? path = null) => DirectoryPath = path ?? Environment.GetEnvironmentVariable("MULTIMP3_DATA_DIR") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MultiMP3");
    public ProjectState Load()
    {
        if (!File.Exists(FilePath)) return new();
        try { return Read(FilePath); }
        catch (Exception ex)
        {
            Warning = "The library could not be read. " + ex.Message;
            try { var state = Read(FilePath + ".bak"); Warning += " Recovered the previous backup."; return state; }
            catch { Warning += " Original data was kept; starting a new library."; File.Copy(FilePath, FilePath + ".damaged-" + DateTime.Now.ToString("yyyyMMddHHmmss"), true); return new(); }
        }
    }
    private static ProjectState Read(string path)
    {
        var state = JsonSerializer.Deserialize<ProjectState>(File.ReadAllText(path)) ?? throw new InvalidDataException("Empty library");
        if (state.Settings is null || state.Tracks is null || state.Albums is null || state.History is null || state.Errors is null) throw new InvalidDataException("Incomplete library");
        state.Settings.Concurrency = Math.Clamp(state.Settings.Concurrency, 1, 4);
        state.Settings.MaxRetries = Math.Clamp(state.Settings.MaxRetries, 0, 5);
        foreach (var track in state.Tracks.Where(t => t.IsActive)) { track.Status = "Queued"; track.Progress = 0; }
        return state;
    }
    public void Save(ProjectState state)
    {
        Directory.CreateDirectory(DirectoryPath);
        var temp = FilePath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true }));
        if (File.Exists(FilePath)) File.Replace(temp, FilePath, FilePath + ".bak");
        else File.Move(temp, FilePath);
    }
}
