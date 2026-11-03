using UnityEngine;

// Passt Rahmen und Hintergrund der Tetris-Szene an die Feldbreite des gewählten Modus an
// (siehe GameSettings: Kinect = 9 breit, Tastatur = 11 breit). Die Szene ist für 11 Spalten gebaut;
// bei 9 rücken die Seitenwände je eine Zelle nach innen, das Feld bleibt mittig bei x = 20.
public class PlayfieldLayout : MonoBehaviour
{
    [Tooltip("Obere und untere Wand (werden schmaler)")]
    public Transform[] horizontalWalls;
    public Transform leftWall;
    public Transform rightWall;
    [Tooltip("Gekacheltes Hintergrund-Mesh hinter dem Spielfeld")]
    public Renderer background;

    void Awake()
    {
        int removedCells = GameSettings.MaxWidth - GameSettings.FieldWidth; // 0 oder 2
        if (removedCells <= 0) return;

        float shift = removedCells * 0.5f * 4f; // Zellen -> Welteinheiten, pro Seite

        if (leftWall != null) leftWall.position += new Vector3(shift, 0, 0);
        if (rightWall != null) rightWall.position -= new Vector3(shift, 0, 0);

        foreach (var wall in horizontalWalls)
        {
            if (wall == null) continue;
            Vector3 s = wall.localScale;
            wall.localScale = new Vector3(s.x - 2 * shift, s.y, s.z);
        }

        if (background != null)
        {
            // Mesh ist 10 Einheiten breit und um 90° gedreht -> localScale.x ist die Breite; Kacheln gleich groß halten
            Transform bg = background.transform;
            float oldWidth = bg.localScale.x;
            float newWidth = oldWidth - 2 * shift / 10f;
            bg.localScale = new Vector3(newWidth, bg.localScale.y, bg.localScale.z);

            Material m = background.material; // eigene Instanz
            Vector2 tiling = m.mainTextureScale;
            m.mainTextureScale = new Vector2(tiling.x * newWidth / oldWidth, tiling.y);
        }
    }
}
