using LabelPrinting.Models;

namespace LabelPrinting.Services;

/// <summary>Speichert/lädt die Druckerkonfiguration app-weit über <see cref="Preferences"/>.</summary>
public class PrinterSettingsStore : IPrinterSettingsStore
{
	const string IpKey = "printer_ip";
	const string PortKey = "printer_port";
	const string LabelWidthKey = "printer_label_width_mm";
	const string LabelHeightKey = "printer_label_height_mm";
	const string DpiKey = "printer_dpi";

	public PrinterSettings Load()
	{
		return new PrinterSettings
		{
			IpAddress = Preferences.Default.Get(IpKey, string.Empty),
			Port = Preferences.Default.Get(PortKey, 9100),
			LabelWidthMm = Preferences.Default.Get(LabelWidthKey, 100.0),
			LabelHeightMm = Preferences.Default.Get(LabelHeightKey, 150.0),
			Dpi = Preferences.Default.Get(DpiKey, 203),
		};
	}

	public void Save(PrinterSettings settings)
	{
		Preferences.Default.Set(IpKey, settings.IpAddress);
		Preferences.Default.Set(PortKey, settings.Port);
		Preferences.Default.Set(LabelWidthKey, settings.LabelWidthMm);
		Preferences.Default.Set(LabelHeightKey, settings.LabelHeightMm);
		Preferences.Default.Set(DpiKey, settings.Dpi);
	}
}
