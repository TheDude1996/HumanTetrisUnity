using UnityEngine;

// Debug-FPS-Anzeige oben rechts. Legt sich beim Start selbst an und bleibt über Szenenwechsel erhalten.
// F3 blendet sie ein/aus. Standardmäßig an im Editor und in Development-Builds, sonst aus (F3 schaltet sie trotzdem ein).
public class FpsCounter : MonoBehaviour
{
    public KeyCode toggleKey = KeyCode.F3;
    [Tooltip("Wie oft die Anzeige aktualisiert wird (Sekunden)")]
    public float interval = 0.5f;

    private bool visible;
    private float timeLeft;
    private int frames;
    private float accumulated;
    private float worstFrame;
    private float fps;
    private float avgMs;
    private float worstMs;
    private GUIStyle style;
    private GUIStyle boxStyle;
    private Texture2D bg;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Create()
    {
        if (FindFirstObjectByType<FpsCounter>() != null) return;
        var go = new GameObject("FpsCounter");
        DontDestroyOnLoad(go);
        go.AddComponent<FpsCounter>();
    }

    void Awake()
    {
        visible = Application.isEditor || Debug.isDebugBuild;
        timeLeft = interval;
    }

    void Update()
    {
        if (Input.GetKeyDown(toggleKey)) visible = !visible;
        if (!visible) return;

        float dt = Time.unscaledDeltaTime;
        frames++;
        accumulated += dt;
        worstFrame = Mathf.Max(worstFrame, dt);
        timeLeft -= dt;

        if (timeLeft <= 0f)
        {
            fps = frames / Mathf.Max(0.0001f, accumulated);
            avgMs = accumulated / Mathf.Max(1, frames) * 1000f;
            worstMs = worstFrame * 1000f;
            frames = 0; accumulated = 0f; worstFrame = 0f; timeLeft = interval;
        }
    }

    void OnGUI()
    {
        if (!visible) return;

        if (style == null)
        {
            bg = new Texture2D(1, 1);
            bg.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.6f));
            bg.Apply();
            boxStyle = new GUIStyle(GUI.skin.box) { normal = { background = bg } };
            style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperRight, richText = true };
        }

        // Schriftgröße folgt der Fensterhöhe
        style.fontSize = Mathf.Max(12, Screen.height / 45);

        string color = fps >= 55f ? "#9EFF00" : (fps >= 30f ? "#FFD84D" : "#FF4D6D");
        string text = "<color=" + color + "><b>" + fps.ToString("0") + " FPS</b></color>\n" +
                      avgMs.ToString("0.0") + " ms  (max " + worstMs.ToString("0.0") + ")";

        float w = style.fontSize * 11f;
        float h = style.fontSize * 3.2f;
        var rect = new Rect(Screen.width - w - 8f, 8f, w, h);
        GUI.Box(rect, GUIContent.none, boxStyle);
        GUI.Label(new Rect(rect.x, rect.y + 2f, rect.width - 8f, rect.height), text, style);
    }

    void OnDestroy()
    {
        if (bg != null) Destroy(bg);
    }
}
