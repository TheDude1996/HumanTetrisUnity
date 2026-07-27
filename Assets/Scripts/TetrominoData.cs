using UnityEngine;

public class TetrominoData : MonoBehaviour
{
    public Vector2Int[] blocks;

    void Awake()
    {
        // Beispiel: T-Stein
        blocks = new Vector2Int[]
        {
            new Vector2Int(-6,0),
            new Vector2Int(-2,0),
            new Vector2Int(2,0),
            new Vector2Int(6,0)
        };
    }
}