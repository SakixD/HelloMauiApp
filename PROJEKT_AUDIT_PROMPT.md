# Arbeitsauftrag: Projekt durchgehen, Backlog erstellen, Punkt für Punkt abarbeiten

> Diesen Prompt in einer **Claude-Code-Session in VS Code** verwenden (dort kann
> gebaut und getestet werden). Er ist self-contained.

Du bist Claude Code und arbeitest im Repository `HelloMauiApp` (.NET MAUI, net10.0,
mit der Bibliothek `LabelPrinting`). Deine Aufgabe: das Projekt vollständig
verstehen, daraus eine **Funktions- und Bugfix-Liste** aufbauen und diese dann
**Stück für Stück** umsetzen.

---

## SCHRITT 0 — ZUERST MICH FRAGEN (nicht überspringen!)

**Bevor du irgendetwas liest, analysierst oder änderst, stellst du mir zuerst
Fragen und wartest auf meine Antworten.** Erst wenn ich geantwortet habe, geht es
weiter. Frag mich mindestens:

1. **Fokus:** Soll ich mich zuerst auf **neue Funktionen**, auf **Bugfixes** oder
   auf **Aufräumen/Refactoring** konzentrieren — oder in welcher Reihenfolge?
2. **Umfang jetzt:** Soll ich mich an die Phasen aus `ROADMAP.md` halten (nächste
   offene Phase zuerst) oder frei alles aufnehmen, was mir auffällt?
3. **Wie umsetzen:** Jeden Punkt einzeln umsetzen und **nach jedem Punkt auf meine
   Freigabe warten**, oder eine ganze Prioritätsstufe am Stück abarbeiten und dann
   pausieren?
4. **Grenzen:** Gibt es Bereiche/Dateien, die ich **nicht** anfassen soll?
5. **Commit-/Branch-Stil:** Auf welchem Branch arbeiten, wie granular committen,
   soll ich pushen und einen PR anlegen?

Fasse meine Antworten anschließend kurz zusammen und bestätige sie, bevor du
loslegst. **Rate nichts** — wenn eine Antwort unklar ist, frag nach.

---

## SCHRITT 1 — Dokumente lesen

Lies vollständig und in dieser Reihenfolge:

1. `PROJECT.md` — Zielbild, Prinzipien, aktueller Stand, Glossar.
2. `ROADMAP.md` — Phasenplan und Arbeitsweise.
3. `PRINTER_ARCHITECTURE_PLAN.md` — Detailspezifikation des ersten Umbauschritts.
4. `LabelPrinting/README.md` — Umfang und bekannte Grenzen der Bibliothek.

Halte danach in **1–2 Sätzen** fest, was das Projekt ist und wo es steht — als
Beleg, dass du den Kontext hast. Widersprüche zwischen Doku und Code notierst du
(nicht auflösen, nur festhalten).

## SCHRITT 2 — Das komplette Projekt durchgehen

Geh den **gesamten** Quellcode systematisch durch, mindestens:

- `LabelPrinting/` — alle Dateien unter `Models/` und `Services/` (inkl.
  `Transport/`, falls vorhanden).
- `HelloMauiApp/` — `MainPage`, alle weiteren Pages (`.xaml` + `.xaml.cs`),
  `MauiProgram.cs`, `App`/`AppShell`, `Platforms/`.
- Projektdateien (`*.csproj`, `*.slnx`) und `.gitignore`.

Verschaff dir ein reales Bild: Was existiert, wie hängt es zusammen, was ist noch
Einzeldrucker-Logik, wo sind Lücken gegenüber dem Zielbild in `PROJECT.md`.

## SCHRITT 3 — Backlog-Datei erstellen (`BACKLOG.md`)

Lege eine Datei `BACKLOG.md` im Repo-Root an mit **zwei getrennten Listen**:

### A) Funktionsliste
Fehlende oder sinnvolle Funktionen, hergeleitet aus `ROADMAP.md`/`PROJECT.md` und
dem realen Code-Stand.

### B) Bugfix-Liste
Konkrete Fehler, Inkonsistenzen, Risiken oder Fallstricke, die du **im echten
Code gesehen** hast.

**Jeder Eintrag enthält:**
- **ID** (z.B. `FEAT-01`, `BUG-01`)
- **Titel** (kurz)
- **Fundstelle** — konkrete Datei(en), wenn möglich mit `Datei:Zeile`
- **Beschreibung** — was ist der Fall / was fehlt
- **Warum** — Nutzen bzw. Auswirkung
- **Lösungsskizze** — geplantes Vorgehen (grob)
- **Priorität** — Hoch / Mittel / Niedrig
- **Aufwand/Risiko** — S / M / L
- **Status** — `[ ]` offen · `[~]` in Arbeit · `[x]` erledigt

Sortiere jede Liste nach Priorität. Halte dich an die Phasenlogik aus
`ROADMAP.md` (nicht mehrere Phasen vermischen), sofern ich in Schritt 0 nichts
anderes gesagt habe.

## SCHRITT 3b — Backlog mit mir abstimmen

Zeig mir die Liste und **warte auf meine Priorisierung/Freigabe**, bevor du mit der
Umsetzung beginnst. Ich sage dir, welche Punkte in welcher Reihenfolge dran sind.

## SCHRITT 4 — Punkt für Punkt abarbeiten

Arbeite die freigegebenen Einträge **einzeln** ab — nie mehrere gebündelt:

1. Eintrag in `BACKLOG.md` auf `[~]` setzen.
2. Änderung umsetzen — so klein wie möglich, kein Umbau über den Eintrag hinaus.
3. `dotnet build` (mindestens Android-Target, das läuft plattformunabhängig) →
   muss grün sein. Wo sinnvoll, das Verhalten konkret prüfen.
4. Committen mit klarer Nachricht, die die ID nennt (z.B. `BUG-03: ...`).
5. Eintrag auf `[x]` setzen.
6. Nächsten Eintrag — bzw. gemäß Schritt 0 auf meine Freigabe warten.

Bestehende, funktionierende Funktionalität (v.a. der ZPL/TCP-Druck) darf sich im
**Verhalten nicht ändern**, außer ein Eintrag verlangt das ausdrücklich.

---

## HARTE REGELN — nicht halluzinieren

- **Jede** Aussage, jeder Backlog-Eintrag und jede Fundstelle muss aus einer
  Datei belegbar sein, die du **tatsächlich gelesen** hast. Keine erfundenen
  Datei-/Klassen-/Methodennamen, keine ausgedachten Zeilennummern.
- Wenn du etwas **nicht** weißt oder nicht verifizieren kannst: schreib das
  ausdrücklich hin („nicht verifiziert", „Annahme"), statt zu raten.
- Keine Bugs erfinden, um die Liste zu füllen. Findest du wenige echte Fehler, ist
  eine kurze Liste das richtige Ergebnis.
- Behaupte **nie**, etwas gebaut/getestet zu haben, ohne den Befehl real
  ausgeführt zu haben. Schlägt ein Build/Test fehl, meldest du das mit der
  echten Ausgabe.
- Erfinde keine Funktionen, die dem Zielbild in `PROJECT.md` widersprechen (die
  Device SDK enthält **keine** Geschäftslogik).
- Im Zweifel: **fragen statt annehmen.**

## Erlaubnisse

Erlaubt ohne Rückfrage: Lese-Befehle, `dotnet build/restore/test`, `git
status/diff/add/commit` auf dem Feature-Branch. **Nicht** ohne Rückfrage:
`git push --force`, `git reset --hard`, Branches löschen, Push auf `master`,
Änderungen außerhalb dieses Vorhabens.
