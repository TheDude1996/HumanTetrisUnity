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
        public string mode;     // "kinect" (Feld 9 breit) oder "keyboard" (Feld 11 breit); leer = alte Runde vor dem Umschalter
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

    public string FindHeadFile() => FindHeadFile(SelectedPlayer);

    public string FindHeadFile(string player)
    {
        string folder = string.IsNullOrEmpty(player) ? null : Path.Combine(ScansRoot, player);
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
            BuildHeadQuadrants(); // großer Kopf für den O-Stein, schon im Menü vorbereiten
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

    public void ApplyHeadLook() => ApplyHeadLook(HeadTemplate);

    public void ApplyHeadLook(GameObject template)
    {
        if (template == null) return;

        foreach (var r in template.GetComponentsInChildren<Renderer>(true))
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

    // Ein zusätzlich geladener Kopf (unabhängig vom gewählten Spieler), siehe LoadHeadAssetAsync
    public class HeadAsset
    {
        public string Player;
        public GameObject Template;
        public GLTFast.GltfImport Import;

        public void Dispose()
        {
            if (Template != null) UnityEngine.Object.Destroy(Template);
            Template = null;
            Import?.Dispose();
            Import = null;
        }
    }

    // Lädt den Kopf eines beliebigen Spielers (z.B. für die Köpfe im Hauptmenü). Der Aufrufer muss Dispose() aufrufen.
    // Die Vorlage gehört zur aktuellen Szene (kein DontDestroyOnLoad).
    public async Task<HeadAsset> LoadHeadAssetAsync(string player)
    {
        string file = FindHeadFile(player);
        if (file == null) return null;

        try
        {
            byte[] data = await Task.Run(() => File.ReadAllBytes(file));
            var gltf = new GLTFast.GltfImport();
            if (!await gltf.Load(data, new Uri(file)))
            {
                gltf.Dispose();
                return null;
            }

            var template = new GameObject("HeadTemplate " + player);
            template.SetActive(false);
            await gltf.InstantiateMainSceneAsync(template.transform);
            ApplyHeadLook(template);
            return new HeadAsset { Player = player, Template = template, Import = gltf };
        }
        catch (Exception e)
        {
            Debug.LogError("Fehler beim Laden des Kopfes " + file + ": " + e.Message);
            return null;
        }
    }

    private void ClearHead()
    {
        DestroyHeadQuadrants();
        if (HeadTemplate != null) Destroy(HeadTemplate);
        HeadTemplate = null;
        HeadLoading = false;

        // Achtung: Kopien in bereits liegenden Steinen verlieren damit ihre Meshes/Texturen.
        // Der Spieler wird aber nur im Menü gewechselt, dort gibt es keine Steine.
        headImport?.Dispose();
        headImport = null;
    }

    // ---------------- Großer Kopf (O-Stein: 2x2 Zellen) ----------------
    //
    // Der Kopf wird einmal auf BigHeadSize skaliert und in vier Viertel zerschnitten (nach Dreiecksmitte).
    // Jede Zelle des O-Steins bekommt ihr Viertel (BigHeadBlock). So wandert/verschwindet beim Line Break
    // nur der Teil der betroffenen Zelle, der Rest bleibt wie bei jedem anderen Stein erhalten.

    public const float BigHeadSize = 8f;  // 2 Zellen à 4 Einheiten: Breite/Höhe des Kopfes füllen das Quadrat
    public const float BigHeadDepth = 4f; // Tiefe wird auf die Zellentiefe gestaucht (der Kopf ist tiefer als breit)

    private GameObject[] headQuadrants;  // Index: 0 = unten links, 1 = unten rechts, 2 = oben links, 3 = oben rechts
    private readonly List<Mesh> quadrantMeshes = new List<Mesh>();

    // Vorlagen der vier Viertel (Meshes in Welteinheiten, Mitte des Kopfes im Ursprung); null, wenn kein Kopf geladen ist
    public GameObject[] GetHeadQuadrants()
    {
        if (headQuadrants == null) BuildHeadQuadrants();
        return headQuadrants;
    }

    private void DestroyHeadQuadrants()
    {
        if (headQuadrants != null)
            foreach (var q in headQuadrants) if (q != null) Destroy(q);
        headQuadrants = null;
        foreach (var m in quadrantMeshes) if (m != null) Destroy(m);
        quadrantMeshes.Clear();
    }

    private void BuildHeadQuadrants()
    {
        DestroyHeadQuadrants();
        if (HeadTemplate == null) return;

        // Kopie in Spielausrichtung, auf BigHeadSize skaliert, Mitte im Ursprung
        var temp = new GameObject("HeadQuadrantSource");
        GameObject copy = Instantiate(HeadTemplate, temp.transform);
        copy.transform.localPosition = Vector3.zero;
        copy.transform.rotation = Quaternion.Euler(config.headRotation);
        copy.transform.localScale = Vector3.one;
        copy.SetActive(true);

        // Breite/Höhe füllen das Quadrat (nicht die Tiefe, sonst bliebe das Gesicht zu klein), Mitte im Ursprung
        var renderers = copy.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) { Destroy(temp); return; }
        Bounds b = renderers[0].bounds;
        foreach (var rd in renderers) b.Encapsulate(rd.bounds);
        float largestXY = Mathf.Max(b.size.x, b.size.y);
        if (largestXY > 0) copy.transform.localScale *= BigHeadSize / largestXY;
        b = renderers[0].bounds;
        foreach (var rd in renderers) b.Encapsulate(rd.bounds);
        copy.transform.position += -b.center;
        float zScale = b.size.z > BigHeadDepth ? BigHeadDepth / b.size.z : 1f; // um z = 0 gestaucht (Mitte liegt im Ursprung)

        var roots = new GameObject[4];
        for (int i = 0; i < 4; i++)
        {
            roots[i] = new GameObject("HeadQuadrant " + i);
            roots[i].SetActive(false); // Vorlage
            DontDestroyOnLoad(roots[i]);
        }

        foreach (var mf in copy.GetComponentsInChildren<MeshFilter>())
        {
            Mesh src = mf.sharedMesh;
            var mr = mf.GetComponent<MeshRenderer>();
            if (src == null || mr == null) continue;
            if (!src.isReadable) { Debug.LogWarning("Kopf-Mesh nicht lesbar, großer Kopf nicht möglich."); continue; }

            Matrix4x4 toWorld = mf.transform.localToWorldMatrix;
            Vector3[] v = src.vertices;
            Vector3[] n = src.normals;
            Vector2[] uv = src.uv;
            Color32[] col = src.colors32;
            for (int i = 0; i < v.Length; i++)
            {
                v[i] = toWorld.MultiplyPoint3x4(v[i]);
                v[i].z *= zScale;
            }
            if (n.Length == v.Length) for (int i = 0; i < n.Length; i++) n[i] = toWorld.MultiplyVector(n[i]).normalized;

            int subCount = src.subMeshCount;
            var tris = new int[subCount][];
            for (int s = 0; s < subCount; s++) tris[s] = src.GetTriangles(s);

            for (int q = 0; q < 4; q++)
            {
                var map = new int[v.Length];
                for (int i = 0; i < map.Length; i++) map[i] = -1;
                var qv = new List<Vector3>(); var qn = new List<Vector3>(); var qu = new List<Vector2>(); var qc = new List<Color32>();
                var qt = new List<int>[subCount];

                for (int s = 0; s < subCount; s++)
                {
                    qt[s] = new List<int>();
                    int[] t = tris[s];
                    for (int i = 0; i < t.Length; i += 3)
                    {
                        Vector3 c = (v[t[i]] + v[t[i + 1]] + v[t[i + 2]]) / 3f;
                        int quadrant = (c.x >= 0 ? 1 : 0) + (c.y >= 0 ? 2 : 0);
                        if (quadrant != q) continue;

                        for (int k = 0; k < 3; k++)
                        {
                            int old = t[i + k];
                            if (map[old] < 0)
                            {
                                map[old] = qv.Count;
                                qv.Add(v[old]);
                                if (n.Length == v.Length) qn.Add(n[old]);
                                if (uv.Length == v.Length) qu.Add(uv[old]);
                                if (col.Length == v.Length) qc.Add(col[old]);
                            }
                            qt[s].Add(map[old]);
                        }
                    }
                }

                if (qv.Count == 0) continue;
                var mesh = new Mesh { name = "HeadQuadrant " + q, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                mesh.SetVertices(qv);
                if (qn.Count == qv.Count) mesh.SetNormals(qn);
                if (qu.Count == qv.Count) mesh.SetUVs(0, qu);
                if (qc.Count == qv.Count) mesh.SetColors(qc);
                mesh.subMeshCount = subCount;
                for (int s = 0; s < subCount; s++) mesh.SetTriangles(qt[s], s);
                mesh.RecalculateBounds();
                quadrantMeshes.Add(mesh);

                var part = new GameObject(mf.name);
                part.transform.SetParent(roots[q].transform, false);
                part.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = part.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = mr.sharedMaterials; // Material gehört dem Import, wie bei allen Kopien
            }
        }

        Destroy(temp);
        headQuadrants = roots;
    }

    // Kopie des Kopfes in einen Block setzen: um 'center' zentriert, auf headSize der Blockgröße skaliert.
    // 'baseRotation' = Drehung des Glaskastens (NICHT des Sockets: der ist im Prefab für den alten FBX-Kopf gedreht).
    public GameObject CreateHead(Transform parent, Vector3 center, Quaternion baseRotation, float blockSize)
        => CreateHead(HeadTemplate, SelectedPlayer, parent, center, baseRotation, blockSize);

    // Wie oben, aber mit einer beliebigen Vorlage (z.B. die Köpfe aller Spieler im Hauptmenü)
    public GameObject CreateHead(GameObject template, string player, Transform parent, Vector3 center, Quaternion baseRotation, float blockSize)
    {
        if (template == null) return null;

        GameObject head = Instantiate(template, parent);
        head.name = "PlayerHead " + player;
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
            mode = GameSettings.ModeId,
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

    // Beste Runde des gewählten Spielers im angegebenen Modus ("kinect"/"keyboard"). Alte Runden ohne Modus zählen als Tastatur (11 breit).
    public Round GetBest(string mode)
    {
        var highscores = GetPlayerHighscores();
        if (highscores == null || highscores.rounds == null) return null;

        Round best = null;
        foreach (var r in highscores.rounds)
        {
            string m = string.IsNullOrEmpty(r.mode) ? GameSettings.KeyboardId : r.mode;
            if (m == mode && (best == null || r.score > best.score)) best = r;
        }
        return best;
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
