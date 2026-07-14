using HelloMauiApp.Services;

namespace HelloMauiApp;

/// <summary>
/// Platzhalter für Rail-Bereiche, die noch keine eigenständige Verwaltung haben, weil die zugrunde
/// liegende Funktion heute nur im Kontext einer Vorlage existiert (z.B. Medien/Platzhalter, siehe
/// <see cref="MediaManagerPage"/>/<see cref="PlaceholderManagerPage"/>, die beide einen
/// <c>LabelTemplate</c>-Konstruktorparameter benötigen). Wird in einer späteren Phase durch eine echte
/// Verwaltung ersetzt.
/// </summary>
public partial class ComingSoonPage : ContentView, IShellSectionView
{
	public ComingSoonPage(string sectionTitle, string message, string actionLabel, Action action)
	{
		InitializeComponent();
		TitleLabel.Text = sectionTitle;
		MessageLabel.Text = message;
		ActionBtn.Text = actionLabel;
		_action = action;
	}

	readonly Action _action;

	void OnActionClicked(object? sender, EventArgs e) => _action();

	public Task OnActivatedAsync() => Task.CompletedTask;
}
