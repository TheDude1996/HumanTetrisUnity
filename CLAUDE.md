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
- **`Steuerung.cs`**: Ältere Tastatur-Steuerung. Sie hängt zwar noch an den Prefabs, ist dort aber **deaktiviert**. Nicht mehr verwenden; `SpawnTetromino`, `HoldPreview` und `NextPiecePreview` arbeiten mit `KinectSteuerung`.
- **`SpawnTetromino.cs`** (auf `Spawner.prefab`): Warteschlange mit 3 kommenden Steinen, `NewTetromino()`, Hold-Logik (`HoldPiece`, `canHold`).
- **`GameManager.cs`**: Singleton (`GameManager.Instance`). Verwaltet Score (100/300/500/800 × Level; Soft Drop +1, Hard Drop +2 pro Reihe über `AddDropScore`), Level (alle `linesPerLevel` = 10 Linien eins höher) und die Fallzeit pro Level (`GetFallTime()`). `GameOver()` lädt Szene 2.
- **`NextPiecePreview.cs` / `HoldPreview.cs`**: Zeigen verkleinerte Kopien der Prefabs an, mit deaktivierter bzw. entfernter Steuerung.
- **`HeadSocket.cs`**: Die Klasse heißt **`CollisionBlock`**, also anders als die Datei. Setzt den Kopf des Spielers in den Glaskasten: bevorzugt den GLB-Kopf vom `PlayerDataManager`, sonst den Standardkopf aus `Resources/Blocks/...` (wird ausgetauscht, sobald der GLB-Kopf fertig geladen ist).
- **`PlayerDataManager.cs`**: Singleton über Szenen hinweg (`PlayerDataManager.Instance`, legt sich selbst an). Verbindung zum Netzwerkordner der Scan-Pipeline, siehe Abschnitt „Spielerdaten / Netzwerkordner“.
- **`WAVMIDIPlayer.cs`**: Die Klasse heißt **`MidiWavPlayer`**, also anders als die Datei. Lädt eine WAV- und eine MIDI-Datei aus `StreamingAssets` und spielt jede Note als gepitchtes Sample ab. Läuft über einen Pool von AudioSources mit `PlayScheduled` auf der `AudioSettings.dspTime`-Uhr, mit Lookahead-Scheduling in `Update()`. Optional als Endlosschleife (`loopMidi`).
- **`WAVUtility.cs`**: WAV-Bytes in einen `AudioClip` umwandeln (nur PCM).
- **`MainMenu.cs` / `GameOverMenu.cs`**: Button-Callbacks für die Menüs. `MainMenu.OpenGameFolder()` (Button „OPEN GAME FOLDER“ im Optionsmenü) öffnet den Spielordner im Explorer: im Build der Ordner mit der .exe, im Editor der Projektordner. Spielerauswahl im Hauptmenü: `PreviousPlayer`/`NextPlayer` (Buttons `<` `>`), `RefreshPlayers` (REFRESH), Anzeige in `playerLabel`.
- `Assets/KinectSDK/KinectScripts/NewMIC.cs` (Klasse `KinectMicRecorder`): Nimmt 2 s über das Kinect-Mikrofonarray auf (Button „Record 2s“ im Hauptmenü). Speichert im Editor nach `StreamingAssets/kinect_sample.wav`, im Build nach `persistentDataPath`, und sichert zusätzlich als `sample.wav` in den Ordner des gewählten Spielers.

## Spielerdaten / Netzwerkordner

- Pfad und Einstellungen stehen in **`HumanTetris_config.json` im Spielordner** (Build: neben der .exe; Editor: Projektordner, per `.gitignore` ausgeschlossen). Wird beim ersten Start mit Standardwerten angelegt. Pfade mit `/` oder `\\` schreiben (JSON).
  - `scansRoot` (Standard `Scans` = relativ zum Spielordner; absolute Pfade und Netzwerkpfade wie `//server/scans` gehen auch), `headFileName` (leer = erste .glb im Ordner), `headSize` (Anteil der Blockgröße, Standard 0,95; ab 1,0 ragt das Gesicht aus dem Glas), `headBrightness` (1,5) und `headSelfLight` (Eigenleuchten 1,0, setzt `_EMISSION`; REFRESH im Menü übernimmt Änderungen sofort), `headRotation` (Grad; Standard 0/180/0, weil die Blender-Exporte der Pipeline nach +Z schauen, die Kamera aber aus −Z), `leaderboardSize`.
- Aufbau (so liefert es die Pipeline): `<scansRoot>/Playermodell_0004/PlayerHead.glb` (Textur eingebettet, `.png` daneben wird nicht gebraucht) und `<scansRoot>/.pipeline_counter.json`. Das Spiel schreibt `sample.wav` und `highscore.json` in den Spieler-Ordner sowie `<scansRoot>/leaderboard.json` (globale Bestenliste). Spieler-Ordner = Ordnername endet auf eine Zahl; sortiert nach dieser Zahl, standardmäßig wird der neueste gewählt. Die Köpfe haben ca. 240.000 Vertices.
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
| Links / rechts | ← / → | Swipe links/rechts (einmal); linke/rechte Hand heben (wiederholt, alle 0,2 s) |
| Drehen | ↑ | Jump; T-Pose halten (wiederholt, alle 0,7 s) |
| Schneller fallen | ↓ (gedrückt halten) | – |
| Hard Drop | Leertaste | – |
| Hold | C | Squat |
| Neustart nach Game Over | R | – |

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

## Aktueller Stand (2026-11-02)

- `main` steht auf `f4db326`. Hinzugekommen sind Hauptmenü, Game-Over-Szene, Level und Fallgeschwindigkeit, Kinect-Gesten (Jump, Squat, T-Pose), Mikrofonaufnahme und MIDI-Loop.
- Branch **`fix/review-bugs`** (auf GitHub gepusht). Behebt aus einem Code-Review: Build-Fehler durch `using UnityEditor`, Doppel-Hold, Gesten auf gelandeten Steinen und schiefe Melodie (Voice-Pool mit `PlayScheduled`). Auf diesem Branch wird weitergearbeitet. Die Fixes sind **noch nicht im Play Mode getestet**.
- Offene Punkte / Auffälligkeiten:
  - `Steuerung.cs` ist nur noch ein Überbleibsel an den Prefabs. Entfernen oder bewusst als reine Tastatur-Variante behalten.
  - Das Laden der WAV-Datei über `File.ReadAllBytes(streamingAssetsPath)` funktioniert nur auf Desktop-Plattformen.
  - Im Build speichert `KinectMicRecorder` lokal nach `persistentDataPath`, der `MidiWavPlayer` liest lokal aber aus `streamingAssetsPath`. Mit gewähltem Spieler ist das egal (beide nutzen `sample.wav` im Spieler-Ordner), ohne Netzwerk wird eine neue Aufnahme im Build aber nicht verwendet.
  - Im README genannte Ordner `/scans` und `/tools` (PyMeshLab-Pipeline) sind nicht im Repo.
