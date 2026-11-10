# HumanTetrisUnity

Tetris-Variante (Projektwoche), bei der die Steine 3D-Scans echter Menschen sind und das Spiel per **Kinect v1** gesteuert wird. Die Hintergrundmelodie wird live aus einer kurzen Mikrofonaufnahme erzeugt (Sample wird per Pitch auf die MIDI-Noten von „Korobeiniki“ gelegt). Siehe auch `README.md`.

## Umgebung

- **Unity 2023.2.22f1** (siehe `ProjectSettings/ProjectVersion.txt`), Windows, Kinect for Windows SDK v1.8
- Pakete: TextMesh Pro / uGUI, Timeline, DryWetMIDI (`Assets/Melanchall`, liest die MIDI-Datei)
- Kinect-Anbindung: `Assets/KinectSDK` (Kinect-Wrapper, `KinectManager`, `KinectGestures`) und `Assets/Plugins/Microsoft.Kinect.dll`
- **Git LFS** für `.png .fbx .wav .mp3 .psd .tga .exr` (`.gitattributes`). `git lfs install` muss eingerichtet sein.
- Remote: `https://github.com/TheDude1996/HumanTetrisUnity`. Mehrere Leute arbeiten am selben Repo, deshalb **vor Änderungen `main` pullen**.

## Szenen (Build-Reihenfolge)

| Index | Szene | Inhalt |
|---|---|---|
| 0 | `Assets/Scenes/MainMenu.unity` | `MainMenu.PlayGame()` lädt die Szene mit Index 1 |
| 1 | `Assets/Scenes/Tetris.unity` | Das Spiel |
| 2 | `Assets/Scenes/GameOverMenu.unity` | Neustart (Index 1) oder zurück ins Menü (Index 0) |

Die Szenen werden per Index geladen (`SceneManager.LoadScene(int)`). Wer die Reihenfolge in den Build Settings ändert, muss auch die Skripte anpassen.

## Spiel-Skripte (`Assets/Scripts`)

- **`KinectSteuerung.cs`**: Die *aktive* Steuerung pro Stein. Enthält Tastatur- und Kinect-Eingabe, Fallen, Ghost, Grid, Line-Clear, Hard Drop und Wall Kicks (`wallKicks`: Drehen am Rand probiert Verschiebungen um 1–2 Felder bzw. 1 Feld hoch). Implementiert `KinectGestures.GestureListenerInterface` und meldet sich in `Start()` selbst beim `KinectManager` an.
- **`GameSettings.cs`**: Steuerungsmodus Kinect/Tastatur (statisch, in `PlayerPrefs` gespeichert, Standard Kinect). Umschalter `ModeButton` im Hauptmenü auf der `PlayerCard` (`MainMenu.ToggleMode()`). **Kinect: Feld 9 breit, Tastatur: 11 breit.** Das Grid bleibt 11 Spalten groß, benutzbar sind `KinectSteuerung.MinX..MaxX` (Kinect 1..9, Spawn bleibt bei x = 20). Im Tastatur-Modus melden sich die Steine nicht als Gesten-Listener an; im Kinect-Modus bleibt die Tastatur aktiv (für Hard/Soft Drop). `PlayfieldLayout.cs` (auf `Border` in `Tetris.unity`) rückt bei 9 Spalten die Seitenwände nach innen und verschmälert den Hintergrund (dafür sind `Background`/Wände nicht static). Runden speichern `mode` (`kinect`/`keyboard`; alte Runden ohne `mode` zählen als Tastatur) in `highscore.json` und `leaderboard.json`; Menü und Game-Over zeigen den Bestwert pro Modus (`PlayerDataManager.GetBest(mode)`).
- **Dummy in den Steinen** (`Assets/Kevin Iglesias/Human Character Dummy`, `Assets/Editor/DummyTetrominoBuilder.cs`): Jeder Stein enthält den posierten Dummy (ohne Kopf). Der Kopf-Scan sitzt weiter im Kopfkasten `Glascube` (dort steht der Dummy-Kopf). Alle Zellen sind Glaskästen in der Steinfarbe: Kanten in Steinfarbe, Scheiben leicht getönt (Körperzellen `Assets/Blöcke/Dummy/<Stein>_Glas.mat`, Kopfkasten `<Stein>_Kopfglas.mat` mit klarer Vorderscheibe, damit der Kopf-Scan ungetönt bleibt). Die Werte (Tönung `GlassTint` 0,10, O-Stein 0,05; `EdgeHighlight`/`EdgeDarken`) stehen als Konstanten im Builder und werden bei jedem Neubau in die Materialien geschrieben. Shader `HumanTetris/BevelBlock` hat dafür `_Highlight`, `_Darken` und `_FrontClear` (Scheibe mit Welt-Normale −z durchsichtig). Die Pose wird gebacken und pro Zelle als Kind `DummyPart` gespeichert (Meshes in `Assets/Blöcke/Dummy/`), damit Zellen beim Landen/Linien löschen einzeln verschoben bzw. zerstört werden. **Posen ändern:** Werte in `BuildPoses()` anpassen, dann `Tools/Dummy in Tetrominos/Build all` (baut alle 7 Prefabs neu), Kontrolle über `Render previews to Temp`. Ghost-Prefabs haben keinen Dummy.
- **O-Stein (2x2) mit großem Kopf** (`BigHeadBlock.cs`, `PlayerDataManager.GetHeadQuadrants()`): Der O-Stein hat keinen Dummy, sondern nur den Kopf-Scan, so groß wie das ganze Quadrat (8 x 8 Einheiten, Tiefe auf 4 gestaucht). Der Kopf wird nach dem Laden einmal in vier Viertel geschnitten (nach Dreiecksmitte); jede Zelle trägt ihr Viertel als Kind `HeadQuadrant`, deshalb funktioniert der Line Break wie bei allen Steinen. Ohne Kopf-Scan bleibt der normale kleine Kopf im `Glascube`. Alle vier Zellen sind weiße Glaskästen (`O_Kopfglas.mat`, klare Vorderscheibe). Im Builder: `headOnly = true` beim O. Kopfneigung `CollisionBlock.tiltZ` (J = 90, L = -90) richtet den Kopf an der liegenden Dummy-Pose aus.
- **`Steuerung.cs`**: Ältere Tastatur-Steuerung. Sie hängt zwar noch an den Prefabs, ist dort aber **deaktiviert**. Nicht mehr verwenden; `SpawnTetromino`, `HoldPreview` und `NextPiecePreview` arbeiten mit `KinectSteuerung`.
- **`SpawnTetromino.cs`** (auf `Spawner.prefab`): Warteschlange mit 3 kommenden Steinen, `NewTetromino()`, Hold-Logik (`HoldPiece`, `canHold`).
- **`GameManager.cs`**: Singleton (`GameManager.Instance`). Verwaltet Score (100/300/500/800 × Level; Soft Drop +1, Hard Drop +2 pro Reihe über `AddDropScore`), Level (alle `linesPerLevel` = 4 Linien eins höher, privat, also nicht im Inspector) und die Fallzeit pro Level (`GetFallTime()`, Level 1 = 0,6 s, schneller als klassisch). `GameOver()` lädt Szene 2.
- **`NextPiecePreview.cs` / `HoldPreview.cs`**: Zeigen verkleinerte Kopien der Prefabs an, mit deaktivierter bzw. entfernter Steuerung.
- **`HeadSocket.cs`**: Die Klasse heißt **`CollisionBlock`**, also anders als die Datei. Setzt den Kopf des Spielers in den Glaskasten: bevorzugt den GLB-Kopf vom `PlayerDataManager`, sonst den Standardkopf aus `Resources/Blocks/...` (wird ausgetauscht, sobald der GLB-Kopf fertig geladen ist).
- **`PlayerDataManager.cs`**: Singleton über Szenen hinweg (`PlayerDataManager.Instance`, legt sich selbst an). Verbindung zum Netzwerkordner der Scan-Pipeline, siehe Abschnitt „Spielerdaten / Netzwerkordner“.
- **`WAVMIDIPlayer.cs`**: Die Klasse heißt **`MidiWavPlayer`**, also anders als die Datei. Lädt eine WAV- und eine MIDI-Datei aus `StreamingAssets` und spielt jede Note als gepitchtes Sample ab. Läuft über einen Pool von AudioSources mit `PlayScheduled` auf der `AudioSettings.dspTime`-Uhr, mit Lookahead-Scheduling in `Update()`. Optional als Endlosschleife (`loopMidi`).
- **`WAVUtility.cs`**: WAV-Bytes in einen `AudioClip` umwandeln (nur PCM).
- **`MainMenu.cs` / `GameOverMenu.cs`**: Button-Callbacks für die Menüs. `GameOverMenu` zeigt Score/Level/Linien der letzten Runde (aus den statischen `GameManager.LastScore/LastLevel/LastLines`, gesetzt in `GameOver()`) und „NEW BEST!“; R = Neustart, Esc = Hauptmenü. Der Game-Over-Screen hat den Neon-Look (pink statt lime, `Menu/TitleGlowPink.mat`). `MainMenu.OpenGameFolder()` (Button „OPEN GAME FOLDER“ im Optionsmenü) öffnet den Spielordner im Explorer: im Build der Ordner mit der .exe, im Editor der Projektordner. Spielerauswahl im Hauptmenü: `PreviousPlayer`/`NextPlayer` (Buttons `<` `>`), `RefreshPlayers` (REFRESH), Anzeige in `playerLabel`.
- **Hauptmenü-Design** (`MainMenu.unity`): Neon-Arcade-Stil der Projekt-Website (dunkles `#070812`, Raster, Lime `#9EFF00`, Cyan, Pink, Violett; unten ein Haufen echter Tetrominos mit den Köpfen der Spieler 1–4, siehe `MenuTetrominoPile`). Canvas: `ScaleWithScreenSize` 1920×1080, Match = 1 (Höhe), damit das UI-Raster (`Grid`, Kachel 98,45 Canvas-Einheiten, `GridTile100.png`) genau so groß ist wie die Blöcke der 3D-Kamera und auf deren Kanten liegt. Ändert man Kamera (Abstand, FOV), `cell` oder Haufen-Position, muss das Raster neu berechnet werden (Kachel = cell × 1080 / (2 × Abstand zur Würfel-Vorderseite × tan(FOV/2)))). Grafiken liegen in `Assets/Menu/` (`Rounded*`, `RadialGlow`, `GridTile` sind per Code erzeugte Sprites, `TitleGlow.mat` = TMP-Underlay für den Titel). `MenuFloat.cs` lässt die beiden Quadrate schweben. Buttons haben im Prefab/Scene Scale 2 gehabt, jetzt 1. **Projekt nutzt Linear-Farbraum**: Alpha-Werte wirken dort heller als in CSS, deshalb sind die Glas-Buttons nur ca. 1 % deckend. `MicRecorder` in der Szene ist eine Prefab-Instanz (`Assets/MicRecorder.prefab`), nicht löschen.
- **`PlayerPreview.cs`** (Objekt `PlayerPreviewCard` in `MainMenu.unity`, rechts neben der Spielerkarte): Zeigt den Kopf-Scan des gewählten Spielers. Eine eigene Kamera bei (5000, 5000, 0) rendert in eine RenderTexture (`RawImage`); der Kopf pendelt ±35°. Aktualisiert sich über `PlayerDataManager.HeadChanged`. Neu aufbauen mit `Tools/Player Preview/Add to Main Menu` (`Assets/Editor/PlayerPreviewSetup.cs`).
- **`FpsCounter.cs`**: Debug-FPS-Anzeige oben rechts (FPS, ms, schlechtester Frame). Legt sich per `RuntimeInitializeOnLoadMethod` selbst an, bleibt über Szenenwechsel. **F3** schaltet sie um; standardmäßig an im Editor und in Development-Builds, in Release-Builds aus.
- **`MenuTetrominoPile.cs`** (Objekt `TetrominoPile` in `MainMenu.unity`): Baut im Hauptmenü einen aufgelockerten Haufen aus den 7 Spiel-Prefabs (feste Zufallszahl, Raster-Simulation mit Lücken und Überhängen; `loose` aus = dichter Haufen). Die Prefabs werden unter einem inaktiven Objekt erzeugt, dort werden `KinectSteuerung`/`Steuerung`/`CollisionBlock` entfernt, bevor sie anlaufen. Die Köpfe der Spieler **1–4** (`minPlayerNumber`/`maxPlayerNumber`, Nummer = Zahl am Ende des Ordnernamens; `maxPlayerNumber` = 0 heißt alle) werden per `PlayerDataManager.LoadHeadAssetAsync(player)` geladen und gleichmäßig gemischt in die Glaskästen gesetzt (aufrecht zur Kamera). Menü-Kamera: (0,0,−40), FOV 60; Haufen-Ursprung y = −21 (unterste Reihe an der Bildunterkante). Der Verlauf `PileFade` (UI) blendet den Haufen nach oben aus. Ca. 24 Köpfe (mit den Low-Poly-Köpfen je ca. 4.000 Vertices, mit High-Poly-Scans je ca. 240.000); bei schwacher GPU `columns`/`averageRows` senken.
- **Spielbildschirm-Design** (`Tetris.unity`): Spielfeld (roter Verlauf + Kachelgitter) bleibt, drumherum Neon-Stil: Kamera-Hintergrund `#070812`, 3D-Sprites `Backdrop` (Raster, Glows) bei z = 120 mit Material `BackdropSprite` (Queue 1000, damit sie hinter dem Spielfeld gezeichnet werden), Karten `Panels/HoldCard` und `NextCard` (Welt-Sprites hinter den Vorschau-Steinen), UI-Karten `LevelCard`/`ScoreCard`, Wände mit `Menu/Wall.mat`. `GameManager` schreibt Score/Level als Rich-Text (`SCORE\n123`). `ScoreText`/`LevelText` sind Teile von Prefab-Instanzen (nicht umhängbar), deshalb per Weltposition platziert.
- `Assets/KinectSDK/KinectScripts/NewMIC.cs` (Klasse `KinectMicRecorder`): Nimmt 2 s über das Kinect-Mikrofonarray auf (Button „Record 2s“ im Hauptmenü). Speichert im Editor nach `StreamingAssets/kinect_sample.wav`, im Build nach `persistentDataPath`, und sichert zusätzlich als `sample.wav` in den Ordner des gewählten Spielers.

## Spielerdaten / Netzwerkordner

- Pfad und Einstellungen stehen in **`HumanTetris_config.json` im Spielordner** (Build: neben der .exe; Editor: Projektordner, per `.gitignore` ausgeschlossen). Wird beim ersten Start mit Standardwerten angelegt. Pfade mit `/` oder `\\` schreiben (JSON).
  - `scansRoot` (Standard `Scans` = relativ zum Spielordner; absolute Pfade und Netzwerkpfade wie `//server/scans` gehen auch), `headFileName` (leer = erste .glb im Ordner), `headSize` (Anteil der Blockgröße, Standard 0,95; ab 1,0 ragt das Gesicht aus dem Glas), `headBrightness` (1,5) und `headSelfLight` (Eigenleuchten 1,0, setzt `_EMISSION`; REFRESH im Menü übernimmt Änderungen sofort), `headRotation` (Grad; Standard 0/180/0, weil die Blender-Exporte der Pipeline nach +Z schauen, die Kamera aber aus −Z), `leaderboardSize`.
- Aufbau (so liefert es die Pipeline): `<scansRoot>/Playermodell_0004/PlayerHead.glb` (Textur eingebettet, `.png` daneben wird nicht gebraucht) und `<scansRoot>/.pipeline_counter.json`. Das Spiel schreibt `sample.wav` und `highscore.json` in den Spieler-Ordner sowie `<scansRoot>/leaderboard.json` (globale Bestenliste). Spieler-Ordner = Ordnername endet auf eine Zahl; sortiert nach dieser Zahl, standardmäßig wird der neueste gewählt. Die Köpfe sind Low-Poly (ca. 4.000 Vertices, siehe „Aktueller Stand“); rohe Pipeline-Exporte haben ca. 240.000.
- Köpfe werden zur Laufzeit mit **glTFast** (`com.unity.cloud.gltfast`) geladen. FBX geht zur Laufzeit nicht. Der Kopf wird einmal geladen (unsichtbare Vorlage `HeadTemplate`) und pro Block kopiert, zentriert und auf `headSize` skaliert.
- **Auslieferung:** Die Scans liegen im Projekt unter `Scans/` (neben `Assets/`, damit Unity die GLBs nicht importiert; `*.glb` über Git LFS). `Assets/Editor/CopyScansOnBuild.cs` kopiert den Ordner nach jedem Build neben die .exe (ohne vom Spiel erzeugte Dateien). Der Kopf wird an der Drehung des Glaskastens ausgerichtet, nicht am Socket (der ist im Prefab für den alten FBX-Kopf um 270/180/0 gedreht).
- Damit die glTFast-Shader im Build enthalten sind, liegen in `Assets/Resources/GltfShaderVariants/` zwei Platzhalter-Materialien (ohne und mit `_EMISSION`). Bei Köpfen mit anderen Materialeigenschaften (z. B. Transparenz) ggf. weitere Platzhalter anlegen.
- `GameManager.GameOver()` speichert die Runde über `SaveRound` (im Hintergrund, nacheinander per Lock, atomar über `.tmp` + Replace). Der `MidiWavPlayer` nimmt `sample.wav` des Spielers, falls vorhanden.

### Grid und Koordinaten

- Grid: `11 × 20` (`KinectSteuerung.width/height`, statisches `Transform[,] grid`). **Eine Zelle entspricht 4 Unity-Einheiten**, deshalb sind alle Bewegungen `±4`.
- Ein Stein besteht aus Child-Transforms (den Einzelblöcken). `rotationPoint` ist der Drehpunkt im lokalen Raum.
- Gelandete Steine: Ihre Blöcke werden ins Grid eingetragen, das Skript wird mit `enabled = false` abgeschaltet, und der `SpawnTetromino` erzeugt den nächsten Stein.

## Steuerung

| Aktion | Tastatur | Kinect-Geste |
|---|---|---|
| Links / rechts | ← / → | Linke/rechte Hand heben (wiederholt, alle 0,15 s; der erste Schritt kommt sofort) |
| Drehen | ↑ | Jump; T-Pose halten (wiederholt, alle 0,5 s; die erste Drehung kommt sofort) |
| Schneller fallen | ↓ (gedrückt halten) | – |
| Hard Drop | Leertaste | – |
| Hold | C | Squat |
| Neustart nach Game Over | R | – |

- Die Gesten laufen über den `KinectManager` (`KinectGestures`), Anmeldung in `KinectSteuerung.UserDetected`. Swipe wird nicht angemeldet. Im Tastatur-Modus melden sich die Steine nicht für Gesten an.
- **Timing (nur das wurde gegenüber der Originalsteuerung verbessert):** `KinectWrapper.Constants.PoseCompleteDuration` = 0,25 s (vorher 1,0 s), `KinectSteuerung.MoveRepeatTime` = 0,15 s (vorher 0,2), `RotateRepeatTimeTpose` = 0,5 s (vorher 0,7), getrennte Timer für Bewegen und Drehen, erster Schritt/erste Drehung sofort beim Auslösen.
- Jump (Hüfte 0,9–1,3 m, dann ≥ 0,15 m höher) und Squat (Hüfte ≤ 0,9 m, dann ≥ 0,15 m tiefer) nutzen absolute Höhen; sie hängen von `KinectManager.SensorHeight` (Aufstellhöhe der Kinect) ab. Das Debug-Menü zeigt die Hüfthöhe gegen diese Schwellen.
- **Debug-Menü (`KinectDebugOverlay.cs`):** **F5** zeigt Sensor/Spieler, Abstand, Hüfthöhe gegen Jump/Squat-Schwellen, Hand-Schulter-Abstände, T-Pose-Haltung, aktive Zustände, Timing, die letzten Gesten und FPS. **F6** schaltet die Kinect-Vorschau ein/aus (Tiefenbild des Spielers mit Skelett, vom `KinectManager` unten rechts gezeichnet; in `Tetris.unity` sind dafür `ComputeUserMap`, `DisplayUserMap`, `DisplaySkeletonLines` an). Im Kinect-Modus ist die Vorschau standardmäßig an, im Tastatur-Modus aus.

Im `KinectManager` ist `MinTimeBetweenGestures` = 0,3 s eingestellt.

## Audio / StreamingAssets

- Der `WAVMIDIPlayer` in `Tetris.unity` nutzt `Korobeiniki.mid` (Teil A + B, 120 BPM, per Code mit DryWetMIDI erzeugt) und `kinect_sample.wav`. Diese Namen sind in der Szene überschrieben; die Standardwerte im Prefab `Assets/WAVMIDIPlayer.prefab` (`wav_c1.wav`, `Tetris - Tetris Main Theme.mid`) gibt es nicht.
- Weitere Dateien: `Tetris Main Theme.mid` und `Tetris Main Theme_old.mid` (alte Versionen, enthalten falsche Töne) sowie `pianoc4.wav`.
- `MidiWavPlayer.PrepareSample()` schneidet die Aufnahme vor dem Abspielen auf den längsten lauten Abschnitt zu (entfernt Knacken/Stille am Anfang), normalisiert. `GetNoteClip()` baut dann pro Note einen Clip wie ein Sampler: Attack (Anfang der Aufnahme) → Sustain (Schleife `sustainLoopStart`/`sustainLoopLength` mit Überblendung) → Release (`releaseTime`). Länge = (Notendauer + Release) × Pitch, damit jede Note trotz Tonhöhe per Abspielgeschwindigkeit genau ihre MIDI-Länge klingt.
- Begleitung (`playBacking`, `backingVolume` = 0,2 für die Melodie, `bassVolume` = 0,4 für den Bass): Der `MidiWavPlayer` erzeugt zusätzlich synthetisch eine Rechteckwellen-Kopie der Melodie und eine Basslinie (Dreieckwelle, Grundtöne aus `bassRoots`, passend zur Akkordfolge von `Korobeiniki.mid`). Alles läuft auf derselben DSP-Uhr. Die originale Tetris-Aufnahme ist urheberrechtlich geschützt und wird bewusst nicht verwendet.
- Pitch-Formel: `2^((note - 69) / 12)`. Das Sample wird als A4 (MIDI-Note 69) behandelt.

## Wichtige Fallstricke

- **Kein `using UnityEditor;` in Skripten unter `Assets/Scripts`.** Im Editor fällt das nicht auf, aber der Player-Build bricht ab. Editor-Code gehört in einen `Editor/`-Ordner oder hinter `#if UNITY_EDITOR`.
- **Listener-Liste im KinectManager:** Der `KinectManager` geht mit `foreach` über `gestureListeners`. Ein Stein darf sich deshalb nicht synchron innerhalb eines Gesten-Callbacks abmelden; dafür gibt es `DeferredUnregister()`, das einen Frame wartet. In der Zwischenzeit schützt der Guard `if (!enabled || hasDropped) return true;` in `GestureCompleted` den bereits gelandeten Stein.
- **Hold:** `NewTetromino()` setzt `canHold = true`. Deshalb muss `canHold = false` *danach* gesetzt werden. `HoldPiece` deaktiviert den alten Stein sofort, weil `Destroy` erst am Ende des Frames greift.
- **Audio:** Niemals `pitch` einer AudioSource ändern, die gerade noch eine Note spielt. Den Voice-Pool im `MidiWavPlayer` verwenden.
- **Datei-Kodierung:** Einige Skripte sind in **Windows-1252** gespeichert, nicht in UTF-8 (z. B. `SpawnTetromino.cs`, `Steuerung.cs`, `GameManager.cs`). Beim Bearbeiten die Kodierung beibehalten, sonst gehen die Umlaute in den Kommentaren kaputt. Neue Dateien bitte als UTF-8 anlegen.
- Unity kompiliert erst neu, wenn das Editor-Fenster den Fokus bekommt. Compile-Fehler stehen in der Console bzw. in `%LOCALAPPDATA%\Unity\Editor\Editor.log`.

## Aktueller Stand (2026-11-05)

- `main` steht auf `f4db326`. Hinzugekommen sind Hauptmenü, Game-Over-Szene, Level und Fallgeschwindigkeit, Kinect-Gesten (Jump, Squat, T-Pose), Mikrofonaufnahme und MIDI-Loop.
- Branch **`fix/review-bugs`** (auf GitHub gepusht): Fixes aus einem Code-Review (Build-Fehler durch `using UnityEditor`, Doppel-Hold, Gesten auf gelandeten Steinen, schiefe Melodie).
- Branch **`feature/grafik-audio-spielerdaten`** (zweigt von `fix/review-bugs` ab, auf GitHub gepusht): **aktueller Arbeitsstand** und alternative Version zu `main`. Enthält Bevel-Grafik, Sampler-Audio mit Begleitung, Wall Kicks/Scoring, Spielerdaten aus dem Scan-Ordner, Neon-Menüs, FPS-Counter, Kinect/Tastatur-Umschalter, Dummy-Körper in den Steinen, großen Kopf im O-Stein, Kopf-Vorschau im Menü und Kinect-Debug-Menü. Test-Build liegt außerhalb des Repos unter `projektwoche/TestBuild`.
- **Low-Poly-Köpfe:** Die `PlayerHead.glb` in `Scans/Playermodell_0001`–`0004` wurden durch polygonreduzierte Versionen ersetzt (ca. 3.600–3.900 Vertices, 5.000 Dreiecke, Farbtextur + Normal-Map eingebettet; dazu je eine `PlayerHead_normal.png`). Erzeugt mit `ply_head_to_lowpoly_glb.py` (Blender-Skript im Ordner `Desktop/pipeline tool`). Die High-Poly-Originale liegen in `Desktop/putput`.
- Verlauf aller Claude-Code-Chats zu diesem Projekt: siehe **`PROMPT-PROTOKOLL.md`**. Bei neuen Arbeiten dort einen Eintrag ergänzen.
- Offene Punkte / Auffälligkeiten:
  - Die Doku mit Screenshot-Galerie wurde abgebrochen; in `docs/images/` liegen nur die exportierten Screenshots.
  - `Steuerung.cs` ist nur noch ein Überbleibsel an den Prefabs. Entfernen oder bewusst als reine Tastatur-Variante behalten.
  - Das Laden der WAV-Datei über `File.ReadAllBytes(streamingAssetsPath)` funktioniert nur auf Desktop-Plattformen.
  - Im Build speichert `KinectMicRecorder` lokal nach `persistentDataPath`, der `MidiWavPlayer` liest lokal aber aus `streamingAssetsPath`. Mit gewähltem Spieler ist das egal (beide nutzen `sample.wav` im Spieler-Ordner), ohne Netzwerk wird eine neue Aufnahme im Build aber nicht verwendet.
  - Im README genannte Ordner `/scans` und `/tools` (PyMeshLab-Pipeline) sind nicht im Repo.
