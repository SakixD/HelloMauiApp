using SkiaSharp;

namespace LabelPrinting.Services;

/// <summary>
/// Wandelt Bilder (Logos, Symbole, Rasterungen von PDFs/Grafiken) in ein ZPL-Grafikfeld (^GFA)
/// um, das ein ZPL-/Zebra-Emulations-Drucker direkt darstellen kann.
/// </summary>
public static class ZplImageConverter
{
	/// <param name="imageBytes">Rohe Bilddaten (PNG/JPG/BMP...).</param>
	/// <param name="maxWidthDots">Zielbreite in Drucker-Dots (z.B. Labelbreite * DPI/25.4). Das Bild wird proportional darauf skaliert.</param>
	/// <param name="maxHeightDots">Optionale maximale Höhe in Dots; falls gesetzt, wird zusätzlich begrenzt.</param>
	/// <param name="threshold">Schwellwert 0-255 für Schwarz/Weiß-Umwandlung; wird mit Floyd-Steinberg-Dithering kombiniert.</param>
	public static ZplGraphic Convert(byte[] imageBytes, int maxWidthDots, int? maxHeightDots = null, byte threshold = 128)
	{
		using var original = DecodeOrThrow(imageBytes);

		var (targetWidth, targetHeight) = ScaleToFit(original.Width, original.Height, maxWidthDots, maxHeightDots);

		var samplingOptions = new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None);
		using var resized = original.Resize(new SKImageInfo(targetWidth, targetHeight), samplingOptions)
			?? throw new InvalidOperationException("Bild konnte nicht skaliert werden.");

		var monochrome = ToMonochromeBits(resized, threshold);

		int bytesPerRow = (targetWidth + 7) / 8;
		int totalBytes = bytesPerRow * targetHeight;
		var packed = new byte[totalBytes];

		for (int y = 0; y < targetHeight; y++)
		{
			for (int x = 0; x < targetWidth; x++)
			{
				if (!monochrome[y * targetWidth + x])
					continue; // false = weiß = Bit bleibt 0

				int byteIndex = y * bytesPerRow + (x / 8);
				int bitIndex = 7 - (x % 8);
				packed[byteIndex] |= (byte)(1 << bitIndex);
			}
		}

		string hex = System.Convert.ToHexString(packed);
		string field = $"^GFA,{totalBytes},{totalBytes},{bytesPerRow},{hex}";

		return new ZplGraphic(targetWidth, targetHeight, field);
	}

	/// <summary>
	/// SKBitmap.Decode gibt bei manchen ungültigen/nicht unterstützten Bildformaten null zurück, wirft
	/// bei anderen (je nach SkiaSharp-Version) stattdessen intern eine ArgumentException – beide Fälle
	/// werden hier auf dieselbe, aussagekräftige Exception vereinheitlicht.
	/// </summary>
	static SKBitmap DecodeOrThrow(byte[] imageBytes)
	{
		try
		{
			return SKBitmap.Decode(imageBytes)
				?? throw new InvalidOperationException("Bilddaten konnten nicht gelesen werden.");
		}
		catch (ArgumentException ex)
		{
			throw new InvalidOperationException("Bilddaten konnten nicht gelesen werden.", ex);
		}
	}

	static (int width, int height) ScaleToFit(int sourceWidth, int sourceHeight, int maxWidth, int? maxHeight)
	{
		double scale = Math.Min(1.0, (double)maxWidth / sourceWidth);
		if (maxHeight is int mh)
			scale = Math.Min(scale, (double)mh / sourceHeight);

		int width = Math.Max(1, (int)Math.Round(sourceWidth * scale));
		int height = Math.Max(1, (int)Math.Round(sourceHeight * scale));
		return (width, height);
	}

	/// <summary>Graustufen + Floyd-Steinberg-Fehlerdiffusion für ein sauberes Schwarz/Weiß-Ergebnis auch bei Verläufen.</summary>
	static bool[] ToMonochromeBits(SKBitmap bitmap, byte threshold)
	{
		int width = bitmap.Width, height = bitmap.Height;
		var gray = new float[width * height];

		for (int y = 0; y < height; y++)
		{
			for (int x = 0; x < width; x++)
			{
				var pixel = bitmap.GetPixel(x, y);
				// Alpha berücksichtigen: transparente Bereiche gelten als Weiß (kein Druck).
				float luminance = (0.299f * pixel.Red + 0.587f * pixel.Green + 0.114f * pixel.Blue);
				float alphaBlended = luminance * (pixel.Alpha / 255f) + 255f * (1 - pixel.Alpha / 255f);
				gray[y * width + x] = alphaBlended;
			}
		}

		var isBlack = new bool[width * height];

		for (int y = 0; y < height; y++)
		{
			for (int x = 0; x < width; x++)
			{
				int i = y * width + x;
				float oldValue = gray[i];
				bool black = oldValue < threshold;
				isBlack[i] = black;

				float newValue = black ? 0 : 255;
				float error = oldValue - newValue;

				Diffuse(gray, width, height, x + 1, y, error * 7 / 16f);
				Diffuse(gray, width, height, x - 1, y + 1, error * 3 / 16f);
				Diffuse(gray, width, height, x, y + 1, error * 5 / 16f);
				Diffuse(gray, width, height, x + 1, y + 1, error * 1 / 16f);
			}
		}

		return isBlack;
	}

	static void Diffuse(float[] gray, int width, int height, int x, int y, float amount)
	{
		if (x < 0 || x >= width || y < 0 || y >= height)
			return;

		gray[y * width + x] += amount;
	}
}
