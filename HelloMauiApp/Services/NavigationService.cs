namespace HelloMauiApp.Services;

public class NavigationService : INavigationService
{
	static INavigation Navigation => Application.Current!.Windows[0].Page!.Navigation;

	public Task PushAsync(Page page) => Navigation.PushAsync(page);

	public Task PopAsync() => Navigation.PopAsync();
}
