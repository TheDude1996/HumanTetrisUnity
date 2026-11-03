using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

// Debug-Menü und Kinect-Vorschau für die Gesten-Steuerung (Szene Tetris, Kinect-Modus).
//
//   F5  Debug-Menü: Sensor/Spieler, Gelenkhöhen gegen die Schwellen der Gesten, aktive Zustände,
//       Timing-Werte, die letzten erkannten Gesten, FPS
//   F6  Kinect-Vorschau (Tiefenbild des Spielers mit Skelett, vom KinectManager unten rechts gezeichnet) ein/aus
//
// Legt sich selbst an und bleibt über Szenenwechsel erhalten (wie FpsCounter).
public class KinectDebugOverlay : MonoBehaviour
{
    public KeyCode debugKey = KeyCode.F5;
    public KeyCode previewKey = KeyCode.F6;

    private bool showDebug;
    private float smoothDt = 0.016f;
    private float worstDt;
    private float worstResetAt;
    private GUIStyle style, boxStyle;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Create()
    {
        var go = new GameObject("KinectDebugOverlay");
        DontDestroyOnLoad(go);
        go.AddComponent<KinectDebugOverlay>();
    }

    void Start()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        ApplyPreviewDefault();
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplyPreviewDefault();
    }

    // Vorschau standardmäßig im Kinect-Modus an, im Tastatur-Modus aus
    // (der KinectManager muss dafür ComputeUserMap an haben, das ist in Tetris.unity eingestellt)
    static void ApplyPreviewDefault()
    {
        KinectManager km = KinectManager.Instance;
        if (km == null) return;
        SetPreview(km, GameSettings.KinectMode && km.ComputeUserMap);
    }

    // Vorschau = Tiefenbild des Spielers; mit Skelettlinien, damit man sieht, ob die Kinect den Spieler erfasst
    static void SetPreview(KinectManager km, bool on)
    {
        km.DisplayUserMap = on;
        km.DisplaySkeletonLines = on;
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        smoothDt = Mathf.Lerp(smoothDt, dt, 0.05f);
        if (Time.unscaledTime > worstResetAt) { worstDt = 0f; worstResetAt = Time.unscaledTime + 2f; }
        worstDt = Mathf.Max(worstDt, dt);

        if (Input.GetKeyDown(debugKey)) showDebug = !showDebug;

        if (Input.GetKeyDown(previewKey))
        {
            KinectManager km = KinectManager.Instance;
            if (km != null && km.ComputeUserMap) SetPreview(km, !km.DisplayUserMap);
        }
    }

    void OnGUI()
    {
        if (!showDebug) return;

        if (style == null)
        {
            style = new GUIStyle(GUI.skin.label) { richText = true, alignment = TextAnchor.UpperLeft, wordWrap = false };
            boxStyle = new GUIStyle(GUI.skin.box);
        }
        style.fontSize = Mathf.Max(12, Screen.height / 55);

        string text = BuildText();
        float width = Mathf.Min(Screen.width * 0.6f, style.fontSize * 38f);
        float height = style.CalcHeight(new GUIContent(text), width);
        var rect = new Rect(10, 10, width, height + 12);
        GUI.Box(rect, GUIContent.none, boxStyle);
        GUI.Label(new Rect(rect.x + 6, rect.y + 6, rect.width - 12, rect.height - 12), text, style);
    }

    static string OnOff(bool value) => value ? "<color=#9EFF00>AN</color>" : "<color=#888888>aus</color>";

    static string Ok(bool value, string label) => (value ? "<color=#9EFF00>" : "<color=#FF6060>") + label + "</color>";

    string BuildText()
    {
        var sb = new StringBuilder();
        sb.AppendLine("<b>KINECT DEBUG</b>   (F5 aus  |  F6 Vorschau)");
        sb.AppendLine("Modus: <b>" + GameSettings.ModeName + "</b>, Feld " + GameSettings.FieldWidth + " breit");

        KinectManager km = KinectManager.Instance;
        if (km == null)
        {
            sb.AppendLine("<color=#FF6060>Kein KinectManager in dieser Szene</color>");
            AppendFps(sb);
            return sb.ToString();
        }

        bool init = km.IsInitialized();
        bool user = init && km.IsUserDetected();
        sb.AppendLine("Sensor: " + Ok(init, init ? "bereit" : "NICHT verbunden") + "    Spieler: " + Ok(user, user ? "erkannt" : "keiner") +
                      "    Vorschau: " + OnOff(km.DisplayUserMap));

        if (user)
        {
            uint id = km.GetPlayer1ID();
            Vector3 pos = km.GetUserPosition(id);
            sb.AppendLine("Abstand: " + Ok(pos.z >= km.MinUserDistance && (km.MaxUserDistance <= 0f || pos.z <= km.MaxUserDistance), pos.z.ToString("F2") + " m") +
                          "   seitlich x " + pos.x.ToString("F2") + " m   (min " + km.MinUserDistance.ToString("F1") + (km.MaxUserDistance > 0f ? ", max " + km.MaxUserDistance.ToString("F1") : "") + ")");

            int hip = (int)KinectWrapper.NuiSkeletonPositionIndex.HipCenter;
            int sl = (int)KinectWrapper.NuiSkeletonPositionIndex.ShoulderLeft, sr = (int)KinectWrapper.NuiSkeletonPositionIndex.ShoulderRight;
            int hl = (int)KinectWrapper.NuiSkeletonPositionIndex.HandLeft, hr = (int)KinectWrapper.NuiSkeletonPositionIndex.HandRight;
            int el = (int)KinectWrapper.NuiSkeletonPositionIndex.ElbowLeft, er = (int)KinectWrapper.NuiSkeletonPositionIndex.ElbowRight;

            if (km.IsJointTracked(id, hip))
            {
                float hy = km.GetJointPosition(id, hip).y;
                // Jump startet, wenn die Hüfte zwischen 0,9 und 1,3 liegt; Squat, wenn sie <= 0,9 ist (absolute Höhen, abhängig von SensorHeight)
                sb.AppendLine("Hüfte y: <b>" + hy.ToString("F2") + " m</b>   Jump-Start " + Ok(hy > 0.9f && hy < 1.3f, "0,9–1,3") +
                              "   Squat-Start " + Ok(hy <= 0.9f, "≤ 0,9") + "   (SensorHeight " + km.SensorHeight.ToString("F1") + ")");
            }
            else sb.AppendLine("Hüfte: <color=#FF6060>nicht getrackt</color>");

            if (km.IsJointTracked(id, hl) && km.IsJointTracked(id, sl) && km.IsJointTracked(id, hr) && km.IsJointTracked(id, sr))
            {
                float dl = km.GetJointPosition(id, hl).y - km.GetJointPosition(id, sl).y;
                float dr = km.GetJointPosition(id, hr).y - km.GetJointPosition(id, sr).y;
                sb.AppendLine("Hand über Schulter:  L " + Ok(dl > 0.1f, dl.ToString("+0.00;-0.00")) + "   R " + Ok(dr > 0.1f, dr.ToString("+0.00;-0.00")) + "   (Bewegen ab +0,10)");

                bool tpose = km.IsJointTracked(id, el) && km.IsJointTracked(id, er) &&
                             Mathf.Abs(dl) < 0.1f && Mathf.Abs(dr) < 0.1f &&
                             Mathf.Abs(km.GetJointPosition(id, el).y - km.GetJointPosition(id, sl).y) < 0.1f &&
                             Mathf.Abs(km.GetJointPosition(id, er).y - km.GetJointPosition(id, sr).y) < 0.1f;
                sb.AppendLine("T-Pose-Haltung: " + Ok(tpose, tpose ? "ja" : "nein") + "   (Hände und Ellbogen auf Schulterhöhe ±0,10)");
            }
            else sb.AppendLine("Hände/Schultern: <color=#FF6060>nicht getrackt</color>");
        }

        sb.AppendLine("Aktiv:  Links " + OnOff(KinectSteuerung.DebugMoveLeft) + "   Rechts " + OnOff(KinectSteuerung.DebugMoveRight) + "   T-Pose " + OnOff(KinectSteuerung.DebugTpose));
        sb.AppendLine("Timing: Pose halten " + KinectWrapper.Constants.PoseCompleteDuration.ToString("F2") + " s   Bewegen alle " + KinectSteuerung.MoveRepeatTime.ToString("F2") +
                      " s   Drehen (T-Pose) alle " + KinectSteuerung.RotateRepeatTimeTpose.ToString("F2") + " s");

        sb.AppendLine("<b>Letzte Gesten</b> (+ erkannt, - beendet):");
        if (KinectSteuerung.GestureLog.Count == 0) sb.AppendLine("  –");
        for (int i = KinectSteuerung.GestureLog.Count - 1; i >= 0; i--) sb.AppendLine("  " + KinectSteuerung.GestureLog[i]);

        AppendFps(sb);
        return sb.ToString();
    }

    void AppendFps(StringBuilder sb)
    {
        float fps = 1f / Mathf.Max(smoothDt, 0.0001f);
        sb.AppendLine("FPS: " + Ok(fps >= 50f, fps.ToString("F0")) + "   schlechtester Frame (2 s): " + (worstDt * 1000f).ToString("F0") + " ms");
    }
}
