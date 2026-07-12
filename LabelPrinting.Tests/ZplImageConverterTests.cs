using LabelPrinting.Services;
using SkiaSharp;
using Xunit;

namespace LabelPrinting.Tests;

public class ZplImageConverterTests
{
	static byte[] CreateSolidPng(int width, int height, SKColor color)
	{
		using var bitmap = new SKBitmap(width, height);
		using var canvas = new SKCanvas(bitmap);
		canvas.Clear(color);
		using var image = SKImage.FromBitmap(bitmap);
		using var data = image.Encode(SKEncodedImageFormat.Png, 100);
		return data.ToArray();
	}

	[Fact]
	public void Convert_UngueltigeBilddaten_WirftAussagekraeftigeException()
	{
		byte[] garbage = [1, 2, 3, 4, 5];

		var ex = Assert.Throws<InvalidOperationException>(() => ZplImageConverter.Convert(garbage, maxWidthDots: 100));
		Assert.Contains("Bilddaten", ex.Message);
	}

	[Fact]
	public void Convert_VollstaendigSchwarzesBild_ErgibtAlleBitsGesetzt()
	{
		byte[] png = CreateSolidPng(8, 8, SKColors.Black);

		var graphic = ZplImageConverter.Convert(png, maxWidthDots: 8, threshold: 128);

		Assert.Equal(8, graphic.WidthDots);
		Assert.Equal(8, graphic.HeightDots);
		Assert.Equal("^GFA,8,8,1,FFFFFFFFFFFFFFFF", graphic.ZplFieldData);
	}

	[Fact]
	public void Convert_VollstaendigWeissesBild_ErgibtKeineGesetztenBits()
	{
		byte[] png = CreateSolidPng(8, 8, SKColors.White);

		var graphic = ZplImageConverter.Convert(png, maxWidthDots: 8, threshold: 128);

		Assert.Equal("^GFA,8,8,1,0000000000000000", graphic.ZplFieldData);
	}

	[Fact]
	public void Convert_SkaliertProportionalAufMaxBreite()
	{
		byte[] png = CreateSolidPng(200, 100, SKColors.Black); // 2:1 Seitenverhältnis

		var graphic = ZplImageConverter.Convert(png, maxWidthDots: 100);

		Assert.Equal(100, graphic.WidthDots);
		Assert.Equal(50, graphic.HeightDots);
	}

	[Fact]
	public void Convert_BegrenztZusaetzlichAufMaxHoeheFallsGesetzt()
	{
		byte[] png = CreateSolidPng(200, 100, SKColors.Black); // 2:1 Seitenverhältnis

		var graphic = ZplImageConverter.Convert(png, maxWidthDots: 100, maxHeightDots: 30);

		// Ohne Höhenlimit wäre HeightDots=50; die 30-Dots-Grenze zieht die Breite proportional mit.
		Assert.Equal(60, graphic.WidthDots);
		Assert.Equal(30, graphic.HeightDots);
	}

	[Fact]
	public void ToFieldOrigin_SetztPositionVorDasGrafikfeld()
	{
		var graphic = new ZplGraphic(10, 10, "^GFA,1,1,1,00");

		Assert.Equal("^FO40,60^GFA,1,1,1,00^FS", graphic.ToFieldOrigin(40, 60));
	}
}
