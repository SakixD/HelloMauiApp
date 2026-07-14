namespace HelloMauiApp;

/// <summary>
/// Übergangs-Platzhalter für Rail-Bereiche, die im neuen Design (Phase 1) noch keine eigenständige
/// Seite haben, weil die zugrunde liegende Funktion heute nur im Kontext einer Vorlage existiert
/// (z.B. Medien/Platzhalter, siehe <see cref="MediaManagerPage"/>/<see cref="PlaceholderManagerPage"/>,
/// die beide einen <c>LabelTemplate</c>-Konstruktorparameter benötigen). Wird in einer späteren Phase
/// durch eine echte, ins Shell eingebettete Ansicht ersetzt.
/// </summary>
public partial class ComingSoonPage : ContentPage
{
	public ComingSoonPage(string sectionTitle, string message, string actionLabel, Func<Task> action)
	{
		InitializeComponent();
		Title = sectionTitle;
		TitleLabel.Text = sectionTitle;
		MessageLabel.Text = message;
		ActionBtn.Text = actionLabel;
		_action = action;
	}

	readonly Func<Task> _action;

	async void OnActionClicked(object? sender, EventArgs e) => await _action();
}
