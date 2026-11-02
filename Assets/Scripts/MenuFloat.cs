using UnityEngine;

// Lässt ein UI-Element sanft schweben und leicht kippen (wie die "Orbit"-Quadrate auf der Website).
// Läuft mit unskalierter Zeit, damit es auch bei Time.timeScale = 0 weiterläuft.
[RequireComponent(typeof(RectTransform))]
public class MenuFloat : MonoBehaviour
{
    [Tooltip("Auslenkung nach oben/unten in Canvas-Einheiten")]
    public float amplitude = 16f;
    [Tooltip("Dauer eines Hin-und-Her in Sekunden")]
    public float period = 7f;
    [Tooltip("Kippen in Grad um die Grundrotation")]
    public float tilt = 6f;
    [Tooltip("Zeitversatz, damit mehrere Elemente nicht synchron laufen")]
    public float phase = 0f;

    private RectTransform rt;
    private Vector2 basePos;
    private float baseRot;

    void OnEnable()
    {
        rt = (RectTransform)transform;
        basePos = rt.anchoredPosition;
        baseRot = rt.localEulerAngles.z;
    }

    void Update()
    {
        float a = (Time.unscaledTime + phase) / Mathf.Max(0.1f, period) * Mathf.PI * 2f;
        rt.anchoredPosition = basePos + new Vector2(0f, Mathf.Sin(a) * amplitude);
        rt.localRotation = Quaternion.Euler(0f, 0f, baseRot + Mathf.Sin(a * 0.5f) * tilt);
    }

    void OnDisable()
    {
        if (rt == null) return;
        rt.anchoredPosition = basePos;
        rt.localRotation = Quaternion.Euler(0f, 0f, baseRot);
    }
}
