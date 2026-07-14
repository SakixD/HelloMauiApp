namespace HelloMauiApp.Services;

/// <summary>
/// Vertrag für Ansichten, die als Rail-Ziel dauerhaft in <see cref="HelloMauiApp.AppShell"/> eingebettet
/// sind (statt gepusht zu werden). <see cref="OnActivatedAsync"/> wird sowohl beim Wechsel per Rail-Klick
/// als auch bei der Rückkehr von einem Drill-down (gepushte Unterseite) aufgerufen – ersetzt einheitlich
/// das frühere <c>OnAppearing</c>-Muster einzelner <c>ContentPage</c>s.
/// </summary>
public interface IShellSectionView
{
	Task OnActivatedAsync();
}
