using HelloMauiApp.Services;
using HelloMauiApp.ViewModels;

namespace HelloMauiApp;

/// <summary>Startseite ("Start" in der Rail) – reine View, alle Logik/Zustand steckt in <see cref="ViewModel"/>.</summary>
public partial class MainPage : ContentView, IShellSectionView
{
	public MainPageViewModel ViewModel { get; }

	public MainPage(MainPageViewModel viewModel)
	{
		InitializeComponent();
		ViewModel = viewModel;
		BindingContext = viewModel;
	}

	/// <summary>
	/// Von AppShell beim Aktivieren der Sektion (App-Start, Rail-Klick, Rückkehr aus Drill-downs)
	/// aufgerufen. Fehlte bisher – dadurch wurden Drucker-Picker, Statuskarte und Statistik nie
	/// (nach-)geladen und "Verbindung testen"/"Kalibrieren" meldeten "kein Profil".
	/// </summary>
	public Task OnActivatedAsync() => ViewModel.RefreshAsync();
}
