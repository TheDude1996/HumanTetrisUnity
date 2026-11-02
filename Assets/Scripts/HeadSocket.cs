using UnityEngine;

// Setzt den Kopf des Spielers in den Glaskasten eines Steins.
// Vorrang hat der Kopf-Scan aus dem Netzwerkordner (PlayerDataManager, GLB).
// Gibt es keinen (kein Netzwerk / kein Spieler / Datei fehlt), wird der Standardkopf aus Resources genommen.
// Lädt der Netzwerk-Kopf noch, wird zunächst der Standardkopf gezeigt und ausgetauscht, sobald er fertig ist.
public class CollisionBlock : MonoBehaviour
{
    public string modelName = "Blocks/PlayerHead";

    private GameObject currentHead;
    private bool subscribed;

    void Start()
    {
        var data = PlayerDataManager.Instance;

        if (data.HeadTemplate != null)
        {
            ShowNetworkHead();
            return;
        }

        LoadFallbackModel();

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
        if (PlayerDataManager.Instance.HeadTemplate != null) ShowNetworkHead();
    }

    void ShowNetworkHead()
    {
        // Mitte und Größe des Glaskastens (Parent); Fallback: 4 Einheiten = 1 Feld
        Transform box = transform.parent != null ? transform.parent : transform;
        float blockSize = transform.parent != null ? transform.parent.lossyScale.x : 4f;

        GameObject head = PlayerDataManager.Instance.CreateHead(transform, box.position, box.rotation, blockSize);
        if (head == null) return;

        if (currentHead != null) Destroy(currentHead);
        currentHead = head;
    }

    void LoadFallbackModel()
    {
        GameObject model = Resources.Load<GameObject>(modelName);

        if (model != null)
        {
            GameObject newModel = Instantiate(
                model,
                transform.position,
                transform.rotation,
                transform
            );

            // lokale Position anpassen
            newModel.transform.localPosition = Vector3.zero;
            newModel.transform.localRotation = Quaternion.identity;
            currentHead = newModel;
        }
        else
        {
            Debug.LogError("Modell nicht gefunden: " + modelName);
        }
    }
}
