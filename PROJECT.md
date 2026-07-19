# HelloMauiApp — Device SDK

> Aktueller, verbindlicher Projektstand und Zielbild. Dieses Dokument ist der
> Einstiegspunkt für jede Session. Für die konkrete Umsetzungsplanung siehe
> [`ROADMAP.md`](./ROADMAP.md), für die Detailspezifikation des ersten
> Umbauschritts siehe [`PRINTER_ARCHITECTURE_PLAN.md`](./PRINTER_ARCHITECTURE_PLAN.md).

---

## 1. Was wir bauen

Wir bauen eine **Device SDK**: eine unabhängige Abstraktionsschicht zwischen
Anwendungen und physischer Hardware. Die SDK übersetzt zwischen Software und
Geräten — eine Anwendung sagt *was* sie will, die SDK löst *wie* es auf der
Hardware passiert.

Die **HelloMauiApp** (.NET MAUI, net10.0) ist dabei **nur die Test- und
Entwicklungsumgebung** für die SDK — austauschbar gegen Web-, Windows-,
Android-Clients oder Industrie-Terminals. Die eigentliche Logik lebt in der SDK,
nicht in der App.

Der **Etikettendruck (ZPL/TCP)** ist der erste konkrete Adapter — der Beweis,
dass die Abstraktion trägt. Er ist bereits funktionsfähig und wird schrittweise
unter das SDK-Modell gezogen.

```
   MAUI-UI (austauschbar)
        │
   Device SDK  ◄── hier liegt die eigentliche Logik
        │
   ┌────┴───────────────┐
 Printer-      Scanner-     … weitere Geräte
 Adapter       Adapter
        │
    Hardware
```

## 2. Kernprinzip

Eine Anwendung soll **niemals** wissen müssen:

- welches Druckermodell / welcher Hersteller verbunden ist
- welches Protokoll (ZPL, TSPL, …) nötig ist
- ob die Verbindung über USB, Bluetooth, WLAN oder LAN läuft
- welche Treiber notwendig sind

Die Anwendung arbeitet ausschließlich mit: **Geräten, Fähigkeiten, Rollen, Jobs
und Statusinformationen.**

```
   NICHT:  "Drucke auf Zebra ZD421 über ZPL"
   SONDERN: "Drucke Label an Gerät mit Rolle Versand.PaketLabel"
```

Die SDK löst intern auf:

```
   Rolle → passendes Gerät → passender Adapter → Hardwarekommunikation
```

## 3. Die wichtigste Architekturregel

> **Die Device SDK kennt Hardware. Sie kennt keine Geschäftslogik.**

Sie kennt **nicht**: Benutzer, Aufträge, Lagerprozesse, Warum ein Label gedruckt wird.
Sie stellt **nur Fähigkeiten** bereit.

- Höhere Systeme entscheiden: **Was** soll passieren?
- Die Device SDK entscheidet: **Wie** wird es mit der Hardware umgesetzt?

Beispiel: Ein Scanner meldet `BarcodeScanned` — er bucht **nicht** einen
Wareneingang. Diese Grenze ist nicht verhandelbar; sie ist der Grund, warum die
SDK wiederverwendbar bleibt.

## 4. Die vier Aufgaben der SDK

| Aufgabe | Bedeutung |
|---|---|
| **Discovery** | Geräte finden und registrieren (lokal, Netzwerk). |
| **Management** | Verwalten: Online/Offline, Verbindung, Eigenschaften, Fähigkeiten, Standort, Rolle. |
| **Abstraction** | Herstellerunterschiede bleiben intern; die App spricht immer gegen dieselbe Schnittstelle. |
| **Jobs / Events** | Commands an die Hardware, Events von der Hardware zurück. |

## 5. Command- & Event-Modell

Klare Richtungstrennung, weil sie die spätere Erweiterbarkeit trägt:

- **Commands** (App → Hardware): `PrintLabel`, `StartScan`, `ReadWeight`
- **Events** (Hardware → App): `BarcodeScanned`, `PrintCompleted`, `DeviceOffline`

Die SDK **liefert** Hardware-Ereignisse, **entscheidet** aber keine Geschäftsaktionen.

## 6. Rollenmodell

Geräte werden **nicht primär über Modelle** definiert, sondern über **Standort +
unterstützte Rollen**:

```
   Packplatz 4
     ├── Versand.PaketLabel
     ├── Versand.ShippingLabel
     └── Versand.RetourenLabel
```

Das skaliert in beide Richtungen: ein kleines Unternehmen hat *einen* Drucker mit
*allen* Rollen; ein großes Lager hat *100* Drucker mit arbeitsplatzabhängigen
Rollen. Die Anwendung fragt immer nach der Rolle, nie nach dem Modell.

## 7. Template / Medium (Sicht der SDK)

Ein Template (aus einer höheren Schicht) definiert z.B.:

```
   DHL Versandlabel · Medium 100×150mm · 203 DPI · benötigt Rolle Versand.PaketLabel
```

Die SDK **nutzt** diese Anforderung, um ein passendes Gerät zu finden. Sie
entscheidet **nicht**, *warum* das Label gedruckt wird — das gehört in höhere Schichten.

## 8. Client-Agent-Konzept (langfristig)

Hardware steht normalerweise lokal. Die SDK arbeitet deshalb langfristig über
lokale Client-Agents, die vom zentralen Server entkoppeln:

```
   Server ──► Client-Agent ──► Drucker (USB/BT/WLAN/LAN — egal)
```

Der Client-Agent erkennt Hardware, hält lokale Verbindungen, führt Jobs aus und
meldet Status. **Kein Peer-zu-Peer** zwischen Clients — immer über den Server.
Der Server selbst ist noch nicht Teil der Arbeit; es werden nur die
clientseitigen Contracts vorbereitet (SignalR kann später sauber andocken).

## 9. Einordnung in die spätere Plattform (nur Kontext)

Die Device SDK ist der **unterste, erste Baustein** einer geplanten SDK-Familie.
Sie muss so gebaut sein, dass die folgenden Ebenen darauf aufsetzen können —
gebaut wird davon jetzt **nichts**:

```
   Auth SDK          → Wer ist der Benutzer?
   Perspective SDK   → Was sieht/darf dieser Benutzer?
   Data SDK          → Wie werden Rohdaten interpretiert?
   Action SDK        → Welche Aktion wird ausgeführt?
   Device SDK   ◄── HIER sind wir. Wie wird es auf der Hardware umgesetzt?
```

---

## 10. Aktueller Stand (Snapshot)

**Umgesetzt — und schon jetzt tragende SDK-Grundlagen (kein Wegwerf-Prototyp):**
- **Trennung Library ↔ App:** `LabelPrinting` ist als eigenständiges Projekt von
  `HelloMauiApp` gelöst → das Prinzip „UI austauschbar, Logik in der SDK" ist
  strukturell bereits realisiert.
- **Interface-Abstraktion:** `IPrinterService` trennt Kontrakt von Implementierung
  → der Keim des späteren Adapter-Modells steht.
- **Saubere Ergebnistypen:** `PrinterResult`/`PrinterQueryResult` kapseln Fehler,
  es leaken **keine** Exceptions zur UI → das Prinzip „App behandelt keine
  Hardware-Fehler selbst" ist eingehalten.
- **Bidirektionale Kommunikation:** `QueryAsync` (`~HS`-Status, SGD `getvar`)
  liest Antworten der Hardware → die Basis für das spätere Event-Modell existiert.
- **Konkrete Funktion:** ZPL-Erzeugung (`ZplLabelBuilder`), Bild→ZPL
  (`ZplImageConverter`/SkiaSharp), RAW-Socket-Druck über TCP/9100
  (`ZplPrinterService`), MAUI-Test-UI (Testverbindung, Testlabel, Bilddruck,
  freier ZPL-Editor, `~JC`-Kalibrierung).

**Umgesetzt — Phase 1 (Mehrfach-Drucker & Transport-Abstraktion, erledigt):**
- **Profile statt Einzeldrucker:** `PrinterProfile` (Liste) mit `IPrinterProfileStore`/
  `PrinterProfileStore`; genau ein Default-Profil ist der app-weit aktive Drucker.
  Labelgeometrie/DPI gehören nun zum Profil, nicht mehr global. Die alten
  Einzeldrucker-Werte werden beim ersten Zugriff **einmalig** migriert
  (Migrations-Flag), die Legacy-`PrinterSettings`/`PrinterSettingsStore` bleiben nur
  als Migrationsquelle bestehen.
- **Transport über Profil:** `IPrinterConnectionFactory`/`PrinterConnectionFactory`
  bauen aus dem Profil die passende `IPrinterConnection` — TCP implementiert,
  USB/Bluetooth als ehrliche Stubs (`UsbPrinterConnection`/`BluetoothPrinterConnection`,
  werfen `PrinterTransportNotImplementedException`). `ZplPrinterService` bekam
  `PrinterProfile`-Überladungen; Verhalten des TCP-Drucks unverändert.
- **Remote-Contracts (nur Interfaces):** `LabelPrinting/Remote/` mit `PrintJobRequest`/
  `PrintJobResult`, `IRemotePrintClient`, `IPrintProviderHost` (+ Registrierungstypen).
  Kein SignalR, keine Implementierung — Remote-Profile liefern ohne Client ein
  sauberes Fail-Ergebnis.
- **UI + DI:** `PrinterProfilesPage` (Profilverwaltung), Services als DI-Singletons in
  `MauiProgram.cs`. 65 Tests grün, Verhaltensgleichheit des TCP-Drucks verifiziert.
- **Einheitliches UI-Design der Test-App (2026-07-18):** Alle Seiten — Rail-Sektionen
  wie Drill-downs — folgen dem Token-Design: Farbtokens (`Tokens.Light/Dark.xaml`,
  zur Laufzeit vom `AppearanceService` gesetzt) plus zentrale Styles in
  `Resources/Styles/Styles.xaml` (`Card`, `ChipButton`, `AccentChipButton`,
  `DangerButton`, `SectionTitle`, `FieldLabel`, `ToolbarSeparator`). Alle Seiten
  beziehen ihre Services per Konstruktor-DI; das alte `new`-Service-Muster ist
  entfernt. **Wichtige Regel:** App-weite Styles per `DynamicResource`
  referenzieren, nie `StaticResource` — sonst Startcrash (Begründung und Lektion
  in `BACKLOG.md`, CLEAN-03). Crash-Diagnose: `%TEMP%\hellomaui_crash.txt`
  (UnhandledException-Logger in `Platforms/Windows/App.xaml.cs`).

**Grenzen des jetzigen Stands (was die nächsten Schritte aufheben):**
- Der Drucker ist noch kein abstraktes *Device* mit *Capability*/*Role* — die App
  spricht weiter den `PrinterService`/das Profil direkt an. Genau hier setzt Phase 2
  an (siehe `ROADMAP.md`).
- USB/Bluetooth sind nur Stubs; Remote hat nur Verträge, keinen Server.

**Bewusst noch nicht angefasst:**
- Device-SDK-Kernbegriffe (Device, Capability, Role, Job) als eigene Schicht.
- Discovery, Client-Agent, Server/Backend, weitere Gerätetypen, alle höheren SDKs.

## 11. Bewertung des Ziels (ehrliche Einschätzung)

**Tragfähig:**
- Die Schichtung (App → SDK → Adapter → Hardware) und die Regel „keine
  Geschäftslogik in der SDK" sind sauber und branchenüblich. Das Command/Event-
  und Rollenmodell ist die richtige Grundlage für Erweiterbarkeit.
- Der bereits geplante Drucker-Refactor (`PRINTER_ARCHITECTURE_PLAN.md`) ist
  **exakt der richtige erste Schritt** — Transport-Abstraktion, Profile und
  Remote-Contracts sind genau die Keimzelle des Device-SDK-Modells. Nichts davon
  ist Wegwerfarbeit.

**Risiken / Leitplanken, die wir bewusst einhalten:**
1. **Nicht alles auf einmal.** Die SDK-Familie (Auth/Perspective/Data/Action) ist
   Fernziel und Kontext — jetzt wird ausschließlich die Device SDK gebaut. Kein
   Vorbau für höhere Schichten.
2. **Erst manuell, dann Discovery.** Geräte werden zunächst manuell registriert
   (wie heute die Druckerprofile). Automatische Discovery (mDNS/SSDP) kommt spät.
3. **Rolle als strukturierte Kennung**, nicht als loser String. Format
   `Bereich.Rolle` (z.B. `Versand.PaketLabel`), damit Auflösung und Gruppierung
   verlässlich bleiben.
4. **Stubs müssen ehrlich sein.** USB/Bluetooth/Remote werden als klar
   erkennbare „nicht implementiert"-Stubs angelegt, die einen sauberen Fehler
   liefern — nie als stiller Fake.
5. **Verhaltensgleichheit.** Der funktionierende ZPL/TCP-Pfad darf sich beim
   Umbau nie im Verhalten ändern — nur die Aufrufwege.
6. **Benennung wächst mit.** Die Library heißt heute `LabelPrinting`. Mittelfristig
   entsteht daneben ein `DeviceSdk`-Kern; der Drucker wird zu einem
   `DeviceSdk.Printing`-Adapter. Umbenannt wird erst, wenn der Kern steht (siehe
   Roadmap), nicht spekulativ vorab.

**Fazit:** Das Ziel ist stimmig und die bisherige Arbeit zahlt darauf ein. Der
Hebel liegt in der Disziplin, die SDK **klein und hardwarenah** zu halten und die
höheren SDKs strikt außen vor zu lassen, bis der Device-Kern trägt.

## 12. Glossar

| Begriff | Bedeutung in diesem Projekt |
|---|---|
| **Device** | Ein physisches Gerät (Drucker, Scanner, …), von der SDK verwaltet. |
| **Adapter** | Hersteller-/protokollspezifische Umsetzung hinter einer einheitlichen Schnittstelle. |
| **Transport** | Anbindungsart (TCP, USB, Bluetooth) — das *Wie* der Verbindung. |
| **Capability / Fähigkeit** | Was ein Gerät kann (z.B. „Label drucken", „Barcode scannen"). |
| **Role / Rolle** | Fachlicher Zweck an einem Standort (`Versand.PaketLabel`), nach dem die App fragt. |
| **Job** | Eine ausgeführte Aufgabe an einem Gerät (z.B. ein Druckauftrag) mit Status. |
| **Command** | Anweisung App → Hardware (`PrintLabel`). |
| **Event** | Meldung Hardware → App (`BarcodeScanned`, `DeviceOffline`). |
| **Client-Agent** | Lokaler Prozess, der Hardware hält und für den Server ausführt. |
| **Profile** | Persistierte Gerätekonfiguration (heute: `PrinterProfile`). |
