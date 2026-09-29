using System.Collections.Generic;
using UnityEngine;

public class SpawnTetromino : MonoBehaviour
{
    public NextPiecePreview preview;
    
    private GameObject holdPiece = null;
    private bool canHold = true;

    public HoldPreview holdPreview;


    public GameObject[] GetNextPieces()
    {
        return nextPieces.ToArray();
    }

    public GameObject[] Tetrominoes;

    // Enthält immer die nächsten 3 Steine
    private Queue<GameObject> nextPieces = new Queue<GameObject>();

    void Start()
    {
        // Queue mit 3 zufälligen Steinen füllen
        for (int i = 0; i < 3; i++)
        {
            AddRandomPiece();
        }

        // Ersten Stein spawnen
        NewTetromino();
    }

    void AddRandomPiece()
    {
        nextPieces.Enqueue(
            Tetrominoes[Random.Range(0, Tetrominoes.Length)]
        );
    }

    public void NewTetromino()
    {
        GameObject nextPiece = nextPieces.Dequeue();

        GameObject piece = Instantiate(nextPiece, transform.position, Quaternion.identity);

        piece.GetComponent<KinectSteuerung>().originalPrefab = nextPiece;

        AddRandomPiece();

        preview.RefreshPreview();

        canHold = true;
    }

    public void SpawnSpecificPiece(GameObject piecePrefab)
    {
        GameObject piece = Instantiate(
            piecePrefab,
            transform.position,
            Quaternion.identity
        );

        piece.GetComponent<KinectSteuerung>().originalPrefab = piecePrefab;

        canHold = false;
    }

    public void HoldPiece(GameObject currentPiece)
    {
        if (!canHold)
            return;


        KinectSteuerung steuerung = currentPiece.GetComponent<KinectSteuerung>();


        if (steuerung.ghost != null)
        {
            Destroy(steuerung.ghost);
        }


        // Erster Hold
        if (holdPiece == null)
        {
            holdPiece = steuerung.originalPrefab;

            // Hold-Anzeige aktualisieren
            holdPreview.UpdateHoldPreview(holdPiece);

            Destroy(currentPiece);

            canHold = false;

            NewTetromino();
        }


        // Tausch
        else
        {
            GameObject temp = holdPiece;

            holdPiece = steuerung.originalPrefab;

            // Hold-Anzeige aktualisieren
            holdPreview.UpdateHoldPreview(holdPiece);

            Destroy(currentPiece);

            SpawnSpecificPiece(temp);

            canHold = false;
        }
    }

}