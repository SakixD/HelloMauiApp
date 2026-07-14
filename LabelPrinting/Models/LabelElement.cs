using System.Text.Json.Serialization;

namespace LabelPrinting.Models;

/// <summary>
/// Ein einzelnes, frei positionierbares Element auf einem Label. Position ist immer die
/// linke obere Ecke, in Millimetern vom Label-Nullpunkt (oben links) aus gemessen.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(TextElement), "text")]
[JsonDerivedType(typeof(BarcodeElement), "barcode")]
[JsonDerivedType(typeof(ImageElement), "image")]
[JsonDerivedType(typeof(FrameElement), "frame")]
[JsonDerivedType(typeof(LineElement), "line")]
[JsonDerivedType(typeof(EllipseElement), "ellipse")]
public abstract class LabelElement
{
	public double X { get; set; }
	public double Y { get; set; }
}

/// <summary>
/// Drehung eines Feldes in 90°-Schritten (ZPL ^A-Rotationsparameter N/R/I/B).
/// Gedreht wird im Uhrzeigersinn um den Feld-Ursprung (^FO, linke obere Ecke).
/// </summary>
public enum TextRotation
{
	None,
	Rotate90,
	Rotate180,
	Rotate270,
}

public class TextElement : LabelElement
{
	public BindableValue Text { get; set; } = BindableValue.Literal("Text");
	public double FontSizeMm { get; set; } = 4;
	public TextRotation Rotation { get; set; } = TextRotation.None;
}

public enum BarcodeSymbology
{
	Code128,
	Ean13,
	Code39,
	QrCode,
	DataMatrix,
	Pdf417,
}

public enum QrErrorCorrection
{
	Low,
	Medium,
	Quality,
	High,
}

/// <summary>
/// Barcode/2D-Code in wählbarer Symbologie. HeightMm/PrintHumanReadable gelten nur für die
/// 1D-Symbologien (Code128/EAN13/Code39), Magnification/QrErrorCorrection nur für die 2D-Codes.
/// </summary>
public class BarcodeElement : LabelElement
{
	public BarcodeSymbology Symbology { get; set; } = BarcodeSymbology.Code128;
	public BindableValue Data { get; set; } = BindableValue.Literal("123456789012");

	public double HeightMm { get; set; } = 15;
	public bool PrintHumanReadable { get; set; } = true;

	public int Magnification { get; set; } = 5;
	public QrErrorCorrection QrErrorCorrection { get; set; } = QrErrorCorrection.Medium;
}

/// <summary>Bild/Logo, eingebettet als Base64 – damit eine Vorlagendatei komplett eigenständig ist.</summary>
public class ImageElement : LabelElement
{
	public string ImageBase64 { get; set; } = string.Empty;
	public double WidthMm { get; set; } = 20;
	public double HeightMm { get; set; } = 20;
}

/// <summary>Rechteckiger Rahmen, wahlweise nur umrandet oder komplett gefüllt.</summary>
public class FrameElement : LabelElement
{
	public double WidthMm { get; set; } = 30;
	public double HeightMm { get; set; } = 20;
	public double ThicknessMm { get; set; } = 0.5;
	public bool Filled { get; set; }
}

/// <summary>Ellipse/Kreis (ZPL ^GE Graphic Ellipse), wahlweise nur umrandet oder komplett gefüllt.</summary>
public class EllipseElement : LabelElement
{
	public double WidthMm { get; set; } = 20;
	public double HeightMm { get; set; } = 20;
	public double ThicknessMm { get; set; } = 0.5;
	public bool Filled { get; set; }
}

public enum LineOrientation
{
	Horizontal,
	Vertical,
}

/// <summary>
/// Gerade Linie. ZPL-Etikettendrucker können Grafikelemente nur in 0°/90°-Schritten drucken
/// (Hardware-/Firmware-Grenze, kein Software-Limit) – daher Ausrichtung statt freiem Winkel.
/// </summary>
public class LineElement : LabelElement
{
	public double LengthMm { get; set; } = 30;
	public double ThicknessMm { get; set; } = 0.5;
	public LineOrientation Orientation { get; set; } = LineOrientation.Horizontal;
}
