# BACKLOG — HelloMauiApp / LabelPrinting

> Erstellt am 2026-07-17 nach vollständigem Durchgang von Doku und Quellcode
> (Auftrag: `PROJEKT_AUDIT_PROMPT.md`). Arbeitsmodus laut Abstimmung:
> **Aufräumen zuerst, dann Bugfixes, dann Features; jeder Punkt einzeln mit
> Freigabe; direkt auf `master`; kein Push ohne Freigabe.**
>
> Statuslegende: `[ ]` offen · `[~]` in Arbeit · `[x]` erledigt

## Kontext (Beleg Schritt 1)

Das Projekt baut eine **Device SDK** (Abstraktionsschicht App ↔ Hardware); die
MAUI-App ist nur Testkonsole, `LabelPrinting` ist der erste Baustein mit
funktionierendem ZPL/TCP-Druck. Stand: Der Mehrfach-Drucker-Umbau
(ROADMAP-Phase 1 / `PRINTER_ARCHITECTURE_PLAN.md`) ist auf `master` bereits
umgesetzt (Profile, Migration, ConnectionFactory, USB/BT-Stubs,
Remote-Contracts, Profilverwaltungs-UI, API-Erweiterung, 65 Tests grün).

**Festgehaltene Doku-Widersprüche (nicht aufgelöst, siehe CLEAN-01):**

- `ROADMAP.md:43-57` führt Phase 1a–1e als `[ ]` offen, obwohl `master` sie
  umgesetzt hat (Commits `439419b`, `ebc5121`, `a974730`; `DEV_BERICHT_2026-07-17.md`).
- `PROJECT.md:168-176` (Snapshot §10) beschreibt den Einzeldrucker-Stand als
  aktuell („Persistenz kennt genau einen Drucker", Mehrfach-Drucker als
  „geplant, noch nicht im Code").
- `LabelPrinting/README.md:22` nennt `PrinterSettings` + `PrinterSettingsStore`
  als Druckerkonfiguration; `PrinterProfile`/`PrinterProfileStore`/
  `PrinterConnectionFactory`/`Remote/*` fehlen in der README komplett.
- `ROADMAP.md:49-52` (Phase 1c/1d: `ZplPrinterService` löschen, separate
  Edit-Page) weicht von der realen Umsetzung ab — die Abweichungen sind in der
  Abgleich-Notiz von `PRINTER_ARCHITECTURE_PLAN.md:7-23` dokumentiert und
  begründet (kein Fehler, nur ROADMAP-Text veraltet).

---

## C) Aufräumen / Refactoring (Fokus laut Abstimmung — zuerst)

### CLEAN-01 — Doku an den Ist-Stand angleichen
- **Fundstelle:** `ROADMAP.md:43-57`, `PROJECT.md:150-176`, `LabelPrinting/README.md:14-24`
- **Beschreibung:** ROADMAP Phase 1 auf `[x]` setzen (mit kurzer Notiz zu den
  dokumentierten Abweichungen), PROJECT.md-Snapshot §10 auf den
  Mehrfach-Drucker-Stand aktualisieren, README um Profile/Factory/Remote ergänzen.
- **Warum:** Die Doku ist laut PROJECT.md „Einstiegspunkt für jede Session" —
  aktuell führt sie neue Sessions in die Irre (Phase 1 würde doppelt geplant).
- **Lösungsskizze:** Reine Doku-Änderung, kein Code. Statusliste + Snapshot +
  README-Tabelle nachziehen.
- **Priorität:** Hoch · **Aufwand/Risiko:** S · **Status:** `[x]`
- **Ergebnis (2026-07-18):** `ROADMAP.md` Phase 1 auf „ERLEDIGT" gesetzt, 1a–1e
  `[x]` mit den realen Naming-/Umsetzungs-Abweichungen (Transport über
  `IPrinterConnectionFactory` statt `IPrinterTransport`; `ZplPrinterService`
  umgestellt statt gelöscht; keine separate Edit-Page) und Verweis auf die
  Abgleich-Notiz. `PROJECT.md`-Snapshot §10 um den Phase-1-Ist-Stand
  (Profile/Migration/Factory/Stubs/Remote-Contracts) erweitert, veralteten
  „geplant"-Block entfernt. `LabelPrinting/README.md`: Transport-/Profil-/
  Remote-Absätze ergänzt, Bausteine-Tabelle um Profile/Factory/Stubs/Remote
  erweitert, `PrinterSettings` als Legacy markiert, veraltetes IP/Port-
  Kurzbeispiel auf den `PrinterProfile`-Weg umgestellt. Kein Code, kein
  Verhaltensänderung.

### CLEAN-02 — Altbestand-Seiten auf DI umstellen (kein `new`-Service-Muster mehr)
- **Fundstelle:** `HelloMauiApp/TemplateTestPage.xaml.cs:8-10`,
  `HelloMauiApp/MediaManagerPage.xaml.cs:14-16`,
  `HelloMauiApp/TemplatePropertiesPage.xaml.cs:14`,
  `HelloMauiApp/PrinterDeviceSettingsPage.xaml.cs:14-15`
- **Beschreibung:** Vier Drill-down-Seiten erzeugen `ZplPrinterService`/
  `PrinterProfileStore`/`PrintMediaStore`/`LabelTemplateStore` direkt per `new`,
  während der Rest der App die DI-Singletons aus `MauiProgram.cs:29-33` nutzt.
  Der statische Lock im Store (`PrinterProfileStore.cs:21-23`) existiert nur
  wegen dieser Mehrfach-Instanzen.
- **Warum:** Zwei parallele Bezugswege für dieselben Services sind fehleranfällig
  (Zustand, Austauschbarkeit, Tests) und im Store-Kommentar bereits als Altlast markiert.
- **Lösungsskizze:** Services per Konstruktor durchreichen — Muster existiert
  schon: `AppearanceSettingsViewModel.cs:71-76` reicht DI-Services an
  `PrinterProfilesPage` durch. Aufrufer (`DesignerViewModel:469-471`,
  `AppShell:147-148`, `MainPageViewModel`) entsprechend anpassen.
  Verhalten unverändert.
- **Priorität:** Hoch · **Aufwand/Risiko:** M · **Status:** `[x]`
- **Ergebnis (2026-07-18):** Alle vier Seiten beziehen die DI-Singletons jetzt
  per Konstruktor als Interfaces (`ILabelTemplateStore`/`IPrintMediaStore`/
  `IPrinterProfileStore`/`IPrinterService`); kein `new`-Service-Muster mehr in
  der App (verifiziert per Suche). Aufrufer angepasst: `DesignerViewModel`
  (+`IPrintMediaStore`-Abhängigkeit, reicht an drei Seiten durch), `AppShell`
  (+3 Services für den templatetest-Drill-down), `AppearanceSettingsViewModel`
  (reicht vorhandene Services weiter). `MainPageViewModel` brauchte keine
  Änderung (baut keine Seiten). Statischer Lock im `PrinterProfileStore`
  bewusst noch drin — Rückstufung auf Instanz-Lock als Folgeschritt. Build 0
  Fehler · 65/65 Tests grün · App-Start + Drill-downs vom Nutzer bestätigt.

### CLEAN-03 — Duplizierte XAML-Styles zentralisieren
- **Fundstelle:** `DesignerPage.xaml:8-58`, `PrinterProfilesPage.xaml:11-47`,
  `MediaLibraryPage.xaml:9-45`, `PlaceholderLibraryPage.xaml:9-27`
- **Beschreibung:** Die Styles `Card`, `ChipButton`, `AccentChipButton`,
  `DangerButton`, `FieldLabel` sind in vier Dateien nahezu identisch kopiert.
- **Warum:** Jede Design-Änderung muss aktuell vierfach gepflegt werden;
  Abweichungsgefahr.
- **Lösungsskizze:** Styles einmal in `Resources/Styles/Styles.xaml` definieren,
  lokale Kopien entfernen. Rein additiv/ersetzend, Optik unverändert.
- **Priorität:** Mittel · **Aufwand/Risiko:** S · **Status:** `[x]`
- **Ergebnis (2026-07-18):** Sieben App-Styles (`Card`, `ChipButton`,
  `AccentChipButton`, `DangerButton`, `SectionTitle`, `FieldLabel`,
  `ToolbarSeparator`) zentral in `Resources/Styles/Styles.xaml`; lokale Kopien
  aus allen vier Seiten entfernt. Die Kopien waren byte-identisch — Optik
  unverändert. Build 0 Fehler.
- **Wichtige Lektion (Nachtrag):** App-weite Styles müssen von den Seiten per
  `DynamicResource` referenziert werden, **nicht** per `StaticResource` — die
  Rail-Sektionen werden beim Start per DI konstruiert, bevor sie im Visual
  Tree hängen; `StaticResource` findet die App-Ressourcen dann nicht und die
  App crasht beim Start (stowed exception 0xc000027b, `XamlParseException:
  StaticResource not found`). Dazu Crash-Logger in
  `Platforms/Windows/App.xaml.cs` ergänzt (`%TEMP%\hellomaui_crash.txt`).
  Fix-Commit `cb39e1e`; App-Start danach stabil, vom Nutzer bestätigt (2026-07-18).

### CLEAN-04 — Doppelte Medienerkennungs-Logik zusammenführen
- **Fundstelle:** `ViewModels/MediaLibraryViewModel.cs:175-216` vs.
  `MediaManagerPage.xaml.cs:177-217` (dazu `SensorLabel` doppelt:
  `MediaLibraryViewModel.cs:87-92` / `MediaManagerPage.xaml.cs:79-84`)
- **Beschreibung:** „Medium vom Drucker erkennen" (Status abfragen → Teile-Liste
  → Editor mit erkannter Länge vorbefüllen) existiert zweimal fast identisch.
- **Warum:** Klassische Copy-Paste-Drift — der Dashboard-Bug aus dem Dev-Bericht
  entstand aus genau solchem doppelten Zustand.
- **Lösungsskizze:** Gemeinsamen Helfer extrahieren (z.B. statische Methode, die
  aus `PrinterStatus` + `Dpi` den Anzeigetext und das vorbefüllte `PrintMedia`
  liefert); beide Stellen darauf umstellen. Bietet sich nach CLEAN-02 an.
- **Priorität:** Mittel · **Aufwand/Risiko:** S · **Status:** `[ ]`

### CLEAN-05 — Toter Doku-Verweis auf gelöschte `PrinterSettingsPage`
- **Fundstelle:** `PrinterDeviceSettingsPage.xaml.cs:8`
- **Beschreibung:** Der XML-Doc-Kommentar verweist per `<see cref>` auf die in
  Phase 3+4 gelöschte `PrinterSettingsPage`.
- **Warum:** Irreführend für Leser; verweist auf nicht existierenden Typ.
- **Lösungsskizze:** Kommentar auf `PrinterProfilesPage` umformulieren.
- **Priorität:** Niedrig · **Aufwand/Risiko:** S · **Status:** `[x]`
- **Ergebnis (2026-07-18):** Kommentar auf `PrinterProfilesPage` umformuliert
  (im Zuge der FEAT-03-Modernisierung von `PrinterDeviceSettingsPage`).

### CLEAN-06 — Platzhalter-Statistik lädt alle Vorlagen vollständig
- **Fundstelle:** `ViewModels/MainPageViewModel.cs:84-90`
- **Beschreibung:** Für die Zahl „Platzhalter" auf dem Dashboard wird bei jedem
  Aktivieren der Startseite jede Vorlagendatei komplett deserialisiert
  (inkl. eingebetteter Base64-Bilder).
- **Warum:** Unnötige I/O-Last bei jedem Rail-Wechsel; wächst linear mit der
  Vorlagenzahl und Bildgröße.
- **Lösungsskizze:** Entweder Zählung cachen/bei Bedarf laden oder akzeptieren
  und dokumentieren — erst relevant bei vielen Vorlagen. Bewusst niedrig priorisiert.
- **Priorität:** Niedrig · **Aufwand/Risiko:** S · **Status:** `[ ]`

---

## B) Bugfix-Liste

### BUG-01 — Ungeprüfter Dashboard-Fix aus letzter Sitzung verifizieren
- **Fundstelle:** `DEV_BERICHT_2026-07-17.md:48-50`, Commit `b31ae2f`
  (`MainPage.xaml.cs:23`, `ViewModels/MainPageViewModel.cs:124-136`)
- **Beschreibung:** Der Fix (MainPage implementiert `IShellSectionView`,
  Store-Fallback in `RequireProfileAsync`) wurde auf Nutzerwunsch ohne
  Build-/Laufzeittest committet.
- **Warum:** Offener Verifikationspunkt aus dem Dev-Bericht; muss vor weiteren
  Änderungen am Dashboard bestätigt sein.
- **Lösungsskizze:** `dotnet build` + App starten, Dashboard → „Verbindung
  testen"/„Kalibrieren" mit vorhandenem Profil prüfen.
- **Priorität:** Hoch · **Aufwand/Risiko:** S · **Status:** `[x]`
- **Ergebnis (2026-07-17):** Build 0 Fehler · 65/65 Tests grün · App-Start stabil,
  API nach 1 s erreichbar · `printer.status` end-to-end gegen 192.168.1.251 sauber
  geparst · UI-Klick Dashboard → „Verbindung testen" vom Nutzer mit
  „Verbindung OK" bestätigt. Keine Code-Änderung nötig.

### BUG-02 — „Speichern unter neuem Namen" hinterlässt Alt-Datei mit derselben Id
- **Fundstelle:** `ViewModels/DesignerViewModel.cs:486-496`,
  `LabelPrinting/Services/LabelTemplateStore.cs:80-84`
- **Beschreibung:** Der Speichern-Dialog erlaubt einen neuen Namen; der Store
  speichert dateibasiert nach Name und löscht die alte Datei nicht. Ein
  „Umbenennen" erzeugt so zwei Vorlagen-Dateien mit **derselben** `Id` —
  obwohl die Id laut `LabelTemplate.cs:6-12` eine stabile, eindeutige Kennung
  sein soll (auch die API `templates.list` zeigt dann zwei Einträge mit gleicher Id).
- **Warum:** Verletzt die Id-Eindeutigkeit, auf der Vorlagen-Referenzen (API,
  künftige Automatisierung) aufbauen; Nutzer bekommen unbeabsichtigte Duplikate.
- **Lösungsskizze:** Beim Speichern mit geändertem Namen die alte Datei
  entfernen (echtes Umbenennen), alternativ explizit „Speichern unter" mit
  neuer Id anbieten. Verhalten mit Tests absichern.
- **Priorität:** Mittel · **Aufwand/Risiko:** S · **Status:** `[ ]`

### BUG-03 — Profileditor verwirft Daten beim Transportwechsel
- **Fundstelle:** `ViewModels/PrinterProfilesViewModel.cs:229-267`
- **Beschreibung:** `TryReadEditor` baut das Profil neu auf und übernimmt
  `IpAddress`/`Port` nur bei `TransportKind == Tcp`; die übrigen Profilfelder
  (`UsbDeviceId`, `BluetoothAddress`, `RemotePrinterId`, …,
  `PrinterProfile.cs:28-38`) werden nie übernommen. Wer ein TCP-Profil
  versehentlich auf „USB" stellt und speichert, verliert die eingetragene IP.
- **Warum:** Stiller Datenverlust; wird relevanter, sobald USB/BT/Remote echte
  Felder bekommen.
- **Lösungsskizze:** Statt Neuaufbau das bestehende Profil klonen und nur die
  im Formular editierbaren Felder überschreiben.
- **Priorität:** Niedrig · **Aufwand/Risiko:** S · **Status:** `[ ]`

### BUG-04 — Fire-and-forget-Navigation schluckt Fehler lautlos
- **Fundstelle:** `AppShell.xaml.cs:44-46,133-149,151-157`
- **Beschreibung:** Rail-Klicks und Sektions-Wiring starten `NavigateTo` als
  verworfenen Task (`_ = NavigateTo(...)`); Exceptions aus
  `OnActivatedAsync`-Implementierungen (Datei-I/O in Refreshes) verschwinden
  dadurch unbeobachtet, die Sektion wirkt dann einfach „leer/eingefroren".
- **Warum:** Erschwert Diagnose genau der Fehlerklasse, die im Dev-Bericht schon
  einmal unbemerkt blieb (nie aufgerufenes `RefreshAsync`).
- **Lösungsskizze:** In `NavigateTo` ein try/catch mit Log/Alert ergänzen;
  Handler-Signaturen bleiben unverändert.
- **Priorität:** Niedrig · **Aufwand/Risiko:** S · **Status:** `[ ]`

---

## A) Funktionsliste

### FEAT-01 — Druckerprofil: Kommentarfeld + Rolle(n)
- **Fundstelle:** Wunsch aus `DEV_BERICHT_2026-07-17.md:54-56`; Zielbild
  `PROJECT.md:94-107` (Rollenmodell) und Leitplanke `PROJECT.md:199-201`
  (Rolle als strukturierte Kennung `Bereich.Rolle`); Modell `PrinterProfile.cs`
- **Beschreibung:** Profile bekommen ein freies Kommentarfeld und zuweisbare
  Rollen (z.B. `Versand.PaketLabel`, `Produktion.Produktetikett`) — sichtbar in
  Profilverwaltung, Dashboard und `printers.list`.
- **Warum:** Expliziter Nutzerwunsch aus der letzten Sitzung; Vorstufe der
  späteren Server-Vermittlung („welcher Drucker macht was").
- **Lösungsskizze:** Minimal-invasiv am Profil (`Comment`, `List<string> Roles`
  im Format `Bereich.Rolle`), Editor + Listenanzeige + API-Felder ergänzen.
  Bewusst als Vorgriff auf ROADMAP Phase 3a markieren, damit die spätere
  `DeviceRole`-Schicht die Daten übernehmen kann (keine Auflösungslogik jetzt).
- **Priorität:** Hoch · **Aufwand/Risiko:** M · **Status:** `[ ]`

### FEAT-02 — MVVM + Fluent-Design für TemplateManagerPage & ZplConsolePage
- **Fundstelle:** `DEV_BERICHT_2026-07-17.md:57`; alte Optik z.B.
  `TemplateManagerPage.xaml.cs:36-79` (`Colors.Gray`/`LightGray`/`IndianRed`),
  `ZplConsolePage.xaml.cs:7-9` (eigener Hinweis „noch nicht gestylt")
- **Beschreibung:** Die beiden Rail-Sektionen auf das ViewModel-Muster und die
  Token-basierten Styles der übrigen Sektionen umbauen.
- **Warum:** Einheitliches Bedienkonzept/Theme (Dark-Mode-Tauglichkeit) und
  testbare Logik; im Dev-Bericht als nächster regulärer Schritt notiert.
- **Lösungsskizze:** Je Seite ein ViewModel nach Muster
  `MediaLibraryViewModel`; XAML auf `DynamicResource`-Tokens; Verhalten identisch.
- **Priorität:** Mittel · **Aufwand/Risiko:** M · **Status:** `[~]`
- **Zwischenstand (2026-07-18):** **Fluent-Design-Teil erledigt** — beide
  Rail-Sektionen auf Kopfzeile + Cards + zentrale Token-Styles umgestellt
  (TemplateManagerPage inkl. Code-behind-Listenzeilen ohne hartcodierte
  Farben; ZplConsolePage nutzte schon Tokens, jetzt auch Karten/ChipButtons).
  Optik vom Nutzer im Sichttest bestätigt (2026-07-18).
  **Offen: MVVM-Teil** — je Seite ein ViewModel nach Muster
  `MediaLibraryViewModel` (Logik testbar machen), als eigener Folgeschritt.

### FEAT-03 — Drill-down-Seiten modernisieren (Fluent + DI)
- **Fundstelle:** `TemplateTestPage.xaml:13-28`, `MediaManagerPage.xaml:12-47`,
  `PlaceholderManagerPage.xaml:12-46`, `TemplatePropertiesPage.xaml:11-34`,
  `PrinterDeviceSettingsPage.xaml:12-83` (hartcodierte Farben, altes Muster)
- **Beschreibung:** Die fünf gepushten Unterseiten optisch und strukturell an
  das neue Design angleichen; überschneidet sich mit CLEAN-02 (DI).
- **Warum:** Konsistenz; hartcodierte Farben ignorieren Dark Mode/Akzentfarbe.
- **Lösungsskizze:** Nach CLEAN-02 seitenweise Token-Styles anwenden; ggf. mit
  FEAT-02 bündeln, aber je Seite ein eigener Commit.
- **Priorität:** Mittel · **Aufwand/Risiko:** L · **Status:** `[x]`
- **Ergebnis (2026-07-18):** Alle fünf Drill-down-Seiten auf das Token-Design
  umgestellt (je Seite ein Commit): Kopfzeile + Cards nach
  PrinterProfilesPage-Muster, zentrale Styles (`Card`/`ChipButton`/
  `AccentChipButton`/`DangerButton`/`SectionTitle`/`FieldLabel`), dynamisch
  erzeugte Controls über `SetDynamicResource`-Tokens — keine hartcodierten
  Farben mehr (Ausnahme: weiße Vorschaufläche in `TemplateTestPage`, die
  bewusst das physische Etikett darstellt). Nebenbei totes Token `ColorBase`
  in `PrinterProfilesPage` auf `ColorBg` korrigiert. Optik vom Nutzer im
  Sichttest bestätigt (2026-07-18).

### FEAT-04 — ROADMAP Phase 2: Device-SDK-Kern
- **Fundstelle:** `ROADMAP.md:61-77`
- **Beschreibung:** `IDevice`/`DeviceStatus`/`ICapability`/`PrintLabelCapability`,
  `IDeviceRegistry`/`DeviceManager` (aus Profilen gespeist), `PrinterDevice`-
  Adapter, Test-UI zeigt „Geräte".
- **Warum:** Nächste offene Roadmap-Phase; macht den Drucker zum ersten Device
  hinter der allgemeinen Abstraktion.
- **Lösungsskizze:** Laut Arbeitsweise in `ROADMAP.md:178-198` schreibt die
  Planungs-Session dafür einen eigenen Arbeitsauftrag-Prompt — hier nur als
  Platzhalter geführt, nicht in dieser Session umsetzen.
- **Priorität:** Mittel (nach Aufräumen/Bugs) · **Aufwand/Risiko:** L · **Status:** `[ ]`

### FEAT-05 — Echte USB-/Bluetooth-Transporte
- **Fundstelle:** Stubs `UsbPrinterConnection.cs`/`BluetoothPrinterConnection.cs`;
  Einschränkung `LabelPrinting/README.md:45`
- **Beschreibung:** Plattformspezifische Implementierungen hinter
  `IPrinterConnection`.
- **Warum:** Macht die vorbereiteten Profil-Anbindungsarten real nutzbar.
- **Lösungsskizze:** Laut README ohne echte Hardware nicht seriös umsetzbar —
  bleibt zurückgestellt, bis Testgeräte verfügbar sind.
- **Priorität:** Niedrig · **Aufwand/Risiko:** L · **Status:** `[ ]`

### FEAT-06 — Remote-Druck & Discovery (ROADMAP Phasen 6/7)
- **Fundstelle:** `ROADMAP.md:128-153`; Contracts `LabelPrinting/Remote/*`
- **Beschreibung:** Netzwerk-Discovery (mDNS/SSDP) und die
  `IRemotePrintClient`-Implementierung gegen einen späteren Server.
- **Warum:** Fernziel laut Roadmap; Contracts liegen bereit.
- **Lösungsskizze:** Erst nach Phasen 2–5 bzw. sobald ein Server existiert —
  nur als Merkposten geführt.
- **Priorität:** Niedrig · **Aufwand/Risiko:** L · **Status:** `[ ]`
