namespace HelloMauiApp.Services;

/// <summary>Kapselt Push/Pop-Navigation, damit ViewModels navigieren können, ohne <see cref="Page.Navigation"/> direkt zu kennen.</summary>
public interface INavigationService
{
	Task PushAsync(Page page);

	Task PopAsync();
}
