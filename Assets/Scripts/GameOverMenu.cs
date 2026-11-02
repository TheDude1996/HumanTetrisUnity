using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class GameOverMenu : MonoBehaviour
{
    [Tooltip("Große Punktzahl der letzten Runde")]
    public TMP_Text scoreText;
    [Tooltip("Level, Linien und Bestwert")]
    public TMP_Text detailText;

    void Start()
    {
        int score = GameManager.LastScore;
        if (scoreText != null)
            scoreText.text = "<size=40%><color=#A4A8C2>SCORE</color></size>\n" + score;

        if (detailText != null)
        {
            // Bestwert des gewählten Spielers (die aktuelle Runde wird im Hintergrund gespeichert, kann also schon drin sein)
            int best = score;
            var data = PlayerDataManager.Existing;
            var highscores = data != null ? data.GetPlayerHighscores() : null;
            if (highscores != null && highscores.best != null) best = Mathf.Max(best, highscores.best.score);

            string line = "LEVEL " + GameManager.LastLevel + "   ·   " + GameManager.LastLines + " LINES";
            string record = score > 0 && score >= best
                ? "<color=#9EFF00>NEW BEST!</color>"
                : "<color=#A4A8C2>BEST " + best + "</color>";
            detailText.text = line + "\n" + record;
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.R)) RestartGame();
        if (Input.GetKeyDown(KeyCode.Escape)) MainMenu();
    }

    public void RestartGame()
    {
        SceneManager.LoadScene(1);
    }

    public void MainMenu()
    {
        SceneManager.LoadScene(0);
    }
}
