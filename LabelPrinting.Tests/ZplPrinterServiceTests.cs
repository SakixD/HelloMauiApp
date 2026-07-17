using System.Text;
using LabelPrinting.Models;
using LabelPrinting.Services;
using Xunit;

namespace LabelPrinting.Tests;

/// <summary>
/// Verifiziert die PrinterProfile-Fassade des Druckservice ohne echte Hardware:
/// Stub-Sicherheit (USB/Bluetooth), Remote-Verhalten ohne Server, Profil-Validierung
/// und die ZPL-Verhaltensgarantien (^CI28-Injektion) über eine Fake-Verbindung.
/// </summary>
public class ZplPrinterServiceTests
{
	/// <summary>Fake-Verbindung: zeichnet Schreibvorgänge auf und liefert eine vorgegebene Antwort.</summary>
	sealed class FakeConnection : IPrinterConnection
	{
		readonly byte[] _response;
		int _readOffset;

		public List<byte[]> Writes { get; } = [];
		public bool Connected { get; private set; }
		public bool Disposed { get; private set; }

		public FakeConnection(string response = "")
		{
			_response = Encoding.Latin1.GetBytes(response);
		}

		public Task ConnectAsync(CancellationToken cancellationToken = default)
		{
			Connected = true;
			return Task.CompletedTask;
		}

		public Task WriteAsync(byte[] data, CancellationToken cancellationToken = default)
		{
			Writes.Add(data);
			return Task.CompletedTask;
		}

		public Task<int> ReadAsync(byte[] buffer, TimeSpan timeout, CancellationToken cancellationToken = default)
		{
			int remaining = _response.Length - _readOffset;
			int count = Math.Min(remaining, buffer.Length);
			Array.Copy(_response, _readOffset, buffer, 0, count);
			_readOffset += count;
			return Task.FromResult(count);
		}

		public ValueTask DisposeAsync()
		{
			Disposed = true;
			return ValueTask.CompletedTask;
		}
	}

	sealed class FakeConnectionFactory(IPrinterConnection connection) : IPrinterConnectionFactory
	{
		public PrinterProfile? LastProfile { get; private set; }

		public IPrinterConnection Create(PrinterProfile profile)
		{
			LastProfile = profile;
			return connection;
		}
	}

	static PrinterProfile TcpProfile(string ip = "192.168.0.50") => new()
	{
		Name = "Testdrucker",
		TransportKind = PrinterTransportKind.Tcp,
		IpAddress = ip,
		Port = 9100,
	};

	[Fact]
	public async Task SendZpl_UsbProfil_LiefertFailStattException()
	{
		var service = new ZplPrinterService(new PrinterConnectionFactory());
		var profile = new PrinterProfile { Name = "USB-Test", TransportKind = PrinterTransportKind.Usb };

		var result = await service.SendZplAsync(profile, "^XA^XZ");

		Assert.False(result.Success);
		Assert.Contains("noch nicht implementiert", result.ErrorMessage);
	}

	[Fact]
	public async Task TestConnection_BluetoothProfil_LiefertFailStattException()
	{
		var service = new ZplPrinterService(new PrinterConnectionFactory());
		var profile = new PrinterProfile { Name = "BT-Test", TransportKind = PrinterTransportKind.Bluetooth };

		var result = await service.TestConnectionAsync(profile);

		Assert.False(result.Success);
		Assert.Contains("noch nicht implementiert", result.ErrorMessage);
	}

	[Fact]
	public async Task SendZpl_RemoteProfilOhneClient_LiefertVerstaendlichesFail()
	{
		var service = new ZplPrinterService(new PrinterConnectionFactory());
		var profile = new PrinterProfile { Name = "Fremddrucker", ConnectionMode = PrinterConnectionMode.Remote };

		var result = await service.SendZplAsync(profile, "^XA^XZ");

		Assert.False(result.Success);
		Assert.Contains("Remote-Druck", result.ErrorMessage);
	}

	[Fact]
	public async Task Query_RemoteProfil_LiefertVerstaendlichesFail()
	{
		var service = new ZplPrinterService(new PrinterConnectionFactory());
		var profile = new PrinterProfile { Name = "Fremddrucker", ConnectionMode = PrinterConnectionMode.Remote };

		var result = await service.QueryAsync(profile, "~HS");

		Assert.False(result.Success);
		Assert.Contains("Remote", result.ErrorMessage);
	}

	[Fact]
	public async Task SendZpl_TcpProfilOhneIp_LiefertKonfigurationsHinweis()
	{
		var service = new ZplPrinterService(new PrinterConnectionFactory());

		var result = await service.SendZplAsync(TcpProfile(ip: ""), "^XA^XZ");

		Assert.False(result.Success);
		Assert.Contains("Keine Drucker-IP", result.ErrorMessage);
	}

	[Fact]
	public async Task SendZpl_InjiziertCi28NachXa()
	{
		var connection = new FakeConnection();
		var service = new ZplPrinterService(new FakeConnectionFactory(connection));

		var result = await service.SendZplAsync(TcpProfile(), "^XA^FDHallo^FS^XZ");

		Assert.True(result.Success);
		string sent = Encoding.UTF8.GetString(Assert.Single(connection.Writes));
		Assert.StartsWith("^XA^CI28", sent);
		Assert.True(connection.Disposed);
	}

	[Fact]
	public async Task SendZpl_VerdoppeltCi28Nicht()
	{
		var connection = new FakeConnection();
		var service = new ZplPrinterService(new FakeConnectionFactory(connection));

		await service.SendZplAsync(TcpProfile(), "^XA^CI28^FDHallo^FS^XZ");

		string sent = Encoding.UTF8.GetString(Assert.Single(connection.Writes));
		Assert.Equal(1, CountOccurrences(sent, "^CI28"));
	}

	[Fact]
	public async Task SendRaw_SchicktBytesUnveraendert()
	{
		var connection = new FakeConnection();
		var service = new ZplPrinterService(new FakeConnectionFactory(connection));
		byte[] payload = [0x01, 0x02, 0xFF];

		var result = await service.SendRawAsync(TcpProfile(), payload);

		Assert.True(result.Success);
		Assert.Equal(payload, Assert.Single(connection.Writes));
	}

	[Fact]
	public async Task Query_LiefertAntwortDerVerbindung()
	{
		var connection = new FakeConnection(response: "014,0,0,1234");
		var service = new ZplPrinterService(new FakeConnectionFactory(connection));

		var result = await service.QueryAsync(TcpProfile(), "~HS");

		Assert.True(result.Success);
		Assert.Contains("014,0,0,1234", result.ResponseText);
	}

	[Fact]
	public async Task TestConnection_NutztProfilAusDerFactory()
	{
		var connection = new FakeConnection();
		var factory = new FakeConnectionFactory(connection);
		var service = new ZplPrinterService(factory);
		var profile = TcpProfile();

		var result = await service.TestConnectionAsync(profile);

		Assert.True(result.Success);
		Assert.Same(profile, factory.LastProfile);
		Assert.True(connection.Connected);
		Assert.True(connection.Disposed);
	}

	static int CountOccurrences(string text, string token)
	{
		int count = 0;
		for (int index = text.IndexOf(token, StringComparison.Ordinal); index >= 0; index = text.IndexOf(token, index + token.Length, StringComparison.Ordinal))
			count++;
		return count;
	}
}
