using UnityEngine;
using UnityEngine.UI;

// Vorschau des Kopf-Scans des gewählten Spielers im Hauptmenü (Spielerauswahl).
// Eine eigene Kamera weit weg vom Rest der Szene rendert den Kopf in eine RenderTexture, die ein RawImage zeigt.
// Der Kopf pendelt langsam hin und her, damit man das Modell von mehreren Seiten sieht.
// Wechselt der Spieler, wird der Kopf neu geladen (PlayerDataManager.HeadChanged) und hier ausgetauscht.
public class PlayerPreview : MonoBehaviour
{
    [Tooltip("Bild, in dem die Vorschau angezeigt wird")]
    public RawImage target;
    [Tooltip("Größe des Kopfes in Welteinheiten")]
    public float headSize = 4f;
    [Tooltip("Maximale Drehung nach links/rechts in Grad")]
    public float swingAngle = 55f;
    [Tooltip("Geschwindigkeit des Hin- und Herdrehens")]
    public float swingSpeed = 0.8f;
    [Tooltip("Auflösung der Vorschau (quadratisch)")]
    public int resolution = 512;

    // weit weg vom Menü (Kamera (0,0,-40), Haufen um den Ursprung), damit sich nichts überschneidet
    private static readonly Vector3 Origin = new Vector3(5000f, 5000f, 0f);

    private Camera cam;
    private RenderTexture rt;
    private Transform rig;   // fest: Kamera hängt hier
    private Transform root;  // wird gedreht: Kopf hängt hier
    private GameObject head;

    void Start()
    {
        rt = new RenderTexture(resolution, resolution, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4, name = "PlayerPreviewRT" };

        // Kamera und Kopf dürfen nicht am selben Objekt hängen, sonst dreht sich die Kamera mit dem Kopf mit
        // und man sieht nur das Licht wandern, nicht das Modell
        rig = new GameObject("PlayerPreviewRig").transform;
        rig.position = Origin;

        root = new GameObject("PlayerPreviewRoot").transform;
        root.SetParent(rig, false);
        root.localPosition = Vector3.zero;

        var camGo = new GameObject("PlayerPreviewCamera");
        camGo.transform.SetParent(rig, false);
        camGo.transform.localPosition = new Vector3(0f, 0f, -12f);
        cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0f, 0f, 0f, 0f); // durchsichtig, der Kartenhintergrund scheint durch
        cam.fieldOfView = 25f;
        cam.nearClipPlane = 1f;
        cam.farClipPlane = 40f;
        cam.depth = -10;
        cam.allowHDR = false;
        cam.targetTexture = rt;

        if (target != null)
        {
            target.texture = rt;
            target.enabled = false;
        }

        var data = PlayerDataManager.Instance;
        data.HeadChanged += Rebuild;
        Rebuild();
    }

    void OnDestroy()
    {
        if (PlayerDataManager.Existing != null) PlayerDataManager.Existing.HeadChanged -= Rebuild;
        if (rt != null) rt.Release();
        if (rt != null) Destroy(rt);
        if (rig != null) Destroy(rig.gameObject);
    }

    void Rebuild()
    {
        if (this == null) return;
        if (head != null) Destroy(head);
        head = null;

        var data = PlayerDataManager.Instance;
        if (target != null) target.enabled = false;
        if (data.HeadTemplate == null) return; // kein Scan (oder lädt noch)

        root.rotation = Quaternion.identity;
        head = data.CreateHead(root, root.position, Quaternion.identity, headSize);
        if (target != null) target.enabled = head != null;
    }

    void Update()
    {
        if (root != null)
            root.rotation = Quaternion.Euler(0f, Mathf.Sin(Time.unscaledTime * swingSpeed) * swingAngle, 0f);
    }

    // Beim Spielerwechsel gibt der PlayerDataManager die Meshes/Materialien des alten Kopfes frei, bevor der neue geladen ist.
    // Den alten Kopf sofort ausblenden, damit nie ein Kopf ohne Material (magenta) gerendert wird.
    void LateUpdate()
    {
        if (head != null && PlayerDataManager.Existing != null && PlayerDataManager.Existing.HeadTemplate == null)
        {
            head.SetActive(false);
            Destroy(head);
            head = null;
            if (target != null) target.enabled = false;
        }
    }
}
