using LabelPrinting.Models;
using Xunit;

namespace LabelPrinting.Tests;

public class PrinterProfileTests
{
	/// <summary>
	/// BUG-03: Der Profileditor klont das bestehende Profil und überschreibt nur Formularfelder —
	/// Clone muss deshalb wirklich alle Werte kopieren und eine unabhängige Instanz liefern.
	/// </summary>
	[Fact]
	public void Clone_KopiertAlleFelderUndIstUnabhaengig()
	{
		var original = new PrinterProfile
		{
			Name = "Zebra Lager",
			IsDefault = true,
			ConnectionMode = PrinterConnectionMode.Local,
			TransportKind = PrinterTransportKind.Tcp,
			IpAddress = "192.168.1.251",
			Port = 6101,
			UsbDeviceId = "USB\\VID_0A5F",
			BluetoothAddress = "AA:BB:CC:DD:EE:FF",
			RemotePrinterId = "remote-1",
			RemoteProviderName = "Agent Halle 2",
			Comment = "Steht am Packplatz 4",
			Roles = ["Versand.PaketLabel", "Versand.RetourenLabel"],
			LabelWidthMm = 103,
			LabelHeightMm = 199,
			Dpi = 300,
		};

		var clone = original.Clone();

		Assert.NotSame(original, clone);
		Assert.Equal(original.Id, clone.Id);
		Assert.Equal(original.Name, clone.Name);
		Assert.True(clone.IsDefault);
		Assert.Equal(original.ConnectionMode, clone.ConnectionMode);
		Assert.Equal(original.TransportKind, clone.TransportKind);
		Assert.Equal(original.IpAddress, clone.IpAddress);
		Assert.Equal(original.Port, clone.Port);
		Assert.Equal(original.UsbDeviceId, clone.UsbDeviceId);
		Assert.Equal(original.BluetoothAddress, clone.BluetoothAddress);
		Assert.Equal(original.RemotePrinterId, clone.RemotePrinterId);
		Assert.Equal(original.RemoteProviderName, clone.RemoteProviderName);
		Assert.Equal(original.Comment, clone.Comment);
		Assert.Equal(original.Roles, clone.Roles);
		Assert.Equal(original.LabelWidthMm, clone.LabelWidthMm);
		Assert.Equal(original.LabelHeightMm, clone.LabelHeightMm);
		Assert.Equal(original.Dpi, clone.Dpi);

		clone.Name = "geändert";
		clone.IpAddress = "10.0.0.1";
		Assert.Equal("Zebra Lager", original.Name);
		Assert.Equal("192.168.1.251", original.IpAddress);

		// Die Rollenliste muss eine echte Kopie sein — sonst editiert der Profileditor
		// über den Klon unbemerkt das Original im Store mit.
		clone.Roles.Add("Produktion.Produktetikett");
		Assert.Equal(2, original.Roles.Count);
	}
}
