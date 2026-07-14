namespace HelloMauiApp.Services;

public class AlertService : IAlertService
{
	static Page CurrentPage => Application.Current!.Windows[0].Page!;

	public Task ShowAsync(string title, string message, string cancel = "OK") => CurrentPage.DisplayAlertAsync(title, message, cancel);

	public Task<bool> ConfirmAsync(string title, string message, string accept, string cancel) => CurrentPage.DisplayAlertAsync(title, message, accept, cancel);

	public Task<string?> PromptAsync(string title, string message, string initialValue = "") => CurrentPage.DisplayPromptAsync(title, message, initialValue: initialValue);

	public Task<string> ActionSheetAsync(string title, string cancel, string? destruction, params string[] buttons) => CurrentPage.DisplayActionSheetAsync(title, cancel, destruction, buttons);
}
