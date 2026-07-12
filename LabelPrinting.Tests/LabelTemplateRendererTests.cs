using LabelPrinting.Models;
using LabelPrinting.Services;
using Xunit;

namespace LabelPrinting.Tests;

public class LabelTemplateRendererTests
{
	static LabelTemplate EmptyTemplate() => new()
	{
		Name = "Test",
		WidthMm = 100,
		HeightMm = 150,
		Dpi = 203,
		Elements = [],
	};

	[Fact]
	public void ToZpl_OhneDaten_RendertLiteralenText()
	{
		var template = EmptyTemplate();
		template.Elements.Add(new TextElement { X = 10, Y = 10, Text = BindableValue.Literal("Hallo"), FontSizeMm = 4 });

		string zpl = LabelTemplateRenderer.ToZpl(template);

		Assert.Contains("^FDHallo^FS", zpl);
	}

	[Fact]
	public void ToZpl_MitPlatzhalter_LoestWertAusDatenAuf()
	{
		var template = EmptyTemplate();
		template.Elements.Add(new TextElement { X = 10, Y = 10, Text = BindableValue.Placeholder("Artikel") });

		var data = new Dictionary<string, string> { ["Artikel"] = "Schraube M4" };
		string zpl = LabelTemplateRenderer.ToZpl(template, data);

		Assert.Contains("^FDSchraube M4^FS", zpl);
	}

	[Fact]
	public void ToZpl_PlatzhalterOhneDaten_RendertLeerenWert()
	{
		var template = EmptyTemplate();
		template.Elements.Add(new TextElement { X = 10, Y = 10, Text = BindableValue.Placeholder("Fehlt") });

		string zpl = LabelTemplateRenderer.ToZpl(template);

		Assert.Contains("^FD^FS", zpl);
	}

	[Theory]
	[InlineData(BarcodeSymbology.Code128, "^BCN")]
	[InlineData(BarcodeSymbology.Ean13, "^BEN")]
	[InlineData(BarcodeSymbology.Code39, "^B3N")]
	[InlineData(BarcodeSymbology.QrCode, "^BQN")]
	[InlineData(BarcodeSymbology.DataMatrix, "^BXN")]
	[InlineData(BarcodeSymbology.Pdf417, "^B7N")]
	public void ToZpl_JedeSymbologie_RuftDenPassendenZplBefehlAuf(BarcodeSymbology symbology, string expectedCommand)
	{
		var template = EmptyTemplate();
		template.Elements.Add(new BarcodeElement { X = 0, Y = 0, Symbology = symbology, Data = BindableValue.Literal("12345") });

		string zpl = LabelTemplateRenderer.ToZpl(template);

		Assert.Contains(expectedCommand, zpl);
	}

	[Fact]
	public void ToZpl_GefuellterRahmen_NutztDickeAlsMaximumVonBreiteUndHoehe()
	{
		var template = EmptyTemplate();
		template.Elements.Add(new FrameElement { X = 0, Y = 0, WidthMm = 10, HeightMm = 20, Filled = true });

		string zpl = LabelTemplateRenderer.ToZpl(template);

		// 10mm -> 80 dots, 20mm -> 160 dots bei 203dpi; "gefüllt" => Dicke = max(80,160) = 160
		Assert.Contains("^GB80,160,160^FS", zpl);
	}

	[Fact]
	public void ToZpl_HorizontaleLinie_HatLaengeAlsBreite()
	{
		var template = EmptyTemplate();
		template.Elements.Add(new LineElement { X = 0, Y = 0, LengthMm = 30, ThicknessMm = 0.5, Orientation = LineOrientation.Horizontal });

		string zpl = LabelTemplateRenderer.ToZpl(template);

		// 30mm -> 240 dots, 0.5mm -> 4 dots bei 203dpi
		Assert.Contains("^GB240,4,4^FS", zpl);
	}

	[Fact]
	public void ToZpl_VertikaleLinie_HatLaengeAlsHoehe()
	{
		var template = EmptyTemplate();
		template.Elements.Add(new LineElement { X = 0, Y = 0, LengthMm = 30, ThicknessMm = 0.5, Orientation = LineOrientation.Vertical });

		string zpl = LabelTemplateRenderer.ToZpl(template);

		Assert.Contains("^GB4,240,4^FS", zpl);
	}
}
