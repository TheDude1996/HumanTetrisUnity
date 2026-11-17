using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Baut die Bestenlisten (Top 5, je eine für Kinect und Tastatur) in die Szene MainMenu: zwei Karten im Stil der PlayerCard
// (Kopien davon) übereinander am linken Rand neben dem Titel, darin je ein Text, den LeaderboardTable füllt.
// Menü: Tools/Leaderboard/Add to Main Menu   (kann beliebig oft ausgeführt werden, ersetzt die alten Karten)
public static class LeaderboardSetup
{
    const string ScenePath = "Assets/Scenes/MainMenu.unity";

    // Die Karten hängen am linken Bildrand (MainMenu wird dafür auf den ganzen Canvas gezogen; seine anderen Kinder
    // sind mittig verankert und bleiben, wo sie sind). Canvas-Höhe 1080, Breite je nach Seitenverhältnis (16:9 = 1920).
    // Der Titel-Schriftzug beginnt bei x = -682 von der Mitte; bei 16:9 reichen die Karten bis -688.
    const float CardMargin = 16f;
    static readonly Vector2 CardSize = new Vector2(256f, 280f);

    [MenuItem("Tools/Leaderboard/Add to Main Menu")]
    public static void AddToMainMenu()
    {
        // offene Szene mit ungespeicherten Änderungen nicht einfach verwerfen
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        GameObject card = GameObject.Find("PlayerCard");
        if (card == null)
        {
            Debug.LogError("PlayerCard nicht gefunden (Szene MainMenu).");
            return;
        }

        foreach (string old in new[] { "LeaderboardCard", "LeaderboardKinect", "LeaderboardKeyboard" })
        {
            GameObject existing = GameObject.Find(old);
            if (existing != null) Object.DestroyImmediate(existing);
        }

        // Überschrift der Spielerkarte ohne Nummerierung (früher "01 / SPIELER")
        Transform playerEyebrow = card.transform.Find("Eyebrow");
        var playerEyebrowText = playerEyebrow != null ? playerEyebrow.GetComponent<TMP_Text>() : null;
        if (playerEyebrowText != null) playerEyebrowText.text = "SPIELER";

        // MainMenu (bisher 100 x 100 in der Mitte) über den ganzen Canvas ziehen, damit die Karten am Bildrand verankert werden können
        var menuRect = (RectTransform)card.transform.parent;
        menuRect.anchorMin = Vector2.zero;
        menuRect.anchorMax = Vector2.one;
        menuRect.pivot = new Vector2(0.5f, 0.5f);
        menuRect.anchoredPosition = Vector2.zero;
        menuRect.sizeDelta = Vector2.zero;

        bool ok = CreateCard(card, "LeaderboardKinect", "TOP 5 KINECT", GameSettings.KinectId, 150f)
               && CreateCard(card, "LeaderboardKeyboard", "TOP 5 KEYBOARD", GameSettings.KeyboardId, -150f);
        if (!ok) return;

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Bestenlisten (Kinect, Tastatur) in MainMenu eingebaut.");
    }

    static bool CreateCard(GameObject playerCard, string name, string title, string mode, float y)
    {
        // Kopie der Spielerkarte: Rahmen, Überschrift (Eyebrow) und Text (PlayerLabel) bleiben, der Rest fliegt raus
        GameObject board = Object.Instantiate(playerCard, playerCard.transform.parent);
        board.name = name;
        for (int i = board.transform.childCount - 1; i >= 0; i--)
        {
            Transform child = board.transform.GetChild(i);
            if (child.name != "Outline" && child.name != "Eyebrow" && child.name != "PlayerLabel")
                Object.DestroyImmediate(child.gameObject);
        }

        var rect = (RectTransform)board.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = new Vector2(CardMargin, y);
        rect.sizeDelta = CardSize;

        Transform eyebrow = board.transform.Find("Eyebrow");
        if (eyebrow != null)
        {
            var eyebrowRect = (RectTransform)eyebrow;
            eyebrowRect.anchorMin = eyebrowRect.anchorMax = new Vector2(0.5f, 1f);
            eyebrowRect.anchoredPosition = new Vector2(0f, -36f);
            eyebrowRect.sizeDelta = new Vector2(CardSize.x - 40f, 30f);
            var eyebrowText = eyebrow.GetComponent<TMP_Text>();
            if (eyebrowText != null)
            {
                eyebrowText.text = title;
                eyebrowText.alignment = TextAlignmentOptions.Left;
            }
        }

        Transform label = board.transform.Find("PlayerLabel");
        var text = label != null ? label.GetComponent<TMP_Text>() : null;
        if (text == null)
        {
            Debug.LogError("PlayerLabel (TMP_Text) in der Kopie nicht gefunden.");
            Object.DestroyImmediate(board);
            return false;
        }
        label.name = "Table";
        var textRect = (RectTransform)label;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(20f, 18f);
        textRect.offsetMax = new Vector2(-20f, -60f);
        text.enableAutoSizing = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;
        text.fontSize = 24f;
        text.lineSpacing = 0f;
        text.alignment = TextAlignmentOptions.TopLeft;
        text.richText = true;
        text.text = "#<pos=13%>PLAYER<pos=55%>SCORE";

        var component = board.AddComponent<LeaderboardTable>();
        component.table = text;
        component.mode = mode;
        return true;
    }
}
