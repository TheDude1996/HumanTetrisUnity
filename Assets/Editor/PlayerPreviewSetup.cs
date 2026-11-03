using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Baut die Modell-Vorschau der Spielerauswahl in die Szene MainMenu: eine Karte im Stil der PlayerCard
// (Kopie davon) rechts neben der Spielerauswahl, darin ein RawImage, das PlayerPreview füllt.
// Menü: Tools/Player Preview/Add to Main Menu   (kann beliebig oft ausgeführt werden, ersetzt die alte Vorschau)
public static class PlayerPreviewSetup
{
    const string ScenePath = "Assets/Scenes/MainMenu.unity";
    const string CardName = "PlayerPreviewCard";

    [MenuItem("Tools/Player Preview/Add to Main Menu")]
    public static void AddToMainMenu()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        GameObject card = GameObject.Find("PlayerCard");
        if (card == null)
        {
            Debug.LogError("PlayerCard nicht gefunden (Szene MainMenu).");
            return;
        }

        GameObject old = GameObject.Find(CardName);
        if (old != null) Object.DestroyImmediate(old);

        // Kopie der Spielerkarte (gleicher Neon-Rahmen), ohne Texte und Buttons
        GameObject preview = Object.Instantiate(card, card.transform.parent);
        preview.name = CardName;
        for (int i = preview.transform.childCount - 1; i >= 0; i--)
        {
            Transform child = preview.transform.GetChild(i);
            if (child.name != "Outline") Object.DestroyImmediate(child.gameObject);
        }

        var rect = (RectTransform)preview.transform;
        rect.anchoredPosition = new Vector2(570f, -170f); // rechts neben der Spielerkarte (Karte: 820 breit bei x = 0)
        rect.sizeDelta = new Vector2(260f, 260f);

        var imageObject = new GameObject("PreviewImage", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
        imageObject.transform.SetParent(preview.transform, false);
        var imageRect = (RectTransform)imageObject.transform;
        imageRect.anchorMin = Vector2.zero;
        imageRect.anchorMax = Vector2.one;
        imageRect.offsetMin = new Vector2(8f, 8f);
        imageRect.offsetMax = new Vector2(-8f, -8f);
        var raw = imageObject.GetComponent<RawImage>();
        raw.raycastTarget = false;
        raw.enabled = false; // wird von PlayerPreview eingeschaltet, sobald ein Kopf da ist

        var component = preview.AddComponent<PlayerPreview>();
        component.target = raw;

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Spieler-Vorschau in MainMenu eingebaut.");
    }
}
