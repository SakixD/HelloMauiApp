using CommunityToolkit.Mvvm.ComponentModel;

namespace HelloMauiApp.ViewModels;

/// <summary>Ein wählbares Akzentfarben-Preset in der Erscheinungsbild-Einstellung (siehe <see cref="AppearanceSettingsViewModel"/>).</summary>
public partial class AccentSwatch : ObservableObject
{
	public Color Color { get; }

	public int Index { get; }

	[ObservableProperty]
	bool isSelected;

	public AccentSwatch(Color color, int index, bool isSelected)
	{
		Color = color;
		Index = index;
		this.isSelected = isSelected;
	}
}
