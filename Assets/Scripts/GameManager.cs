using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;
    public static int LastScore, LastLevel, LastLines; // Ergebnis der letzten Runde (Game-Over-Screen)

    public TMP_Text scoreText;
    public TMP_Text levelText;      // Neu
    //public GameObject gameOverPanel;

    private int score = 0;
    private bool gameOver = false;

    public int level = 1;
    public int totalLines = 0;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        scoreText.text = "<size=40%><color=#A4A8C2>SCORE</color></size>\n" + score;
        levelText.text = "<size=40%><color=#A4A8C2>LEVEL</color></size>\n" + level;
    }

    private void Update()
    {
        if (gameOver && Input.GetKeyDown(KeyCode.R))
        {
            Restart();
        }
    }

    [Tooltip("Nach wie vielen gelöschten Linien das Level steigt")]
    private int linesPerLevel = 4;

    // Grundpunkte für 1/2/3/4 Linien auf einmal (klassisch), werden mit dem Level multipliziert
    private static readonly int[] lineScores = { 0, 100, 300, 500, 800 };

    public void AddScore(int lines)
    {
        if (lines <= 0) return;

        // Punkte vergeben: Grundpunkte × aktuelles Level (Level vor einem möglichen Aufstieg)
        score += lineScores[Mathf.Min(lines, 4)] * level;

        // Linien und Level aktualisieren
        totalLines += lines;
        level = totalLines / Mathf.Max(1, linesPerLevel) + 1;

        UpdateUI();
    }

    // Punkte für Soft Drop (1 pro Reihe) und Hard Drop (2 pro Reihe)
    public void AddDropScore(int points)
    {
        if (points <= 0) return;
        score += points;
        UpdateUI();
    }

    private void UpdateUI()
    {
        scoreText.text = "<size=40%><color=#A4A8C2>SCORE</color></size>\n" + score;
        levelText.text = "<size=40%><color=#A4A8C2>LEVEL</color></size>\n" + level;
    }

    private readonly float[] fallTimes =
    {
        0.60f, // Level 1
        0.50f, // Level 2
        0.40f, // Level 3
        0.37f, // Level 4
        0.33f, // Level 5
        0.30f, // Level 6
        0.27f, // Level 7
        0.23f, // Level 8
        0.20f, // Level 9
        0.18f, // Level 10
        0.15f, // Level 11
        0.12f, // Level 12
        0.10f, // Level 13+
    };


    public float GetFallTime()
    {
        int index = Mathf.Min(level - 1, fallTimes.Length - 1);
        return fallTimes[index];
    }

    //public void GameOver()
    //{
    //    Debug.Log("GAME OVER WIRD AUSGEFÜHRT");

    //    gameOver = true;
    //    gameOverPanel.SetActive(true);

    //    Time.timeScale = 0;
    //}

    public void GameOver()
    {
        if (gameOver) return; // nur einmal pro Runde speichern
        gameOver = true;

        // Runde im Ordner des Spielers und in der globalen Bestenliste sichern (Netzwerk, im Hintergrund)
        LastScore = score; LastLevel = level; LastLines = totalLines;
        PlayerDataManager.Instance.SaveRound(score, level, totalLines);

        SceneManager.LoadScene(2);
    }

    public void Restart()
    {
        Debug.Log("NEUSTART");

        Time.timeScale = 1;
        SceneManager.LoadScene(1);
    }
}