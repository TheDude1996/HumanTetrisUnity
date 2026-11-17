using System.Text;
using TMPro;
using UnityEngine;

// Bestenliste im Hauptmenü: die besten Runden eines Modus aus <scansRoot>/leaderboard.json als Tabelle.
// Je eine Karte für Kinect und Tastatur (Feldbreiten 9 und 11 sind nicht vergleichbar).
// Jeder Spieler steht nur einmal drin (mit seiner besten Runde), siehe PlayerDataManager.GetTopScores.
// Der gewählte Spieler wird hervorgehoben. Neu eingelesen wird beim Start, bei REFRESH und beim Spielerwechsel (MainMenu).
// Aufbau in der Szene: Tools/Leaderboard/Add to Main Menu (Assets/Editor/LeaderboardSetup.cs)
public class LeaderboardTable : MonoBehaviour
{
    [Tooltip("Text, in den die Tabelle geschrieben wird")]
    public TMP_Text table;
    [Tooltip("Modus der Runden: \"kinect\" oder \"keyboard\" (alte Runden ohne Modus zählen als Tastatur)")]
    public string mode = GameSettings.KinectId;
    [Tooltip("Anzahl der Plätze")]
    public int count = 5;

    private const string Lime = "#9EFF00";
    private const string Dim = "#8A8FB0";

    void Start()
    {
        Refresh();
    }

    public void Refresh()
    {
        if (table == null) return;
        var data = PlayerDataManager.Instance;
        var top = data.GetTopScores(count, mode);

        var sb = new StringBuilder();
        sb.Append("<size=60%><color=").Append(Dim).Append(">#<pos=13%>PLAYER<pos=55%>SCORE</color></size>");

        for (int i = 0; i < count; i++)
        {
            sb.Append('\n');
            if (i >= top.Count)
            {
                sb.Append("<color=").Append(Dim).Append(">").Append(i + 1).Append("<pos=13%>---</color>");
                continue;
            }

            var r = top[i];
            bool selected = r.player == data.SelectedPlayer;
            if (selected) sb.Append("<color=").Append(Lime).Append(">");
            sb.Append(i + 1)
              .Append("<pos=13%>P").Append(PlayerDataManager.GetPlayerNumberText(r.player))
              .Append("<pos=55%>").Append(r.score);
            if (selected) sb.Append("</color>");
        }

        table.text = sb.ToString();
    }
}
