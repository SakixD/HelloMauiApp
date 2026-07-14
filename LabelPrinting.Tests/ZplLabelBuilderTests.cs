using LabelPrinting.Services;
using Xunit;

namespace LabelPrinting.Tests;

public class ZplLabelBuilderTests
{
	[Theory]
	[InlineData(100, 203, 799)]  // 100mm @ 203dpi
	[InlineData(150, 203, 1199)] // 150mm @ 203dpi
	[InlineData(50, 300, 591)]   // 50mm @ 300dpi
	public void MmToDots_RundetKorrekt(double mm, int dpi, int expectedDots)
	{
		Assert.Equal(expectedDots, ZplLabelBuilder.MmToDots(mm, dpi));
	}

	[Fact]
	public void Build_EnthaeltLabelrahmenUndMasse()
	{
		string zpl = new ZplLabelBuilder(widthMm: 100, heightMm: 150, dpi: 203).Build();

		Assert.StartsWith("^XA^CI28", zpl);
		Assert.Contains("^PW799", zpl);
		Assert.Contains("^LL1199", zpl);
		Assert.EndsWith("^XZ", zpl);
	}

	[Fact]
	public void AddText_EntferntSteuerzeichenAusDemText()
	{
		string zpl = new ZplLabelBuilder(100, 150, 203)
			.AddText(10, 10, "Preis: ^12~34€")
			.Build();

		Assert.Contains("^FDPreis: 1234€^FS", zpl);
		Assert.DoesNotContain("^12~34€", zpl);
	}

	[Fact]
	public void AddBarcode128_ErzeugtErwartetesFeld()
	{
		string zpl = new ZplLabelBuilder(100, 150, 203)
			.AddBarcode128(40, 40, "123456789012", height: 80, printHumanReadable: true)
			.Build();

		Assert.Contains("^FO40,40^BY2^BCN,80,Y,N,N^FD123456789012^FS", zpl);
	}

	[Fact]
	public void AddQrCode_KodiertFehlerkorrekturUndDaten()
	{
		string zpl = new ZplLabelBuilder(100, 150, 203)
			.AddQrCode(10, 10, "https://example.com", magnification: 5, errorCorrection: 'Q')
			.Build();

		Assert.Contains("^FO10,10^BQN,2,5^FDQA,https://example.com^FS", zpl);
	}

	[Fact]
	public void AddBox_ThicknessWirdNieGroesserAlsBoxSelbstIgnoriert_AberMindestensSoGross()
	{
		// Eine "Linie" (Box mit width=thickness) darf durch Math.Max nicht kleiner als die
		// angegebene Dicke werden.
		string zpl = new ZplLabelBuilder(100, 150, 203)
			.AddBox(0, 0, width: 1, height: 30, thickness: 3)
			.Build();

		Assert.Contains("^GB3,30,3^FS", zpl);
	}

	[Theory]
	[InlineData('N')]
	[InlineData('R')]
	[InlineData('I')]
	[InlineData('B')]
	public void AddText_KodiertRotation(char rotation)
	{
		string zpl = new ZplLabelBuilder(100, 150, 203)
			.AddText(10, 10, "Hallo", fontHeight: 30, fontWidth: 30, font: "0", rotation: rotation)
			.Build();

		Assert.Contains($"^A0{rotation},30,30", zpl);
	}

	[Fact]
	public void AddEllipse_ErzeugtGraphicEllipseFeld()
	{
		string zpl = new ZplLabelBuilder(100, 150, 203)
			.AddEllipse(10, 20, width: 100, height: 60, thickness: 4)
			.Build();

		Assert.Contains("^FO10,20^GE100,60,4,B^FS", zpl);
	}

	[Fact]
	public void AddEllipse_KlemmtMasseNieUnterDieDicke()
	{
		string zpl = new ZplLabelBuilder(100, 150, 203)
			.AddEllipse(0, 0, width: 1, height: 30, thickness: 3)
			.Build();

		Assert.Contains("^GE3,30,3,B^FS", zpl);
	}

	[Fact]
	public void AddFilledEllipse_FuelltUeberRanddicke()
	{
		string zpl = new ZplLabelBuilder(100, 150, 203)
			.AddFilledEllipse(0, 0, width: 80, height: 40)
			.Build();

		// Dicke = max(width, height), damit der nach innen wachsende Rand alles füllt.
		Assert.Contains("^GE80,40,80,B^FS", zpl);
	}

	[Fact]
	public void AddRaw_FuegtSchnipselUnveraendertEin()
	{
		string zpl = new ZplLabelBuilder(100, 150, 203)
			.AddRaw("^FO0,0^GFA,1,1,1,C^FS")
			.Build();

		Assert.Contains("^FO0,0^GFA,1,1,1,C^FS", zpl);
	}
}
