# Dev-Bericht 2026-07-17 — Mehrfach-Drucker-Architektur

## Was in dieser Sitzung entstanden ist

Umsetzung von `PRINTER_ARCHITECTURE_PLAN.md` (Plandatei stammt vom Branch
`claude/maui-printer-library-mymu6s`; der Branch basiert auf dem Initial-Commit und darf
**nicht gemergt** werden — nur die Datei wurde übernommen, mit Abgleich-Notiz).

- **Druckerprofile statt globalem Einzeldrucker**: `PrinterProfile` (Name, Anbindung,
  Labelgeometrie **pro Profil**), verwaltet über `PrinterProfileStore` (JSON in Preferences).
  Alte Einstellungen (`printer_ip` …) werden einmalig in ein Profil „Standarddrucker"
  migriert; ein Einmal-Flag verhindert, dass gelöschte Profile wieder auferstehen.
- **Konzept: Das Default-Profil ist der app-weit aktive Drucker.** Der Picker auf der
  Startseite setzt es; alle Seiten und die lokale API folgen ihm.
- **Transport-Vorbereitung**: `PrinterConnectionFactory` baut aus dem Profil die passende
  Verbindung. TCP ist echt, USB/Bluetooth sind Architektur-Stubs (liefern beim Drucken einen
  sauberen Hinweis statt eines Absturzes).
- **Remote-Verträge** in `LabelPrinting/Remote/`: nur Schnittstellen für den späteren
  zentralen Druckserver (SignalR) — kein Netzwerkcode, keine Paket-Referenz.
- **`IPrinterService` spricht jetzt Profile** (ip/port-Überladungen entfernt); die
  `IPrinterConnection`-Überladungen bleiben für Spezialfälle (Geräteeinstellungen seriell).
- **Neue Seite „Druckerprofile"** (Einstellungen → Druckerprofile verwalten): Liste + Editor
  auf einer Seite, Anlegen/Bearbeiten/Löschen/Als-Standard, Verbindungstest aus dem Formular,
  Medium-Übernahme. Die alte `PrinterSettingsPage` ist gelöscht.
- **API erweitert**: neues Kommando `printers.list`; `print.template`, `zpl.send`,
  `printer.status`, `printer.settings` akzeptieren optional `printer` (Profilname oder Id).
- **Tests**: 65/65 grün, davon 10 neue für die Profil-Fassade (Stub-Sicherheit, Remote-Fail,
  IP-Validierung, `^CI28` genau einmal, Rohbytes unverändert).
- End-to-end verifiziert: `printer.status` hat den echten Drucker (192.168.1.251) über den
  neuen Profil-Pfad abgefragt und sauber geparst geantwortet.

## Behobener Bug aus dem Nutzertest

**Symptom:** Auf dem Dashboard meldeten „Verbindung testen"/„Kalibrieren" „kein Druckerprofil",
obwohl ein Standard-Profil existiert.

**Ursache (Altlast aus dem Shell-Umbau):** `MainPage` implementierte als einzige Rail-Seite
kein `IShellSectionView` — ihr `RefreshAsync` (lädt Profilliste, Statuskarte, Statistik) wurde
daher **nie** aufgerufen. Vorher fiel das nicht auf, weil die alten Buttons die Einstellungen
bei jedem Klick direkt aus dem Store lasen.

**Fix (in diesem Commit):**
1. `MainPage` implementiert jetzt `IShellSectionView` → `RefreshAsync` läuft bei App-Start,
   Rail-Klick und Rückkehr aus Unterseiten.
2. Zusätzlich Absicherung: Die Buttons greifen notfalls direkt auf das Default-Profil aus dem
   Store zu, falls die Picker-Auswahl (Windows-Binding-Eigenheit) kurz leer ist.

**Hinweis:** Auf Nutzerwunsch wurde die Sitzung ohne weiteren Build-/Laufzeittest beendet —
der Fix ist klein und folgt exakt dem Muster der anderen sechs Rail-Seiten, bitte beim
nächsten Start einmal Dashboard → „Verbindung testen" gegenprüfen.

## Für die nächste Sitzung notiert (nicht umgesetzt)

- **Profil-Kommentare/-Parameter**: Wunsch, Profilen ein Kommentarfeld und Zusatzparameter
  wie eine „Rolle" (z.B. Versandetiketten / Produktetiketten / Labor) zu geben — nützlich
  für die spätere Server-Vermittlung („welcher Drucker macht was").
- Danach regulär laut Roadmap: MVVM+Fluent für `TemplateManagerPage`/`ZplConsolePage`,
  echte USB/Bluetooth-Transporte, Server + `IRemotePrintClient`-Implementierung.

## Wie du die lokale API testest

Die App startet den Server automatisch: `http://localhost:5299/` (nur auf diesem Rechner
erreichbar). Testen geht mit `curl.exe` im Terminal oder PowerShell — die App muss laufen.

**1. Alle Kommandos entdecken:**
```powershell
curl.exe http://localhost:5299/api
```

**2. Nur lesen (ungefährlich):**
```powershell
curl.exe http://localhost:5299/api/printers.list          # alle Druckerprofile
curl.exe http://localhost:5299/api/printer.settings       # aktives (Default-)Profil
curl.exe http://localhost:5299/api/printer.status         # ~HS-Status vom Drucker
curl.exe http://localhost:5299/api/templates.list         # gespeicherte Vorlagen
curl.exe http://localhost:5299/api/media.list             # Medien-Presets
```

**3. Anderes Profil gezielt ansprechen** (Query-Parameter `printer` = Name oder Id):
```powershell
curl.exe "http://localhost:5299/api/printer.status?printer=LabelDrucker"
```

**4. Vorlage rendern ohne zu drucken** (POST mit JSON-Body):
```powershell
curl.exe -X POST http://localhost:5299/api/templates.render -H "Content-Type: application/json" -d "{\"name\":\"MeineVorlage\",\"data\":{\"ArtNr\":\"12345\"}}"
```

**5. Wirklich drucken** (Achtung: druckt ein echtes Etikett!):
```powershell
curl.exe -X POST http://localhost:5299/api/print.template -H "Content-Type: application/json" -d "{\"name\":\"MeineVorlage\",\"data\":{\"ArtNr\":\"12345\"}}"
curl.exe -X POST http://localhost:5299/api/zpl.send -H "Content-Type: application/json" -d "{\"zpl\":\"^XA^FO50,50^A0N,40,40^FDHallo^FS^XZ\"}"
```
Auch hier kann `"printer":"Name"` in den Body, um an ein bestimmtes Profil zu drucken.

**Antwortformat** immer gleich: `{ "success": true/false, "data": …, "error": "…" }`.
Fehlerfälle sind gefahrlos testbar, z.B. `?printer=gibtsnicht` → verständliche Fehlermeldung.
