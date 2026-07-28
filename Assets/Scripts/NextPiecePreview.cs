using System.Collections.Generic;
using UnityEngine;

public class NextPiecePreview : MonoBehaviour
{
    public SpawnTetromino spawner;

    public Transform preview1;
    public Transform preview2;
    public Transform preview3;

    private List<GameObject> currentPreviews = new List<GameObject>();

    public void RefreshPreview()
    {

        Debug.Log("RefreshPreview");
        // Alte Vorschau löschen

        foreach (GameObject obj in currentPreviews)
        {
            Destroy(obj);
        }

        currentPreviews.Clear();

        GameObject[] nextPieces = spawner.GetNextPieces();

        Transform[] previewPositions =
        {
            preview1,
            preview2,
            preview3
        };

        for (int i = 0; i < nextPieces.Length && i < previewPositions.Length; i++)
        {
            GameObject preview = Instantiate(
                nextPieces[i],
                previewPositions[i].position,
                Quaternion.identity,
                previewPositions[i]
            );

            currentPreviews.Add(preview);

            // Steuerung deaktivieren
            Steuerung control = preview.GetComponent<Steuerung>();
            if (control != null)
                control.enabled = false;

            // Etwas kleiner darstellen
            preview.transform.localScale = Vector3.one * 0.5f;
        }
    }
}