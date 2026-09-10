using System.IO;
using System.Text.RegularExpressions;
namespace MultiMP3.Core;
public static class FileManager
{
    public static string SafeName(string name)
    {
        var clean = new string(name.Select(c => c < 32 || "<>:\"/\\|?*".Contains(c) ? '_' : c).ToArray()).Trim().TrimEnd('.');
        if (clean.Length > 100) clean = clean[..100].TrimEnd('.', ' ');
        if (string.IsNullOrWhiteSpace(clean)) clean = "Untitled";
        if (Regex.IsMatch(clean, @"^(CON|PRN|AUX|NUL|COM[0-9¹²³]|LPT[0-9¹²³])($|\.)", RegexOptions.IgnoreCase)) clean = "_" + clean;
        return clean;
    }
    public static string AlbumFolder(string root, Album? album) => album is null ? Path.GetFullPath(root) : Path.Combine(Path.GetFullPath(root), SafeName(album.Name));
    public static string CommitUnique(string source, string folder, string title)
    {
        Directory.CreateDirectory(folder);
        for (var i = 0; i < 10000; i++)
        {
            var path = Path.Combine(folder, SafeName(title) + (i == 0 ? "" : $" ({i + 1})") + ".mp3");
            try { File.Move(source, path, false); return path; }
            catch (IOException) when (File.Exists(path)) { }
        }
        throw new IOException("Too many files with the same title.");
    }
    public static bool TryUrl(string input, out string normalized)
    {
        normalized = "";
        if (!Uri.TryCreate(input.Trim(), UriKind.Absolute, out var uri) || uri.Scheme != "https" || !string.IsNullOrEmpty(uri.UserInfo)) return false;
        var host = uri.Host.ToLowerInvariant();
        string? id = null;
        if (host == "youtu.be") id = uri.AbsolutePath.Trim('/');
        else if (host is "youtube.com" or "www.youtube.com" or "m.youtube.com" or "music.youtube.com")
        {
            if (uri.AbsolutePath == "/watch") id = uri.Query.TrimStart('?').Split('&').FirstOrDefault(x => x.StartsWith("v="))?[2..];
            else if (uri.AbsolutePath.StartsWith("/shorts/") || uri.AbsolutePath.StartsWith("/live/")) id = uri.AbsolutePath.Split('/').ElementAtOrDefault(2);
        }
        if (id is null || !Regex.IsMatch(id, "^[A-Za-z0-9_-]{11}$")) return false;
        normalized = "https://www.youtube.com/watch?v=" + id;
        return true;
    }
    public static bool TryAlbumUrl(string input, out string normalized)
    {
        normalized = "";
        if (!Uri.TryCreate(input.Trim(), UriKind.Absolute, out var uri) || uri.Scheme != "https" || uri.UserInfo.Length > 0 || !uri.IsDefaultPort) return false;
        if (uri.Host is not ("music.youtube.com" or "www.youtube.com" or "youtube.com")) return false;
        if (uri.AbsolutePath.TrimEnd('/') == "/playlist")
        {
            var id = uri.Query.TrimStart('?').Split('&').FirstOrDefault(x => x.StartsWith("list="))?[5..];
            if (id is null || !Regex.IsMatch(id, "^OLAK5uy_[A-Za-z0-9_-]+$")) return false;
            normalized = "https://music.youtube.com/playlist?list=" + id;
            return true;
        }
        if (uri.Host == "music.youtube.com" && Regex.IsMatch(uri.AbsolutePath, "^/browse/MP(RE|AD)[A-Za-z0-9_-]+/?$"))
        {
            normalized = "https://music.youtube.com" + uri.AbsolutePath.TrimEnd('/');
            return true;
        }
        return false;
    }
    public static bool TryCollectionUrl(string input, out string normalized)
    {
        if (TryAlbumUrl(input, out normalized)) return true;
        if (!Uri.TryCreate(input.Trim(), UriKind.Absolute, out var uri) || uri.Scheme != "https" || uri.UserInfo.Length > 0 || !uri.IsDefaultPort) return false;
        if (uri.Host is not ("music.youtube.com" or "www.youtube.com" or "youtube.com" or "m.youtube.com") || uri.AbsolutePath.TrimEnd('/') != "/playlist") return false;
        var id = uri.Query.TrimStart('?').Split('&').FirstOrDefault(x => x.StartsWith("list="))?[5..];
        if (id is null || !Regex.IsMatch(id, "^(PL|UU)[A-Za-z0-9_-]+$")) return false;
        normalized = "https://music.youtube.com/playlist?list=" + id;
        return true;
    }
}
