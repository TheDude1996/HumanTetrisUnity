using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    public TMP_Text scoreText;
    public TMP_Text levelText;      // Neu
    public GameObject gameOverPanel;

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
        scoreText.text = "Score: " + score;
        levelText.text = "Level: " + level;
    }

    private void Update()
    {
        if (gameOver && Input.GetKeyDown(KeyCode.R))
        {
            Restart();
        }
    }

    public void AddScore(int lines)
    {
        // Punkte vergeben
        switch (lines)
        {
            case 1: score += 100; break;
            case 2: score += 300; break;
            case 3: score += 500; break;
            case 4: score += 800; break;
        }

        // Linien und Level aktualisieren
        totalLines += lines;
        level = totalLines / 10 + 1;

        // UI aktualisieren
        scoreText.text = "Score: " + score;
        levelText.text = "Level: " + level;
    }

    private readonly float[] fallTimes =
    {
        0.80f, // Level 1
        0.72f, // Level 2
        0.63f, // Level 3
        0.55f, // Level 4
        0.47f, // Level 5
        0.40f, // Level 6
        0.33f, // Level 7
        0.27f, // Level 8
        0.22f, // Level 9
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

    public void GameOver()
    {
        Debug.Log("GAME OVER WIRD AUSGEFÜHRT");

        gameOver = true;
        gameOverPanel.SetActive(true);

        Time.timeScale = 0;
    }

    public void Restart()
    {
        Debug.Log("NEUSTART");

        Time.timeScale = 1;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}