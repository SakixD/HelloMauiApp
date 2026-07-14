using System.Text;

namespace LabelPrinting.Services;

/// <summary>
/// Baut ZPL-Labels aus Text-, Barcode- und Grafikfeldern zusammen (z.B. für selbst gestaltete
/// Versandetiketten mit Logo). Koordinaten/Größen sind in Drucker-Dots anzugeben.
/// </summary>
public class ZplLabelBuilder
{
	readonly List<string> _fields = [];
	readonly int _widthDots;
	readonly int _heightDots;
	int? _printSpeedIps;
	int? _darkness;

	public ZplLabelBuilder(double widthMm, double heightMm, int dpi)
	{
		_widthDots = MmToDots(widthMm, dpi);
		_heightDots = MmToDots(heightMm, dpi);
	}

	public static int MmToDots(double mm, int dpi) => (int)Math.Round(mm / 25.4 * dpi);

	public static double DotsToMm(int dots, int dpi) => dots / (double)dpi * 25.4;

	/// <summary>Druckgeschwindigkeit in ips (^PR). Nicht aufgerufen = Druckerstandard bleibt unverändert.</summary>
	public ZplLabelBuilder SetPrintSpeed(int ips)
	{
		_printSpeedIps = ips;
		return this;
	}

	/// <summary>Druckdichte/Darkness 0-30 (~SD). Nicht aufgerufen = Druckerstandard bleibt unverändert.</summary>
	public ZplLabelBuilder SetDarkness(int darkness)
	{
		_darkness = darkness;
		return this;
	}

	/// <summary>
	/// Textfeld. rotation: 'N' (normal), 'R' (90° im Uhrzeigersinn), 'I' (180°, kopfüber),
	/// 'B' (270°, von unten nach oben) – ZPL ^A-Rotationsparameter, gedreht wird um den Feld-Ursprung.
	/// </summary>
	public ZplLabelBuilder AddText(int x, int y, string text, int fontHeight = 30, int fontWidth = 30, string font = "0", char rotation = 'N')
	{
		string safe = text.Replace("^", string.Empty).Replace("~", string.Empty);
		_fields.Add($"^FO{x},{y}^A{font}{rotation},{fontHeight},{fontWidth}^FD{safe}^FS");
		return this;
	}

	/// <summary>Code128-Barcode, z.B. für Tracking-/Sendungsnummern.</summary>
	public ZplLabelBuilder AddBarcode128(int x, int y, string data, int height = 80, bool printHumanReadable = true)
	{
		string safe = data.Replace("^", string.Empty).Replace("~", string.Empty);
		string hr = printHumanReadable ? "Y" : "N";
		_fields.Add($"^FO{x},{y}^BY2^BCN,{height},{hr},N,N^FD{safe}^FS");
		return this;
	}

	/// <summary>EAN-13-Barcode. Data sollte 12 Ziffern enthalten (13. Ziffer = Prüfziffer, wird vom Drucker berechnet).</summary>
	public ZplLabelBuilder AddEan13(int x, int y, string data, int height = 80, bool printHumanReadable = true)
	{
		string safe = data.Replace("^", string.Empty).Replace("~", string.Empty);
		string hr = printHumanReadable ? "Y" : "N";
		_fields.Add($"^FO{x},{y}^BY2^BEN,{height},{hr},N^FD{safe}^FS");
		return this;
	}

	/// <summary>Code39-Barcode.</summary>
	public ZplLabelBuilder AddCode39(int x, int y, string data, int height = 80, bool printHumanReadable = true)
	{
		string safe = data.Replace("^", string.Empty).Replace("~", string.Empty);
		string hr = printHumanReadable ? "Y" : "N";
		_fields.Add($"^FO{x},{y}^B3N,N,{height},{hr},N^FD{safe}^FS");
		return this;
	}

	/// <summary>QR-Code. errorCorrection: L (~7%), M (~15%), Q (~25%) oder H (~30%).</summary>
	public ZplLabelBuilder AddQrCode(int x, int y, string data, int magnification = 5, char errorCorrection = 'M')
	{
		string safe = data.Replace("^", string.Empty).Replace("~", string.Empty);
		_fields.Add($"^FO{x},{y}^BQN,2,{magnification}^FD{errorCorrection}A,{safe}^FS");
		return this;
	}

	/// <summary>DataMatrix-Code (ECC200).</summary>
	public ZplLabelBuilder AddDataMatrix(int x, int y, string data, int moduleSize = 5)
	{
		string safe = data.Replace("^", string.Empty).Replace("~", string.Empty);
		_fields.Add($"^FO{x},{y}^BXN,{moduleSize},200^FD{safe}^FS");
		return this;
	}

	/// <summary>PDF417-Stapelcode.</summary>
	public ZplLabelBuilder AddPdf417(int x, int y, string data, int rowHeight = 8)
	{
		string safe = data.Replace("^", string.Empty).Replace("~", string.Empty);
		_fields.Add($"^FO{x},{y}^B7N,{rowHeight},0^FD{safe}^FS");
		return this;
	}

	/// <summary>Platziert ein zuvor mit ZplImageConverter erzeugtes Grafikfeld (Logo, Symbol, ...).</summary>
	public ZplLabelBuilder AddImage(int x, int y, ZplGraphic graphic)
	{
		_fields.Add(graphic.ToFieldOrigin(x, y));
		return this;
	}

	/// <summary>Rechteck oder Linie (^GB Graphic Box). Für eine Linie width oder height gleich der Dicke setzen.</summary>
	public ZplLabelBuilder AddBox(int x, int y, int width, int height, int thickness = 2)
	{
		width = Math.Max(width, thickness);
		height = Math.Max(height, thickness);
		_fields.Add($"^FO{x},{y}^GB{width},{height},{thickness}^FS");
		return this;
	}

	/// <summary>
	/// Vollflächig gefülltes Rechteck. Nutzt bewusst nicht <see cref="AddBox"/>, dessen Dicke-Klemme
	/// (Box nie schmaler als die eigene Randstärke) für Linien gedacht ist – hier würde sie die
	/// absichtlich übergroße Füll-Dicke fälschlich in die Breite/Höhe übernehmen und das Rechteck
	/// dadurch größer drucken als angegeben.
	/// </summary>
	public ZplLabelBuilder AddFilledBox(int x, int y, int width, int height)
	{
		int thickness = Math.Max(width, height);
		_fields.Add($"^FO{x},{y}^GB{width},{height},{thickness}^FS");
		return this;
	}

	/// <summary>Ellipse/Kreis-Umriss (^GE Graphic Ellipse). Der Rand wächst wie bei ^GB nach innen.</summary>
	public ZplLabelBuilder AddEllipse(int x, int y, int width, int height, int thickness = 2)
	{
		width = Math.Max(width, thickness);
		height = Math.Max(height, thickness);
		_fields.Add($"^FO{x},{y}^GE{width},{height},{thickness},B^FS");
		return this;
	}

	/// <summary>
	/// Vollflächig gefüllte Ellipse. Analog zu <see cref="AddFilledBox"/>: Die Rand-Dicke wird so groß
	/// gewählt, dass der nach innen wachsende Rand die gesamte Fläche füllt.
	/// </summary>
	public ZplLabelBuilder AddFilledEllipse(int x, int y, int width, int height)
	{
		int thickness = Math.Max(width, height);
		_fields.Add($"^FO{x},{y}^GE{width},{height},{thickness},B^FS");
		return this;
	}

	/// <summary>Fügt einen beliebigen, bereits fertigen ZPL-Schnipsel ein (z.B. von einer Carrier-API).</summary>
	public ZplLabelBuilder AddRaw(string zpl)
	{
		_fields.Add(zpl);
		return this;
	}

	public string Build()
	{
		var sb = new StringBuilder();
		sb.Append("^XA");
		sb.Append("^CI28"); // UTF-8-Zeichenkodierung
		sb.Append($"^PW{_widthDots}");
		sb.Append($"^LL{_heightDots}");
		if (_printSpeedIps is int speed)
			sb.Append($"^PR{speed}");
		if (_darkness is int darkness)
			sb.Append($"~SD{darkness:00}");
		foreach (var field in _fields)
			sb.Append(field);
		sb.Append("^XZ");
		return sb.ToString();
	}
}
