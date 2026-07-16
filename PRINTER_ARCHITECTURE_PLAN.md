# Auftrag: Mehrfach-Drucker-Architektur für LabelPrinting/HelloMauiApp

Dieses Dokument ist ein vollständiger, eigenständiger Arbeitsauftrag für eine Coding-Session
(z.B. Claude Code in VS Code). Es setzt kein Wissen aus einer vorherigen Konversation voraus —
alles Nötige steht hier drin.

## Erlaubnisse für diese Session

Du darfst in diesem Repository **Terminal-/Bash-Befehle eigenständig ausführen**, ohne für
jeden einzelnen Befehl eine Bestätigung einzuholen — insbesondere:

- `dotnet build`, `dotnet restore`, `dotnet test`, `dotnet run` (auch mehrfach, zur Fehlersuche)
- `git status`, `git diff`, `git add`, `git commit` auf dem aktuellen Feature-Branch
- Beliebige Lese-Befehle (`ls`, `find`, Datei-Suche etc.)

**Nicht ohne Rückfrage:** destruktive/irreversible Git-Operationen (`git push --force`,
`git reset --hard`, `git clean -f`, Branches löschen), `git push` auf `main`/`master`, sowie
das Ändern/Löschen von Dateien außerhalb dieses Vorhabens. Bei normalem `git push` auf den
aktuellen Feature-Branch: kurz Bescheid geben, was gepusht wird, dann ausführen.

Baue und teste iterativ nach jedem der Schritte unten (`dotnet build` mindestens für das
Android-Target, das läuft plattformunabhängig) — nicht erst ganz am Ende.

## Kontext

Die `LabelPrinting`-Bibliothek und `HelloMauiApp` (.NET MAUI, net10.0) funktionieren aktuell
mit **genau einem** global konfigurierten Netzwerkdrucker (ZPL über TCP/Port 9100, RAW-Socket-
Druck). Ziel dieses Umbaus: die Grundlage für eine größere Software legen — mehrere Drucker
gleichzeitig verwaltbar, unterschiedliche Anbindungsarten (Netzwerk jetzt, USB/Bluetooth später),
und die Möglichkeit, dass ein Client mit lokal angeschlossenem Drucker diesen später **über
einen zentralen Server** anderen Clients als Druckdienst zur Verfügung stellt (kein
Peer-zu-Peer-Netzwerkprotokoll zwischen Clients).

Abgestimmte Leitplanken für diesen Schritt:

- Alle Endanwendungen sind MAUI-Apps — keine Notwendigkeit, die Bibliothek von MAUI zu lösen.
- Mehrere Drucker-Profile jetzt architektonisch vorbereiten (Liste statt Einzeldrucker).
- USB/Bluetooth: nur Architektur-Skelett (klar erkennbare "nicht implementiert"-Stubs), keine
  echte Plattform-Anbindung.
- Server für Remote-Druckdienste existiert noch nicht und ist **nicht Teil dieses Auftrags** —
  nur clientseitige Contracts/Schnittstellen vorbereiten, damit ein späteres SignalR-Backend
  sauber andocken kann. Keine SignalR-Package-Referenz, keine Netzwerk-Implementierung dafür.
  Es soll ausschließlich der Client/die Bibliothek verändert werden.
- Bestehende, funktionierende ZPL/TCP-Druckfunktionalität darf sich im **Verhalten nicht
  ändern** — nur die Aufruf-Signaturen ändern sich.

## Aktueller Stand (vor diesem Umbau)

- `LabelPrinting/Services/IPrinterService.cs` — Interface mit `TestConnectionAsync`,
  `SendRawAsync`, `SendZplAsync`, `QueryAsync`, alle mit expliziten `(string ipAddress, int port, ...)`-Parametern.
- `LabelPrinting/Services/ZplPrinterService.cs` — einzige Implementierung, TCP-Socket-Logik
  (`System.Net.Sockets.TcpClient`), Timeouts: `DefaultTimeout`=5s, `InitialResponseTimeout`=3s,
  `FollowupResponseTimeout`=400ms; `SendZplAsync` patcht `^XA` → `^XA^CI28` (UTF-8-Encoding) vor
  dem Senden; `QueryAsync` liest die Antwort in einer Read-Loop mit Latin1-Decoding.
- `LabelPrinting/Services/ZplLabelBuilder.cs`, `ZplImageConverter.cs`, `ZplGraphic.cs`,
  `PrinterResult.cs`, `PrinterQueryResult.cs` — reine Logik, keine Transport-/MAUI-Abhängigkeit,
  **bleiben unverändert**.
- `LabelPrinting/Models/PrinterSettings.cs` — Einzeldrucker-Konfiguration (IP, Port, Labelgröße,
  DPI), persistiert über MAUI `Preferences` (Keys: `printer_ip`, `printer_port`,
  `printer_label_width_mm`, `printer_label_height_mm`, `printer_dpi`).
- `HelloMauiApp/PrinterSettingsPage.xaml(.cs)` — Formular für den einen Drucker.
- `HelloMauiApp/MainPage.xaml(.cs)` — Testverbindung, Testlabel-Druck, Bilddruck, freier
  ZPL-Editor (senden / senden+Antwort lesen), Status abfragen (`~HS`), Medium kalibrieren
  (`~JC`) — alles ruft `_printerService` (eine `ZplPrinterService`-Instanz) mit
  `settings.IpAddress`/`settings.Port` auf.
- Keine Testprojekte, kein Backend/Server-Projekt, keine USB/Bluetooth/Discovery-Vorarbeiten
  im Repo vorhanden.

## Zielarchitektur

### 1. Neue/geänderte Typen in `LabelPrinting`

**Modelle (`LabelPrinting/Models/`)**
- `PrinterConnectionMode` (enum: `Local`, `Remote`)
- `PrinterTransportKind` (enum: `Tcp`, `Usb`, `Bluetooth`)
- `PrinterProfile` (neu, ersetzt `PrinterSettings` als Laufzeit-Datenform): `Id` (Guid), `Name`,
  `IsDefault`, `ConnectionMode`, `TransportKind`, `IpAddress`/`Port` (Tcp), `UsbDeviceId`/
  `BluetoothAddress` (Platzhalter für Usb/Bluetooth), `RemotePrinterId`/`RemoteProviderName`
  (Platzhalter für Remote), `LabelWidthMm`/`LabelHeightMm`/`Dpi` (jetzt pro Profil statt global)

**Persistenz (`LabelPrinting/Services/`)**
- `IPrinterProfileStore` / `PrinterProfileStore`: verwaltet eine **Liste** von `PrinterProfile`
  als ein JSON-Blob in `Preferences` (System.Text.Json, keine neue Dependency nötig). Methoden:
  `GetAll()`, `GetDefault()`, `GetById(Guid)`, `Save(PrinterProfile)` (insert or update by Id),
  `Delete(Guid)`, `SetDefault(Guid)` (erzwingt genau ein Default-Profil).

**Transport-Abstraktion (`LabelPrinting/Services/Transport/`)**
- `IPrinterTransport`: `PrinterTransportKind Kind { get; }`, `TestConnectionAsync(PrinterProfile, CancellationToken)`,
  `SendRawAsync(PrinterProfile, byte[], CancellationToken)`, `QueryAsync(PrinterProfile, string, CancellationToken)`
  — nimmt jetzt ein `PrinterProfile` statt einzelner ip/port-Parameter.
- `TcpPrinterTransport`: 1:1-Refactor der bestehenden Socket-Logik aus `ZplPrinterService.cs`
  (gleiche Timeouts, gleiches Latin1-Decoding im Read-Loop, gleiche
  `SocketException`/`OperationCanceledException`-Behandlung) — **Verhalten muss identisch
  bleiben**, nur `profile.IpAddress`/`profile.Port` statt direkter Parameter.
- `PrinterTransportNotImplementedException(PrinterTransportKind kind)` — eigene Exception-Klasse.
- `UsbPrinterTransport` / `BluetoothPrinterTransport`: Stubs, jede Methode wirft
  `PrinterTransportNotImplementedException(Kind)` — bewusst kein `#if ANDROID`-Code, nur die
  Schnittstelle.
- `IPrinterTransportFactory` / `PrinterTransportFactory`: liefert die passende
  Transport-Instanz je `PrinterTransportKind` (einfacher `switch`, gecachte Singletons reichen).

**Fassade — neue öffentliche API**
- `IPrinterService` (Signaturen geändert): `TestConnectionAsync(PrinterProfile, CancellationToken = default)`,
  `SendRawAsync(PrinterProfile, byte[], CancellationToken = default)`,
  `SendZplAsync(PrinterProfile, string, CancellationToken = default)`,
  `QueryAsync(PrinterProfile, string, CancellationToken = default)`.
- `PrinterService` (neu, **ersetzt** `ZplPrinterService.cs`, welches gelöscht wird): behält die
  `^XA`→`^XA^CI28`-UTF8-Patch-Logik (ZPL-Sprachebene, gehört nicht in den Transport). Verzweigt
  bei `ConnectionMode.Remote` auf einen optionalen `IRemotePrintClient?` (Konstruktor-Parameter,
  Default `null` → liefert `PrinterResult.Fail("Remote-Druck ist in dieser Version noch nicht
  verfügbar.")`), sonst auf `IPrinterTransportFactory.Resolve(profile.TransportKind)`. Fängt
  `PrinterTransportNotImplementedException` ab und wandelt sie in ein reguläres `Fail`-Ergebnis
  um — die UI muss weiterhin **keine** Exceptions behandeln, exakt wie bisher.

**Remote-Contracts — nur Schnittstellen, keine Implementierung (`LabelPrinting/Remote/`)**
- `PrintJobRequest` (Id, RemotePrinterId, PayloadKind [Zpl/Raw], Payload-Bytes, Zeitstempel)
- `PrintJobResult` (JobId, Success, ErrorMessage, CompletedAtUtc)
- `PrintProviderRegistration` / `PrintProviderPrinterInfo` ("was bietet dieser Client an" —
  ClientId, DisplayName, Liste angebotener Drucker mit RemotePrinterId/Name/TransportKind/
  Labelgeometrie)
- `IRemotePrintClient` (Sicht des anfragenden Clients: `SubmitJobAsync`, `TestRemotePrinterAsync`)
- `IPrintProviderHost` (Sicht des druckbereitstellenden Clients: `RegisterAsync`,
  `UnregisterAsync`, `event Func<PrintJobRequest, Task<PrintJobResult>>? JobReceived`)
- Bewusst **keine** SignalR-Package-Referenz, keine Netzwerk-Implementierung — nur die Verträge,
  an die später ein SignalR-Client andockt.

### 2. Änderungen in `HelloMauiApp`

- `PrinterSettingsPage` wird ersetzt durch:
  - **`PrinterProfilesPage`** (Liste aller Profile via `CollectionView`: Name, Transport-Badge,
    Verbindungs-Zusammenfassung — `IP:Port` bei Tcp, "USB"/"Bluetooth" bei den Stubs; Zeilen-
    aktionen Löschen/Als-Standard-setzen; Toolbar "+" → neues Profil anlegen)
  - **`PrinterProfileEditPage`** (Formular wie bisher — Name, Transport-Picker, IP/Port/Breite/
    Höhe/DPI-Felder; Usb/Bluetooth im Picker wählbar, aber zugehörige Felder deaktiviert mit
    "noch nicht unterstützt"-Hinweis; `Remote` ist noch **keine** wählbare Option, da es keinen
    Server gibt, von dem man Remote-Drucker beziehen könnte; Konstruktor nimmt `Guid?
    profileId`, lädt bestehendes Profil oder startet leer; Save/Delete/Als-Standard-setzen-Buttons)
- `MainPage.xaml`: neuer `Picker x:Name="PrinterPicker"` zur Druckerauswahl, befüllt aus
  `IPrinterProfileStore.GetAll()`, Vorauswahl = Default-Profil.
- `MainPage.xaml.cs`:
  - `_printerService` wird `new PrinterService(new PrinterTransportFactory())`.
  - Neues Feld `IPrinterProfileStore _profileStore = new PrinterProfileStore();` und
    `PrinterProfile? _selectedProfile`.
  - `OnAppearing`/`UpdatePrinterStatusLabel` laden `_profileStore.GetAll()` neu, befüllen den
    Picker, lösen `_selectedProfile` neu auf (vorherige Auswahl falls noch vorhanden, sonst
    Default, sonst `null`).
  - `RequirePrinterConfigured` wird zu `_selectedProfile is not null` (eine reine
    IP-Leer-Prüfung ergibt bei Usb/Bluetooth/Remote-Profilen keinen Sinn mehr).
  - Alle bisherigen `_printerService.XxxAsync(settings.IpAddress, settings.Port, ...)`-Aufrufe
    werden zu `_printerService.XxxAsync(_selectedProfile, ...)`.
  - `OnSettingsClicked` navigiert zu `PrinterProfilesPage` statt `PrinterSettingsPage`.
- `HelloMauiApp/PrinterSettingsPage.xaml(.cs)` — wird gelöscht.

DI-Hinweis: Die App nutzt aktuell nirgends `MauiProgram.Services` für diese Typen — alles wird
per `new` in Seiten-Konstruktoren erzeugt. Dieses Muster beibehalten (Seiten instanziieren
`PrinterProfileStore`/`PrinterService`/`PrinterTransportFactory` weiterhin direkt per `new`),
um den Umbau klein zu halten. Eine spätere Umstellung auf DI ist möglich, aber **nicht** Teil
dieses Auftrags.

### 3. Migration (Bestandsschutz)

Einmalig in `PrinterProfileStore`, beim ersten Zugriff:

1. Prüfen, ob `Preferences.Default.Get("printer_profiles_migrated_v1", false)` bereits `true` ist.
2. Falls nicht: bestehende `PrinterSettings.Load()`-Werte lesen (unveränderte, alte Klasse).
   Falls `IpAddress` nicht leer ist: **ein** `PrinterProfile` bauen (`Name = "Standarddrucker"`,
   `ConnectionMode = Local`, `TransportKind = Tcp`, `IsDefault = true`, übrige Felder 1:1 aus
   den Legacy-Werten übernommen), an die neue Liste anhängen und speichern. Falls die Legacy-IP
   leer ist (Neuinstallation): nichts migrieren, Liste bleibt leer.
3. Migrations-Flag **unbedingt** auf `true` setzen, auch wenn nichts migriert wurde — sonst
   würde ein später gelöschtes (oder nie erstelltes) Profil beim nächsten Start wieder aus den
   Legacy-Keys neu entstehen, weil `GetAll()` dann wieder leer aussähe.
4. Alte Preferences-Keys **nicht** löschen (Sicherheitsnetz).
5. `PrinterSettings.cs` bleibt inhaltlich unverändert, nur mit `[Obsolete("Ersetzt durch
   PrinterProfile/IPrinterProfileStore; nur noch für die einmalige Migration gelesen.")]`
   markieren.

### 4. Reihenfolge der Umsetzung (bitte in dieser Reihenfolge, jeweils mit Build-Check)

1. **Additiv, ungefährlich:** neue Modelle (`PrinterConnectionMode`, `PrinterTransportKind`,
   `PrinterProfile`) + `IPrinterProfileStore`/`PrinterProfileStore` (inkl. Migration) +
   komplettes `LabelPrinting/Remote/*`. Nichts nutzt sie noch — App bleibt unverändert
   lauffähig. `dotnet build` → muss grün sein.
2. **Additiv, ungefährlich:** `IPrinterTransport` + `TcpPrinterTransport` (Kopie der
   Socket-Logik) + `UsbPrinterTransport`/`BluetoothPrinterTransport`-Stubs +
   `PrinterTransportNotImplementedException` + `IPrinterTransportFactory`/
   `PrinterTransportFactory`. Alte `IPrinterService`/`ZplPrinterService` bleiben weiterhin
   aktiv und unverändert genutzt. `dotnet build` → muss grün sein.
3. **Breaking, zusammen mit Schritt 4:** `IPrinterService`-Signaturen auf `PrinterProfile`
   umstellen, `PrinterService` einführen, `ZplPrinterService.cs` löschen.
4. **Gleicher Schritt wie 3 (Solution muss danach wieder kompilieren):**
   `PrinterProfilesPage`/`PrinterProfileEditPage` hinzufügen, `MainPage.xaml`/`.cs` auf
   Profile+Picker umstellen, `PrinterSettingsPage.xaml(.cs)` löschen. `dotnet build` → muss
   grün sein.
5. Verifikation (siehe unten) durchführen.

Schritt 1–2 können einzeln committet werden. Schritt 3–4 **nicht trennen** — dazwischen
kompiliert die App nicht.

### 5. Explizit ausgeklammert (nicht umsetzen)

- Echte USB-Implementierung je Plattform (Android/iOS/Windows).
- Echte Bluetooth-Implementierung je Plattform.
- Der Server/Backend selbst (SignalR-Hub, Job-Queue, Registry, Auth) — nur die clientseitigen
  Interfaces in `LabelPrinting/Remote` anlegen, nichts davon aufrufen oder implementieren.
- Jede SignalR-Package-Referenz oder konkrete Implementierung von `IRemotePrintClient`/
  `IPrintProviderHost`.
- Netzwerk-Discovery (mDNS/SSDP) für Drucker — Profile weiterhin manuell eingetragen.
- UI zum Durchsuchen fremder Remote-Drucker (braucht den Server).
- Job-Routing-Sicherheit/Auth.
- Umstellung auf `MauiProgram.cs`-DI-Container.

## Kritische Dateien

- `LabelPrinting/Services/ZplPrinterService.cs` — wird zu `Transport/TcpPrinterTransport.cs` +
  `PrinterService.cs` aufgeteilt, danach gelöscht
- `LabelPrinting/Services/IPrinterService.cs` — Signaturänderung
- `LabelPrinting/Models/PrinterSettings.cs` — bleibt als Migrationsquelle, wird `[Obsolete]`
- `HelloMauiApp/MainPage.xaml.cs` / `MainPage.xaml` — Umstellung auf Profile + Picker
- `HelloMauiApp/PrinterSettingsPage.xaml(.cs)` — gelöscht, ersetzt durch
  `PrinterProfilesPage`/`PrinterProfileEditPage`

## Verifikation

1. **Verhaltensgleichheit TCP:** Testverbindung, Testlabel-Druck, Bilddruck, freies ZPL senden,
   Status/Kalibrierung — alle bestehenden Buttons in `MainPage` müssen nach dem Refactor
   identisch funktionieren (idealerweise mit `nc -l 9100` o.ä. die gesendeten Rohbytes vor/nach
   dem Refactor byte-für-byte vergleichen, insbesondere die `^CI28`-Injektion darf nicht doppelt
   passieren).
2. **Profil-Persistenz:** Profil anlegen/bearbeiten, App neu starten, Profil + Default-Flag
   bleiben erhalten (Preferences-JSON-Roundtrip).
3. **Migration:** alte `printer_ip`/`printer_port`/... -Keys simulieren (z.B. manuell in
   Preferences setzen), erster Start nach der Umstellung muss genau ein Default-Profil mit den
   alten Werten erzeugen und in `MainPage` automatisch vorauswählen; nach Löschen des Profils
   darf es beim nächsten Start **nicht** wieder auftauchen (validiert das Einmal-Migrations-Flag).
4. **Stub-Sicherheit:** Versuch, auf ein Usb/Bluetooth-Profil zu drucken/testen, muss einen
   sauberen Fehlerdialog zeigen ("... noch nicht implementiert ..."), keinen Absturz — bestätigt,
   dass `PrinterService` die `PrinterTransportNotImplementedException` korrekt abfängt.
5. `dotnet build` für mindestens das Android-Target muss nach jedem Schritt grün sein.
