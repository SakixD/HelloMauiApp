using Microsoft.UI.Xaml;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace HelloMauiApp.WinUI;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : MauiWinUIApplication
{
	/// <summary>
	/// Initializes the singleton application object.  This is the first line of authored code
	/// executed, and as such is the logical equivalent of main() or WinMain().
	/// </summary>
	public App()
	{
		// Letzte Diagnose-Instanz: WinUI meldet unbehandelte XAML-Fehler (z.B. StaticResource nicht
		// gefunden) nur als "stowed exception" 0xc000027b ohne Details im Event-Log. Dieser Handler
		// schreibt die echte Exception nach %TEMP%\hellomaui_crash.txt, bevor die App stirbt.
		this.UnhandledException += (s, e) =>
		{
			try
			{
				System.IO.File.WriteAllText(
					System.IO.Path.Combine(System.IO.Path.GetTempPath(), "hellomaui_crash.txt"),
					$"{DateTime.Now:O}\n{e.Message}\n\n{e.Exception}");
			}
			catch { /* Diagnose darf nie selbst crashen */ }
		};

		this.InitializeComponent();
	}

	protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}

