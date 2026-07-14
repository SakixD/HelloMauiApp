# LabelPrinting

.NET MAUI-Klassenbibliothek für die Kommunikation mit netzwerkfähigen Etiketten-/Versandlabeldruckern. Losgelöst von jeder konkreten App, damit sie von mehreren Apps referenziert und unabhängig weiterentwickelt werden kann.

## Umfang

- **Transport:** RAW-Socket-Druck über TCP (Standardport `9100`, "JetDirect-Port") sowie seriell (RS232/USB-CDC) über `System.IO.Ports`. Beide laufen über die transportunabhängige `IPrinterConnection`-Abstraktion; weitere Verbindungsarten (USB, Bluetooth) lassen sich als zusätzliche `IPrinterConnection`-Implementierung ergänzen, ohne `ZplPrinterService` selbst anzufassen.
- **Sprache:** Aktuell **ZPL** (Zebra Printer Language) – nativ bei Zebra-Druckern, per Emulationsmodus auch bei vielen anderen Herstellern (z.B. Honeywell "ZSim"). Drucker, die ausschließlich eine andere Sprache sprechen (TSPL, DPL, EPL, ESC/POS, ...), werden aktuell nicht unterstützt – dafür müsste ein weiterer `I...Service`/Encoder für die jeweilige Sprache ergänzt werden, der Transport-Code kann dabei wiederverwendet werden.

## Bausteine

| Datei | Zweck |
|---|---|
| `Services/IPrinterConnection.cs` + `TcpPrinterConnection.cs` + `SerialPrinterConnection.cs` | Transportunabhängige Verbindungsabstraktion (Connect/Write/Read) mit TCP- und seriellen Implementierungen |
| `Services/IPrinterService.cs` + `ZplPrinterService.cs` | Senden von Rohdaten/ZPL, bidirektionale Abfragen (Status, SGD) – sowohl über IP/Port (baut intern eine `TcpPrinterConnection` auf) als auch direkt über eine beliebige `IPrinterConnection` |
| `Services/PrinterStatus.cs` + `ZplStatusParser.cs` | Wertet die `~HS`-Statusantwort in strukturierte Felder aus (Papier/Band/Kopf, kalibrierte Etikettenlänge) – Grundlage der automatischen Medienerkennung |
| `Services/SgdResponseParser.cs` + `IPrinterService.GetVariableAsync`/`SetVariableAsync`/`RestartAsync` | Lesen/Schreiben einzelner SGD-Variablen (ZPL "! U1 getvar"/"setvar", z.B. `device.friendly_name`, `ip.addr`) sowie Geräteneustart (`device.reset`) – Grundlage für Geräteeinstellungen (Name, Netzwerk) direkt am Drucker, z.B. beim Anlernen eines neuen Geräts per USB/seriell |
| `Services/ZplLabelBuilder.cs` | Fluent-Builder für Labels aus Text-, Barcode- und Grafikfeldern |
| `Services/ZplImageConverter.cs` | Wandelt Bilder (Logos/Symbole) via SkiaSharp + Dithering in ZPL-Grafikfelder (`^GFA`) um |
| `Services/PrinterResult.cs`, `PrinterQueryResult.cs`, `ZplGraphic.cs` | Ergebnistypen |
| `Services/LabelSamples.cs` | Fertige ZPL-Beispiellabels (Testdruck, Bild-als-Label) für die Testfunktionen der App |
| `Models/PrinterSettings.cs` + `Services/PrinterSettingsStore.cs` | Druckerkonfiguration (IP, Port, Labelgröße, DPI) als reines Datenmodell plus Store, der sie über `Preferences` persistiert |
| `Models/PrintMedia.cs` + `Services/PrintMediaStore.cs` | Druckmedien-Presets (Breite/Höhe/Gap/Sensor/Material), persistiert per stabiler Id |
| `Models/LabelTemplate.cs` (`Id`, `Metadata`, `PrintParameters`) | Vorlagen haben eine stabile Id (überlebt Umbenennung), freie Metadaten (Beschreibung/Kategorie/Tags) sowie optionale Druckparameter (bevorzugtes Medium, Geschwindigkeit, Darkness), die der Renderer automatisch als `^PR`/`~SD` anwendet |

## Verwendung (Kurzbeispiel)

```csharp
IPrinterService printer = new ZplPrinterService();

var label = new ZplLabelBuilder(widthMm: 100, heightMm: 150, dpi: 203)
    .AddText(40, 40, "Testlabel")
    .AddBarcode128(40, 100, "123456789012")
    .Build();

var result = await printer.SendZplAsync("192.168.1.50", 9100, label);
```

## Bekannte Grenzen

- Nur ZPL-fähige Drucker (siehe oben).
- Statusfeld-Interpretation von `~HS` ist best-effort (Zebra-Standard-Feldreihenfolge) – je nach Firmware/Emulation können Feldpositionen leicht abweichen; vor produktivem Einsatz gegen den jeweiligen Drucker verifizieren. Aus demselben Grund kann die automatische Medienerkennung nur die Etikettenlänge (aus der letzten Kalibrierung) sowie Papier-/Band-/Kopfstatus liefern – die Breite meldet der Drucker nicht und muss manuell eingetragen werden.
- Die in `PrinterDeviceSettingsPage` kuratierten SGD-Variablennamen (`device.friendly_name`, `ip.addr`, `ip.netmask`, `ip.gateway`, `ip.dhcp.enable`) folgen dem Zebra-SGD-Standard und sind ein Startpunkt, kein verifizierter Fakt – Honeywell-Modelle im ZSim-Emulationsmodus können abweichende Namen verwenden. Vor produktivem Einsatz am jeweiligen Gerät prüfen (die Seite zeigt den verwendeten Variablennamen neben jedem Feld an) und bei Bedarf über die "Freie Variable"-Zeile die tatsächlichen Namen ermitteln.
- `SerialPrinterConnection` funktioniert laut Microsoft-Dokumentation nur unter Windows und Linux; auf Android/iOS/MacCatalyst wirft das Öffnen des Ports eine `PlatformNotSupportedException`, die wie ein normaler Verbindungsfehler behandelt wird (kein Absturz, aber auch keine Funktion dort).
- USB und Bluetooth sind architektonisch vorbereitet (weitere `IPrinterConnection`-Implementierungen), aber noch nicht umgesetzt – beide brauchen plattformspezifischen Code (WinUSB/Windows.Devices.Bluetooth, Android USB-Host-API/BluetoothSocket, ...), der sich ohne echte Hardware zum Testen nicht seriös umsetzen lässt.
- Kein PDF-Rendering: Labels, die nur als PDF vorliegen (z.B. manche Carrier-APIs), müssen vorher extern in ZPL oder ein Rasterbild umgewandelt werden.
