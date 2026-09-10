using MultiMP3.Core;
namespace MultiMP3.Services;
public class QueueManager(ProjectState state, IMediaService media, Action<Action> dispatch)
{
    private CancellationTokenSource? cancellation;
    public bool Running { get; private set; }
    public bool Paused { get; private set; }
    public event Action? Changed;
    public void TogglePause() { Paused = !Paused; Changed?.Invoke(); }
    public void Cancel() => cancellation?.Cancel();
    private void Update(Action action) => dispatch(() => { action(); Changed?.Invoke(); });
    public async Task Run(IEnumerable<Track> tracks)
    {
        if (Running) return;
        Running = true; Paused = false; cancellation = new();
        var token = cancellation.Token;
        var pending = new System.Collections.Concurrent.ConcurrentQueue<Track>(tracks.Where(t => t.Status == "Queued"));
        Changed?.Invoke();
        async Task Worker()
        {
            while (!token.IsCancellationRequested)
            {
                while (Paused && !token.IsCancellationRequested) await Task.Delay(150);
                if (token.IsCancellationRequested || !pending.TryDequeue(out var track)) break;
                var retries = state.Settings.AutoRetry ? state.Settings.MaxRetries : 0;
                for (var attempt = 0; attempt <= retries; attempt++)
                {
                    try
                    {
                        Update(() => { track.Status = "Reading Info"; track.Progress = 0; });
                        await media.ReadInfo(track, token);
                        token.ThrowIfCancellationRequested();
                        Update(() => track.Status = track.IsLocal ? "Copying" : "Downloading");
                        var album = state.Albums.FirstOrDefault(a => a.Id == track.AlbumId);
                        var path = await media.Download(track, album, state.Settings, (p, status) => Update(() => { track.Progress = p; track.Status = status; }), token);
                        Update(() => { track.Destination = path; track.Progress = 100; track.Status = "Completed"; state.History.Insert(0, new(DateTime.Now, track.Title, track.Url, path, track.Quality)); });
                        break;
                    }
                    catch (OperationCanceledException) when (token.IsCancellationRequested) { Update(() => { track.Status = "Queued"; track.Progress = 0; }); break; }
                    catch (Exception ex)
                    {
                        if (attempt < retries) { try { await Task.Delay(1000 * (attempt + 1), token); } catch (OperationCanceledException) { Update(() => track.Status = "Queued"); break; } continue; }
                        Update(() => { track.Status = "Failed"; state.Errors.Insert(0, new(DateTime.Now, track.Title, track.Url, SimpleReason(ex), ex.ToString())); });
                    }
                }
            }
        }
        try { await Task.WhenAll(Enumerable.Range(0, Math.Clamp(state.Settings.Concurrency, 1, 4)).Select(_ => Worker())); }
        finally { Running = false; Paused = false; cancellation.Dispose(); cancellation = null; Changed?.Invoke(); }
    }
    private static string SimpleReason(Exception ex)
    {
        if (ex is OperationCanceledException) return "Reading media information timed out. Retry when your connection is available.";
        if (ex is System.IO.IOException or UnauthorizedAccessException) return "Could not access the download file or folder. Check the path and permissions.";
        var lines = ex.Message.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var reason = lines.LastOrDefault(l => l.Contains("ERROR", StringComparison.OrdinalIgnoreCase)) ?? lines.LastOrDefault() ?? "Download failed. See technical details.";
        return reason.Length > 260 ? reason[..260] : reason;
    }
}
