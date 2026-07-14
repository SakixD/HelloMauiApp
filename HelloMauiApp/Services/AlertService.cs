namespace HelloMauiApp.Services;

public class AlertService : IAlertService
{
	static Page CurrentPage => Application.Current!.Windows[0].Page!;

	public Task ShowAsync(string title, string message, string cancel = "OK") => CurrentPage.DisplayAlertAsync(title, message, cancel);

	public Task<bool> ConfirmAsync(string title, string message, string accept, string cancel) => CurrentPage.DisplayAlertAsync(title, message, accept, cancel);
}
