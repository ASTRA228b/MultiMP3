using Avalonia;

namespace MultiMP3.Mac;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args) => Build().StartWithClassicDesktopLifetime(args);
    private static AppBuilder Build() => AppBuilder.Configure<App>().UsePlatformDetect().WithInterFont().LogToTrace();
}
