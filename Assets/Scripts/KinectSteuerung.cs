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

    // Grid Definition
    public static int width = 11;
    public static int height = 40;
    public static Transform[,] grid = new Transform[width, height];

    // --- Kinect ---
    private bool squatActive = false;
    private bool moveLeftActive = false;
    private bool moveRightActive = false;

    public float moveRepeatTime = 0.3f; // Wiederholrate für Links/Rechts, solange Hand oben ist
    private float previousMoveTime;
    void Start()
    {
        previousTime = Time.time;

        // --- NEU: Sich selbst als Gesture-Listener beim KinectManager registrieren ---
        RegisterAsGestureListener();

        if (!ValidMove())
        {
            GameManager.Instance.GameOver();
        }
    }

    void Update()
    {
        // --- Bewegung Links/Rechts (Tastatur) ---
        if (Input.GetKeyDown(KeyCode.RightArrow))
        {
            TryMove(new Vector3(4, 0, 0));
        }
        else if (Input.GetKeyDown(KeyCode.LeftArrow))
        {
            TryMove(new Vector3(-4, 0, 0));
        }
        // --- Rotation (Tastatur) ---
        else if (Input.GetKeyDown(KeyCode.UpArrow))
        {
            TryRotate();
        }

        // --- Fallen ---
        bool schnellFallen = Input.GetKey(KeyCode.DownArrow) || squatActive;

        if (Time.time - previousTime > (schnellFallen ? fallTime / 10 : fallTime))
        {
            transform.position += new Vector3(0, -4, 0);

            if (!ValidMove())
            {
                transform.position -= new Vector3(0, -4, 0);
                AddToGrid();
                CheckforLines();

                // --- NEU: Sich selbst wieder als Gesture-Listener entfernen ---
                UnregisterAsGestureListener();

                this.enabled = false;
                FindObjectsByType<SpawnTetromino>(FindObjectsSortMode.None)[0].NewTetromino();
            }
            else
            {
                previousTime = Time.time;
            }
        }

        // --- Kontinuierliches Verschieben, solange Hand-Geste aktiv ist ---
        if ((moveLeftActive || moveRightActive) && Time.time - previousMoveTime > moveRepeatTime)
        {
            if (moveLeftActive) TryMove(new Vector3(-4, 0, 0));
            if (moveRightActive) TryMove(new Vector3(4, 0, 0));
            previousMoveTime = Time.time;
        }
    }

    // --- Hilfsfunktionen für Bewegung/Rotation ---
    void TryMove(Vector3 delta)
    {
        transform.position += delta;
        if (!ValidMove()) transform.position -= delta;
    }

    void TryRotate()
    {
        transform.RotateAround(transform.TransformPoint(rotationPoint), new Vector3(0, 0, 1), 90);
        if (!ValidMove()) transform.RotateAround(transform.TransformPoint(rotationPoint), new Vector3(0, 0, 1), -90);
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
        manager.DetectGesture(userId, KinectGestures.Gestures.Squat);
        manager.DetectGesture(userId, KinectGestures.Gestures.Wave); // -> Drehen
    }

    public void UserLost(uint userId, int userIndex)
    {
        squatActive = false;
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
        switch (gesture)
        {
            // --- Einmalige Bewegung ---
            case KinectGestures.Gestures.SwipeRight:
                TryMove(new Vector3(4, 0, 0));
                break;

            case KinectGestures.Gestures.SwipeLeft:
                TryMove(new Vector3(-4, 0, 0));
                break;

            // --- Kontinuierliche Bewegung, solange Hand oben bleibt ---
            case KinectGestures.Gestures.RaiseRightHand:
                moveRightActive = true;
                break;

            case KinectGestures.Gestures.RaiseLeftHand:
                moveLeftActive = true;
                break;

            case KinectGestures.Gestures.Wave:
                TryRotate();
                break;

            case KinectGestures.Gestures.Squat:
                squatActive = true;
                StartCoroutine(ResetSquatAfterDelay());
                break;
        }

        return true; // Gestenerkennung sofort neu starten
    }

    public bool GestureCancelled(uint userId, int userIndex, KinectGestures.Gestures gesture,
                                  KinectWrapper.NuiSkeletonPositionIndex joint)
    {
        switch (gesture)
        {
            case KinectGestures.Gestures.Squat:
                squatActive = false;
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
    private IEnumerator ResetSquatAfterDelay()
    {
        yield return new WaitForSeconds(0.5f);
        squatActive = false;
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