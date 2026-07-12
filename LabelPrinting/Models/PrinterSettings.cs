namespace LabelPrinting.Models;

public class PrinterSettings
{
	const string IpKey = "printer_ip";
	const string PortKey = "printer_port";
	const string LabelWidthKey = "printer_label_width_mm";
	const string LabelHeightKey = "printer_label_height_mm";
	const string DpiKey = "printer_dpi";

	public string IpAddress { get; set; } = string.Empty;
	public int Port { get; set; } = 9100;
	public double LabelWidthMm { get; set; } = 100;
	public double LabelHeightMm { get; set; } = 150;
	public int Dpi { get; set; } = 203;

	public static PrinterSettings Load()
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

	public void Save()
	{
		Preferences.Default.Set(IpKey, IpAddress);
		Preferences.Default.Set(PortKey, Port);
		Preferences.Default.Set(LabelWidthKey, LabelWidthMm);
		Preferences.Default.Set(LabelHeightKey, LabelHeightMm);
		Preferences.Default.Set(DpiKey, Dpi);
	}
}
