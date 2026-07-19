using LabelPrinting.Models;
using Xunit;

namespace LabelPrinting.Tests;

/// <summary>
/// FEAT-01: Rollen sind strukturierte Kennungen "Bereich.Rolle" (Leitplanke aus PROJECT.md),
/// keine losen Strings — die Validierung sitzt zentral in <see cref="DeviceRoleName"/>.
/// </summary>
public class DeviceRoleNameTests
{
	[Theory]
	[InlineData("Versand.PaketLabel", "Versand.PaketLabel")]
	[InlineData("  Produktion.Produktetikett  ", "Produktion.Produktetikett")]
	[InlineData("lager.klein", "lager.klein")]
	public void TryNormalize_AkzeptiertGueltigeKennungen(string input, string expected)
	{
		Assert.True(DeviceRoleName.TryNormalize(input, out string normalized));
		Assert.Equal(expected, normalized);
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	[InlineData("OhnePunkt")]
	[InlineData("Zu.Viele.Punkte")]
	[InlineData(".RolleOhneBereich")]
	[InlineData("BereichOhneRolle.")]
	[InlineData("Mit Leerzeichen.Rolle")]
	[InlineData("Bereich.Mit Leerzeichen")]
	public void TryNormalize_LehntUngueltigeKennungenAb(string? input)
	{
		Assert.False(DeviceRoleName.TryNormalize(input, out _));
	}

	[Fact]
	public void ParseList_TrenntNormalisiertUndEntferntDuplikate()
	{
		var roles = DeviceRoleName.ParseList(
			"Versand.PaketLabel, Produktion.Produktetikett; versand.paketlabel\nVersand.RetourenLabel",
			out var invalid);

		Assert.Equal(["Versand.PaketLabel", "Produktion.Produktetikett", "Versand.RetourenLabel"], roles);
		Assert.Empty(invalid);
	}

	[Fact]
	public void ParseList_SammeltUngueltigeEintraege()
	{
		var roles = DeviceRoleName.ParseList("Versand.PaketLabel, kaputt, Auch.Kaputt.Doppelt", out var invalid);

		Assert.Equal(["Versand.PaketLabel"], roles);
		Assert.Equal(["kaputt", "Auch.Kaputt.Doppelt"], invalid);
	}

	[Fact]
	public void ParseList_LeereEingabeLiefertLeereListe()
	{
		var roles = DeviceRoleName.ParseList("   ", out var invalid);

		Assert.Empty(roles);
		Assert.Empty(invalid);
	}
}
