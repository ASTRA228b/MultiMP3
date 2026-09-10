using System.Diagnostics;
using System.Reflection;
using System.Windows;
namespace MultiMP3;

public partial class MainWindow
{
    // The SDK generates this attribute from <Version> in MultiMP3.csproj.
    private static string ApplicationVersion => typeof(MainWindow).Assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? "Version unavailable";

    private void VisitEcosystem(object sender, RoutedEventArgs e)
    {
        try { Process.Start(new ProcessStartInfo("https://chudgpt-landing.vercel.app/") { UseShellExecute = true }); }
        catch (Exception ex) { Notice.Text = "Could not open the ChudGPT website: " + ex.Message; }
    }
}
