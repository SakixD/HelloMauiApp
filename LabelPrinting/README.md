# LabelPrinting

.NET MAUI-Klassenbibliothek für die Kommunikation mit netzwerkfähigen Etiketten-/Versandlabeldruckern. Losgelöst von jeder konkreten App, damit sie von mehreren Apps referenziert und unabhängig weiterentwickelt werden kann.

## Umfang

- **Transport:** RAW-Socket-Druck über TCP (Standardport `9100`, "JetDirect-Port") sowie seriell (RS232/USB-CDC) über `System.IO.Ports`. Beide laufen über die transportunabhängige `IPrinterConnection`-Abstraktion; weitere Verbindungsarten (USB, Bluetooth) lassen sich als zusätzliche `IPrinterConnection`-Implementierung ergänzen, ohne `ZplPrinterService` selbst anzufassen. Welche Verbindung zu einem Drucker gehört, ergibt sich aus seinem `PrinterProfile`: die `IPrinterConnectionFactory` baut daraus die passende `IPrinterConnection` (Profil sagt *was*, Factory baut *wie*).
- **Drucker als Profil:** Statt einer globalen Einzeldrucker-Konfiguration verwaltet die Bibliothek eine Liste von `PrinterProfile`s (je Profil: Anbindungsart, Adressdaten, Labelgeometrie/DPI), von denen genau eines das Default-Profil ist. Persistenz und Einmal-Migration der Alt-Konfiguration übernimmt `IPrinterProfileStore`.
- **Remote (Verträge):** Der Ordner `Remote/` enthält nur die Schnittstellen für späteren Server-vermittelten Druck (ein Client stellt Drucker bereit, ein anderer nutzt sie) – ohne Implementierung, ohne SignalR. Profile mit `PrinterConnectionMode.Remote` liefern ohne angebundenen `IRemotePrintClient` ein sauberes Fail-Ergebnis.
- **Sprache:** Aktuell **ZPL** (Zebra Printer Language) – nativ bei Zebra-Druckern, per Emulationsmodus auch bei vielen anderen Herstellern (z.B. Honeywell "ZSim"). Drucker, die ausschließlich eine andere Sprache sprechen (TSPL, DPL, EPL, ESC/POS, ...), werden aktuell nicht unterstützt – dafür müsste ein weiterer `I...Service`/Encoder für die jeweilige Sprache ergänzt werden, der Transport-Code kann dabei wiederverwendet werden.

## Bausteine

| Datei | Zweck |
|---|---|
| `Services/IPrinterConnection.cs` + `TcpPrinterConnection.cs` + `SerialPrinterConnection.cs` | Transportunabhängige Verbindungsabstraktion (Connect/Write/Read) mit TCP- und seriellen Implementierungen |
| `Services/IPrinterService.cs` + `ZplPrinterService.cs` | Senden von Rohdaten/ZPL, bidirektionale Abfragen (Status, SGD) – Standardweg über `PrinterProfile`-Überladungen (die `IPrinterConnectionFactory` baut die Verbindung; Remote-Profile delegieren an den optionalen `IRemotePrintClient`), für Spezialfälle (z.B. seriell) zusätzlich Überladungen mit beliebiger `IPrinterConnection` |
| `Services/PrinterStatus.cs` + `ZplStatusParser.cs` | Wertet die `~HS`-Statusantwort in strukturierte Felder aus (Papier/Band/Kopf, kalibrierte Etikettenlänge) – Grundlage der automatischen Medienerkennung |
| `Services/SgdResponseParser.cs` + `IPrinterService.GetVariableAsync`/`SetVariableAsync`/`RestartAsync` | Lesen/Schreiben einzelner SGD-Variablen (ZPL "! U1 getvar"/"setvar", z.B. `device.friendly_name`, `ip.addr`) sowie Geräteneustart (`device.reset`) – Grundlage für Geräteeinstellungen (Name, Netzwerk) direkt am Drucker, z.B. beim Anlernen eines neuen Geräts per USB/seriell |
| `Services/ZplLabelBuilder.cs` | Fluent-Builder für Labels aus Text-, Barcode- und Grafikfeldern |
| `Services/ZplImageConverter.cs` | Wandelt Bilder (Logos/Symbole) via SkiaSharp + Dithering in ZPL-Grafikfelder (`^GFA`) um |
| `Services/PrinterResult.cs`, `PrinterQueryResult.cs`, `ZplGraphic.cs` | Ergebnistypen |
| `Services/LabelSamples.cs` | Fertige ZPL-Beispiellabels (Testdruck, Bild-als-Label) für die Testfunktionen der App |
| `Models/PrinterProfile.cs` (+ `PrinterConnectionMode.cs`, `PrinterTransportKind.cs`) | Ein konfigurierter Drucker als eigenständiges Profil: Anbindungsart (Local/Remote, Tcp/Usb/Bluetooth), Adressdaten, Labelgeometrie/DPI, Default-Kennzeichnung; ersetzt die frühere globale Einzeldrucker-Konfiguration |
| `Services/IPrinterProfileStore.cs` + `PrinterProfileStore.cs` | Persistenz der Profil-Liste (JSON in `Preferences`); höchstens ein Default-Profil (= app-weit aktiver Drucker); migriert die Legacy-Einzeldrucker-Werte **einmalig** in ein Default-Profil |
| `Services/IPrinterConnectionFactory.cs` + `PrinterConnectionFactory.cs` | Baut aus einem lokalen `PrinterProfile` die passende, noch nicht verbundene `IPrinterConnection` (Profil sagt *was*, Factory baut *wie*) |
| `Services/UsbPrinterConnection.cs` + `BluetoothPrinterConnection.cs` + `PrinterTransportNotImplementedException.cs` | Ehrliche Stubs für USB/Bluetooth: architektonisch vorbereitet, werfen bei Nutzung eine klar erkennbare „nicht implementiert"-Ausnahme (kein stiller Fake) |
| `Remote/*` (`IRemotePrintClient`, `IPrintProviderHost`, `PrintJobRequest`/`PrintJobResult`, `PrintProviderRegistration`/`PrintProviderPrinterInfo`, `PrintPayloadKind`) | Verträge für späteren Server-vermittelten Druck – nur Interfaces/DTOs, keine Implementierung |
| `Models/PrinterSettings.cs` + `Services/PrinterSettingsStore.cs` | **Legacy** (als obsolet markiert): frühere Einzeldrucker-Konfiguration; bleibt nur noch als Quelle der einmaligen Profil-Migration bestehen |
| `Models/PrintMedia.cs` + `Services/PrintMediaStore.cs` | Druckmedien-Presets (Breite/Höhe/Gap/Sensor/Material), persistiert per stabiler Id |
| `Models/LabelTemplate.cs` (`Id`, `Metadata`, `PrintParameters`) | Vorlagen haben eine stabile Id (überlebt Umbenennung), freie Metadaten (Beschreibung/Kategorie/Tags) sowie optionale Druckparameter (bevorzugtes Medium, Geschwindigkeit, Darkness), die der Renderer automatisch als `^PR`/`~SD` anwendet |

## Verwendung (Kurzbeispiel)

```csharp
IPrinterService printer = new ZplPrinterService();

// Ein Profil beschreibt den Zieldrucker (i.d.R. aus dem IPrinterProfileStore).
var profile = new PrinterProfile
{
    Name = "Packplatz 4",
    TransportKind = PrinterTransportKind.Tcp,
    IpAddress = "192.168.1.50",
    Port = 9100,
    LabelWidthMm = 100,
    LabelHeightMm = 150,
    Dpi = 203,
};

var label = new ZplLabelBuilder(widthMm: 100, heightMm: 150, dpi: 203)
    .AddText(40, 40, "Testlabel")
    .AddBarcode128(40, 100, "123456789012")
    .Build();

var result = await printer.SendZplAsync(profile, label);
```

## Bekannte Grenzen

- Nur ZPL-fähige Drucker (siehe oben).
- Statusfeld-Interpretation von `~HS` ist best-effort (Zebra-Standard-Feldreihenfolge) – je nach Firmware/Emulation können Feldpositionen leicht abweichen; vor produktivem Einsatz gegen den jeweiligen Drucker verifizieren. Aus demselben Grund kann die automatische Medienerkennung nur die Etikettenlänge (aus der letzten Kalibrierung) sowie Papier-/Band-/Kopfstatus liefern – die Breite meldet der Drucker nicht und muss manuell eingetragen werden.
- Die in `PrinterDeviceSettingsPage` kuratierten SGD-Variablennamen (`device.friendly_name`, `ip.addr`, `ip.netmask`, `ip.gateway`, `ip.dhcp.enable`) folgen dem Zebra-SGD-Standard und sind ein Startpunkt, kein verifizierter Fakt – Honeywell-Modelle im ZSim-Emulationsmodus können abweichende Namen verwenden. Vor produktivem Einsatz am jeweiligen Gerät prüfen (die Seite zeigt den verwendeten Variablennamen neben jedem Feld an) und bei Bedarf über die "Freie Variable"-Zeile die tatsächlichen Namen ermitteln.
- `SerialPrinterConnection` funktioniert laut Microsoft-Dokumentation nur unter Windows und Linux; auf Android/iOS/MacCatalyst wirft das Öffnen des Ports eine `PlatformNotSupportedException`, die wie ein normaler Verbindungsfehler behandelt wird (kein Absturz, aber auch keine Funktion dort).
- USB und Bluetooth sind architektonisch vorbereitet (weitere `IPrinterConnection`-Implementierungen), aber noch nicht umgesetzt – beide brauchen plattformspezifischen Code (WinUSB/Windows.Devices.Bluetooth, Android USB-Host-API/BluetoothSocket, ...), der sich ohne echte Hardware zum Testen nicht seriös umsetzen lässt.
- Kein PDF-Rendering: Labels, die nur als PDF vorliegen (z.B. manche Carrier-APIs), müssen vorher extern in ZPL oder ein Rasterbild umgewandelt werden.
