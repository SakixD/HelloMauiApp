using HelloMauiApp.Resources.Styles;

namespace HelloMauiApp.Services;

public enum AppThemePreference
{
	System,
	Light,
	Dark,
}

/// <summary>
/// Verwaltet Theme (System/Hell/Dunkel) und Akzentfarbe als Laufzeit-Einstellung: persistiert über
/// <see cref="Preferences"/>, angewendet über <see cref="Application.UserAppTheme"/> plus Ein-/Aushängen
/// der passenden Farbtoken-Dictionary (<see cref="TokensLight"/>/<see cref="TokensDark"/>) in
/// <c>Application.Current.Resources.MergedDictionaries</c>. Alle konsumierenden Styles/Seiten binden
/// über <c>{DynamicResource}</c>, damit ein Wechsel bereits angezeigte Inhalte sofort neu einfärbt.
/// </summary>
public class AppearanceService
{
	public static AppearanceService Instance { get; } = new();

	const string ThemeKey = "appearance_theme";
	const string AccentIndexKey = "appearance_accent_index";

	/// <summary>Presets aus dem Design: Lila, Blau, Türkis (Standard), Pink.</summary>
	public static readonly Color[] AccentPresets =
	[
		Color.FromArgb("#8B7BFF"),
		Color.FromArgb("#4F7CFF"),
		Color.FromArgb("#2DD4BF"),
		Color.FromArgb("#FF5D8F"),
	];

	const int DefaultAccentIndex = 2;

	TokensLight? _lightTokens;
	TokensDark? _darkTokens;

	public AppThemePreference Theme { get; private set; } = AppThemePreference.System;
	public int AccentIndex { get; private set; } = DefaultAccentIndex;
	public Color AccentColor => AccentPresets[AccentIndex];

	AppearanceService()
	{
	}

	/// <summary>Muss vor der ersten Fenster-/Seitenerzeugung aufgerufen werden (siehe App-Konstruktor), damit kein falsch eingefärbter erster Frame sichtbar wird.</summary>
	public void Initialize()
	{
		Theme = Enum.TryParse<AppThemePreference>(Preferences.Default.Get(ThemeKey, nameof(AppThemePreference.System)), out var theme)
			? theme
			: AppThemePreference.System;
		AccentIndex = Math.Clamp(Preferences.Default.Get(AccentIndexKey, DefaultAccentIndex), 0, AccentPresets.Length - 1);

		Application.Current!.RequestedThemeChanged += (_, _) => ApplyTokensForCurrentTheme();

		SetTheme(Theme, persist: false);
	}

	public void SetTheme(AppThemePreference theme, bool persist = true)
	{
		Theme = theme;
		if (persist)
			Preferences.Default.Set(ThemeKey, theme.ToString());

		Application.Current!.UserAppTheme = theme switch
		{
			AppThemePreference.Light => AppTheme.Light,
			AppThemePreference.Dark => AppTheme.Dark,
			_ => AppTheme.Unspecified,
		};

		ApplyTokensForCurrentTheme();
	}

	public void SetAccent(int index, bool persist = true)
	{
		AccentIndex = Math.Clamp(index, 0, AccentPresets.Length - 1);
		if (persist)
			Preferences.Default.Set(AccentIndexKey, AccentIndex);

		ApplyAccent();
	}

	bool IsEffectivelyDark => Application.Current!.RequestedTheme == AppTheme.Dark;

	void ApplyTokensForCurrentTheme()
	{
		var resources = Application.Current!.Resources;
		_lightTokens ??= new TokensLight();
		_darkTokens ??= new TokensDark();

		resources.MergedDictionaries.Remove(_lightTokens);
		resources.MergedDictionaries.Remove(_darkTokens);
		resources.MergedDictionaries.Add(IsEffectivelyDark ? _darkTokens : _lightTokens);

		ApplyAccent();
	}

	void ApplyAccent()
	{
		bool dark = IsEffectivelyDark;
		var resources = Application.Current!.Resources;

		resources["AccentColor"] = AccentColor;
		resources["AccentHoverColor"] = AccentColor;
		resources["AccentSoftColor"] = dark ? Color.FromArgb("#1AFFFFFF") : Color.FromArgb("#0D000000");
		resources["AccentTextColor"] = dark ? Color.FromArgb("#151221") : Colors.White;
	}
}
