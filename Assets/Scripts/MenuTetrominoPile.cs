using System.Collections.Generic;
using UnityEngine;

// Hauptmenü-Deko: Ein Haufen echter Tetrominos (die Spiel-Prefabs), in deren Glaskästen die Köpfe
// aller Spieler aus dem Scan-Ordner stecken, bunt gemischt.
//
// Die Steine werden wie im Spiel auf ein Raster "fallen gelassen" (feste Zufallszahl, also immer derselbe Haufen).
// Die Spiel-Skripte der Prefabs (KinectSteuerung, Steuerung, CollisionBlock) werden entfernt, bevor sie starten:
// Die Steine werden deshalb unter einem inaktiven Objekt erzeugt, dort läuft kein Awake/Start.
public class MenuTetrominoPile : MonoBehaviour
{
    [Tooltip("Die sieben Spiel-Prefabs (ohne Ghost)")]
    public GameObject[] tetrominoPrefabs;
    [Tooltip("Spalten des Rasters (1 Zelle = 'cell' Einheiten)")]
    public int columns = 26;
    [Tooltip("Durchschnittliche Höhe des Haufens in Reihen")]
    public int averageRows = 3;
    [Tooltip("Höchste erlaubte Reihe")]
    public int maxRows = 6;
    public float cell = 4f;
    public int seed = 7;
    [Tooltip("An: aufgelockert mit Lücken und Überhängen. Aus: dichter Haufen ohne Lücken")]
    public bool loose = true;
    [Tooltip("Wenn kein Kopf-Scan geladen werden kann: Standardkopf aus Resources")]
    public string fallbackHead = "Blocks/PlayerHead";
    [Tooltip("Nur Köpfe der Spieler mit dieser Nummer (Ordnername endet auf die Zahl, z. B. Playermodell_0001) im Haufen zeigen")]
    public int minPlayerNumber = 1;
    [Tooltip("Höchste Spielernummer im Haufen (0 = alle)")]
    public int maxPlayerNumber = 4;

    private class Piece
    {
        public Transform box;                 // der Glaskasten
        public GameObject head;               // aktueller Kopf darin
    }

    private readonly List<Piece> pieces = new List<Piece>();
    private readonly List<PlayerDataManager.HeadAsset> heads = new List<PlayerDataManager.HeadAsset>();
    private System.Random rng;
    [Tooltip("Spielerkarte im Menü (Steine dahinter zählen nicht als sichtbar). Leer = Objekt 'PlayerCard' suchen")]
    public RectTransform cardRect;
    private float lastAspect;
    private float aspectChangedAt;
    private bool pendingReapply;

    void Start()
    {
        rng = new System.Random(seed);
        BuildPile();
        LoadHeads();
    }

    // Ändert sich das Seitenverhältnis (Fenstergröße), sind andere Steine im Bild: Köpfe neu verteilen,
    // damit unter den sichtbaren weiterhin jeder Kopf gleich oft vorkommt.
    void Update()
    {
        Camera cam = Camera.main;
        if (cam == null || heads.Count == 0) return;

        if (Mathf.Abs(cam.aspect - lastAspect) > 0.01f)
        {
            aspectChangedAt = Time.unscaledTime;
            lastAspect = cam.aspect;
            pendingReapply = true;
        }

        if (pendingReapply && Time.unscaledTime - aspectChangedAt > 0.4f)
        {
            pendingReapply = false;
            ApplyHeads();
        }
    }

    void OnDestroy()
    {
        foreach (var h in heads) h.Dispose();
        heads.Clear();
    }

    // ---------------- Haufen bauen ----------------

    void BuildPile()
    {
        if (tetrominoPrefabs == null || tetrominoPrefabs.Length == 0) return;

        var holder = new GameObject("PileHolder");
        holder.SetActive(false); // darunter laufen die Skripte der Prefabs nicht an
        holder.transform.SetParent(transform, false);

        int[] height = new int[columns];
        int filled = 0, target = columns * averageRows;
        int guard = 0;

        while (filled < target && guard++ < 400)
        {
            GameObject prefab = tetrominoPrefabs[rng.Next(tetrominoPrefabs.Length)];
            Vector2Int[] cells = ReadCells(prefab);
            if (cells.Length == 0) continue;

            if (loose)
            {
                // Aufgelockert: zufällige Drehung und Spalte, der Stein landet auf dem höchsten Punkt darunter (Lücken und Überhänge erlaubt)
                for (int attempt = 0; attempt < 40; attempt++)
                {
                    int rot = rng.Next(4);
                    Vector2Int[] r = Rotate(cells, rot, out int minX, out int minY, out int w, out int h);
                    int x0 = rng.Next(0, Mathf.Max(1, columns - w + 1));
                    if (x0 + w > columns) continue;

                    int y0 = 0;
                    foreach (var c in r) y0 = Mathf.Max(y0, height[x0 + c.x] - c.y);
                    if (y0 + h > maxRows) continue;

                    Spawn(holder.transform, prefab, new Vector2Int(x0 - minX, y0 - minY), rot);
                    foreach (var c in r) height[x0 + c.x] = Mathf.Max(height[x0 + c.x], y0 + c.y + 1);
                    filled += cells.Length;
                    break;
                }
                continue;
            }

            // Dicht: alle Drehungen und Spalten durchprobieren, möglichst ohne Lücken und niedrig, mit etwas Zufall
            float bestScore = float.MaxValue;
            int bestRot = -1, bestX = 0, bestY = 0, bestMinX = 0, bestMinY = 0;
            Vector2Int[] bestCells = null;

            for (int rot = 0; rot < 4; rot++)
            {
                Vector2Int[] r = Rotate(cells, rot, out int minX, out int minY, out int w, out int h);
                for (int x0 = 0; x0 + w <= columns; x0++)
                {
                    // Auflagehöhe: der Stein fällt, bis er auf dem Haufen aufliegt
                    int y0 = 0;
                    foreach (var c in r) y0 = Mathf.Max(y0, height[x0 + c.x] - c.y);
                    if (y0 + h > maxRows) continue;

                    // Lücken unter dem Stein
                    int gaps = 0;
                    foreach (var c in r)
                    {
                        bool lowest = true;
                        foreach (var o in r) if (o.x == c.x && o.y < c.y) { lowest = false; break; }
                        if (lowest) gaps += y0 + c.y - height[x0 + c.x];
                    }

                    float score = gaps * 40f + y0 * 1.5f + (float)rng.NextDouble() * 7f;
                    if (score < bestScore)
                    {
                        bestScore = score; bestRot = rot; bestX = x0; bestY = y0;
                        bestMinX = minX; bestMinY = minY; bestCells = r;
                    }
                }
            }

            if (bestCells == null) break; // nichts passt mehr

            // Position der Prefab-Wurzel im Raster
            Spawn(holder.transform, prefab, new Vector2Int(bestX - bestMinX, bestY - bestMinY), bestRot);
            foreach (var c in bestCells) height[bestX + c.x] = Mathf.Max(height[bestX + c.x], bestY + c.y + 1);
            filled += cells.Length;
        }

        // aus dem inaktiven Halter in den Haufen (dabei werden die Steine aktiv)
        var list = new List<Transform>();
        foreach (Transform t in holder.transform) list.Add(t);
        foreach (var t in list) t.SetParent(transform, true);
        Destroy(holder);
    }

    // Zellen eines Prefabs: alle Würfel (auch der Glaskasten) als Rasterpositionen
    Vector2Int[] ReadCells(GameObject prefab)
    {
        var cells = new List<Vector2Int>();
        foreach (Transform c in prefab.transform)
        {
            if (c.GetComponent<MeshRenderer>() == null) continue;
            cells.Add(new Vector2Int(Mathf.RoundToInt(c.localPosition.x / 4f), Mathf.RoundToInt(c.localPosition.y / 4f)));
        }
        return cells.ToArray();
    }

    // um 90° * rot drehen (gegen den Uhrzeigersinn), danach in die Ecke (0,0) schieben
    Vector2Int[] Rotate(Vector2Int[] cells, int rot, out int minX, out int minY, out int w, out int h)
    {
        var r = new Vector2Int[cells.Length];
        minX = int.MaxValue; minY = int.MaxValue;
        int maxX = int.MinValue, maxY = int.MinValue;
        for (int i = 0; i < cells.Length; i++)
        {
            Vector2Int c = cells[i];
            for (int k = 0; k < rot; k++) c = new Vector2Int(-c.y, c.x);
            r[i] = c;
            minX = Mathf.Min(minX, c.x); minY = Mathf.Min(minY, c.y);
            maxX = Mathf.Max(maxX, c.x); maxY = Mathf.Max(maxY, c.y);
        }
        for (int i = 0; i < r.Length; i++) r[i] -= new Vector2Int(minX, minY);
        w = maxX - minX + 1; h = maxY - minY + 1;
        return r;
    }

    void Spawn(Transform holder, GameObject prefab, Vector2Int root, int rot)
    {
        GameObject go = Instantiate(prefab, holder);
        go.name = prefab.name;

        // Spiellogik entfernen (läuft unter dem inaktiven Halter noch nicht an)
        foreach (var c in go.GetComponentsInChildren<MonoBehaviour>(true)) DestroyImmediate(c);
        foreach (var c in go.GetComponentsInChildren<Collider>(true)) DestroyImmediate(c);

        float left = -columns * cell * 0.5f + cell * 0.5f;
        go.transform.localPosition = new Vector3(left + root.x * cell, root.y * cell, 0f);
        go.transform.localRotation = Quaternion.Euler(0f, 0f, 90f * rot);
        go.transform.localScale = Vector3.one;

        Transform box = go.transform.Find("Glascube");
        if (box != null) pieces.Add(new Piece { box = box });
    }

    // ---------------- Köpfe ----------------

    async void LoadHeads()
    {
        var data = PlayerDataManager.Instance;

        // Nur die gewählten Spielernummern (Spielerauswahl im Menü bleibt davon unberührt)
        var players = new List<string>();
        foreach (string player in data.Players)
        {
            long n = PlayerDataManager.GetPlayerNumber(player);
            if (n >= minPlayerNumber && (maxPlayerNumber <= 0 || n <= maxPlayerNumber)) players.Add(player);
        }

        // Ohne Scans: Standardkopf in alle Kästen
        if (players.Count == 0)
        {
            ApplyHeads();
            return;
        }

        foreach (string player in players)
        {
            var asset = await data.LoadHeadAssetAsync(player);
            if (this == null) { asset?.Dispose(); return; } // Szene wurde inzwischen verlassen
            if (asset == null) continue;

            heads.Add(asset);
            ApplyHeads(); // zwischendurch schon zeigen, am Ende sind alle gemischt
        }

        if (heads.Count == 0) ApplyHeads(); // alle Scans fehlgeschlagen -> Standardkopf
    }

    // Bereich der Spielerkarte im Bild (0..1), leicht verkleinert. Steine dahinter sieht man nicht.
    Rect CardViewportRect()
    {
        if (cardRect == null)
        {
            var go = GameObject.Find("PlayerCard");
            if (go != null) cardRect = go.GetComponent<RectTransform>();
        }
        if (cardRect == null || Screen.width == 0 || Screen.height == 0) return new Rect(-1f, -1f, 0f, 0f);

        var c = new Vector3[4];
        cardRect.GetWorldCorners(c); // Overlay-Canvas: Weltkoordinaten = Pixel
        float x0 = c[0].x / Screen.width, y0 = c[0].y / Screen.height, x1 = c[2].x / Screen.width, y1 = c[2].y / Screen.height;
        // Unten etwas größer (Kästen an der Unterkante sind zur Hälfte verdeckt), oben und seitlich etwas kleiner
        return Rect.MinMaxRect(x0 + 0.012f, y0 - 0.02f, x1 - 0.012f, y1 - 0.012f);
    }

    // Verteilt die geladenen Köpfe gemischt auf die Glaskästen
    void ApplyHeads()
    {
        var data = PlayerDataManager.Instance;
        int count = Mathf.Max(1, heads.Count);
        var shuffle = new System.Random(seed + 1);

        // Zuerst die Steine, die im Bild zu sehen sind (von links nach rechts), danach der Rest.
        // Jeder Kopf kommt unter den sichtbaren Steinen gleich oft vor (bei 8 sichtbaren also genau 2x).
        var visible = new List<int>();
        var hidden = new List<int>();
        Camera cam = Camera.main;
        Rect card = CardViewportRect();
        for (int i = 0; i < pieces.Count; i++)
        {
            bool inView = true;
            if (cam != null && pieces[i].box != null)
            {
                // sichtbar = Mitte des Kastens liegt im Bild und nicht hinter der (fast undurchsichtigen) Spielerkarte
                Vector3 v = cam.WorldToViewportPoint(pieces[i].box.position);
                inView = v.z > 0f && v.x > 0f && v.x < 1f && v.y > 0f && v.y < 1f && !card.Contains(new Vector2(v.x, v.y));
            }
            (inView ? visible : hidden).Add(i);
        }
        visible.Sort((a, b) => pieces[a].box.position.x.CompareTo(pieces[b].box.position.x));

        var sequence = new List<int>();
        while (sequence.Count < pieces.Count)
        {
            var round = new List<int>();
            for (int i = 0; i < count; i++) round.Add(i);
            for (int i = round.Count - 1; i > 0; i--) { int j = shuffle.Next(i + 1); int t = round[i]; round[i] = round[j]; round[j] = t; }
            sequence.AddRange(round);
        }

        var order = new int[pieces.Count];
        int next = 0;
        foreach (int idx in visible) order[idx] = sequence[next++];
        foreach (int idx in hidden) order[idx] = sequence[next++];

        GameObject fallback = heads.Count == 0 ? Resources.Load<GameObject>(fallbackHead) : null;

        for (int i = 0; i < pieces.Count; i++)
        {
            var p = pieces[i];
            if (p.box == null) continue;
            if (p.head != null) Destroy(p.head);

            if (heads.Count > 0)
            {
                var asset = heads[order[i]];
                // aufrecht und zur Kamera: Basisdrehung = keine (im Spiel dreht der Kopf mit dem Stein)
                p.head = data.CreateHead(asset.Template, asset.Player, p.box, p.box.position, Quaternion.identity, p.box.lossyScale.x);
            }
            else if (fallback != null)
            {
                p.head = Instantiate(fallback, p.box.position, Quaternion.identity, p.box);
            }
        }
    }
}
