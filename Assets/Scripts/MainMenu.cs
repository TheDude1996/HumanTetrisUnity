using System.Collections;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;


public class MainMenu : MonoBehaviour {

    [Header("Spielerauswahl (Netzwerkordner, siehe PlayerDataManager)")]
    [Tooltip("Zeigt den gewählten Spieler, Ladezustand und Bestwert")]
    public TMP_Text playerLabel;

    void Start()
    {
        var data = PlayerDataManager.Instance;
        data.HeadChanged += UpdatePlayerLabel;
        RefreshPlayers();
    }

    void OnDestroy()
    {
        if (PlayerDataManager.Existing != null)
            PlayerDataManager.Existing.HeadChanged -= UpdatePlayerLabel;
    }

    public void PlayGame()
    {
        SceneManager.LoadScene(1);
    }

    public void QuitGame()
    {
        Debug.Log("QUIT!");
        Application.Quit();
    }

    // Öffnet den Spielordner im Windows Explorer.
    // Im Build: der Ordner mit der .exe (Application.dataPath zeigt auf "<Spiel>_Data", eine Ebene darüber).
    // Im Editor: der Projektordner (Application.dataPath zeigt auf "Assets").
    public void OpenGameFolder()
    {
        string folder = Directory.GetParent(Application.dataPath).FullName;
        Debug.Log("Öffne Spielordner: " + folder);

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        // explorer.exe direkt starten (funktioniert auch mit Leerzeichen im Pfad)
        System.Diagnostics.Process.Start("explorer.exe", "\"" + folder + "\"");
#else
        Application.OpenURL("file://" + folder);
#endif
    }

    // ---------------- Spielerauswahl ----------------

    public void NextPlayer()
    {
        PlayerDataManager.Instance.SelectRelative(1);
        UpdatePlayerLabel();
    }

    public void PreviousPlayer()
    {
        PlayerDataManager.Instance.SelectRelative(-1);
        UpdatePlayerLabel();
    }

    // Konfiguration und Ordnerliste neu einlesen (z.B. nachdem die Pipeline einen neuen Spieler angelegt hat).
    // Ist der bisher gewählte Spieler noch da, bleibt er gewählt, sonst wird der neueste genommen.
    public void RefreshPlayers()
    {
        var data = PlayerDataManager.Instance;
        data.LoadConfig();
        data.ApplyHeadLook(); // geänderte Helligkeit sofort übernehmen
        data.RefreshPlayers();

        if (data.Players.Count > 0 && !data.Players.Contains(data.SelectedPlayer))
            data.SelectPlayer(data.Players[data.Players.Count - 1]);

        UpdatePlayerLabel();
    }

    void UpdatePlayerLabel()
    {
        if (playerLabel == null) return;
        var data = PlayerDataManager.Instance;

        if (data.Players.Count == 0)
        {
            playerLabel.text = data.LastError != null ? "SCAN FOLDER NOT FOUND" : "NO PLAYERS FOUND";
            return;
        }

        string text = "PLAYER " + PlayerDataManager.GetPlayerNumberText(data.SelectedPlayer);
        if (data.HeadLoading) text += "  (loading...)";
        else if (data.HeadTemplate == null) text += "  (no scan)";

        var scores = data.GetPlayerHighscores();
        if (scores != null && scores.best != null)
            text += "\n<size=70%>BEST: " + scores.best.score + "</size>";

        playerLabel.text = text;
    }
}
