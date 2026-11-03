using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SocialPlatforms.Impl;

public class KinectSteuerung : MonoBehaviour, KinectGestures.GestureListenerInterface
{
    public Vector3 rotationPoint;
    private float previousTime;
    public float fallTime = 0.8f;

    // Ghost
    public GameObject ghostPrefab;
    public GameObject ghost;


    public GameObject originalPrefab;

    // Grid Definition
    // Das Grid ist immer 11 breit; benutzbar sind die Spalten MinX..MaxX (Kinect-Modus: 9, Tastatur-Modus: 11)
    public static int width => GameSettings.FieldWidth;
    public static int MinX => GameSettings.FieldMinX;
    public static int MaxX => MinX + width - 1;
    public static int height = 20;
    public static Transform[,] grid = new Transform[GameSettings.MaxWidth, height];

    // --- Kinect ---
    private bool TposeActive = false;
    private bool moveLeftActive = false;
    private bool moveRightActive = false;

    // Timing der Gesten (Sekunden). Statisch, damit alle Steine dieselben Werte nutzen (Debug-Menü zeigt sie an).
    // Hinweis: Die Haltezeit der Posen selbst steht in KinectWrapper.Constants.PoseCompleteDuration (0,25 s).
    public static float MoveRepeatTime = 0.15f;        // Wiederholrate für Links/Rechts, solange Hand oben ist (vorher 0,2)
    public static float RotateRepeatTimeTpose = 0.5f;  // Wiederholrate fürs Drehen, solange T-Pose gehalten wird (vorher 0,7)
    private float previousMoveTime;
    private float previousRotateTime; // eigener Timer: Bewegen und Drehen blockieren sich nicht mehr gegenseitig

    // Für das Debug-Menü (KinectDebugOverlay)
    public static bool DebugMoveLeft, DebugMoveRight, DebugTpose;
    public static readonly List<string> GestureLog = new List<string>();
    static void LogGesture(string text)
    {
        GestureLog.Add(Time.time.ToString("F1") + "s  " + text);
        if (GestureLog.Count > 8) GestureLog.RemoveAt(0);
    }
    void Start()
    {
        previousTime = Time.time;

        fallTime = GameManager.Instance.GetFallTime();

        // Gesten nur im Kinect-Modus auswerten
        if (GameSettings.KinectMode) RegisterAsGestureListener();

        // Ragt der Stein oben aus dem Feld (z. B. das senkrechte I), nach unten schieben
        while (TopRow() >= height)
        {
            transform.position += Vector3.down * 4;
        }

        ghost = Instantiate(ghostPrefab);
        UpdateGhost();

        if (!ValidMove(transform))
        {
            GameManager.Instance.GameOver();
        }
    }

    void Update()
    {
        DebugMoveLeft = moveLeftActive; DebugMoveRight = moveRightActive; DebugTpose = TposeActive;

        // --- Bewegung Links/Rechts (Tastatur) ---
        if (Input.GetKeyDown(KeyCode.RightArrow))
        {
            TryMove(new Vector3(4, 0, 0));
        }

        else
        {
            UpdateGhost();
        }

        if (Input.GetKeyDown(KeyCode.LeftArrow))
        {
            TryMove(new Vector3(-4, 0, 0));
        }

        else
        {
            UpdateGhost();
        }

        // --- Rotation (Tastatur) ---
        if (Input.GetKeyDown(KeyCode.UpArrow))
        {
            TryRotate();
        }

        else
        {
            UpdateGhost();
        }

        // --- Fallen ---
        bool schnellFallen = Input.GetKey(KeyCode.DownArrow);

        if (Time.time - previousTime > (schnellFallen ? fallTime / 10 : fallTime))
        {
            transform.position += new Vector3(0, -4, 0);

            if (!ValidMove(transform))
            {
                transform.position -= new Vector3(0, -4, 0);
                if (hasDropped) return;
                hasDropped = true;

                AddToGrid();
                CheckforLines();
                Destroy(ghost);

                this.enabled = false;
                StartCoroutine(DeferredUnregister());

                FindObjectsByType<SpawnTetromino>(FindObjectsSortMode.None)[0].NewTetromino();
            }
            else
            {
                previousTime = Time.time;

                // Soft Drop: 1 Punkt pro Reihe, die durch ↓ schneller gefallen ist
                if (schnellFallen) GameManager.Instance.AddDropScore(1);
            }
        }
        //Hard Drop

        if (Input.GetKeyDown(KeyCode.Space))
        {
            HardDrop();
            return;
        }


        if (Input.GetKeyDown(KeyCode.C))
        {
            //FindObjectsByType<SpawnTetromino>(FindObjectsSortMode.None)[0]
            //    .HoldPiece(gameObject);

            Hold();            
            return;
        }

        // --- Kontinuierliches Verschieben, solange Hand-Geste aktiv ist ---
        if ((moveLeftActive || moveRightActive) && Time.time - previousMoveTime > MoveRepeatTime)
        {
            if (moveLeftActive) TryMove(new Vector3(-4, 0, 0));
            if (moveRightActive) TryMove(new Vector3(4, 0, 0));

            //if (TposeActive) TryRotate();
            
            previousMoveTime = Time.time;
        }

        if ((TposeActive) && Time.time - previousRotateTime > RotateRepeatTimeTpose)
        {
            //if (moveLeftActive) TryMove(new Vector3(-4, 0, 0));
            //if (moveRightActive) TryMove(new Vector3(4, 0, 0));

            if (TposeActive) TryRotate();
            previousRotateTime = Time.time;
        }
    }
    void UpdateGhost()
    {
        // Position und Rotation übernehmen
        ghost.transform.position = transform.position;
        ghost.transform.rotation = transform.rotation;

        // So lange nach unten bewegen,
        // bis der Ghost nicht mehr gültig ist
        while (true)
        {
            ghost.transform.position += Vector3.down * 4;

            if (!ValidMove(ghost.transform))
            {
                // Ein Feld zurück
                ghost.transform.position += Vector3.up * 4;
                break;
            }
        }

        // Liegt der Ghost genau auf dem Stein, würde er ihn nur blass überdecken
        ghost.SetActive(ghost.transform.position != transform.position);
    }
    // --- Hilfsfunktionen für Bewegung/Rotation ---
    void TryMove(Vector3 delta)
    {
        transform.position += delta;
        if (!ValidMove(transform)) transform.position -= delta;
    }

    // Wall Kicks: Passt die Drehung an Ort und Stelle nicht (Rand, Boden oder andere Steine im Weg),
    // werden nacheinander kleine Verschiebungen probiert (in Feldern). Die erste gültige wird genommen.
    // Reihenfolge: seitlich 1, hoch 1 (Drehen am Boden), schräg hoch, seitlich 2 (vor allem für das I).
    private static readonly Vector2Int[] wallKicks =
    {
        new Vector2Int(0, 0),
        new Vector2Int(-1, 0), new Vector2Int(1, 0),
        new Vector2Int(0, 1),
        new Vector2Int(-1, 1), new Vector2Int(1, 1),
        new Vector2Int(-2, 0), new Vector2Int(2, 0),
    };

    void TryRotate()
    {
        Vector3 pivot = transform.TransformPoint(rotationPoint);
        transform.RotateAround(pivot, Vector3.forward, 90);

        foreach (Vector2Int kick in wallKicks)
        {
            Vector3 offset = new Vector3(kick.x * 4, kick.y * 4, 0);
            transform.position += offset;
            if (ValidMove(transform)) return;
            transform.position -= offset;
        }

        // Keine Position passt -> Drehung zurücknehmen
        transform.RotateAround(pivot, Vector3.forward, -90);
    }

    private bool hasDropped = false;

    void HardDrop()
    {
        if (hasDropped) return;
        hasDropped = true;

        // Hard Drop: 2 Punkte pro übersprungener Reihe
        int rows = Mathf.RoundToInt((transform.position.y - ghost.transform.position.y) / 4);
        if (rows > 0) GameManager.Instance.AddDropScore(rows * 2);

        transform.position = ghost.transform.position;

        AddToGrid();
        CheckforLines();
        Destroy(ghost);

        this.enabled = false;

        // NICHT synchron abmelden -> verschieben, damit die
        // laufende foreach-Schleife im KinectManager nicht
        // während der Iteration verändert wird
        StartCoroutine(DeferredUnregister());

        FindObjectsByType<SpawnTetromino>(FindObjectsSortMode.None)[0].NewTetromino();
    }

    void Hold()
    {
        FindObjectsByType<SpawnTetromino>(FindObjectsSortMode.None)[0]
                .HoldPiece(gameObject);
    }

    private IEnumerator DeferredUnregister()
    {
        // einen Frame warten, bis KinectManager.Update() fertig ist
        yield return null;
        UnregisterAsGestureListener();
    }

    // =========================================================
    // ============  NEU: LISTENER-VERWALTUNG  =================
    // =========================================================

    private void RegisterAsGestureListener()
    {
        KinectManager manager = KinectManager.Instance;
        if (manager != null && manager.gestureListeners != null &&
            !manager.gestureListeners.Contains(this))
        {
            manager.gestureListeners.Add(this);
        }
    }

    private void UnregisterAsGestureListener()
    {
        KinectManager manager = KinectManager.Instance;
        if (manager != null && manager.gestureListeners != null)
        {
            manager.gestureListeners.Remove(this);
        }
    }

    // Sicherheitsnetz: falls das Objekt zerstört wird, ohne dass der obige
    // Update()-Zweig durchlaufen wurde (z. B. GameOver), trotzdem austragen
    void OnDestroy()
    {
        UnregisterAsGestureListener();
    }

    // =========================================================
    // ============  KINECT GESTURE LISTENER INTERFACE  ========
    // =========================================================

    public void UserDetected(uint userId, int userIndex)
    {
        KinectManager manager = KinectManager.Instance;

        manager.DetectGesture(userId, KinectGestures.Gestures.RaiseLeftHand);
        manager.DetectGesture(userId, KinectGestures.Gestures.RaiseRightHand);
        manager.DetectGesture(userId, KinectGestures.Gestures.Squat);   //
        manager.DetectGesture(userId, KinectGestures.Gestures.Jump);    // Drehen
        manager.DetectGesture(userId, KinectGestures.Gestures.Tpose);   // Halten für Rotieren
    }

    public void UserLost(uint userId, int userIndex)
    {
        TposeActive = false;
        moveLeftActive = false;
        moveRightActive = false;
    }

    public void GestureInProgress(uint userId, int userIndex, KinectGestures.Gestures gesture,
                                   float progress, KinectWrapper.NuiSkeletonPositionIndex joint, Vector3 screenPos)
    {
        // Optional: Fortschrittsanzeige o.ä.
    }

    public bool GestureCompleted(uint userId, int userIndex, KinectGestures.Gestures gesture,
                                  KinectWrapper.NuiSkeletonPositionIndex joint, Vector3 screenPos)
    {
        // Gelandeter oder gerade gehaltener Stein ist bis zum Abmelden noch registriert -> ignorieren
        if (!enabled || hasDropped) return true;
        LogGesture("+ " + gesture);

        switch (gesture)
        {
            // --- Einmalige Bewegung ---
            case KinectGestures.Gestures.SwipeRight:
                TryMove(new Vector3(4, 0, 0));
                break;

            case KinectGestures.Gestures.SwipeLeft:
                TryMove(new Vector3(-4, 0, 0));
                break;


            case KinectGestures.Gestures.Jump:
                TryRotate();
                break;

            case KinectGestures.Gestures.Squat:
                Hold();
                break;

            // --- Kontinuierliche Bewegung, solange Hand zur Seite gehoben wird---
            case KinectGestures.Gestures.RaiseRightHand:
                moveRightActive = true;
                if (!moveLeftActive) { TryMove(new Vector3(4, 0, 0)); previousMoveTime = Time.time; } // erster Schritt sofort
                break;

            case KinectGestures.Gestures.RaiseLeftHand:
                moveLeftActive = true;
                if (!moveRightActive) { TryMove(new Vector3(-4, 0, 0)); previousMoveTime = Time.time; } // erster Schritt sofort
                break;
            
            case KinectGestures.Gestures.Tpose:
                TposeActive = true;
                TryRotate(); previousRotateTime = Time.time; // erste Drehung sofort
                break;

                //case KinectGestures.Gestures.Tpose:
                //    TposeActive = true;
                //    StartCoroutine(ResetTposeAfterDelay());
                //    break;

                //case KinectGestures.Gestures.Squat:
                //   HardDrop();
                // break
        }

        return true; // Gestenerkennung sofort neu starten
    }

    public bool GestureCancelled(uint userId, int userIndex, KinectGestures.Gestures gesture,
                                  KinectWrapper.NuiSkeletonPositionIndex joint)
    {
        LogGesture("- " + gesture);
        switch (gesture)
        {
            case KinectGestures.Gestures.Tpose:
                TposeActive = false;
                break;

            case KinectGestures.Gestures.RaiseLeftHand:
                moveLeftActive = false;
                break;

            case KinectGestures.Gestures.RaiseRightHand:
                moveRightActive = false;
                break;

                // SwipeLeft/SwipeRight brauchen hier keine Behandlung,
                // da sie keinen Dauerzustand setzen.
        }

        return true;
    }
    private IEnumerator ResetTposeAfterDelay()
    {
        yield return new WaitForSeconds(0.4f);
        TposeActive = false;
    }

    // =========================================================
    // ==================  TETRIS-LOGIK  =======================
    // =========================================================

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
        for (int x = MinX; x <= MaxX; x++)
        {
            if (grid[x, y] == null)
                return false;
        }
        return true;
    }

    void DeleteLine(int y)
    {
        for (int x = MinX; x <= MaxX; x++)
        {
            Destroy(grid[x, y].gameObject);
            grid[x, y] = null;
        }
    }

    void RowDown(int row)
    {
        for (int y = row + 1; y < height; y++)
        {
            for (int x = MinX; x <= MaxX; x++)
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

    void AddToGrid()
    {
        foreach (Transform collisionBlock in transform)
        {
            int x = Mathf.RoundToInt(collisionBlock.position.x / 4);
            int y = Mathf.RoundToInt(collisionBlock.position.y / 4);

            if (x >= MinX && x <= MaxX && y >= 0 && y < height)
            {
                grid[x, y] = collisionBlock;
            }
        }
    }

    int TopRow()
    {
        int top = int.MinValue;
        foreach (Transform collisionBlock in transform)
        {
            top = Mathf.Max(top, Mathf.RoundToInt(collisionBlock.position.y / 4));
        }
        return top;
    }

    bool ValidMove(Transform piece)
    {
        foreach (Transform collisionBlock in piece)
        {
            int x = Mathf.RoundToInt(collisionBlock.position.x / 4);
            int y = Mathf.RoundToInt(collisionBlock.position.y / 4);

            if (x < MinX || x > MaxX || y < 0 || y >= height)
                return false;

            if (grid[x, y] != null)
                return false;
        }

        return true;
    }
}