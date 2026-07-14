using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace HelloMauiApp.Services.Api;

/// <summary>
/// Lokaler HTTP-Zugang zum <see cref="IAppApi"/>-Dispatcher, damit die laufende App von außen
/// (Terminal/curl/andere Prozesse) angesprochen werden kann:
///
///   GET  http://localhost:5299/api                 → alle Kommandos (Discovery)
///   GET  http://localhost:5299/api/templates.list  → Kommando ohne Payload
///   GET  http://localhost:5299/api/templates.get?name=X   → Query-Parameter werden zur Payload
///   POST http://localhost:5299/api/print.template  (JSON-Body = Payload)
///
/// Bewusst <see cref="HttpListener"/> statt ASP.NET Core: keine zusätzlichen Abhängigkeiten in der
/// MAUI-App, und der "localhost"-Prefix ist ohne Adminrechte/URL-ACL bindbar. Es wird NUR auf
/// Loopback gelauscht – von anderen Rechnern aus ist die API nicht erreichbar.
/// </summary>
public sealed class LocalApiServer : IDisposable
{
	public const int DefaultPort = 5299;

	readonly IAppApi _api;
	HttpListener? _listener;
	CancellationTokenSource? _cts;

	public int Port { get; } = DefaultPort;

	public string BaseUrl => $"http://localhost:{Port}/";

	public bool IsRunning { get; private set; }

	/// <summary>Grund, falls der Start fehlschlug (z.B. Port belegt) – wird auf der Startseite angezeigt.</summary>
	public string? LastError { get; private set; }

	public LocalApiServer(IAppApi api)
	{
		_api = api;
	}

	public void Start()
	{
		if (IsRunning)
			return;

		try
		{
			_listener = new HttpListener();
			_listener.Prefixes.Add(BaseUrl);
			_listener.Start();
			_cts = new CancellationTokenSource();
			IsRunning = true;
			LastError = null;
			_ = Task.Run(() => AcceptLoopAsync(_listener, _cts.Token));
		}
		catch (Exception ex)
		{
			LastError = ex.Message;
			IsRunning = false;
			_listener = null;
		}
	}

	public void Dispose()
	{
		_cts?.Cancel();
		try
		{
			_listener?.Stop();
			_listener?.Close();
		}
		catch
		{
			// Beim Herunterfahren egal.
		}
		IsRunning = false;
	}

	async Task AcceptLoopAsync(HttpListener listener, CancellationToken ct)
	{
		while (!ct.IsCancellationRequested && listener.IsListening)
		{
			HttpListenerContext context;
			try
			{
				context = await listener.GetContextAsync();
			}
			catch (Exception)
			{
				if (ct.IsCancellationRequested || !listener.IsListening)
					break;
				continue;
			}

			_ = Task.Run(() => HandleRequestAsync(context), CancellationToken.None);
		}
	}

	async Task HandleRequestAsync(HttpListenerContext context)
	{
		var response = context.Response;
		try
		{
			var request = context.Request;
			string path = request.Url?.AbsolutePath.Trim('/') ?? string.Empty;

			if (!path.Equals("api", StringComparison.OrdinalIgnoreCase)
				&& !path.StartsWith("api/", StringComparison.OrdinalIgnoreCase))
			{
				await WriteJsonAsync(response, 404, ApiResult.Fail("Unbekannter Pfad. Einstieg: GET /api"));
				return;
			}

			string command = path.Length > 4 ? path[4..] : string.Empty;

			if (command.Length == 0)
			{
				await WriteJsonAsync(response, 200, ApiResult.Ok(new
				{
					Service = "HelloMauiApp Local API",
					BaseUrl = BaseUrl + "api",
					Usage = "GET/POST /api/{kommando} – Payload als JSON-Body (POST) oder Query-Parameter (GET).",
					Commands = _api.Commands,
				}));
				return;
			}

			JsonObject? payload = null;

			if (request.HasEntityBody)
			{
				using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
				string body = await reader.ReadToEndAsync();
				if (body.Trim().Length > 0)
				{
					payload = JsonNode.Parse(body) as JsonObject;
					if (payload is null)
					{
						await WriteJsonAsync(response, 400, ApiResult.Fail("Der Body muss ein JSON-Objekt sein."));
						return;
					}
				}
			}

			// Query-Parameter ergänzen die Payload (praktisch für GET-Aufrufe aus dem Terminal).
			foreach (string? key in request.QueryString.AllKeys)
			{
				if (key is null)
					continue;
				payload ??= new JsonObject();
				payload[key] ??= request.QueryString[key];
			}

			var result = await _api.ExecuteAsync(command, payload);
			int statusCode = result.Success
				? 200
				: result.Error?.StartsWith("Unbekanntes Kommando", StringComparison.Ordinal) == true ? 404 : 400;

			await WriteJsonAsync(response, statusCode, result);
		}
		catch (JsonException ex)
		{
			await TryWriteErrorAsync(response, 400, $"Ungültiges JSON: {ex.Message}");
		}
		catch (Exception ex)
		{
			await TryWriteErrorAsync(response, 500, ex.Message);
		}
		finally
		{
			try { response.Close(); } catch { /* Verbindung ggf. schon zu */ }
		}
	}

	static async Task TryWriteErrorAsync(HttpListenerResponse response, int statusCode, string message)
	{
		try
		{
			await WriteJsonAsync(response, statusCode, ApiResult.Fail(message));
		}
		catch
		{
			// Antwort nicht mehr schreibbar (Client weg) – nichts zu tun.
		}
	}

	static async Task WriteJsonAsync(HttpListenerResponse response, int statusCode, ApiResult result)
	{
		byte[] body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(result, AppApi.SerializerOptions));
		response.StatusCode = statusCode;
		response.ContentType = "application/json; charset=utf-8";
		response.ContentLength64 = body.Length;
		await response.OutputStream.WriteAsync(body);
	}
}
