# Prompt-Protokoll — HumanTetrisUnity

Dieses Protokoll fasst die Prompts aus den Claude-Code-Chats zu diesem Projekt zusammen und hält fest, was jeweils umgesetzt wurde. Ausgewertet wurden die beiden Chats vom 02.11.2026 im Projektordner sowie der Chat zur Scan-Pipeline vom 05.11.2026 (Ordner `pipeline tool`). Ältere Arbeiten (vor dem 02.11.2026) sind nur über die Git-Historie nachvollziehbar. Zu den Arbeiten vom 03.11.2026 liegt auf diesem Rechner kein Chat vor; sie sind anhand der Commits zusammengefasst. Rückfragen und reine Bestätigungen („ja“) sind beim jeweiligen Schritt mit erfasst.

## 02.11.2026 — Code-Review und Branch `fix/review-bugs`

### 1. Fehler aus dem Code-Review beheben

**Prompt:** Eingefügter Review-Bericht (u. a. „Build schlägt fehl: KinectSteuerung.cs enthält using UnityEditor; … Melodie klingt schief …“), danach: „ja fixen, aber nicht committen ich glaube wir sollten vor dem fixen einen pull von main machen um sicher zu sein die aktuelle verrsion der kollegen zu haben“

**Umsetzung:** `main` gepullt (4 neue Commits der Kollegen, Stand `f4db326`), die Fixes darauf neu eingebaut: Build-Fehler, Doppel-Hold, Gesten auf bereits gelandeten Steinen, schiefe Melodie (Voice-Pool mit `PlayScheduled` statt Pitch-Änderung an einer AudioSource).

### 2. Commit und Push auf eigenem Branch

**Prompt:** „comitte unter einem neuen branch in diesem branch werden wir dann auch weiterarbeiten“ / „push den branch“

**Umsetzung:** Commit `0772566` auf `fix/review-bugs`, Branch auf GitHub gepusht. Git-Autor global auf Severin Benthin gesetzt.

### 3. CLAUDE.md anlegen

**Prompt:** „erstelle eine claude.md und dokumentiere dort den stand des projekts“

**Umsetzung:** `CLAUDE.md` mit Umgebung, Szenen, Skripten, Steuerung, Audio, Fallstricken und aktuellem Stand angelegt.

## 02.11.2026 — Grafik, Audio, Spielmechanik, Spielerdaten (Branch `feature/grafik-audio-spielerdaten`)

### 4. Kamera auf das ganze Spielfeld

**Prompt:** „kannst du das vorsdchu fenster bitte so anpassen das ich die gesamte spiel fläche sehe“

**Umsetzung:** Spielkamera in `Tetris.unity` mittig vor das Feld gesetzt und zurückgezogen, sodass der ganze Rahmen sichtbar ist.

### 5. Ghost, Umrandung, Startposition und Block-Schattierung

**Prompt:** „es kommt jetzt zu anzeigefehlernm bei den ghosts, sowie die umrandng bei den blöcken ist auch fehlerhaft. dessweitern starten die blöcke im unteren drittel“ / „Baue bitte schatierungen für die blöcke wie auf diesem bild“

**Umsetzung:** Anzeigefehler behoben, Blöcke bekamen eine klassische Tetris-Schattierung als echte Geometrie (flache Mitte, vier abgeschrägte Kanten).

### 6. Klassische Spielfeldhöhe

**Prompt:** „setzte die start position der blöcke auf die klassiche tertis höhe“ / „ja“

**Umsetzung:** Grid von 40 auf 20 Reihen verkleinert (`KinectSteuerung.height`), Steine starten in den obersten zwei Reihen. Blöcke und Köpfe erscheinen dadurch etwa doppelt so groß.

### 7. Block-Form überarbeiten

**Prompts:** „bau bitte das kachel mesh so um das es auch oben und unten auf den 3d blöcken zu sehen ist, am besten alle seiten“ / „mach auch die glaskästen mit den köpfgen in der form und mach die tetrominos etwas weniger rund“ / „verstärke bitte den kontrast von block und kante, an den glas kästen mach die linuien halb so dick“ / „mach an den glas kästen einfach dei selbe kante wie an den anderen blöcken das sollte besser aussehen“ / „die kanten sind alle sehr gleich grau... überlege eine lösung“

**Umsetzung:** Neues Mesh `Assets/Blöcke/BevelCube.asset` mit Schrägen an allen 12 Kanten (8 % der Blockbreite). Glaskästen nutzen dasselbe Mesh, die schwarzen Kanten-Stäbe wurden aus `Glascube.prefab` entfernt. Eigener Shader `Assets/Shaders/BevelBlock.shader`, damit jede Kante in der Blockfarbe unterschiedlich hell erscheint statt einheitlich grau.

### 8. Melodie überarbeiten

**Prompts:** „bitte schau dir nochmal den midi player an das hört sich absulut nicht an wie die tetris melodie“ / „kannst du es so machen das durch schnellersd abspielen der ton höher wird pitch bend, dafür muss das smple aber glaube unterschiedlich lang sein“ / „dar anfang ist schon gut in der mitte ist es noch komisch, ich glaube es fehlt etewas sustain bei den noten sie enden so abrupt“

**Umsetzung:** Neue MIDI-Datei `Korobeiniki.mid` mit korrekter Melodie (die alte enthielt falsche Töne). `WAVMIDIPlayer.cs` schneidet das Sample pro Note passend zu, sodass jede Note trotz Tonhöhe ihre MIDI-Länge hat, und baut Noten wie ein Sampler aus Anschlag, Sustain-Schleife und Ausklingen.

### 9. Begleitung unter die Stimme

**Prompts:** „können wir die orgninale tertis melodie irgendwie mit 10 lautstärke drunter legen …“ / „mach es auf 20 und den bass doppelöt so laut“ / „in welcher datei kann ich das einstellen“

**Umsetzung:** Die Originalaufnahme wurde aus Urheberrechtsgründen nicht verwendet. Stattdessen synthetische 8-Bit-Begleitung (Rechteck-Melodie 20 %, Dreieck-Bass 40 %), einstellbar im Inspector am Objekt `WAVMIDIPlayer` in `Tetris.unity`.

### 10. Wall Kicks, Level und Punkte

**Prompt:** „lass uns 2 weiter dinge angehen 1 wall kicks, drehen am Rand. sowie level und entsprechende punkte steigerung“

**Umsetzung:** Wall Kicks in `KinectSteuerung.TryRotate` (Verschiebung um 1–2 Felder bzw. 1 Feld hoch). Punkte 100/300/500/800 × Level, Level alle 10 Linien, Fallzeit pro Level im `GameManager`.

### 11. Spielordner aus dem Optionsmenü öffnen

**Prompt:** „gibt es die möglichkeit über den options butten einen windows explorer in der game directory zu öffnen“

**Umsetzung:** Button „OPEN GAME FOLDER“ im Optionsmenü (`MainMenu.OpenGameFolder()`).

### 12. Spielerdaten aus dem Scan-Netzwerkordner

**Prompt:** „wir haben eine scan pipeline die an einen netzwerk ordnber ordner mit fortlaufender nummer übermittelt, in diesen ordner liegen jewils die player heads später sollen auch die sampels und highscores in diesen ordner zurück gesichert werden, es benötigt die option die daten zur runtime zu laden baue das“ / „C:\Users\sever\Desktop\putput nutze dien pfad zum testen der ordner strucktur“

**Umsetzung:** `PlayerDataManager.cs` mit `HumanTetris_config.json`: Köpfe werden zur Laufzeit per glTFast aus `<scansRoot>/Playermodell_XXXX/` geladen, Spielerauswahl im Hauptmenü, `sample.wav` und `highscore.json` werden in den Spielerordner zurückgeschrieben. Mit `putput` als Testordner geprüft.

### 13. Helligkeit und Auslieferung der Scans

**Prompt:** „das sind die gleichen modelle auf der website sind sie gut von der helligkeit versuche es über die konfiguration anzupassen ist es möglich den ordener puput in dem die scans liegen zusammen mit dem spiel auszuliefern? …“

**Umsetzung:** `headBrightness` und `headSelfLight` in der Konfiguration, zwei Fehler behoben, die die Köpfe zusätzlich abgedunkelt hatten. Scans liegen jetzt im Projekt unter `Scans/` und werden per `Assets/Editor/CopyScansOnBuild.cs` neben die .exe kopiert.

### 14. Kopfgröße

**Prompts:** „finde heraus wie viel größer man die köpfe skalieren kann ohne das sie aus den blöcken herausschauen“ / „zeig mal head size 0,95“ / „hinten fehlt die glas kante“ / „Richte diese 0,95 Kopfgröße noch fest in den Code ein und commite und pushen“

**Umsetzung:** Vermessung aller vier Scans: bis `headSize` 1,0 ragt kein Kopf aus dem Glas. Standard auf 0,95 gesetzt (Commit `7d756db`). Fehlende hintere Glaskanten im `BevelBlock`-Shader behoben.

### 15. Commits, Push und Push-Erlaubnis

**Prompts:** „Commite das in den neuen Branch“ / „Ja und comutte ruhig das MCP mit werde das morgen auf der Entwicklungs Maschine eh brauchen“ / „! git push -u origin feature/grafik-audio-spielerdaten“ / „… Richte das ein“

**Umsetzung:** Commits `646c793` (Grafik, Audio, Wall Kicks/Scoring, Spielerdaten) und `a7d639f` (Unity-MCP-Paket, CLAUDE.md) auf `feature/grafik-audio-spielerdaten`, vom Nutzer selbst gepusht (inkl. Köpfe über Git LFS, ca. 76 MB). Erlaubnis für `git push` in `.claude/settings.local.json` eingerichtet (nur lokal).

### 16. Hauptmenü im Website-Stil

**Prompts:** „Schau dir im Browser die lokal gehostete human Tetris Seite an, erstelle in einem ähnlichen Stiel das Hauptmenü“ / „Sind diese Texte noch im Hauptmenü oder ist es blank wie bei der Optionen Seite. …“ / „Ja“

**Umsetzung:** Neon-Arcade-Hauptmenü nach Vorlage der lokal gehosteten Website (Farben, Schrift, Raster, Glow), Zusatztexte entfernt, sodass es wie die Optionen-Seite aufgebaut ist. Commit `e400684`.

### 17. Spielbildschirm und Tetromino-Haufen

**Prompts:** „Baue den Spiel Bildschirm ähnlich aus behalte aber die Background Farbe und das Gitter“ / „Nutze eine aufgelockerte zusammensetzung aus tetroominos wie im 1screenshot das sieht doch viel cooler aus“ / „Schau bitte das alle 4 köpfe 2 mal im Main Menu zu sehen sind ein Kollege fehlt ganz ein anderer ist dreimal dabei ..“ / „passe das gitter im hintergund an doe lock größe an“

**Umsetzung:** Spielbildschirm im Neon-Stil (Spielfeld mit rotem Verlauf und Gitter bleibt). `MenuTetrominoPile.cs` baut im Menü einen aufgelockerten Haufen aus echten Tetrominos, alle Köpfe gleichmäßig verteilt. Menü-Raster auf Blockgröße angepasst.

### 18. FPS-Counter und Test-Build

**Prompts:** „blrnde bitte einen debugg fbs counter ein“ / „mach einen test build“ / „nein nutze weiterhin diesen branch der soll der aktuelle alternative zustand sein“

**Umsetzung:** `FpsCounter.cs` (F3 schaltet um). Development-Build unter `projektwoche/TestBuild` (außerhalb des Repos), Köpfe laden dort aus dem mitkopierten `Scans`-Ordner. Commit `86658be` auf `feature/grafik-audio-spielerdaten`.

### 19. Game-Over-Screen

**Prompt:** „Stil Game-Over-Screen auch im Neon-Look um“

**Umsetzung:** Game-Over-Szene im Neon-Look mit Ergebnis-Karte (Score, Level, Linien, „NEW BEST!“), R = Neustart, Esc = Hauptmenü. Commit `6770969`.

### 20. Doku mit Screenshot-Galerie

**Prompt:** „Erstelle eine schöne Menschen lesbare Doku unsere Arbeiten am Spiel nutze auch die Screenshots die du gemacht hast alle als eine Art Galerie“

**Umsetzung:** Abgebrochen. Es wurden nur Screenshots nach `docs/images/` exportiert (6 Bilder), die Doku selbst entstand nicht.

## 03.11.2026 — Weiterentwicklung (aus der Git-Historie, kein Chat vorhanden)

Die Prompts dieses Tages sind nicht erhalten. Umgesetzt wurde laut Commits auf `feature/grafik-audio-spielerdaten`:

- `c652dee` Umschalter Kinect/Tastatur (Feldbreite 9 bzw. 11), Modus wird in den Highscores gespeichert.
- `0087862` Posierter Dummy-Körper in den Steinen, großer Kopf im O-Stein (`BigHeadBlock.cs`).
- `bde3ad1` Modell-Vorschau des Kopfes in der Spielerauswahl (`PlayerPreview.cs`).
- `8ab4f8c` Kinect: schnelleres Gesten-Timing, Debug-Menü (F5) und Kinect-Vorschau (F6).
- `f6ebd87` Begleitung/Bass standardmäßig 50 %, Doku aktualisiert.

## 05.11.2026 — Low-Poly-Köpfe (Chat im Ordner `pipeline tool`)

### 21. Skript für polygonreduzierte Köpfe

**Prompt:** „… erstelle ein neues skript das mithilfe von blender die ply kopfscans in polygon reduzierte glb datein umwandelt“

**Umsetzung:** `pipeline tool/ply_head_to_lowpoly_glb.py`: Cleanup, Decimate auf 5.000 Dreiecke, Farbe und Normal-Map vom High-Poly-Scan auf das Low-Poly gebacken, GLB-Export.

### 22. Test in Spiel und Blender

**Prompts:** „öfnne mir das glb in muman tetris“ / „öfnne mir die low poly variante in blender“

**Umsetzung:** Testkopf im TestBuild geladen (fehlerfrei laut Player-Log) und in Blender geöffnet.

### 23. Alle vier Köpfe ersetzen

**Prompt:** „ok mach das mit allen 4 scans und lege sie entsprechend in playermodell nach aktueller names konvention ab“ / „starte das spiel“

**Umsetzung:** `PlayerHead.glb`/`.png` in `Playermodell_0001`–`0004` (Joel, Joko, Kacpar, Severin) in `Scans/` und `TestBuild/Scans` ersetzt: ca. 3.600–3.900 Vertices statt ca. 240.000, ca. 4 MB statt 16–18 MB pro Kopf. Highscores blieben erhalten, die High-Poly-Originale liegen weiter in `Desktop/putput`. Noch nicht committet.

### 24. Prompt-Protokoll

**Prompt:** „schau dir bitte das claude md von human tetris an und ergänze es ggf mit einem prompt protokoll werte dafür ggf die dazugehörigen chats aus“

**Umsetzung:** Dieses Protokoll angelegt und `CLAUDE.md` aktualisiert (Verweis hierauf, aktueller Stand, Kopf-Größen).

## 10.11.2026 — Farbige Glaskästen

### 25. Kanten und Glas in Steinfarbe, klarer Kopfkasten

**Prompt:** (Screenshot der Tetromino-Prefabs, Auftrag aus einer anderen Claude-Session) Alle Kanten der Glaskästen in der Farbe des Steins, Glas durchsichtig, aber in Steinfarbe getönt; am Kopfkasten (`Glascube`) und beim großen Kopf im O-Stein bleibt die Vorderscheibe farblos. Ghost-Prefabs nicht verändern.

**Umsetzung:** Shader `BevelBlock` um `_Highlight`/`_Darken` (wie stark Kanten Richtung Weiß/Schwarz schattiert werden) und `_FrontClear` (Scheibe zur Kamera durchsichtig) erweitert; Standardwerte lassen alle anderen Materialien unverändert. `DummyTetrominoBuilder` setzt Tönung und Kanten jetzt bei jedem Neubau und legt pro Stein ein `<Stein>_Kopfglas.mat` für den Kopfkasten an (O-Stein: alle vier Zellen). Alle 7 Prefabs neu gebaut, Kontrolle über die Vorschaubilder in `Temp/DummyPreviews`. Da der Unity-MCP-Server nicht erreichbar war, lief der Neubau über ein temporäres Editor-Skript (danach wieder gelöscht). Noch nicht committet.

### 26. Commits, MCP-Verbindung und Haufen nur mit Spieler 1–4

**Prompts:** „Erst im Spiel testen“ / „verbinde den mcp server neu“ / „Ja, committen“ / „comitte auch diese manuellen änderungen“ / „im haupt menu sollen nur die modelle 1-4 angezeigt werden“

**Umsetzung:** Unity hatte sich nicht beim MCP-Server angemeldet; die Bridge über ein temporäres Editor-Skript neu gestartet. Glaskästen im Play-Mode geprüft (Menü und Spiel), Commit `fdad2de` gepusht. Manuelle Änderungen (Level alle 4 Linien, schnellere Fallzeiten, Spawner y = 68, Scans `Playermodell_0005`/`0006`, neue `kinect_sample.wav`) als `c17481c` committet und gepusht. `MenuTetrominoPile` zeigt im Haufen nur noch die Köpfe der Spieler 1–4 (`minPlayerNumber`/`maxPlayerNumber`); die Spielerauswahl enthält weiter alle Spieler.

