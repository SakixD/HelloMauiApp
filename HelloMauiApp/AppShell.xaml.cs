using HelloMauiApp.Services;
using LabelPrinting.Models;

namespace HelloMauiApp;

/// <summary>
/// Hauptfenster: Titelleiste (normale Inhalts-Zeile, keine native Einbindung – Windows zeichnet seine
/// eigene Standard-Titelleiste) + einklappbare Nav-Rail links + Inhaltsbereich. Alle 7 Rail-Ziele sind
/// dauerhaft im Shell eingebettete <see cref="IShellSectionView"/>-Instanzen, die beim Klick per
/// Content-Tausch aktiviert werden (kein <c>Navigation.PushAsync</c> mehr für Rail-Ziele – nur echte
/// Drill-downs von innerhalb einer Sektion bleiben gepushte Seiten, siehe <see cref="INavigationService"/>).
/// Läuft in einer <see cref="NavigationPage"/> (siehe App.xaml.cs), deren eigene Navigationsleiste hier
/// bewusst ausgeblendet wird – nur damit Push/Pop für Drill-downs überhaupt zur Verfügung steht.
/// </summary>
public partial class AppShell : ContentPage
{
	readonly AppearanceService _appearanceService;
	readonly INavigationService _navigationService;
	readonly Dictionary<string, (Border Row, Microsoft.Maui.Controls.Shapes.Path Icon, Label Label, BoxView Stripe)> _navRows = [];
	readonly Dictionary<string, View> _sections = [];

	string _activeSection = "home";
	bool _railCollapsed;

	public AppShell(
		MainPage home,
		DesignerPage designer,
		TemplateManagerPage templateManager,
		MediaLibraryPage mediaLibrary,
		PlaceholderLibraryPage placeholderLibrary,
		ZplConsolePage zplConsole,
		AppearanceSettingsPage appearanceSettings,
		AppearanceService appearanceService,
		INavigationService navigationService)
	{
		InitializeComponent();
		NavigationPage.SetHasNavigationBar(this, false);

		_appearanceService = appearanceService;
		_navigationService = navigationService;

		RegisterNavRows();

		home.ViewModel.NavigateToSection = section => _ = NavigateTo(section);
		templateManager.OpenInDesigner = template => { designer.LoadTemplate(template); _ = NavigateTo("designer"); };
		placeholderLibrary.ViewModel.OpenInDesigner = template => { designer.LoadTemplate(template); _ = NavigateTo("designer"); };

		_sections["home"] = home;
		_sections["designer"] = designer;
		_sections["templates"] = templateManager;
		_sections["zpl"] = zplConsole;
		_sections["settings"] = appearanceSettings;
		_sections["media"] = mediaLibrary;
		_sections["placeholders"] = placeholderLibrary;

		ContentHost.Content = _sections["home"];
		UpdateRailHighlight();

		if (DeviceInfo.Current.Idiom == DeviceIdiom.Phone)
		{
			_railCollapsed = true;
			ApplyRailCollapsedState();
		}
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();

		if (_sections.TryGetValue(_activeSection, out var view) && view is IShellSectionView sectionView)
			await sectionView.OnActivatedAsync();
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
		if (_sections.TryGetValue(section, out var view))
		{
			_activeSection = section;
			UpdateRailHighlight();
			TitleBarSectionLabel.Text = "— " + SectionTitle(section);

			ContentHost.Content = view;
			if (view is IShellSectionView sectionView)
				await sectionView.OnActivatedAsync();
			return;
		}

		if (section == "templatetest")
			await _navigationService.PushAsync(new TemplateTestPage());
	}

	void OnHomeTapped(object? sender, TappedEventArgs e) => _ = NavigateTo("home");
	void OnDesignerTapped(object? sender, TappedEventArgs e) => _ = NavigateTo("designer");
	void OnTemplatesTapped(object? sender, TappedEventArgs e) => _ = NavigateTo("templates");
	void OnMediaTapped(object? sender, TappedEventArgs e) => _ = NavigateTo("media");
	void OnPlaceholdersTapped(object? sender, TappedEventArgs e) => _ = NavigateTo("placeholders");
	void OnZplTapped(object? sender, TappedEventArgs e) => _ = NavigateTo("zpl");
	void OnSettingsTapped(object? sender, TappedEventArgs e) => _ = NavigateTo("settings");
}
