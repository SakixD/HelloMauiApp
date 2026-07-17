using LabelPrinting.Models;

namespace LabelPrinting.Services;

/// <summary>
/// Fertige ZPL-Labels für die Testfunktionen der App (Testdruck, Bild-als-Label) – damit das UI
/// selbst nie <see cref="ZplLabelBuilder"/>/<see cref="ZplImageConverter"/> anfassen muss.
/// </summary>
public static class LabelSamples
{
	/// <summary>Einfaches Testlabel mit Text, Zeitstempel und Barcode in der Labelgröße des Profils.</summary>
	public static string CreateTestLabelZpl(PrinterProfile profile)
	{
		return new ZplLabelBuilder(profile.LabelWidthMm, profile.LabelHeightMm, profile.Dpi)
			.AddText(40, 40, "Testlabel", fontHeight: 50, fontWidth: 50)
			.AddText(40, 110, DateTime.Now.ToString("dd.MM.yyyy HH:mm"), fontHeight: 30, fontWidth: 30)
			.AddBarcode128(40, 170, "123456789012")
			.Build();
	}

	/// <summary>Wandelt ein Bild (Logo/Symbol) in ein Label um, das das Bild formatfüllend platziert.</summary>
	public static string CreateImageLabelZpl(PrinterProfile profile, byte[] imageBytes)
	{
		int labelWidthDots = ZplLabelBuilder.MmToDots(profile.LabelWidthMm, profile.Dpi);
		int labelHeightDots = ZplLabelBuilder.MmToDots(profile.LabelHeightMm, profile.Dpi);
		var graphic = ZplImageConverter.Convert(imageBytes, maxWidthDots: labelWidthDots - 40, maxHeightDots: labelHeightDots - 40);

		return new ZplLabelBuilder(profile.LabelWidthMm, profile.LabelHeightMm, profile.Dpi)
			.AddImage(20, 20, graphic)
			.Build();
	}
}
