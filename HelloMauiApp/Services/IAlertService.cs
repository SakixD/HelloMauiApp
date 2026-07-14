namespace HelloMauiApp.Services;

/// <summary>Kapselt <see cref="Page.DisplayAlertAsync(string, string, string)"/>, damit ViewModels Meldungen anzeigen können, ohne eine Page-Referenz zu halten.</summary>
public interface IAlertService
{
	Task ShowAsync(string title, string message, string cancel = "OK");

	Task<bool> ConfirmAsync(string title, string message, string accept, string cancel);
}
