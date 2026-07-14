namespace HelloMauiApp.Services;

/// <summary>MAUI-Implementierung von <see cref="IFileDialogService"/> (Essentials FilePicker/Share + WinRT-FileSavePicker).</summary>
public class FileDialogService : IFileDialogService
{
	public Task<PickedFile?> PickImageAsync(string title) =>
		PickAsync(new PickOptions { PickerTitle = title, FileTypes = FilePickerFileType.Images });

	public Task<PickedFile?> PickJsonAsync(string title)
	{
		var jsonFileType = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
		{
			{ DevicePlatform.WinUI, new[] { ".json" } },
			{ DevicePlatform.Android, new[] { "application/json" } },
			{ DevicePlatform.iOS, new[] { "public.json" } },
			{ DevicePlatform.MacCatalyst, new[] { "json" } },
		});

		return PickAsync(new PickOptions { PickerTitle = title, FileTypes = jsonFileType });
	}

	static async Task<PickedFile?> PickAsync(PickOptions options)
	{
		var file = await FilePicker.Default.PickAsync(options);
		if (file is null)
			return null;

		using var stream = await file.OpenReadAsync();
		using var ms = new MemoryStream();
		await stream.CopyToAsync(ms);
		return new PickedFile(file.FileName, ms.ToArray());
	}

	public async Task<string?> SaveTextFileAsAsync(string suggestedFileName, string fileTypeDescription, string fileExtension, string content)
	{
#if WINDOWS
		var nativeWindow = Microsoft.Maui.Controls.Application.Current?.Windows.FirstOrDefault()?.Handler?.PlatformView as Microsoft.UI.Xaml.Window;
		if (nativeWindow is not null)
		{
			var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(nativeWindow);
			var picker = new Windows.Storage.Pickers.FileSavePicker();
			WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
			picker.SuggestedFileName = Path.GetFileNameWithoutExtension(suggestedFileName);
			picker.FileTypeChoices.Add(fileTypeDescription, new List<string> { fileExtension });

			var file = await picker.PickSaveFileAsync();
			if (file is null)
				return null;

			await Windows.Storage.FileIO.WriteTextAsync(file, content);
			return file.Path;
		}
#endif
		// Android/iOS/MacCatalyst haben ohne Zusatzpaket keinen systemweiten "Speichern unter"-Dialog –
		// dort bleibt Teilen der Weg, um die Datei z.B. direkt in "Dateien"/Drive/OneDrive abzulegen.
		await ShareTextFileAsync("Exportieren", suggestedFileName, content);
		return null;
	}

	public async Task ShareTextFileAsync(string title, string fileName, string content)
	{
		string tempPath = Path.Combine(FileSystem.Current.CacheDirectory, fileName);
		await File.WriteAllTextAsync(tempPath, content);

		await Share.Default.RequestAsync(new ShareFileRequest
		{
			Title = title,
			File = new ShareFile(tempPath),
		});
	}
}
