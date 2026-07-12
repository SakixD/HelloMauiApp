# LabelPrinting

.NET MAUI-Klassenbibliothek für die Kommunikation mit netzwerkfähigen Etiketten-/Versandlabeldruckern. Losgelöst von jeder konkreten App, damit sie von mehreren Apps referenziert und unabhängig weiterentwickelt werden kann.

## Umfang

- **Transport:** RAW-Socket-Druck über TCP (Standardport `9100`, "JetDirect-Port"). Funktioniert mit praktisch jedem netzwerkfähigen Drucker, unabhängig vom Hersteller.
- **Sprache:** Aktuell **ZPL** (Zebra Printer Language) – nativ bei Zebra-Druckern, per Emulationsmodus auch bei vielen anderen Herstellern (z.B. Honeywell "ZSim"). Drucker, die ausschließlich eine andere Sprache sprechen (TSPL, DPL, EPL, ESC/POS, ...), werden aktuell nicht unterstützt – dafür müsste ein weiterer `I...Service`/Encoder für die jeweilige Sprache ergänzt werden, der TCP-Transport-Code kann dabei wiederverwendet werden.

## Bausteine

| Datei | Zweck |
|---|---|
| `Services/IPrinterService.cs` + `ZplPrinterService.cs` | TCP-Verbindungsaufbau, Senden von Rohdaten/ZPL, bidirektionale Abfragen (Status, SGD) |
| `Services/ZplLabelBuilder.cs` | Fluent-Builder für Labels aus Text-, Barcode- und Grafikfeldern |
| `Services/ZplImageConverter.cs` | Wandelt Bilder (Logos/Symbole) via SkiaSharp + Dithering in ZPL-Grafikfelder (`^GFA`) um |
| `Services/PrinterResult.cs`, `PrinterQueryResult.cs`, `ZplGraphic.cs` | Ergebnistypen |
| `Services/LabelSamples.cs` | Fertige ZPL-Beispiellabels (Testdruck, Bild-als-Label) für die Testfunktionen der App |
| `Models/PrinterSettings.cs` + `Services/PrinterSettingsStore.cs` | Druckerkonfiguration (IP, Port, Labelgröße, DPI) als reines Datenmodell plus Store, der sie über `Preferences` persistiert |

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
- Statusfeld-Interpretation von `~HS` ist best-effort (Zebra-Standard-Feldreihenfolge) – je nach Firmware/Emulation können Feldpositionen leicht abweichen; vor produktivem Einsatz gegen den jeweiligen Drucker verifizieren.
- Kein PDF-Rendering: Labels, die nur als PDF vorliegen (z.B. manche Carrier-APIs), müssen vorher extern in ZPL oder ein Rasterbild umgewandelt werden.
