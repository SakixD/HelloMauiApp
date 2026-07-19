# Roadmap — Device SDK

> Session-für-Session-Plan. Jede Phase ist ein in sich abgeschlossener,
> baubarer Schritt mit klarer „Definition of Done" (DoD). Wir arbeiten strikt
> von unten nach oben: erst der hardwarenahe Kern, dann Rollen/Jobs, dann
> Discovery/Agent. Höhere SDKs (Auth/Perspective/Data/Action) sind **nicht** Teil
> dieser Roadmap. Grundlage & Prinzipien: siehe [`PROJECT.md`](./PROJECT.md).

## Leitplanken für jede Session

- Nach jedem Schritt baubar halten (`dotnet build`, mind. Android-Target).
- Der funktionierende ZPL/TCP-Druck darf sich im **Verhalten** nie ändern.
- Keine Geschäftslogik in der SDK. Keine höheren SDKs vorbauen.
- Stubs (USB/BT/Remote) liefern saubere Fehler, nie stille Fakes.
- Additive Schritte einzeln committen; „breaking" Schritte in sich geschlossen.

## Statuslegende

`[ ]` offen · `[~]` in Arbeit · `[x]` erledigt

---

## Phase 0 — Fundament (ERLEDIGT)

`[x]` Grundlagen stehen und tragen bereits zentrale SDK-Prinzipien:
- Library ↔ App getrennt (`LabelPrinting` vs. `HelloMauiApp`) → „UI austauschbar".
- Interface-Abstraktion `IPrinterService` → Keim des Adapter-Modells.
- Ergebnistypen ohne Exception-Leak → „App behandelt keine Hardware-Fehler".
- Bidirektionale Kommunikation (`QueryAsync`/`~HS`/SGD) → Basis fürs Event-Modell.
- Funktion: ZPL-Erzeugung, Bild→ZPL, RAW-Druck (TCP/9100), MAUI-Test-UI,
  Einzeldrucker-Persistenz.

**DoD:** erreicht — funktionsfähige, sauber geschichtete Basis. Offen bleibt nur
die Verallgemeinerung (Profil-/Geräteobjekt statt `ip`/`port`), siehe Phase 1.

---

## Phase 1 — Mehrfach-Drucker & Transport-Abstraktion (ERLEDIGT)

Der spezifizierte Umbau ist auf `master` umgesetzt (Commits `439419b`, `ebc5121`,
`a974730`; siehe `DEV_BERICHT_2026-07-17.md`). Detailspec: `PRINTER_ARCHITECTURE_PLAN.md`.
Dies ist die Keimzelle des Device-Modells (Transport = das spätere „Wie").

- `[x]` **1a** Modelle + Persistenz: `PrinterConnectionMode`, `PrinterTransportKind`,
  `PrinterProfile` (Liste statt Einzeldrucker), `IPrinterProfileStore` inkl.
  Einmal-Migration der Legacy-Preferences. Additiv, App bleibt lauffähig.
- `[x]` **1b** Transport-Abstraktion: über die bereits vorhandene
  `IPrinterConnection`-Abstraktion realisiert — `IPrinterConnectionFactory`/
  `PrinterConnectionFactory` bauen aus dem Profil die passende `TcpPrinterConnection`
  (identisches Verhalten), USB/BT-Stubs (`UsbPrinterConnection`/`BluetoothPrinterConnection`).
- `[x]` **1c** Fassade umstellen: `IPrinterService` um `PrinterProfile`-Überladungen
  ergänzt. **Abweichung:** `ZplPrinterService` wurde *nicht* gelöscht, sondern auf
  Profile umgestellt (die Factory baut die Verbindung, Remote-Profile delegieren an
  den optionalen `IRemotePrintClient`) — siehe Abgleich-Notiz
  `PRINTER_ARCHITECTURE_PLAN.md:7-23`.
- `[x]` **1d** UI: `PrinterProfilesPage` (Profilverwaltung mit inline-Editor im
  `PrinterProfilesViewModel`), `MainPage` mit Drucker-Bezug; alte `PrinterSettingsPage`
  entfernt. **Abweichung:** keine separate `PrinterProfileEditPage` — siehe Abgleich-Notiz.
- `[x]` **1e** Remote-Contracts (nur Interfaces): `PrintJobRequest/Result`,
  `IRemotePrintClient`, `IPrintProviderHost` (+ `PrintProviderRegistration`/
  `PrintProviderPrinterInfo`/`PrintPayloadKind`) in `LabelPrinting/Remote/`.
  Kein SignalR, keine Implementierung.

**DoD:** erreicht — Mehrere Druckerprofile anleg-/wählbar, Verhaltensgleichheit des
TCP-Drucks verifiziert (65 Tests grün), Migration greift genau einmal, USB/BT/Remote
zeigen sauberen Fehler. Die Text-Abweichungen zu 1c/1d sind in
`PRINTER_ARCHITECTURE_PLAN.md:7-23` begründet dokumentiert.

---

## Phase 2 — Device-SDK-Kern einführen

Die abstrakte Schicht über den Druckern. Der Drucker wird zum *Device* mit einer
*Capability*. Neuer Projektkern `DeviceSdk` (Core), `LabelPrinting` wird der erste
Adapter dahinter.

- `[ ]` **2a** Kernbegriffe: `IDevice` (Id, Name, Standort, Status), `DeviceStatus`
  (Online/Offline/Unbekannt), `ICapability`-Basis, `PrintLabelCapability`.
- `[ ]` **2b** `IDeviceRegistry` / `DeviceManager`: registrieren, auflisten,
  nach Id/Capability finden, Status halten. Zunächst aus den `PrinterProfile`s gespeist.
- `[ ]` **2c** Printer-Adapter: `PrinterProfile` + `PrinterService` hinter einem
  `PrinterDevice : IDevice` mit `PrintLabelCapability` kapseln.
- `[ ]` **2d** Test-UI zeigt „Geräte" (nicht mehr nur „Drucker") und druckt über
  die Capability statt direkt über den `PrinterService`.

**DoD:** Die App druckt über `device.Capability<PrintLabelCapability>()` statt über
den Drucker-Service direkt; der Druckerbegriff ist ein Implementierungsdetail des Adapters.

---

## Phase 3 — Rollenmodell & Auflösung

Die App fragt nach **Rollen**, nicht nach Geräten.

- `[ ]` **3a** `DeviceRole` als strukturierte Kennung (`Bereich.Rolle`,
  z.B. `Versand.PaketLabel`); Rollen pro Gerät/Profil zuweis- und persistierbar.
  Datenebene bereits vorhanden (FEAT-01: `PrinterProfile.Roles` + Format-
  Validierung `DeviceRoleName`, aktuell freie Strings). **Kern von 3a und noch
  offen:** ein zentrales **Rollen-Verzeichnis** (`DeviceRoleStore`) als einzige
  Quelle gültiger Rollen — Voraussetzung für Auswahlliste im Editor statt
  Freitext, zentrale Namensregeln, „überall umbenennen" und die verlässliche
  Auflösung in 3b (siehe BACKLOG FEAT-07). Gehört bewusst hierher (Device-SDK-
  Phase, keine höhere SDK), zusammen mit dem Standort-/Raummodell.
- `[ ]` **3b** `IRoleResolver`: „finde Gerät(e) mit Rolle X" → 0..n Geräte,
  Default-/Auswahlstrategie bei Mehrdeutigkeit.
- `[ ]` **3c** UI: Rollenverwaltung pro Gerät; ein „Drucke an Rolle"-Testpfad.

**DoD:** Ein Testdruck lässt sich allein über eine Rolle auslösen; das konkrete
Gerät wird von der SDK aufgelöst, die App nennt kein Modell und keine IP.

---

## Phase 4 — Command- & Event-/Job-Modell

Von synchronen Aufrufen zu einem gerichteten Command/Event-Modell mit Jobs.

- `[ ]` **4a** `IDeviceCommand` (`PrintLabelCommand`, …) und `DeviceEvent`
  (`PrintCompleted`, `DeviceOffline`, später `BarcodeScanned`).
- `[ ]` **4b** `Job`-Modell: Aufgabe an ein Gerät mit Status
  (Queued/Running/Completed/Failed) und Ergebnis.
- `[ ]` **4c** Event-Verteilung (`IDeviceEventStream`/Observer), an die die
  Test-UI andockt (Live-Status statt reiner Rückgabewerte).

**DoD:** Ein Druck läuft als Job mit beobachtbaren Statusübergängen; die UI
reagiert auf Events statt nur auf den Methodenrückgabewert.

---

## Phase 5 — Template-/Medium-Matching

Anforderungen (Rolle + Medium + DPI) treffen auf Gerätefähigkeiten.

- `[ ]` **5a** `MediaRequirement` (Größe, DPI) + `DeviceRequirement`
  (benötigte Rolle + Medium) als Eingabe an die SDK.
- `[ ]` **5b** Matching: Gerät nach Rolle **und** passender Mediengeometrie
  auswählen; verständliche Fehlermeldung, wenn nichts passt.

**DoD:** Eine Anforderung „Rolle X, 100×150mm, 203 DPI" wählt automatisch ein
geeignetes Gerät oder meldet nachvollziehbar, warum keins passt.

---

## Phase 6 — Discovery

Erst jetzt: Geräte automatisch finden statt nur manuell eintragen.

- `[ ]` **6a** `IDeviceDiscovery`-Abstraktion + manuelle „Discovery" (bestehende
  Profile) als erste Implementierung.
- `[ ]` **6b** Netzwerk-Discovery (mDNS/SSDP) für Netzwerkdrucker; USB/BT als Stub.
- `[ ]` **6c** UI: gefundene Geräte übernehmen/registrieren.

**DoD:** Mindestens Netzwerkdrucker werden automatisch vorgeschlagen und lassen
sich als Gerät übernehmen; manuelle Registrierung bleibt möglich.

---

## Phase 7 — Client-Agent & Remote (Server-Andockung)

Die vorbereiteten Remote-Contracts (Phase 1e) bekommen eine reale Anbindung.
Server-seitiges Backend bleibt außerhalb dieses Repos; hier nur die Client-Seite.

- `[ ]` **7a** Client-Agent-Rolle: ein Client bietet seine lokalen Geräte als
  Remote-Geräte an (`IPrintProviderHost`-Implementierung, Transport offen).
- `[ ]` **7b** Anfragender Client konsumiert Remote-Geräte (`IRemotePrintClient`),
  Remote wird als `ConnectionMode` in der UI wählbar.
- `[ ]` **7c** Konkreter Transport (voraussichtlich SignalR) — separater Schritt,
  sobald ein Server existiert.

**DoD:** Ein lokal angeschlossenes Gerät eines Clients ist über den Server für
einen anderen Client als Gerät nutzbar (sobald das Backend bereitsteht).

---

## Phase 8 — Zweiter Gerätetyp (Scanner) als Beweis

Validiert, dass die Abstraktion nicht druckerspezifisch ist.

- `[ ]` **8a** `ScannerDevice` + `ScanCapability`, Event `BarcodeScanned`.
- `[ ]` **8b** Test-UI: Scan starten, Barcode-Event anzeigen — **ohne** jede
  Geschäftslogik (kein „Wareneingang buchen").

**DoD:** Ein zweiter Gerätetyp läuft über denselben Device/Capability/Event-Kern
wie der Drucker, ohne Sonderweg.

---

## Ausblick (nicht Teil dieser Roadmap)

Auth-, Perspective-, Data- und Action-SDK sowie jede konkrete Fulfillment- oder
Lager-Anwendung. Sie setzen später auf die dann stabile Device SDK auf. Bis
dahin gilt: Device SDK klein, hardwarenah und geschäftslogikfrei halten.

---

## Arbeitsweise — zwei getrennte Rollen

Diese Roadmap wird **nicht** dort umgesetzt, wo sie geschrieben wird. Es gibt zwei
getrennte Sessions:

- **Planungs-Session (Web):** kann **nicht** bauen oder testen. Ihre Aufgabe ist,
  pro Phase einen **self-contained Arbeitsauftrag-Prompt** zu schreiben — im Stil
  von `PRINTER_ARCHITECTURE_PLAN.md` (vollständiger Kontext, Zieltypen, Reihenfolge
  mit Build-Checks, Verifikation). Kein Code, nur die ausführbare Spezifikation.
- **Umsetzungs-Session (Claude Code in VS Code):** bekommt genau diesen Prompt,
  **schreibt den Code, baut (`dotnet build`) und testet** ihn lokal, committet und
  pusht. Hier passieren die „bauen/verifizieren"-Schritte aus den DoD.

**Ablauf pro Phase:**
1. Planungs-Session: `PROJECT.md` + aktuelle Phase abgleichen, die **eine** nächste
   offene Aufgabe wählen (nicht mehrere Phasen mischen).
2. Planungs-Session: dafür einen Arbeitsauftrag-Prompt schreiben (eigene Datei,
   z.B. `prompts/phase-1a-....md`) und committen.
3. Umsetzungs-Session (VS Code): Prompt ausführen — Code, Build, Test, Commit, Push.
4. Danach Status hier von `[ ]`/`[~]` auf `[x]` setzen und bei Bedarf den
   `PROJECT.md`-Snapshot (Abschnitt 10) aktualisieren.
