using System.Text.Json;
using System.Text.Json.Nodes;
using LabelPrinting.Models;
using LabelPrinting.Services;

namespace HelloMauiApp.Services.Api;

/// <summary>
/// Implementierung des Kommando-Dispatchers: übersetzt benannte Kommandos in SDK-Aufrufe
/// (Stores, <see cref="IPrinterService"/>, <see cref="LabelTemplateRenderer"/>). Enthält selbst
/// keine Druck-/ZPL-Logik – die bleibt vollständig im SDK.
/// </summary>
public class AppApi : IAppApi
{
	/// <summary>Gemeinsame JSON-Konventionen der API: camelCase raus, Groß/Klein egal rein.</summary>
	public static readonly JsonSerializerOptions SerializerOptions = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		PropertyNameCaseInsensitive = true,
		WriteIndented = true,
	};

	record RegisteredCommand(ApiCommandInfo Info, Func<JsonObject?, CancellationToken, Task<ApiResult>> Handler);

	readonly Dictionary<string, RegisteredCommand> _commands = new(StringComparer.OrdinalIgnoreCase);

	readonly ILabelTemplateStore _templateStore;
	readonly IPrintMediaStore _mediaStore;
	readonly IPrinterSettingsStore _settingsStore;
	readonly IPrinterService _printerService;

	public AppApi(
		ILabelTemplateStore templateStore,
		IPrintMediaStore mediaStore,
		IPrinterSettingsStore settingsStore,
		IPrinterService printerService)
	{
		_templateStore = templateStore;
		_mediaStore = mediaStore;
		_settingsStore = settingsStore;
		_printerService = printerService;

		Register("app.info", "Name, Version und Kommandoanzahl der laufenden App.", null, AppInfoAsync);
		Register("templates.list", "Alle gespeicherten Vorlagen (Kurzform).", null, TemplatesListAsync);
		Register("templates.get", "Eine Vorlage vollständig als JSON.", "{ \"name\": \"...\" }", TemplatesGetAsync);
		Register("templates.save", "Vorlage speichern/überschreiben (Payload = Vorlagen-JSON).", "{ \"name\": \"...\", \"widthMm\": 100, ... }", TemplatesSaveAsync);
		Register("templates.delete", "Vorlage löschen.", "{ \"name\": \"...\" }", TemplatesDeleteAsync);
		Register("templates.render", "Vorlage mit Daten befüllen und als ZPL zurückgeben (druckt nicht).", "{ \"name\": \"...\", \"data\": { \"Key\": \"Wert\" } }", TemplatesRenderAsync);
		Register("print.template", "Vorlage befüllen und auf dem konfigurierten Drucker drucken.", "{ \"name\": \"...\", \"data\": { \"Key\": \"Wert\" } }", PrintTemplateAsync);
		Register("zpl.send", "Rohen ZPL-Code an den konfigurierten Drucker senden.", "{ \"zpl\": \"^XA...^XZ\" }", ZplSendAsync);
		Register("printer.status", "Druckerstatus abfragen (~HS, ausgewertete Felder).", null, PrinterStatusAsync);
		Register("printer.settings", "Konfigurierte Druckerverbindung und Labelmaße (nur lesen).", null, PrinterSettingsAsync);
		Register("media.list", "Alle gespeicherten Druckmedien-Presets.", null, MediaListAsync);
	}

	void Register(string name, string description, string? payloadHint, Func<JsonObject?, CancellationToken, Task<ApiResult>> handler) =>
		_commands[name] = new RegisteredCommand(new ApiCommandInfo(name, description, payloadHint), handler);

	public IReadOnlyList<ApiCommandInfo> Commands =>
		_commands.Values.Select(c => c.Info).OrderBy(i => i.Name, StringComparer.Ordinal).ToList();

	public async Task<ApiResult> ExecuteAsync(string command, JsonObject? payload, CancellationToken cancellationToken = default)
	{
		if (!_commands.TryGetValue(command, out var registered))
			return ApiResult.Fail($"Unbekanntes Kommando \"{command}\". GET /api listet alle Kommandos.");

		try
		{
			return await registered.Handler(payload, cancellationToken);
		}
		catch (Exception ex)
		{
			return ApiResult.Fail(ex.Message);
		}
	}

	// ---------- Payload-Helfer ----------

	static string? GetString(JsonObject? payload, string key) =>
		payload?[key] is JsonValue value && value.TryGetValue<string>(out var s) ? s : payload?[key]?.ToString();

	static Dictionary<string, string> GetDataDictionary(JsonObject? payload)
	{
		var data = new Dictionary<string, string>();
		if (payload?["data"] is JsonObject obj)
		{
			foreach (var (key, value) in obj)
				data[key] = value?.ToString() ?? string.Empty;
		}
		return data;
	}

	// ---------- Kommandos ----------

	Task<ApiResult> AppInfoAsync(JsonObject? payload, CancellationToken ct) =>
		Task.FromResult(ApiResult.Ok(new
		{
			App = "HelloMauiApp (LabelPrinting SDK Testkonsole)",
			Version = AppInfo.Current.VersionString,
			Platform = DeviceInfo.Current.Platform.ToString(),
			Commands = _commands.Count,
		}));

	async Task<ApiResult> TemplatesListAsync(JsonObject? payload, CancellationToken ct)
	{
		var names = await _templateStore.ListTemplateNamesAsync();
		var list = new List<object>();

		foreach (var name in names)
		{
			var template = await _templateStore.LoadAsync(name);
			if (template is null)
				continue;

			list.Add(new
			{
				template.Id,
				template.Name,
				template.WidthMm,
				template.HeightMm,
				template.Dpi,
				Elements = template.Elements.Count,
				Placeholders = template.Placeholders.Select(p => p.Key).ToList(),
			});
		}

		return ApiResult.Ok(list);
	}

	async Task<ApiResult> TemplatesGetAsync(JsonObject? payload, CancellationToken ct)
	{
		var (template, error) = await LoadTemplateAsync(payload);
		return template is null ? ApiResult.Fail(error!) : ApiResult.Ok(template);
	}

	async Task<ApiResult> TemplatesSaveAsync(JsonObject? payload, CancellationToken ct)
	{
		if (payload is null)
			return ApiResult.Fail("Payload fehlt: es wird das Vorlagen-JSON als Body erwartet.");

		var template = payload.Deserialize<LabelTemplate>(SerializerOptions);
		if (template is null || string.IsNullOrWhiteSpace(template.Name))
			return ApiResult.Fail("Payload ist keine gültige Vorlage (mindestens \"name\" wird benötigt).");

		await _templateStore.SaveAsync(template);
		return ApiResult.Ok(new { Saved = template.Name });
	}

	async Task<ApiResult> TemplatesDeleteAsync(JsonObject? payload, CancellationToken ct)
	{
		string? name = GetString(payload, "name");
		if (string.IsNullOrWhiteSpace(name))
			return ApiResult.Fail("Parameter \"name\" fehlt.");

		var existing = await _templateStore.LoadAsync(name);
		if (existing is null)
			return ApiResult.Fail($"Vorlage \"{name}\" wurde nicht gefunden.");

		await _templateStore.DeleteAsync(name);
		return ApiResult.Ok(new { Deleted = name });
	}

	async Task<ApiResult> TemplatesRenderAsync(JsonObject? payload, CancellationToken ct)
	{
		var rendered = await RenderAsync(payload);
		return rendered.Error is not null
			? ApiResult.Fail(rendered.Error)
			: ApiResult.Ok(new { rendered.Template!.Name, Zpl = rendered.Zpl, ResolvedData = rendered.ResolvedData });
	}

	async Task<ApiResult> PrintTemplateAsync(JsonObject? payload, CancellationToken ct)
	{
		var rendered = await RenderAsync(payload);
		if (rendered.Error is not null)
			return ApiResult.Fail(rendered.Error);

		var (settings, settingsError) = LoadConfiguredPrinter();
		if (settings is null)
			return ApiResult.Fail(settingsError!);

		var result = await _printerService.SendZplAsync(settings.IpAddress, settings.Port, rendered.Zpl!, ct);
		return result.Success
			? ApiResult.Ok(new { Printed = rendered.Template!.Name, Printer = $"{settings.IpAddress}:{settings.Port}" })
			: ApiResult.Fail(result.ErrorMessage ?? "Unbekannter Druckerfehler.");
	}

	async Task<ApiResult> ZplSendAsync(JsonObject? payload, CancellationToken ct)
	{
		string? zpl = GetString(payload, "zpl");
		if (string.IsNullOrWhiteSpace(zpl))
			return ApiResult.Fail("Parameter \"zpl\" fehlt.");

		var (settings, settingsError) = LoadConfiguredPrinter();
		if (settings is null)
			return ApiResult.Fail(settingsError!);

		var result = await _printerService.SendZplAsync(settings.IpAddress, settings.Port, zpl, ct);
		return result.Success
			? ApiResult.Ok(new { Sent = zpl.Length, Printer = $"{settings.IpAddress}:{settings.Port}" })
			: ApiResult.Fail(result.ErrorMessage ?? "Unbekannter Druckerfehler.");
	}

	async Task<ApiResult> PrinterStatusAsync(JsonObject? payload, CancellationToken ct)
	{
		var (settings, settingsError) = LoadConfiguredPrinter();
		if (settings is null)
			return ApiResult.Fail(settingsError!);

		var status = await _printerService.GetDetailedStatusAsync(settings.IpAddress, settings.Port, ct);
		return status.Success ? ApiResult.Ok(status) : ApiResult.Fail(status.ErrorMessage ?? "Statusabfrage fehlgeschlagen.");
	}

	Task<ApiResult> PrinterSettingsAsync(JsonObject? payload, CancellationToken ct) =>
		Task.FromResult(ApiResult.Ok(_settingsStore.Load()));

	async Task<ApiResult> MediaListAsync(JsonObject? payload, CancellationToken ct) =>
		ApiResult.Ok(await _mediaStore.ListAsync());

	// ---------- gemeinsame Abläufe ----------

	async Task<(LabelTemplate? Template, string? Error)> LoadTemplateAsync(JsonObject? payload)
	{
		string? name = GetString(payload, "name");
		if (string.IsNullOrWhiteSpace(name))
			return (null, "Parameter \"name\" fehlt.");

		var template = await _templateStore.LoadAsync(name);
		return template is null ? (null, $"Vorlage \"{name}\" wurde nicht gefunden.") : (template, null);
	}

	async Task<(LabelTemplate? Template, string? Zpl, IReadOnlyDictionary<string, string>? ResolvedData, string? Error)> RenderAsync(JsonObject? payload)
	{
		var (template, error) = await LoadTemplateAsync(payload);
		if (template is null)
			return (null, null, null, error);

		var fill = LabelTemplateFillService.Fill(template, GetDataDictionary(payload));
		if (!fill.Success)
			return (null, null, null, $"Pflicht-Platzhalter fehlen: {string.Join(", ", fill.MissingRequiredKeys)}. Werte über \"data\" mitgeben.");

		string zpl = LabelTemplateRenderer.ToZpl(template, fill.ResolvedData);
		return (template, zpl, fill.ResolvedData, null);
	}

	(PrinterSettings? Settings, string? Error) LoadConfiguredPrinter()
	{
		var settings = _settingsStore.Load();
		return string.IsNullOrWhiteSpace(settings.IpAddress)
			? (null, "Kein Drucker konfiguriert. Bitte in der App unter Drucker-Einstellungen die IP-Adresse eintragen.")
			: (settings, null);
	}
}
