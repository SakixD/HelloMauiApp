using LabelPrinting.Services;
using Xunit;

namespace LabelPrinting.Tests;

public class ZplStatusParserTests
{
	const char Stx = (char)0x02;
	const char Etx = (char)0x03;

	[Fact]
	public void Parse_WertetAlleFelderAusEinerTypischenHsAntwort()
	{
		string raw =
			$"{Stx}030,1,0,0812,000,0,0,0,000,0,0,0{Etx}\r\n" +
			$"{Stx}000,0,1,1,1,2,6,0,00000000,1,000{Etx}\r\n" +
			$"{Stx}1234,0{Etx}\r\n";

		var status = ZplStatusParser.Parse(raw);

		Assert.True(status.Success);
		Assert.Equal(raw, status.RawResponse);
		Assert.True(status.PaperOut);
		Assert.False(status.Paused);
		Assert.Equal(812, status.LabelLengthDots);
		Assert.True(status.HeadOpen);
		Assert.True(status.RibbonOut);
	}

	[Fact]
	public void Parse_LeereAntwortErgibtFehler()
	{
		var status = ZplStatusParser.Parse(string.Empty);

		Assert.False(status.Success);
		Assert.NotNull(status.ErrorMessage);
	}

	[Fact]
	public void Parse_OhneZweiteZeileLaesstKopfUndBandFelderLeer()
	{
		string raw = $"{Stx}030,0,1,1218,000,0,0,0,000,0,0,0{Etx}\r\n";

		var status = ZplStatusParser.Parse(raw);

		Assert.True(status.Success);
		Assert.False(status.PaperOut);
		Assert.True(status.Paused);
		Assert.Equal(1218, status.LabelLengthDots);
		Assert.Null(status.HeadOpen);
		Assert.Null(status.RibbonOut);
	}

	[Fact]
	public void Parse_NichtNumerischesFeldErgibtNull()
	{
		string raw = $"{Stx}030,x,0,0812{Etx}\r\n";

		var status = ZplStatusParser.Parse(raw);

		Assert.True(status.Success);
		Assert.Null(status.PaperOut);
	}
}
