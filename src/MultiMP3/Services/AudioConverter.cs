using MultiMP3.Core;
namespace MultiMP3.Services;
public static class AudioConverter
{
    public static Task<string> Convert(string ffmpeg, string input, string output, string? cover, Track track, Album? album, Settings settings, CancellationToken token)
    {
        if (System.IO.File.Exists(output)) throw new System.IO.IOException("The output file already exists; conversion will not overwrite it.");
        var args = new List<string> { "-nostdin", "-hide_banner", "-loglevel", "error", "-n", "-i", input };
        if (cover is not null) args.AddRange(["-i", cover]);
        args.AddRange(["-map", "0:a:0", "-c:a", "libmp3lame"]);
        if (track.Quality == "Best Available") args.AddRange(["-q:a", "0"]);
        else args.AddRange(["-b:a", track.Quality.Split(' ')[0] + "k"]);
        if (settings.EmbedMetadata)
        {
            args.AddRange(["-metadata", "title=" + track.Title, "-metadata", "artist=" + track.Artist]);
            if (album is not null) args.AddRange(["-metadata", "album=" + album.Name]);
        }
        else args.AddRange(["-map_metadata", "-1"]);
        if (cover is not null) args.AddRange(["-map", "1:v:0", "-c:v", "mjpeg", "-disposition:v:0", "attached_pic", "-metadata:s:v", "title=Album cover", "-metadata:s:v", "comment=Cover (front)"]);
        args.AddRange(["-id3v2_version", "3", output]);
        return ProcessRunner.Run(ffmpeg, args, null, token);
    }
}
