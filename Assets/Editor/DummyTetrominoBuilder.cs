using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Setzt das Dummy-Modell (Kevin Iglesias "Human Character Dummy") in die 7 Tetromino-Prefabs.
//
// Pro Stein wird der Dummy so posiert, dass er in die vier Zellen passt. Der Kopf des Dummys fehlt:
// er sitzt in der Zelle "Glascube", dort setzt CollisionBlock (HeadSocket) wie bisher den Kopf-Scan des Spielers.
// Die Pose wird in ein statisches Mesh gebacken und nach Zellen aufgeteilt (jede Zelle bekommt ihren Teil als
// Kind "DummyPart"). Das ist nötig, weil Zellen beim Landen/Linien löschen einzeln verschoben oder zerstört werden.
//
// Menü: Tools/Dummy in Tetrominos/...   (Pose-Werte unten in BuildPoses anpassen und neu bauen)
public static class DummyTetrominoBuilder
{
    const string DummyPrefab = "Assets/Kevin Iglesias/Human Character Dummy/Prefabs/HumanDummy_M White.prefab";
    const string MaterialDir = "Assets/Kevin Iglesias/Human Character Dummy/Materials/";
    const string PieceDir = "Assets/Blöcke/";
    const string MeshDir = "Assets/Blöcke/Dummy";
    const string PartName = "DummyPart";
    const string HeadCellName = "Glascube";

    enum Pick { ByX, ByY } // wie die zwei Arme/Beine den Rollen A/B zugeordnet werden: A = links (kleineres x) bzw. oben (größeres y)

    class Pose
    {
        public string piece;                 // "I", "J", ...
        public string material;              // Material im Dummy-Ordner
        public float k = 7f;                 // Skalierung des Dummys (Welteinheiten pro Meter)
        public Vector2 spineUp = Vector2.up; // Richtung Hüfte -> Hals
        public Pick pick = Pick.ByX;
        public float armScale = 1f, legScale = 1f;
        public bool headOnly = false;        // kein Dummy: der Kopf-Scan füllt den ganzen Stein (BigHeadBlock)
        public float headTilt = 0f;          // Drehung des Kopf-Scans um z, damit er zur Körperachse passt (liegende Posen)
        // Richtungen (x, y, z): z < 0 = zur Kamera
        public Vector3 armA_up, armA_fore, armB_up, armB_fore;
        public Vector3 legA_up, legA_lo, legB_up, legB_lo;
    }

    static List<Pose> BuildPoses()
    {
        Vector3 V(float x, float y, float z = 0f) => new Vector3(x, y, z);

        return new List<Pose>
        {
            // I: stehend, Arme locker neben dem Körper
            new Pose { piece = "I", material = "HumanDummy.mat", spineUp = Vector2.up, pick = Pick.ByX,
                armA_up = V(-0.3f, -1), armA_fore = V(-0.1f, -1), armB_up = V(0.3f, -1), armB_fore = V(0.1f, -1),
                legA_up = V(-0.1f, -1), legA_lo = V(-0.05f, -1), legB_up = V(0.1f, -1), legB_lo = V(0.05f, -1) },

            // J: liegt, Kopf links, Beine nach rechts, ein Arm nach oben (Zelle über dem Kopf)
            new Pose { piece = "J", material = "HumanDummy.mat", spineUp = new Vector2(-1, 0), pick = Pick.ByY, headTilt = 90f,
                armScale = 0.8f, legScale = 0.85f,
                armA_up = V(-0.5f, 1), armA_fore = V(-0.3f, 1), armB_up = V(1, -0.1f), armB_fore = V(1, -0.05f),
                legA_up = V(1, 0.1f, 0.2f), legA_lo = V(1, 0.05f, 0.2f), legB_up = V(1, -0.1f, -0.2f), legB_lo = V(1, -0.05f, -0.2f) },

            // L: Spiegelbild von J
            new Pose { piece = "L", material = "HumanDummy.mat", spineUp = new Vector2(1, 0), pick = Pick.ByY, headTilt = -90f,
                armScale = 0.8f, legScale = 0.85f,
                armA_up = V(0.5f, 1), armA_fore = V(0.3f, 1), armB_up = V(-1, -0.1f), armB_fore = V(-1, -0.05f),
                legA_up = V(-1, 0.1f, 0.2f), legA_lo = V(-1, 0.05f, 0.2f), legB_up = V(-1, -0.1f, -0.2f), legB_lo = V(-1, -0.05f, -0.2f) },

            // O: sitzt, Beine nach rechts, rechter Arm winkt in der Zelle oben rechts
            new Pose { piece = "O", material = "HumanDummy.mat", headOnly = true, spineUp = Vector2.up, pick = Pick.ByX,
                armScale = 0.85f, legScale = 0.85f,
                armA_up = V(-0.1f, -1), armA_fore = V(-0.05f, -1), armB_up = V(0.7f, 0.7f), armB_fore = V(0.5f, 1),
                legA_up = V(1, 0.05f, 0.25f), legA_lo = V(1, -0.05f, 0.25f), legB_up = V(1, 0.05f, -0.25f), legB_lo = V(1, -0.05f, -0.25f) },

            // S: sitzt, Beine nach rechts, linker Arm hoch (Zelle oben links)
            new Pose { piece = "S", material = "HumanDummy.mat", spineUp = Vector2.up, pick = Pick.ByX,
                armScale = 0.85f, legScale = 0.85f,
                armA_up = V(-0.7f, 0.7f), armA_fore = V(-0.5f, 1), armB_up = V(0.1f, -1), armB_fore = V(0.3f, -1),
                legA_up = V(1, 0.05f, 0.25f), legA_lo = V(1, -0.05f, 0.25f), legB_up = V(1, 0.05f, -0.25f), legB_lo = V(1, -0.05f, -0.25f) },

            // T: Arme waagerecht (T-Pose), Schneidersitz (Beine bleiben in der Zeile)
            new Pose { piece = "T", material = "HumanDummy.mat", spineUp = Vector2.up, pick = Pick.ByX,
                armScale = 0.85f, legScale = 0.8f,
                armA_up = V(-1, 0), armA_fore = V(-1, 0), armB_up = V(1, 0), armB_fore = V(1, 0),
                legA_up = V(-1, 0.1f, -0.45f), legA_lo = V(0.9f, 0.1f, -0.6f), legB_up = V(1, 0.1f, -0.45f), legB_lo = V(-0.9f, 0.1f, -0.6f) },

            // Z: sitzt, Beine nach links, rechter Arm hoch (Zelle oben rechts)
            new Pose { piece = "Z", material = "HumanDummy.mat", spineUp = Vector2.up, pick = Pick.ByX,
                armScale = 0.85f, legScale = 0.85f,
                armA_up = V(-0.1f, -1), armA_fore = V(-0.3f, -1), armB_up = V(0.7f, 0.7f), armB_fore = V(0.5f, 1),
                legA_up = V(-1, 0.05f, 0.25f), legA_lo = V(-1, -0.05f, 0.25f), legB_up = V(-1, 0.05f, -0.25f), legB_lo = V(-1, -0.05f, -0.25f) },
        };
    }

    // ------------------------------------------------------------------

    [MenuItem("Tools/Dummy in Tetrominos/Build all")]
    public static void BuildAll()
    {
        foreach (var pose in BuildPoses()) Build(pose);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Dummy in alle Tetromino-Prefabs gesetzt.");
    }

    public static void BuildOne(string piece)
    {
        var pose = BuildPoses().First(p => p.piece == piece);
        Build(pose);
        AssetDatabase.SaveAssets();
    }

    static void Build(Pose pose)
    {
        string prefabPath = PieceDir + pose.piece + "-Tetromino.prefab";
        GameObject contents = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            // alte Teile entfernen (Neubau)
            foreach (Transform cell in contents.transform)
            {
                var old = cell.Find(PartName);
                if (old != null) Object.DestroyImmediate(old.gameObject);
            }

            Transform[] cells = contents.transform.Cast<Transform>().ToArray();
            Transform headCell = cells.First(c => c.name == HeadCellName);

            // O-Stein: nur der Kopf-Scan, so groß wie das ganze Quadrat (BigHeadBlock); alle vier Zellen sind weiße Glaskästen
            if (pose.headOnly)
            {
                var whiteGlass = AssetDatabase.LoadAssetAtPath<Material>("Assets/Material/Glaskasten Bevel.mat");
                foreach (var c in cells) c.GetComponent<MeshRenderer>().sharedMaterial = whiteGlass;
                if (contents.GetComponent<BigHeadBlock>() == null) contents.AddComponent<BigHeadBlock>();
                PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
                return;
            }

            foreach (var socket in headCell.GetComponentsInChildren<CollisionBlock>(true))
                socket.tiltZ = pose.headTilt;

            // Körperzellen werden durchsichtige Kästen in der Farbe des Steins (sonst wäre der Dummy verdeckt)
            Material glass = MakeGlassMaterial(pose.piece, cells.First(c => c != headCell).GetComponent<MeshRenderer>().sharedMaterial);
            foreach (var c in cells)
                if (c != headCell) c.GetComponent<MeshRenderer>().sharedMaterial = glass;

            // Pose in einer Vorschau-Szene backen
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var dummy = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(DummyPrefab), scene);
                Vector3 headCenter = headCell.localPosition;
                ApplyPose(dummy, pose, headCenter);

                Mesh baked = new Mesh();
                var smr = dummy.GetComponentInChildren<SkinnedMeshRenderer>();
                smr.BakeMesh(baked, true);

                Material mat = MakeDummyMaterial(pose.piece, glass);
                SplitIntoCells(pose, smr, baked, cells, headCell, mat);
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }

            PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }
    }

    // Glaskasten-Material (wie der Kopfkasten) in der Farbe des Steins; 'source' = bisheriges Blockmaterial.
    // Ist 'source' schon ein Glasmaterial dieses Tools (beim Neubau), bleibt dessen Farbe erhalten.
    static Material MakeGlassMaterial(string piece, Material source)
    {
        Directory.CreateDirectory(MeshDir);
        string path = MeshDir + "/" + piece + "_Glas.mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) return existing;

        var glass = new Material(AssetDatabase.LoadAssetAtPath<Material>("Assets/Material/Glaskasten Bevel.mat"));
        glass.name = piece + "_Glas";
        Color c = source.GetColor("_Color");
        glass.SetColor("_Color", c);
        glass.SetFloat("_BevelAlpha", 0.9f);
        AssetDatabase.CreateAsset(glass, path);
        return glass;
    }

    // Material für den Dummy in genau der Farbe des Steins (gleicher Shader und gleiche Schattierung wie die Blöcke)
    static Material MakeDummyMaterial(string piece, Material glass)
    {
        Directory.CreateDirectory(MeshDir);
        string path = MeshDir + "/" + piece + "_Dummy.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(Shader.Find("HumanTetris/BevelBlock"));
            mat.name = piece + "_Dummy";
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.SetColor("_Color", glass.GetColor("_Color"));
        mat.SetFloat("_FaceShade", 1.2f);
        mat.SetFloat("_BevelShade", 1.2f);   // UVs des Dummys sind eine Farbpalette, keine Kanten -> überall gleich schattieren
        mat.SetFloat("_FaceAlpha", 1f);
        mat.SetFloat("_BevelAlpha", 1f);
        mat.SetFloat("_SrcBlend", 1f);       // undurchsichtig
        mat.SetFloat("_DstBlend", 0f);
        mat.SetFloat("_ZWrite", 1f);
        mat.renderQueue = 2000;
        EditorUtility.SetDirty(mat);
        return mat;
    }

    // ------------------------------------------------------------------ Pose

    static Transform Find(Transform root, string name)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == name) return t;
        Debug.LogError("Knochen nicht gefunden: " + name);
        return null;
    }

    // Dreht 'bone' so, dass die Richtung zu 'child' der Zielrichtung entspricht
    static void Aim(Transform bone, Transform child, Vector3 dir)
    {
        Vector3 cur = (child.position - bone.position).normalized;
        bone.rotation = Quaternion.FromToRotation(cur, dir.normalized) * bone.rotation;
    }

    static void ApplyPose(GameObject dummy, Pose p, Vector3 headCenter)
    {
        Transform root = dummy.transform;
        root.position = Vector3.zero;
        root.rotation = Quaternion.Euler(0, 180, 0); // Gesicht zur Kamera (-z)
        root.localScale = Vector3.one * p.k;

        Transform hips = Find(root, "B-hips"), neck = Find(root, "B-neck");

        // Körper ausrichten: Hüfte -> Hals zeigt in spineUp
        Aim(hips, neck, new Vector3(p.spineUp.x, p.spineUp.y, 0));

        // Rollen A/B den Seiten zuordnen
        Transform upL = Find(root, "B-upperArm.L"), upR = Find(root, "B-upperArm.R");
        Transform thL = Find(root, "B-thigh.L"), thR = Find(root, "B-thigh.R");
        bool armLIsA = p.pick == Pick.ByX ? upL.position.x < upR.position.x : upL.position.y > upR.position.y;
        bool legLIsA = p.pick == Pick.ByX ? thL.position.x < thR.position.x : thL.position.y > thR.position.y;

        Limb(root, armLIsA ? "L" : "R", "arm", p.armScale, p.armA_up, p.armA_fore);
        Limb(root, armLIsA ? "R" : "L", "arm", p.armScale, p.armB_up, p.armB_fore);
        Limb(root, legLIsA ? "L" : "R", "leg", p.legScale, p.legA_up, p.legA_lo);
        Limb(root, legLIsA ? "R" : "L", "leg", p.legScale, p.legB_up, p.legB_lo);

        // Hals auf die Unterkante der Kopfzelle legen (Kopf-Scan ist ca. 3,8 Einheiten groß)
        Vector3 neckTarget = headCenter - new Vector3(p.spineUp.x, p.spineUp.y, 0) * 1.6f;
        root.position += neckTarget - neck.position;
    }

    static void Limb(Transform root, string side, string kind, float scale, Vector3 upDir, Vector3 lowDir)
    {
        Transform upper, lower, end;
        if (kind == "arm")
        {
            upper = Find(root, "B-upperArm." + side); lower = Find(root, "B-forearm." + side); end = Find(root, "B-hand." + side);
        }
        else
        {
            upper = Find(root, "B-thigh." + side); lower = Find(root, "B-shin." + side); end = Find(root, "B-foot." + side);
        }
        upper.localScale = Vector3.one * scale; // verkürzt die ganze Gliedmaße
        Aim(upper, lower, upDir);
        Aim(lower, end, lowDir);
    }

    // ------------------------------------------------------------------ Mesh aufteilen

    static void SplitIntoCells(Pose pose, SkinnedMeshRenderer smr, Mesh baked, Transform[] cells, Transform headCell, Material mat)
    {
        Directory.CreateDirectory(MeshDir);

        // Vertex-Gewicht des Kopfes (der Kopf wird durch den Scan ersetzt)
        BoneWeight[] weights = smr.sharedMesh.boneWeights;
        Transform[] bones = smr.bones;
        float HeadWeight(int v)
        {
            BoneWeight w = weights[v];
            float sum = 0;
            void Add(int index, float weight) { if (weight > 0 && (bones[index].name == "B-head" || bones[index].name == "B-jaw")) sum += weight; }
            Add(w.boneIndex0, w.weight0); Add(w.boneIndex1, w.weight1); Add(w.boneIndex2, w.weight2); Add(w.boneIndex3, w.weight3);
            return sum;
        }

        Matrix4x4 toWorld = Matrix4x4.TRS(smr.transform.position, smr.transform.rotation, smr.transform.lossyScale); // BakeMesh liefert die Vertices ohne Skalierung
        Vector3[] verts = baked.vertices.Select(v => toWorld.MultiplyPoint3x4(v)).ToArray();
        Vector3[] normals = baked.normals.Select(n => toWorld.MultiplyVector(n)).ToArray();
        Vector2[] uvs = baked.uv;
        int[] tris = baked.triangles;

        var perCell = cells.Select(_ => new List<int>()).ToArray();
        for (int i = 0; i < tris.Length; i += 3)
        {
            int a = tris[i], b = tris[i + 1], c = tris[i + 2];
            if ((HeadWeight(a) + HeadWeight(b) + HeadWeight(c)) / 3f > 0.5f) continue; // Kopf weglassen

            Vector3 centroid = (verts[a] + verts[b] + verts[c]) / 3f;
            int best = 0;
            float bestDist = float.MaxValue;
            for (int ci = 0; ci < cells.Length; ci++)
            {
                Vector3 d = centroid - cells[ci].localPosition;
                d.z = 0;
                if (d.sqrMagnitude < bestDist) { bestDist = d.sqrMagnitude; best = ci; }
            }
            perCell[best].AddRange(new[] { a, b, c });
        }

        for (int ci = 0; ci < cells.Length; ci++)
        {
            if (perCell[ci].Count == 0) continue;
            Transform cell = cells[ci];

            var remap = new Dictionary<int, int>();
            var mv = new List<Vector3>(); var mn = new List<Vector3>(); var mu = new List<Vector2>(); var mt = new List<int>();
            foreach (int index in perCell[ci])
            {
                if (!remap.TryGetValue(index, out int n))
                {
                    n = mv.Count;
                    remap[index] = n;
                    mv.Add(verts[index] - cell.localPosition); // relativ zur Zellenmitte
                    mn.Add(normals[index]);
                    mu.Add(uvs[index]);
                }
                mt.Add(n);
            }

            var mesh = new Mesh { name = pose.piece + "_" + cell.name.Replace(" ", "").Replace("(", "").Replace(")", "") };
            mesh.indexFormat = mv.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.SetVertices(mv); mesh.SetNormals(mn); mesh.SetUVs(0, mu); mesh.SetTriangles(mt, 0);
            mesh.RecalculateBounds();

            string meshPath = MeshDir + "/" + mesh.name + ".asset";
            AssetDatabase.DeleteAsset(meshPath);
            AssetDatabase.CreateAsset(mesh, meshPath);

            // Zellen sind mit Faktor 4 skaliert -> Kind mit 1/4, damit die Mesh-Koordinaten Welteinheiten bleiben
            var part = new GameObject(PartName);
            part.transform.SetParent(cell, false);
            part.transform.localPosition = Vector3.zero;
            part.transform.localRotation = Quaternion.identity;
            part.transform.localScale = Vector3.one / cell.localScale.x;
            part.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = part.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }
    }

    // ------------------------------------------------------------------ Vorschau (Test)

    // Rendert die Prefabs der Steine als PNG, um die Posen zu prüfen.
    public static void RenderPreviews(string outDir)
    {
        Directory.CreateDirectory(outDir);
        foreach (var pose in BuildPoses())
        {
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PieceDir + pose.piece + "-Tetromino.prefab");
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);

                var camGo = new GameObject("cam");
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(camGo, scene);
                var cam = camGo.AddComponent<Camera>();
                cam.scene = scene;
                cam.orthographic = true;
                cam.orthographicSize = 11;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.03f, 0.03f, 0.07f);
                cam.transform.position = new Vector3(2, 4, -40);
                cam.nearClipPlane = 0.1f; cam.farClipPlane = 100;

                var lightGo = new GameObject("light");
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(lightGo, scene);
                lightGo.AddComponent<Light>().type = LightType.Directional;
                lightGo.transform.rotation = Quaternion.Euler(30, -20, 0);

                var rt = new RenderTexture(700, 700, 24);
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(700, 700, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, 700, 700), 0, 0);
                tex.Apply();
                File.WriteAllBytes(Path.Combine(outDir, pose.piece + ".png"), tex.EncodeToPNG());
                RenderTexture.active = null;
                Object.DestroyImmediate(tex); rt.Release(); Object.DestroyImmediate(rt);
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }
    }

    [MenuItem("Tools/Dummy in Tetrominos/Render previews to Temp")]
    static void RenderPreviewsMenu() => RenderPreviews(Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Temp", "DummyPreviews"));
}
