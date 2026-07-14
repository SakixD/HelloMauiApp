using LabelPrinting.Services;
using Xunit;

namespace LabelPrinting.Tests;

public class SgdResponseParserTests
{
	[Fact]
	public void Parse_EntferntAnfuehrungszeichenUndCrlf()
	{
		Assert.Equal("192.168.1.50", SgdResponseParser.Parse("\"192.168.1.50\"\r\n"));
	}

	[Fact]
	public void Parse_EntferntStxEtx()
	{
		char stx = (char)0x02;
		char etx = (char)0x03;
		Assert.Equal("on", SgdResponseParser.Parse($"{stx}\"on\"{etx}\r\n"));
	}

	[Fact]
	public void Parse_OhneAnfuehrungszeichenBleibtUnveraendert()
	{
		Assert.Equal("ZTC ZD420", SgdResponseParser.Parse("ZTC ZD420\r\n"));
	}

	[Fact]
	public void Parse_LeereAntwortErgibtLeerenString()
	{
		Assert.Equal(string.Empty, SgdResponseParser.Parse(string.Empty));
	}
}
