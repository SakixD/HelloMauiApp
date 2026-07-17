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

## Phase 1 — Mehrfach-Drucker & Transport-Abstraktion (NÄCHSTE SESSIONS)

Der bereits spezifizierte Umbau. Detailspec: `PRINTER_ARCHITECTURE_PLAN.md`.
Dies ist die Keimzelle des Device-Modells (Transport = das spätere „Wie").

- `[ ]` **1a** Modelle + Persistenz: `PrinterConnectionMode`, `PrinterTransportKind`,
  `PrinterProfile` (Liste statt Einzeldrucker), `IPrinterProfileStore` inkl.
  Einmal-Migration der Legacy-Preferences. Additiv, App bleibt lauffähig.
- `[ ]` **1b** Transport-Abstraktion: `IPrinterTransport`, `TcpPrinterTransport`
  (1:1-Refactor der Socket-Logik, identisches Verhalten), USB/BT-Stubs,
  `PrinterTransportFactory`. Additiv.
- `[ ]` **1c** Fassade umstellen: `IPrinterService` auf `PrinterProfile`-Signaturen,
  neuer `PrinterService`, `ZplPrinterService` entfernen. Breaking — zusammen mit 1d.
- `[ ]` **1d** UI: `PrinterProfilesPage` + `PrinterProfileEditPage`, `MainPage`
  mit Drucker-Picker; alte `PrinterSettingsPage` entfernen.
- `[ ]` **1e** Remote-Contracts (nur Interfaces): `PrintJobRequest/Result`,
  `IRemotePrintClient`, `IPrintProviderHost`. Kein SignalR, keine Implementierung.

**DoD:** Mehrere Druckerprofile anleg-/wählbar, Verhaltensgleichheit des TCP-Drucks
verifiziert, Migration greift genau einmal, USB/BT/Remote zeigen sauberen Fehler.

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

## Arbeitsweise pro Session

1. In `PROJECT.md` das Zielbild und die aktuelle Phase abgleichen.
2. Die **eine** nächste offene Aufgabe aus dieser Roadmap nehmen (nicht mehrere Phasen mischen).
3. Umsetzen, bauen, verifizieren; Status hier von `[ ]`/`[~]` auf `[x]` setzen.
4. Commit mit klarer Nachricht; bei Bedarf `PROJECT.md`-Snapshot (Abschnitt 10) aktualisieren.
