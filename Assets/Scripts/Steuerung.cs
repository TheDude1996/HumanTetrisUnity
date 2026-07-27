using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SocialPlatforms.Impl;

public class Steuerung : MonoBehaviour
{
    public Vector3 rotationPoint;
    private float previousTime;
    public float fallTime = 0.8f;

    // Grid Definition
    public static int width = 11;
    public static int height = 40;
    public static Transform[,] grid = new Transform[width, height];


    void Start()
    {
        previousTime = Time.time;

        if (!ValidMove())
        {
            GameManager.Instance.GameOver();
        }

    }

    void Update()
    {
        // --- Bewegung Links/Rechts ---
        if (Input.GetKeyDown(KeyCode.RightArrow))
        {
            transform.position += new Vector3(4, 0, 0);
            if (!ValidMove()) transform.position -= new Vector3(4, 0, 0);
        }
        else if (Input.GetKeyDown(KeyCode.LeftArrow))
        {
            transform.position += new Vector3(-4, 0, 0);
            if (!ValidMove()) transform.position -= new Vector3(-4, 0, 0);
        }
        // --- Rotation ---
        else if (Input.GetKeyDown(KeyCode.UpArrow))
        {
            transform.RotateAround(transform.TransformPoint(rotationPoint), new Vector3(0, 0, 1), 90);
            if (!ValidMove()) transform.RotateAround(transform.TransformPoint(rotationPoint), new Vector3(0, 0, 1), -90);
        }

        // --- Fallen ---
        if (Time.time - previousTime > (Input.GetKey(KeyCode.DownArrow) ? fallTime / 10 : fallTime))
        {
            transform.position += new Vector3(0, -4, 0);

            if (!ValidMove())
            {
                // Kollision: Zurückbewegen
                transform.position -= new Vector3(0, -4, 0);

                // WICHTIG: Block ins Grid eintragen
                AddToGrid();

                CheckforLines();

                // Skript deaktivieren & neuen spawnen
                this.enabled = false;
                FindObjectsByType<SpawnTetromino>(FindObjectsSortMode.None)[0].NewTetromino();
            }
            else
            {
                // Erfolgreich gefallen: Timer resetten
                previousTime = Time.time;
            }
        }
    }

    void CheckforLines()
    {
        int linesCleared = 0;

        for (int y = height - 1; y >= 0; y--)
        {
            if (HasLine(y))
            {
                DeleteLine(y);
                RowDown(y);
                linesCleared++;
                y++;
            }
        }

        if (linesCleared > 0)
        {
            GameManager.Instance.AddScore(linesCleared);
        }
    }

    

    bool HasLine(int y)
    {
        for (int x = 0; x < width; x++)
        {
            if (grid[x, y] == null)
                return false;
        }

        return true;
    }


    void DeleteLine(int y)
    {
        for (int x = 0; x < width; x++)
        {
            Destroy(grid[x, y].gameObject);
            grid[x, y] = null;
        }
    }

    void RowDown(int row)
    {
        for (int y = row + 1; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (grid[x, y] != null)
                {
                    grid[x, y - 1] = grid[x, y];
                    grid[x, y] = null;

                    grid[x, y - 1].position += Vector3.down * 4;
                }
            }
        }
    }


    // 1. Die Speicher-Funktion (fehlt noch in deinem Code)
    void AddToGrid()
    {
        foreach (Transform collisionBlock in transform)
        {
            int x = Mathf.RoundToInt(collisionBlock.position.x / 4);
            int y = Mathf.RoundToInt(collisionBlock.position.y / 4);

            if (x >= 0 && x < width && y >= 0 && y < height)
            {
                grid[x, y] = collisionBlock;
            }
        }
    }

    // 2. Die Prüffunktion (muss das Grid abfragen!)
    bool ValidMove()
    {
        foreach (Transform collisionBlock in transform)
        {
            int x = Mathf.RoundToInt(collisionBlock.position.x / 4);
            int y = Mathf.RoundToInt(collisionBlock.position.y / 4);

            if (x < 0 || x >= width || y < 0 || y >= height)
                return false;

            if (grid[x, y] != null)
                return false;
        }

        return true;
    }
}