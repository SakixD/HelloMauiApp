using LabelPrinting.Models;
using LabelPrinting.Services;
using Xunit;

namespace LabelPrinting.Tests;

public class LabelTemplateFillServiceTests
{
	static LabelTemplate TemplateWithPlaceholders(params PlaceholderDefinition[] placeholders) => new()
	{
		Name = "Test",
		Placeholders = [.. placeholders],
	};

	[Fact]
	public void Fill_AlleErforderlichenFelderVorhanden_LiefertOk()
	{
		var template = TemplateWithPlaceholders(
			new PlaceholderDefinition { Key = "Artikel", Required = true },
			new PlaceholderDefinition { Key = "Preis", Required = true });

		var data = new Dictionary<string, string> { ["Artikel"] = "Schraube", ["Preis"] = "1,99" };
		var result = LabelTemplateFillService.Fill(template, data);

		Assert.True(result.Success);
		Assert.Equal("Schraube", result.ResolvedData["Artikel"]);
		Assert.Equal("1,99", result.ResolvedData["Preis"]);
	}

	[Fact]
	public void Fill_PflichtfeldFehltUndHatKeinenDefault_LiefertMissing()
	{
		var template = TemplateWithPlaceholders(
			new PlaceholderDefinition { Key = "Charge", Required = true });

		var result = LabelTemplateFillService.Fill(template, new Dictionary<string, string>());

		Assert.False(result.Success);
		Assert.Contains("Charge", result.MissingRequiredKeys);
	}

	[Fact]
	public void Fill_PflichtfeldFehltAberHatDefault_WirdMitDefaultAufgefuellt()
	{
		var template = TemplateWithPlaceholders(
			new PlaceholderDefinition { Key = "Land", Required = true, DefaultValue = "DE" });

		var result = LabelTemplateFillService.Fill(template, new Dictionary<string, string>());

		Assert.True(result.Success);
		Assert.Equal("DE", result.ResolvedData["Land"]);
	}

	[Fact]
	public void Fill_OptionalesFeldFehltOhneDefault_WirdLeererString()
	{
		var template = TemplateWithPlaceholders(
			new PlaceholderDefinition { Key = "Notiz", Required = false });

		var result = LabelTemplateFillService.Fill(template, new Dictionary<string, string>());

		Assert.True(result.Success);
		Assert.Equal(string.Empty, result.ResolvedData["Notiz"]);
	}

	[Fact]
	public void Fill_LeererStringGiltAlsFehlendUndWirdDurchDefaultErsetzt()
	{
		var template = TemplateWithPlaceholders(
			new PlaceholderDefinition { Key = "Charge", Required = true, DefaultValue = "unbekannt" });

		var data = new Dictionary<string, string> { ["Charge"] = "" };
		var result = LabelTemplateFillService.Fill(template, data);

		Assert.True(result.Success);
		Assert.Equal("unbekannt", result.ResolvedData["Charge"]);
	}

	[Fact]
	public void Fill_MehrereFehlendePflichtfelder_ListetAlleAuf()
	{
		var template = TemplateWithPlaceholders(
			new PlaceholderDefinition { Key = "A", Required = true },
			new PlaceholderDefinition { Key = "B", Required = true });

		var result = LabelTemplateFillService.Fill(template, new Dictionary<string, string>());

		Assert.False(result.Success);
		Assert.Equal(["A", "B"], result.MissingRequiredKeys);
	}
}
