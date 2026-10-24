# FLIP

**Fast Loudspeaker / In-ear Profiles.** Ein Symbol in der Taskleiste: Linksklick wechselt zwischen Lautsprecher und Kopfhörer, Rechtsklick wählt das Klangprofil für den Kopfhörer (über [Equalizer APO](https://sourceforge.net/projects/equalizerapo/)).

## Bedienung

- **Symbol:** Lautsprecher oder Kopfhörer, je nachdem, was gerade spielt. Grau heißt: Ein drittes Gerät ist Standard, etwa eine Bluetooth-Box.
- **Linksklick:** wechselt. FLIP stellt alle drei Windows-Rollen um (Standard, Multimedia, Kommunikation). Programme, die auf „Standard“ stehen, wechseln also mit, auch Discord.
- **Rechtsklick:** Lautsprecher oder Kopfhörer direkt wählen, Klangprofil am Kopfhörer, „Geräte festlegen“, „Mit Windows starten“, „Beenden“.
- **Erster Start:** Kopfhörer wird das Gerät für Kommunikation, Lautsprecher das Standardgerät oder, wenn beides dasselbe ist, das nächste Gerät. Ton über HDMI/DisplayPort (Monitore) kommt nur in Frage, wenn es nichts anderes gibt. Unter „Geräte festlegen“ lässt sich das ändern.

Neue Symbole legt Windows zuerst in den Überlauf (^ neben der Uhr). Zum Anheften in die Taskleiste ziehen.

## Klangprofile einrichten

Ohne diesen Schritt funktioniert nur das Umschalten.

1. Equalizer APO installieren und im „Device Selector“ den Kopfhörer anhaken, dann neu starten.
2. Im Konfigurationsordner (`C:\Program Files\EqualizerAPO\config`) einen Ordner `flip` anlegen und die Profile als `.txt` hineinlegen. Der Dateiname ist der Name im Menü. Beispiele liegen in [`beispiele/flip`](beispiele/flip).
3. In `config.txt` den Kopfhörer eintragen und die Datei einbinden, die FLIP schreibt:

   ```
   Device: {8da3dcf7-eba7-4d4a-a45b-7db7d679d87d}
   Include: flip-aktiv.txt
   ```

   Die GUID ist der Teil der Geräte-ID nach dem Punkt; `Flip.exe --dump` zeigt alle IDs.

FLIP ändert nur `flip-aktiv.txt`. Equalizer APO merkt die Änderung und lädt sofort neu, ohne Neustart.

Die Beispiele gehören zu In-Ears KZ ZS10 Pro: `Neutral.txt` ist die Klangkorrektur von [AutoEq](https://github.com/jaakkopasanen/AutoEq) (Messung crinacle), `Gaming.txt` nimmt zusätzlich Bass unter 150 Hz weg und hebt 2,8 und 5 kHz an, wo Schritte und Nachladen liegen. Für andere Kopfhörer bei AutoEq das eigene Modell suchen und dessen `ParametricEQ.txt` nehmen.

## Kommandozeile

Gut für Tastenkürzel: eine Verknüpfung auf `Flip.exe --toggle` anlegen (Desktop oder Startmenü) und in deren Eigenschaften eine Tastenkombination setzen.

| Aufruf | Wirkung |
| --- | --- |
| `Flip.exe --toggle` | zwischen Lautsprecher und Kopfhörer wechseln |
| `Flip.exe --profile Gaming` | Klangprofil wählen; `--profile off` schaltet es aus |
| `Flip.exe --dump` | Geräte, Rollen und Profile ausgeben |

## Dateien

- `%LOCALAPPDATA%\Flip\settings.json`: die beiden Geräte
- `%LOCALAPPDATA%\Flip\flip.log`: Protokoll, begrenzt auf 256 KB
- `flip-aktiv.txt` im Konfigurationsordner von Equalizer APO: das aktive Profil
- Autostart: Aufgabe `Flip` in der Aufgabenplanung („Bei Anmeldung“, 10 s Verzögerung, nur dein Benutzer, normale Rechte)

## Im Hintergrund

Ein Programm ohne Fenster: `Flip.exe`, rund 6 bis 8 MB eigener Speicher (Task-Manager, Spalte „Arbeitsspeicher“). Solange nichts passiert, rechnet es nicht: Windows meldet Gerätewechsel von selbst. Kein Dienst, kein Treiber, nichts im Netz.

Das Standardgerät setzt FLIP über `IPolicyConfig`. Die Schnittstelle ist nicht dokumentiert, die Soundeinstellungen von Windows nutzen aber dieselbe.

## Entfernen

Im Menü „Mit Windows starten“ abschalten und „Beenden“ wählen. Danach diesen Ordner und `%LOCALAPPDATA%\Flip` löschen und in `config.txt` die Zeile `Include: flip-aktiv.txt` entfernen.

## Bauen

```powershell
.\build.ps1
```

Braucht nur das .NET-10-SDK und lädt nichts herunter; das Ergebnis liegt in `dist\Flip.exe`.
