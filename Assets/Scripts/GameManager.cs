using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    public TMP_Text scoreText;
    public GameObject gameOverPanel;

    int score = 0;
    bool gameOver = false;

    private void Awake()
    {
        Instance = this;
    }

    private void Update()
    {
        if (gameOver && Input.GetKeyDown(KeyCode.Space))
        {
            Restart();
        }
    }

    public void AddScore(int lines)
    {
        switch (lines)
        {
            case 1: score += 100; break;
            case 2: score += 300; break;
            case 3: score += 500; break;
            case 4: score += 800; break;
        }

        scoreText.text = "Score: " + score;
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