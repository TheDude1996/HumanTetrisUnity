using System.Collections.Generic;
using UnityEngine;

// Für den quadratischen O-Stein (2x2 Zellen): Statt eines kleinen Kopfes pro Kasten füllt EIN großer Kopf-Scan
// das ganze Quadrat. Der Kopf ist in vier Viertel geschnitten (PlayerDataManager.GetHeadQuadrants); jede Zelle trägt
// ihr Viertel. Dadurch funktioniert der Line Break wie bei allen anderen Steinen: Zellen werden einzeln gelöscht
// oder verschoben, und ihr Viertel des Kopfes geht mit.
//
// Ist (noch) kein Kopf-Scan da, bleibt der normale Kopf im Kopfkasten (CollisionBlock) bestehen.
public class BigHeadBlock : MonoBehaviour
{
    private readonly List<GameObject> spawned = new List<GameObject>();
    private bool subscribed;

    void Start()
    {
        var data = PlayerDataManager.Instance;

        if (data.HeadTemplate != null)
        {
            Apply();
            return;
        }

        if (data.HeadLoading)
        {
            data.HeadChanged += OnHeadChanged;
            subscribed = true;
        }
    }

    void OnDestroy()
    {
        if (subscribed && PlayerDataManager.Existing != null)
            PlayerDataManager.Existing.HeadChanged -= OnHeadChanged;
    }

    void OnHeadChanged()
    {
        if (this == null) return;
        if (PlayerDataManager.Instance.HeadTemplate != null) Apply();
    }

    void Apply()
    {
        GameObject[] quadrants = PlayerDataManager.Instance.GetHeadQuadrants();
        if (quadrants == null) return;

        // Den kleinen Kopf im Kopfkasten durch den großen ersetzen
        foreach (var socket in GetComponentsInChildren<CollisionBlock>(true))
        {
            socket.enabled = false;
            Destroy(socket.gameObject);
        }

        foreach (var old in spawned) if (old != null) Destroy(old);
        spawned.Clear();

        // Die vier Zellen (direkte Kinder mit MeshRenderer) und ihre Mitte
        var cells = new List<Transform>();
        foreach (Transform child in transform)
            if (child.GetComponent<MeshRenderer>() != null) cells.Add(child);
        if (cells.Count != 4) return;

        Vector3 center = Vector3.zero;
        foreach (var c in cells) center += c.localPosition;
        center /= cells.Count;

        foreach (var cell in cells)
        {
            int index = (cell.localPosition.x >= center.x ? 1 : 0) + (cell.localPosition.y >= center.y ? 1 : 0) * 2;

            GameObject part = Instantiate(quadrants[index], cell);
            part.name = "HeadQuadrant";
            // Zellen sind mit 4 skaliert: Kind mit 1/4, damit die Mesh-Koordinaten Welteinheiten bleiben
            part.transform.localScale = Vector3.one / cell.localScale.x;
            part.transform.localPosition = (center - cell.localPosition) / cell.localScale.x;
            part.transform.localRotation = Quaternion.identity;
            part.SetActive(true);
            spawned.Add(part);
        }
    }
}
