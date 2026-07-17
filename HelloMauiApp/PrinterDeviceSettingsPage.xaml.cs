using System.IO.Ports;
using LabelPrinting.Services;

namespace HelloMauiApp;

/// <summary>
/// Liest/schreibt Einstellungen direkt im Drucker selbst (Name, Netzwerkkonfiguration, ...) – im
/// Unterschied zu <see cref="PrinterSettingsPage"/>, die nur die App-seitige Verbindungskonfiguration
/// verwaltet. Typischer Ablauf: neuer Drucker per USB (virtueller COM-Port) anstecken, verbinden,
/// Name/IP setzen, neu starten, damit er danach über das Netzwerk erreichbar ist.
/// </summary>
public partial class PrinterDeviceSettingsPage : ContentPage
{
	readonly IPrinterService _printerService;
	readonly IPrinterProfileStore _profileStore;

	static readonly (string SgdName, Func<PrinterDeviceSettingsPage, Entry> Entry)[] CuratedTextFields =
	[
		("device.friendly_name", p => p.DeviceNameEntry),
		("ip.addr", p => p.DeviceIpEntry),
		("ip.netmask", p => p.DeviceNetmaskEntry),
		("ip.gateway", p => p.DeviceGatewayEntry),
	];

	const string DhcpVariableName = "ip.dhcp.enable";

	public PrinterDeviceSettingsPage(IPrinterService printerService, IPrinterProfileStore profileStore)
	{
		InitializeComponent();
		_printerService = printerService;
		_profileStore = profileStore;
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();

		RefreshPorts();

		// Netzwerkfelder mit dem aktiven TCP-Profil vorbelegen (bei USB/Bluetooth-Profil leer lassen).
		if (_profileStore.GetDefault() is { TransportKind: LabelPrinting.Models.PrinterTransportKind.Tcp } profile)
		{
			NetworkIpEntry.Text = profile.IpAddress;
			NetworkPortEntry.Text = profile.Port.ToString();
		}

		ConnectionTypePicker.SelectedIndex = 0;
	}

	void RefreshPorts()
	{
		string? selected = ComPortPicker.SelectedItem as string;
		var ports = SerialPort.GetPortNames();
		ComPortPicker.ItemsSource = ports;
		if (selected is not null && Array.IndexOf(ports, selected) is int idx and >= 0)
			ComPortPicker.SelectedIndex = idx;
	}

	void OnRefreshPortsClicked(object? sender, EventArgs e) => RefreshPorts();

	void OnConnectionTypeChanged(object? sender, EventArgs e)
	{
		bool serial = ConnectionTypePicker.SelectedIndex != 1;
		SerialPanel.IsVisible = serial;
		NetworkPanel.IsVisible = !serial;
	}

	/// <summary>
	/// Baut bei Bedarf eine neue <see cref="IPrinterConnection"/> aus den aktuellen Eingaben. Da
	/// <see cref="ZplPrinterService"/> jede Verbindung pro Aufruf selbst öffnet und wieder verwirft
	/// (siehe transportunabhängige Überladungen), liefert dies eine Factory statt einer einzelnen
	/// Instanz, damit für jede Abfrage/jeden Schreibvorgang eine frische Verbindung entsteht.
	/// </summary>
	bool TryGetConnectionFactory(out Func<IPrinterConnection> factory, out string? error)
	{
		factory = null!;
		error = null;

		if (ConnectionTypePicker.SelectedIndex == 1)
		{
			string ip = NetworkIpEntry.Text?.Trim() ?? string.Empty;
			if (string.IsNullOrWhiteSpace(ip))
			{
				error = "Bitte eine IP-Adresse eingeben.";
				return false;
			}
			if (!int.TryParse(NetworkPortEntry.Text, out int port) || port is <= 0 or > 65535)
			{
				error = "Bitte einen gültigen Port (1-65535) eingeben.";
				return false;
			}

			factory = () => new TcpPrinterConnection(ip, port);
			return true;
		}

		if (ComPortPicker.SelectedItem is not string portName)
		{
			error = "Bitte einen COM-Port auswählen (ggf. \"Aktualisieren\" drücken, nachdem der Drucker angesteckt wurde).";
			return false;
		}
		if (!int.TryParse(BaudRateEntry.Text, out int baudRate) || baudRate <= 0)
		{
			error = "Bitte eine gültige Baudrate eingeben.";
			return false;
		}

		factory = () => new SerialPrinterConnection(portName, baudRate);
		return true;
	}

	void SetBusy(bool busy)
	{
		BusyIndicator.IsVisible = busy;
		BusyIndicator.IsRunning = busy;
		ConnectBtn.IsEnabled = !busy;
		SaveDeviceSettingsBtn.IsEnabled = !busy;
		ReadFreeVariableBtn.IsEnabled = !busy;
		WriteFreeVariableBtn.IsEnabled = !busy;
		RestartBtn.IsEnabled = !busy;
	}

	static bool ParseDhcp(string value) =>
		value.Trim().Equals("on", StringComparison.OrdinalIgnoreCase) || value.Trim() == "1";

	async void OnConnectClicked(object? sender, EventArgs e)
	{
		if (!TryGetConnectionFactory(out var factory, out var error))
		{
			await DisplayAlertAsync("Ungültige Eingabe", error!, "OK");
			return;
		}

		SetBusy(true);
		StatusLabel.Text = "Lese Geräteeinstellungen...";

		var failures = new List<string>();

		foreach (var (sgdName, getEntry) in CuratedTextFields)
		{
			var result = await _printerService.GetVariableAsync(factory(), sgdName);
			if (result.Success)
				getEntry(this).Text = result.ResponseText;
			else
				failures.Add($"{sgdName}: {result.ErrorMessage}");
		}

		var dhcpResult = await _printerService.GetVariableAsync(factory(), DhcpVariableName);
		if (dhcpResult.Success)
			DhcpSwitch.IsToggled = ParseDhcp(dhcpResult.ResponseText);
		else
			failures.Add($"{DhcpVariableName}: {dhcpResult.ErrorMessage}");

		SetBusy(false);
		StatusLabel.Text = failures.Count == 0
			? "Geräteeinstellungen gelesen."
			: $"Teilweise gelesen. Nicht erreichbar/unbekannt: {string.Join("; ", failures)}";
	}

	async void OnSaveDeviceSettingsClicked(object? sender, EventArgs e)
	{
		if (!TryGetConnectionFactory(out var factory, out var error))
		{
			await DisplayAlertAsync("Ungültige Eingabe", error!, "OK");
			return;
		}

		SetBusy(true);
		StatusLabel.Text = "Schreibe Geräteeinstellungen...";

		var failures = new List<string>();

		foreach (var (sgdName, getEntry) in CuratedTextFields)
		{
			var result = await _printerService.SetVariableAsync(factory(), sgdName, getEntry(this).Text ?? string.Empty);
			if (!result.Success)
				failures.Add($"{sgdName}: {result.ErrorMessage}");
		}

		var dhcpResult = await _printerService.SetVariableAsync(factory(), DhcpVariableName, DhcpSwitch.IsToggled ? "on" : "off");
		if (!dhcpResult.Success)
			failures.Add($"{DhcpVariableName}: {dhcpResult.ErrorMessage}");

		SetBusy(false);
		StatusLabel.Text = failures.Count == 0
			? "Gespeichert. Netzwerkänderungen werden ggf. erst nach einem Neustart wirksam."
			: $"Teilweise gespeichert. Fehler: {string.Join("; ", failures)}";
	}

	async void OnReadFreeVariableClicked(object? sender, EventArgs e)
	{
		if (!TryGetConnectionFactory(out var factory, out var error))
		{
			await DisplayAlertAsync("Ungültige Eingabe", error!, "OK");
			return;
		}
		if (string.IsNullOrWhiteSpace(FreeVariableNameEntry.Text))
		{
			await DisplayAlertAsync("Kein Variablenname", "Bitte einen SGD-Variablennamen eingeben, z.B. device.friendly_name.", "OK");
			return;
		}

		SetBusy(true);
		var result = await _printerService.GetVariableAsync(factory(), FreeVariableNameEntry.Text.Trim());
		SetBusy(false);

		if (result.Success)
		{
			FreeVariableValueEntry.Text = result.ResponseText;
			StatusLabel.Text = "Variable gelesen.";
		}
		else
		{
			StatusLabel.Text = $"Fehler: {result.ErrorMessage}";
		}
	}

	async void OnWriteFreeVariableClicked(object? sender, EventArgs e)
	{
		if (!TryGetConnectionFactory(out var factory, out var error))
		{
			await DisplayAlertAsync("Ungültige Eingabe", error!, "OK");
			return;
		}
		if (string.IsNullOrWhiteSpace(FreeVariableNameEntry.Text))
		{
			await DisplayAlertAsync("Kein Variablenname", "Bitte einen SGD-Variablennamen eingeben, z.B. device.friendly_name.", "OK");
			return;
		}

		SetBusy(true);
		var result = await _printerService.SetVariableAsync(factory(), FreeVariableNameEntry.Text.Trim(), FreeVariableValueEntry.Text ?? string.Empty);
		SetBusy(false);

		StatusLabel.Text = result.Success ? "Variable geschrieben." : $"Fehler: {result.ErrorMessage}";
	}

	async void OnRestartClicked(object? sender, EventArgs e)
	{
		if (!TryGetConnectionFactory(out var factory, out var error))
		{
			await DisplayAlertAsync("Ungültige Eingabe", error!, "OK");
			return;
		}

		bool confirmed = await DisplayAlertAsync(
			"Drucker neu starten",
			"Der Drucker startet neu, damit z.B. geänderte Netzwerkeinstellungen wirksam werden. Fortfahren?",
			"Ja, neu starten",
			"Abbrechen");

		if (!confirmed)
			return;

		SetBusy(true);
		var result = await _printerService.RestartAsync(factory());
		SetBusy(false);

		StatusLabel.Text = result.Success ? "Neustart wurde ausgelöst." : $"Fehler: {result.ErrorMessage}";
	}
}
