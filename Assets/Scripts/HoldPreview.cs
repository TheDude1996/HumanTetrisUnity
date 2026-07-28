using UnityEngine;

public class HoldPreview : MonoBehaviour
{
    private GameObject currentHoldPreview;

    public void UpdateHoldPreview(GameObject piecePrefab)
    {
        // Alten Hold-Stein entfernen
        if (currentHoldPreview != null)
        {
            Destroy(currentHoldPreview);
        }

        // Wenn nichts gehalten wird, nichts anzeigen
        if (piecePrefab == null)
            return;


        // Neuen Vorschau-Stein erzeugen
        currentHoldPreview = Instantiate(
            piecePrefab,
            transform.position,
            Quaternion.identity
        );


        // Steuerung entfernen
        Steuerung steuerung = currentHoldPreview.GetComponent<Steuerung>();

        if (steuerung != null)
        {
            Destroy(steuerung);
        }


        // Ghost entfernen, falls erzeugt
        foreach (Transform child in currentHoldPreview.transform)
        {
            Collider collider = child.GetComponent<Collider>();

            if (collider != null)
                Destroy(collider);
        }


        // kleiner machen
        currentHoldPreview.transform.localScale = Vector3.one * 0.5f;
    }
}