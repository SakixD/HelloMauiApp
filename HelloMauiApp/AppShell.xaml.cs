using HelloMauiApp.Services;
using Microsoft.Extensions.DependencyInjection;

#if WINDOWS
using Microsoft.UI.Windowing;
using WinRT.Interop;
#endif

namespace HelloMauiApp;

/// <summary>
/// Hauptfenster im neuen Design: eigene Titelleiste (Windows: echte Fenstersteuerung, siehe unten) +
/// einklappbare Nav-Rail links + Inhaltsbereich. "Start" läuft dauerhaft als <see cref="MainPage"/>
/// (ContentView) im Inhaltsbereich; alle anderen Rail-Ziele sind in Phase 1 eine Brücke auf die
/// bestehenden, unveränderten Seiten via <c>Navigation.PushAsync</c> (siehe NavigateTo). Läuft in
/// einer <see cref="NavigationPage"/> (siehe App.xaml.cs), deren eigene Navigationsleiste hier bewusst
/// ausgeblendet wird – nur damit Push/Pop überhaupt zur Verfügung steht.
/// </summary>
public partial class AppShell : ContentPage
{
	readonly MainPage _home;
	readonly AppearanceService _appearanceService;
	readonly IServiceProvider _serviceProvider;
	readonly Dictionary<string, (Border Row, Microsoft.Maui.Controls.Shapes.Path Icon, Label Label, BoxView Stripe)> _navRows = [];

	string _activeSection = "home";
	bool _railCollapsed;

	public AppShell(MainPage home, AppearanceService appearanceService, IServiceProvider serviceProvider)
	{
		InitializeComponent();
		NavigationPage.SetHasNavigationBar(this, false);

		_appearanceService = appearanceService;
		_serviceProvider = serviceProvider;

		RegisterNavRows();
		_home = home;
		_home.ViewModel.NavigateToSection = section => _ = NavigateTo(section);
		ContentHost.Content = _home;
		UpdateRailHighlight();

		if (DeviceInfo.Current.Idiom == DeviceIdiom.Phone)
		{
			_railCollapsed = true;
			ApplyRailCollapsedState();
		}

		Loaded += OnAppShellLoaded;
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();

		_activeSection = "home";
		UpdateRailHighlight();
		TitleBarSectionLabel.Text = "— Start";

		await _home.ViewModel.RefreshAsync();
	}

	void RegisterNavRows()
	{
		_navRows["home"] = (HomeRow, HomeIcon, HomeLabelText, HomeStripe);
		_navRows["designer"] = (DesignerRow, DesignerIcon, DesignerLabelText, DesignerStripe);
		_navRows["templates"] = (TemplatesRow, TemplatesIcon, TemplatesLabelText, TemplatesStripe);
		_navRows["media"] = (MediaRow, MediaIcon, MediaLabelText, MediaStripe);
		_navRows["placeholders"] = (PlaceholdersRow, PlaceholdersIcon, PlaceholdersLabelText, PlaceholdersStripe);
		_navRows["zpl"] = (ZplRow, ZplIcon, ZplLabelText, ZplStripe);
		_navRows["settings"] = (SettingsRow, SettingsIcon, SettingsLabelText, SettingsStripe);
	}

	static string SectionTitle(string section) => section switch
	{
		"designer" => "Label-Designer",
		"templates" => "Vorlagen",
		"media" => "Medien",
		"placeholders" => "Platzhalter",
		"zpl" => "ZPL-Konsole",
		"settings" => "Einstellungen",
		_ => "Start",
	};

	void UpdateRailHighlight()
	{
		foreach (var (section, views) in _navRows)
		{
			bool isActive = section == _activeSection;

			if (isActive)
				views.Row.SetDynamicResource(BackgroundColorProperty, "AccentSoftColor");
			else
				views.Row.BackgroundColor = Colors.Transparent;

			views.Stripe.IsVisible = isActive;
			views.Icon.SetDynamicResource(Microsoft.Maui.Controls.Shapes.Shape.StrokeProperty, isActive ? "AccentColor" : "ColorText2");
			views.Label.SetDynamicResource(Label.TextColorProperty, isActive ? "AccentColor" : "ColorText2");
		}
	}

	void ApplyRailCollapsedState()
	{
		RailGrid.WidthRequest = _railCollapsed ? 60 : 248;
		bool showLabels = !_railCollapsed;
		foreach (var views in _navRows.Values)
			views.Label.IsVisible = showLabels;
	}

	void OnToggleRailClicked(object? sender, EventArgs e)
	{
		_railCollapsed = !_railCollapsed;
		ApplyRailCollapsedState();
	}

	void OnThemeToggleClicked(object? sender, EventArgs e)
	{
		var next = Application.Current!.RequestedTheme == AppTheme.Dark ? AppThemePreference.Light : AppThemePreference.Dark;
		_appearanceService.SetTheme(next);
	}

	async Task NavigateTo(string section)
	{
		if (_navRows.ContainsKey(section))
		{
			_activeSection = section;
			UpdateRailHighlight();
			TitleBarSectionLabel.Text = "— " + SectionTitle(section);
		}

		switch (section)
		{
			case "home":
				ContentHost.Content = _home;
				await _home.ViewModel.RefreshAsync();
				break;

			case "designer":
				await Navigation.PushAsync(new DesignerPage());
				break;

			case "templates":
				await Navigation.PushAsync(new TemplateManagerPage());
				break;

			case "settings":
				await Navigation.PushAsync(_serviceProvider.GetRequiredService<AppearanceSettingsPage>());
				break;

			case "media":
				await Navigation.PushAsync(new ComingSoonPage(
					"Medien",
					"Medien-Presets werden aktuell im Kontext einer Vorlage verwaltet (Label-Designer → Medium/Größe). Eine eigenständige Medienverwaltung an dieser Stelle folgt in einer späteren Phase.",
					"Vorlagen öffnen",
					async () => { await Navigation.PopAsync(); await NavigateTo("templates"); }));
				break;

			case "placeholders":
				await Navigation.PushAsync(new ComingSoonPage(
					"Platzhalter",
					"Platzhalter werden aktuell pro Vorlage verwaltet (Label-Designer → Platzhalter verwalten). Eine eigenständige Platzhalterverwaltung an dieser Stelle folgt in einer späteren Phase.",
					"Label-Designer öffnen",
					async () => { await Navigation.PopAsync(); await NavigateTo("designer"); }));
				break;

			case "zpl":
				await Navigation.PushAsync(new ZplConsolePage());
				break;

			case "templatetest":
				await Navigation.PushAsync(new TemplateTestPage());
				break;
		}
	}

	void OnHomeTapped(object? sender, TappedEventArgs e) => _ = NavigateTo("home");
	void OnDesignerTapped(object? sender, TappedEventArgs e) => _ = NavigateTo("designer");
	void OnTemplatesTapped(object? sender, TappedEventArgs e) => _ = NavigateTo("templates");
	void OnMediaTapped(object? sender, TappedEventArgs e) => _ = NavigateTo("media");
	void OnPlaceholdersTapped(object? sender, TappedEventArgs e) => _ = NavigateTo("placeholders");
	void OnZplTapped(object? sender, TappedEventArgs e) => _ = NavigateTo("zpl");
	void OnSettingsTapped(object? sender, TappedEventArgs e) => _ = NavigateTo("settings");

	// ---------- Windows: eigene Titelleiste mit echter Fenstersteuerung ----------

	void OnAppShellLoaded(object? sender, EventArgs e)
	{
#if WINDOWS
		SetupWindowsTitleBar();
#endif
	}

#if WINDOWS
	Microsoft.UI.Windowing.AppWindow? _appWindow;

	/// <summary>
	/// Minimieren/Maximieren/Schließen kommen bewusst von den echten System-Schaltflächen, die
	/// Windows nach <see cref="Microsoft.UI.Xaml.Window.ExtendsContentIntoTitleBar"/> automatisch
	/// transparent über den rechten Rand der Titelleiste zeichnet – eigene Buttons dafür würden sich
	/// mit den System-Buttons überlagern (zwei Bedienelemente für dieselbe Aktion). Dieser Code
	/// bereitet ihnen nur farblich den Weg (transparenter Hintergrund, zum Theme passende Glyphen)
	/// und reserviert per <see cref="SystemButtonsColumn"/> den Platz, den Windows dafür braucht,
	/// damit eigener Inhalt (Theme-Umschalter) nicht darunter verschwindet.
	/// </summary>
	void SetupWindowsTitleBar()
	{
		if (Window?.Handler?.PlatformView is not Microsoft.UI.Xaml.Window nativeWindow)
			return;

		nativeWindow.ExtendsContentIntoTitleBar = true;

		var hwnd = WindowNative.GetWindowHandle(nativeWindow);
		var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
		_appWindow = AppWindow.GetFromWindowId(windowId);

		if (TitleBarGrid.Handler?.PlatformView is Microsoft.UI.Xaml.FrameworkElement titleBarElement)
			nativeWindow.SetTitleBar(titleBarElement);

		ApplyWindowsTitleBarButtonColors();
		UpdateSystemButtonsReservedWidth();

		if (_appWindow is not null)
		{
			_appWindow.Changed += (_, args) =>
			{
				if (args.DidSizeChange || args.DidPresenterChange)
					UpdateSystemButtonsReservedWidth();
			};
		}

		Application.Current!.RequestedThemeChanged += (_, _) => ApplyWindowsTitleBarButtonColors();
	}

	void ApplyWindowsTitleBarButtonColors()
	{
		var titleBar = _appWindow?.TitleBar;
		if (titleBar is null)
			return;

		bool dark = Application.Current!.RequestedTheme == AppTheme.Dark;
		var foreground = dark ? Windows.UI.Color.FromArgb(255, 168, 168, 179) : Windows.UI.Color.FromArgb(255, 92, 92, 102);
		var hoverBackground = dark ? Windows.UI.Color.FromArgb(26, 255, 255, 255) : Windows.UI.Color.FromArgb(13, 0, 0, 0);

		titleBar.ButtonBackgroundColor = Microsoft.UI.Colors.Transparent;
		titleBar.ButtonInactiveBackgroundColor = Microsoft.UI.Colors.Transparent;
		titleBar.ButtonForegroundColor = foreground;
		titleBar.ButtonInactiveForegroundColor = foreground;
		titleBar.ButtonHoverForegroundColor = foreground;
		titleBar.ButtonHoverBackgroundColor = hoverBackground;
		titleBar.ButtonPressedBackgroundColor = hoverBackground;
	}

	void UpdateSystemButtonsReservedWidth()
	{
		var titleBar = _appWindow?.TitleBar;
		if (titleBar is null || TitleBarGrid.Handler?.PlatformView is not Microsoft.UI.Xaml.FrameworkElement element)
			return;

		double scale = element.XamlRoot?.RasterizationScale ?? 1.0;
		SystemButtonsColumn.Width = new GridLength(Math.Max(0, titleBar.RightInset / scale));
	}
#endif
}
