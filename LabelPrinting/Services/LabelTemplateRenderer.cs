using LabelPrinting.Models;

namespace LabelPrinting.Services;

/// <summary>Wandelt eine im Designer erstellte <see cref="LabelTemplate"/> in fertigen ZPL-Code um.</summary>
public static class LabelTemplateRenderer
{
	static readonly IReadOnlyDictionary<string, string> NoData = new Dictionary<string, string>();

	/// <summary>Rendert ohne Platzhalter-Daten (Platzhalter-Felder werden leer). Für Vorlagen ohne Platzhalter gedacht.</summary>
	public static string ToZpl(LabelTemplate template) => ToZpl(template, NoData);

	/// <summary>Rendert mit aufgelösten Platzhalter-Daten, z.B. aus <see cref="LabelTemplateFillService"/>.</summary>
	public static string ToZpl(LabelTemplate template, IReadOnlyDictionary<string, string> data)
	{
		var builder = new ZplLabelBuilder(template.WidthMm, template.HeightMm, template.Dpi);

		if (template.PrintParameters.PrintSpeed is int speed)
			builder.SetPrintSpeed(speed);
		if (template.PrintParameters.Darkness is int darkness)
			builder.SetDarkness(darkness);

		foreach (var element in template.Elements)
		{
			int x = ZplLabelBuilder.MmToDots(element.X, template.Dpi);
			int y = ZplLabelBuilder.MmToDots(element.Y, template.Dpi);

			switch (element)
			{
				case TextElement text:
					int fontDots = ZplLabelBuilder.MmToDots(text.FontSizeMm, template.Dpi);
					builder.AddText(x, y, text.Text.Resolve(data), fontDots, fontDots);
					break;

				case BarcodeElement barcode:
					AddBarcode(builder, x, y, barcode, template.Dpi, data);
					break;

				case ImageElement image when image.ImageBase64.Length > 0:
					byte[] bytes = Convert.FromBase64String(image.ImageBase64);
					int imageWidthDots = ZplLabelBuilder.MmToDots(image.WidthMm, template.Dpi);
					int imageHeightDots = ZplLabelBuilder.MmToDots(image.HeightMm, template.Dpi);
					var graphic = ZplImageConverter.Convert(bytes, imageWidthDots, imageHeightDots);
					builder.AddImage(x, y, graphic);
					break;

				case FrameElement frame:
					int frameWidthDots = ZplLabelBuilder.MmToDots(frame.WidthMm, template.Dpi);
					int frameHeightDots = ZplLabelBuilder.MmToDots(frame.HeightMm, template.Dpi);
					if (frame.Filled)
						builder.AddFilledBox(x, y, frameWidthDots, frameHeightDots);
					else
						builder.AddBox(x, y, frameWidthDots, frameHeightDots, Math.Max(1, ZplLabelBuilder.MmToDots(frame.ThicknessMm, template.Dpi)));
					break;

				case LineElement line:
					int lengthDots = ZplLabelBuilder.MmToDots(line.LengthMm, template.Dpi);
					int lineThicknessDots = Math.Max(1, ZplLabelBuilder.MmToDots(line.ThicknessMm, template.Dpi));
					if (line.Orientation == LineOrientation.Horizontal)
						builder.AddBox(x, y, lengthDots, lineThicknessDots, lineThicknessDots);
					else
						builder.AddBox(x, y, lineThicknessDots, lengthDots, lineThicknessDots);
					break;
			}
		}

		return builder.Build();
	}

	static void AddBarcode(ZplLabelBuilder builder, int x, int y, BarcodeElement barcode, int dpi, IReadOnlyDictionary<string, string> data)
	{
		string value = barcode.Data.Resolve(data);
		int heightDots = ZplLabelBuilder.MmToDots(barcode.HeightMm, dpi);

		switch (barcode.Symbology)
		{
			case BarcodeSymbology.Code128:
				builder.AddBarcode128(x, y, value, heightDots, barcode.PrintHumanReadable);
				break;
			case BarcodeSymbology.Ean13:
				builder.AddEan13(x, y, value, heightDots, barcode.PrintHumanReadable);
				break;
			case BarcodeSymbology.Code39:
				builder.AddCode39(x, y, value, heightDots, barcode.PrintHumanReadable);
				break;
			case BarcodeSymbology.QrCode:
				char ec = barcode.QrErrorCorrection switch
				{
					QrErrorCorrection.Low => 'L',
					QrErrorCorrection.Quality => 'Q',
					QrErrorCorrection.High => 'H',
					_ => 'M',
				};
				builder.AddQrCode(x, y, value, barcode.Magnification, ec);
				break;
			case BarcodeSymbology.DataMatrix:
				builder.AddDataMatrix(x, y, value, barcode.Magnification);
				break;
			case BarcodeSymbology.Pdf417:
				builder.AddPdf417(x, y, value, Math.Max(1, heightDots / 10));
				break;
		}
	}
}
