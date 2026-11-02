using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

// Verbindung zum Netzwerkordner der Scan-Pipeline.
//
// Aufbau des Netzwerkordners (Pfad steht in HumanTetris_config.json im Spielordner):
//   <scansRoot>\Playermodell_0004\PlayerHead.glb Kopf-Scan des Spielers (von der Pipeline; erste .glb im Ordner)
//   <scansRoot>\Playermodell_0004\sample.wav     Sprachaufnahme des Spielers (vom Spiel zurückgesichert)
//   <scansRoot>\Playermodell_0004\highscore.json alle Runden + bester Score des Spielers (vom Spiel)
//   <scansRoot>\leaderboard.json                 globale Bestenliste über alle Spieler (vom Spiel)
// Spieler-Ordner = jeder Ordner, dessen Name auf eine Zahl endet ("Playermodell_0004", "0004", ...).
//
// Die Klasse lebt über Szenenwechsel hinweg (DontDestroyOnLoad) und legt sich bei Bedarf selbst an,
// damit auch ein direkter Start der Tetris-Szene im Editor funktioniert.
public class PlayerDataManager : MonoBehaviour
{
    public const string ConfigFileName = "HumanTetris_config.json";
    public const string SampleFileName = "sample.wav";
    public const string HighscoreFileName = "highscore.json";
    public const string LeaderboardFileName = "leaderboard.json";

    [Serializable]
    public class Config
    {
        [Tooltip("Ordner mit den nummerierten Spieler-Ordnern. Relativ (z.B. \"Scans\") = neben der .exe bzw. im Projektordner; absolut oder Netzwerkpfad (//server/scans) geht auch.")]
        public string scansRoot = "Scans";
        [Tooltip("Optional: fester Dateiname des Kopf-Scans. Leer = die erste .glb im Spieler-Ordner")]
        public string headFileName = "";
        [Tooltip("Größe des Kopfes im Block (Anteil der Blockgröße, 1 = füllt den Block)")]
        public float headSize = 0.95f; // 1.0 = Gesicht berührt die Glasfläche (Pipeline normiert die Kopftiefe)
        [Tooltip("Zusätzliche Drehung des Kopfes in Grad (falls die Scans in eine andere Richtung schauen)")]
        public Vector3 headRotation = new Vector3(0, 180, 0); // Blender-Export der Pipeline schaut nach +Z, Kamera schaut aus -Z
        [Tooltip("Helligkeit der Kopf-Textur (1 = Original, 1.5 = 50 % heller)")]
        public float headBrightness = 1.5f;
        [Tooltip("Eigenleuchten des Kopfes, unabhängig vom Szenenlicht (0 = aus, 0.5 = halb so hell wie die Textur)")]
        public float headSelfLight = 1f;
        [Tooltip("Anzahl Einträge in der globalen Bestenliste")]
        public int leaderboardSize = 50;
    }

    [Serializable]
    public class Round
    {
        public string player;   // Spieler-Ordner, z.B. "Playermodell_0004"
        public int score;
        public int level;
        public int lines;
        public string date;
    }

    [Serializable]
    public class PlayerHighscores
    {
        public string player;
        public Round best;
        public List<Round> rounds = new List<Round>();
    }

    [Serializable]
    public class Leaderboard
    {
        public List<Round> entries = new List<Round>();
    }

    // ------------------------------------------------------------------

    private static PlayerDataManager instance;

    public static PlayerDataManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindFirstObjectByType<PlayerDataManager>();
                if (instance == null)
                {
                    var go = new GameObject("PlayerDataManager");
                    instance = go.AddComponent<PlayerDataManager>();
                }
            }
            return instance;
        }
    }

    // Vorhandene Instanz ohne sie neu anzulegen (z.B. für OnDestroy beim Beenden)
    public static PlayerDataManager Existing => instance;

    public Config config = new Config();

    // Alle gefundenen Spieler-Ordner (Name endet auf eine Zahl), aufsteigend nach dieser Zahl sortiert
    public List<string> Players { get; private set; } = new List<string>();
    public string SelectedPlayer { get; private set; }
    public string LastError { get; private set; }

    // Geladener Kopf als unsichtbare Vorlage; Blöcke erzeugen davon Kopien
    public GameObject HeadTemplate { get; private set; }
    public bool HeadLoading { get; private set; }
    public event Action HeadChanged;

    private int headLoadVersion;
    private static readonly object fileLock = new object();
    private GLTFast.GltfImport headImport; // hält Meshes/Texturen des Kopfes, solange er benutzt wird

    public static string GameFolder => Directory.GetParent(Application.dataPath).FullName;
    public static string ConfigPath => Path.Combine(GameFolder, ConfigFileName);

    // Aufgelöster Scan-Ordner: relative Pfade gelten ab dem Spielordner (neben der .exe / Projektordner)
    public string ScansRoot
    {
        get
        {
            string root = string.IsNullOrWhiteSpace(config.scansRoot) ? "Scans" : config.scansRoot;
            return Path.IsPathRooted(root) ? root : Path.GetFullPath(Path.Combine(GameFolder, root));
        }
    }

    public string SelectedFolder =>
        string.IsNullOrEmpty(SelectedPlayer) ? null : Path.Combine(ScansRoot, SelectedPlayer);

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);

        LoadConfig();
        RefreshPlayers();
        if (Players.Count > 0) SelectPlayer(Players[Players.Count - 1]); // standardmäßig der neueste
    }

    // ---------------- Konfiguration ----------------

    public void LoadConfig()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                JsonUtility.FromJsonOverwrite(File.ReadAllText(ConfigPath), config);
                Debug.Log("Konfiguration geladen: " + ConfigPath);
            }
            else
            {
                // Vorlage anlegen, damit man den Pfad nur noch eintragen muss
                File.WriteAllText(ConfigPath, JsonUtility.ToJson(config, true));
                Debug.Log("Konfiguration angelegt (bitte scansRoot eintragen): " + ConfigPath);
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("Konfiguration konnte nicht gelesen/angelegt werden: " + e.Message);
        }
    }

    // ---------------- Spieler-Ordner ----------------

    public void RefreshPlayers()
    {
        LastError = null;
        try
        {
            if (!Directory.Exists(ScansRoot))
            {
                Players = new List<string>();
                LastError = "Scan-Ordner nicht erreichbar: " + ScansRoot;
                Debug.LogWarning(LastError);
                return;
            }

            Players = Directory.GetDirectories(ScansRoot)
                .Select(Path.GetFileName)
                .Where(n => GetPlayerNumber(n) >= 0)
                .OrderBy(GetPlayerNumber)
                .ThenBy(n => n)
                .ToList();
        }
        catch (Exception e)
        {
            Players = new List<string>();
            LastError = "Fehler beim Lesen von " + ScansRoot + ": " + e.Message;
            Debug.LogWarning(LastError);
        }
    }

    // Laufende Nummer am Ende des Ordnernamens ("Playermodell_0004" -> 4), -1 wenn keine
    public static long GetPlayerNumber(string folderName)
    {
        var match = System.Text.RegularExpressions.Regex.Match(folderName ?? "", @"(\d+)$");
        return match.Success && long.TryParse(match.Groups[1].Value, out long number) ? number : -1;
    }

    // Anzeige-Nummer mit führenden Nullen, wie im Ordnernamen ("Playermodell_0004" -> "0004")
    public static string GetPlayerNumberText(string folderName)
    {
        var match = System.Text.RegularExpressions.Regex.Match(folderName ?? "", @"(\d+)$");
        return match.Success ? match.Groups[1].Value : folderName;
    }

    public void SelectPlayer(string player)
    {
        if (player == SelectedPlayer && (HeadTemplate != null || HeadLoading)) return;
        SelectedPlayer = player;
        _ = LoadHeadAsync();
    }

    // Blättern im Menü: +1 = nächster, -1 = vorheriger (mit Umlauf)
    public void SelectRelative(int step)
    {
        if (Players.Count == 0) return;
        int index = Players.IndexOf(SelectedPlayer);
        if (index < 0) index = Players.Count - 1;
        index = ((index + step) % Players.Count + Players.Count) % Players.Count;
        SelectPlayer(Players[index]);
    }

    // ---------------- Kopf laden (GLB über glTFast) ----------------

    public string FindHeadFile()
    {
        string folder = SelectedFolder;
        if (folder == null || !Directory.Exists(folder)) return null;

        if (!string.IsNullOrEmpty(config.headFileName))
        {
            string preferred = Path.Combine(folder, config.headFileName);
            if (File.Exists(preferred)) return preferred;
        }

        return Directory.GetFiles(folder, "*.glb").OrderBy(f => f).FirstOrDefault();
    }

    private async Task LoadHeadAsync()
    {
        int version = ++headLoadVersion;
        ClearHead();

        string file = FindHeadFile();
        if (file == null)
        {
            Debug.LogWarning("Kein Kopf-Scan (.glb) gefunden für Spieler " + SelectedPlayer + ", nutze Standardkopf.");
            HeadChanged?.Invoke();
            return;
        }

        HeadLoading = true;
        try
        {
            // Netzwerkzugriff im Hintergrund, damit das Menü nicht hängt
            byte[] data = await Task.Run(() => File.ReadAllBytes(file));
            if (version != headLoadVersion) return; // inzwischen anderer Spieler gewählt

            var gltf = new GLTFast.GltfImport();
            bool ok = await gltf.Load(data, new Uri(file));
            if (version != headLoadVersion) { gltf.Dispose(); return; }
            if (!ok)
            {
                Debug.LogError("GLB konnte nicht gelesen werden: " + file);
                gltf.Dispose();
                return;
            }

            var template = new GameObject("HeadTemplate " + SelectedPlayer);
            template.SetActive(false); // unsichtbare Vorlage
            DontDestroyOnLoad(template);
            await gltf.InstantiateMainSceneAsync(template.transform);
            if (version != headLoadVersion) { Destroy(template); gltf.Dispose(); return; }

            HeadTemplate = template;
            headImport = gltf;
            ApplyHeadLook();
            Debug.Log("Kopf geladen: " + file);
        }
        catch (Exception e)
        {
            Debug.LogError("Fehler beim Laden des Kopfes " + file + ": " + e.Message);
        }
        finally
        {
            if (version == headLoadVersion)
            {
                HeadLoading = false;
                HeadChanged?.Invoke();
            }
        }
    }

    // Helligkeit und Eigenleuchten aus der Konfiguration auf die Materialien des Kopfes anwenden.
    // Die Materialien gehören dem Import und werden von allen Kopien geteilt -> wirkt sofort überall.
    private static readonly int BaseColorFactor = Shader.PropertyToID("baseColorFactor");
    private static readonly int BaseColorTexture = Shader.PropertyToID("baseColorTexture");
    private static readonly int EmissiveFactor = Shader.PropertyToID("emissiveFactor");
    private static readonly int EmissiveTexture = Shader.PropertyToID("emissiveTexture");

    public void ApplyHeadLook()
    {
        if (HeadTemplate == null) return;

        foreach (var r in HeadTemplate.GetComponentsInChildren<Renderer>(true))
        {
            foreach (var m in r.sharedMaterials)
            {
                if (m == null) continue;

                if (m.HasProperty(BaseColorFactor))
                {
                    float b = Mathf.Max(0f, config.headBrightness);
                    m.SetColor(BaseColorFactor, new Color(b, b, b, m.GetColor(BaseColorFactor).a));
                }

                if (m.HasProperty(EmissiveFactor) && m.HasProperty(EmissiveTexture))
                {
                    float e = Mathf.Max(0f, config.headSelfLight);
                    if (e > 0f)
                    {
                        // Textur selbst leuchten lassen: Emission = Textur × Faktor
                        if (m.HasProperty(BaseColorTexture))
                            m.SetTexture(EmissiveTexture, m.GetTexture(BaseColorTexture));
                        m.SetColor(EmissiveFactor, new Color(e, e, e, 1f));
                        m.EnableKeyword("_EMISSION");
                    }
                    else
                    {
                        m.SetColor(EmissiveFactor, Color.black);
                        m.DisableKeyword("_EMISSION");
                    }
                }
            }
        }
    }

    private void ClearHead()
    {
        if (HeadTemplate != null) Destroy(HeadTemplate);
        HeadTemplate = null;
        HeadLoading = false;

        // Achtung: Kopien in bereits liegenden Steinen verlieren damit ihre Meshes/Texturen.
        // Der Spieler wird aber nur im Menü gewechselt, dort gibt es keine Steine.
        headImport?.Dispose();
        headImport = null;
    }

    // Kopie des Kopfes in einen Block setzen: um 'center' zentriert, auf headSize der Blockgröße skaliert.
    // 'baseRotation' = Drehung des Glaskastens (NICHT des Sockets: der ist im Prefab für den alten FBX-Kopf gedreht).
    public GameObject CreateHead(Transform parent, Vector3 center, Quaternion baseRotation, float blockSize)
    {
        if (HeadTemplate == null) return null;

        GameObject head = Instantiate(HeadTemplate, parent);
        head.name = "PlayerHead " + SelectedPlayer;
        head.transform.localPosition = Vector3.zero;
        head.transform.rotation = baseRotation * Quaternion.Euler(config.headRotation);
        head.transform.localScale = Vector3.one;
        head.SetActive(true);

        FitInto(head, center, blockSize * config.headSize);
        return head;
    }

    private static void FitInto(GameObject head, Vector3 center, float targetSize)
    {
        var renderers = head.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return;

        Bounds b = renderers[0].bounds;
        foreach (var r in renderers) b.Encapsulate(r.bounds);

        float largest = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
        if (largest > 0)
            head.transform.localScale *= targetSize / largest;

        // Nach dem Skalieren neu messen und mittig ausrichten
        b = renderers[0].bounds;
        foreach (var r in renderers) b.Encapsulate(r.bounds);
        head.transform.position += center - b.center;
    }

    // ---------------- Sample (Sprachaufnahme) ----------------

    // Pfad der Aufnahme im Spieler-Ordner (null, wenn kein Spieler gewählt / Ordner nicht erreichbar)
    public string SelectedSamplePath
    {
        get
        {
            string folder = SelectedFolder;
            return folder != null && Directory.Exists(folder) ? Path.Combine(folder, SampleFileName) : null;
        }
    }

    public bool SaveSample(byte[] wavBytes)
    {
        string path = SelectedSamplePath;
        if (path == null) return false;
        try
        {
            WriteAtomic(path, wavBytes);
            Debug.Log("Sample gesichert: " + path);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogWarning("Sample konnte nicht gesichert werden: " + e.Message);
            return false;
        }
    }

    // ---------------- Highscores ----------------

    public void SaveRound(int score, int level, int lines)
    {
        if (string.IsNullOrEmpty(SelectedPlayer)) return;

        var round = new Round
        {
            player = SelectedPlayer,
            score = score,
            level = level,
            lines = lines,
            date = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
        };

        // Netzwerk-Schreibzugriffe im Hintergrund, damit der Szenenwechsel nicht hängt
        string folder = SelectedFolder;
        string root = ScansRoot;
        int size = Mathf.Max(1, config.leaderboardSize);

        Task.Run(() =>
        {
            // Speichervorgänge nacheinander ausführen, sonst überschreiben sie sich gegenseitig
            lock (fileLock)
            try
            {
                // 1) Spieler-Datei
                string playerFile = Path.Combine(folder, HighscoreFileName);
                var data = ReadJson<PlayerHighscores>(playerFile) ?? new PlayerHighscores();
                data.player = round.player;
                data.rounds.Add(round);
                if (data.best == null || round.score > data.best.score) data.best = round;
                WriteAtomic(playerFile, System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(data, true)));

                // 2) Globale Bestenliste
                string boardFile = Path.Combine(root, LeaderboardFileName);
                var board = ReadJson<Leaderboard>(boardFile) ?? new Leaderboard();
                board.entries.Add(round);
                board.entries = board.entries.OrderByDescending(e => e.score).Take(size).ToList();
                WriteAtomic(boardFile, System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(board, true)));

                Debug.Log("Runde gesichert: Spieler " + round.player + ", " + round.score + " Punkte");
            }
            catch (Exception e)
            {
                Debug.LogWarning("Highscore konnte nicht gesichert werden: " + e.Message);
            }
        });
    }

    public PlayerHighscores GetPlayerHighscores()
    {
        if (SelectedFolder == null) return null;
        return ReadJson<PlayerHighscores>(Path.Combine(SelectedFolder, HighscoreFileName));
    }

    public Leaderboard GetLeaderboard()
    {
        return ReadJson<Leaderboard>(Path.Combine(ScansRoot, LeaderboardFileName));
    }

    // ---------------- Hilfsfunktionen ----------------

    private static T ReadJson<T>(string path) where T : class
    {
        try
        {
            return File.Exists(path) ? JsonUtility.FromJson<T>(File.ReadAllText(path)) : null;
        }
        catch (Exception e)
        {
            Debug.LogWarning("Konnte " + path + " nicht lesen: " + e.Message);
            return null;
        }
    }

    // Erst in eine temporäre Datei schreiben, dann ersetzen -> keine halb geschriebenen Dateien im Netzwerk
    private static void WriteAtomic(string path, byte[] bytes)
    {
        string tmp = path + ".tmp";
        File.WriteAllBytes(tmp, bytes);
        if (File.Exists(path)) File.Replace(tmp, path, null);
        else File.Move(tmp, path);
    }
}
