using UnityEngine;

// Steuerungsmodus des Spiels (im Hauptmenü bei der Spielerauswahl umschaltbar, bleibt gespeichert).
//   Kinect:   Spielfeld 9 breit, Gesten werden ausgewertet (Tastatur bleibt als Ergänzung für Hard/Soft Drop)
//   Tastatur: Spielfeld 11 breit, Gesten werden ignoriert
// Das Spielfeld-Grid ist immer 11 Spalten groß; im Kinect-Modus sind nur die mittleren 9 Spalten (1..9) benutzbar.
public static class GameSettings
{
    public const int MaxWidth = 11;
    public const int KinectWidth = 9;
    public const int KeyboardWidth = 11;

    public const string KinectId = "kinect";
    public const string KeyboardId = "keyboard";

    private const string PrefKey = "HumanTetris.KinectMode";
    private static int mode = -1; // -1 = noch nicht gelesen, 0 = Tastatur, 1 = Kinect

    public static bool KinectMode
    {
        get
        {
            if (mode < 0) mode = PlayerPrefs.GetInt(PrefKey, 1);
            return mode == 1;
        }
        set
        {
            mode = value ? 1 : 0;
            PlayerPrefs.SetInt(PrefKey, mode);
            PlayerPrefs.Save();
        }
    }

    public static string ModeId => KinectMode ? KinectId : KeyboardId;
    public static string ModeName => KinectMode ? "KINECT" : "KEYBOARD";

    public static string ModeNameOf(string id) => id == KinectId ? "KINECT" : "KEYBOARD";

    // Breite des Spielfelds und erste benutzbare Grid-Spalte
    public static int FieldWidth => KinectMode ? KinectWidth : KeyboardWidth;
    public static int FieldMinX => (MaxWidth - FieldWidth) / 2;
}
