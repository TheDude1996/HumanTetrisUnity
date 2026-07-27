using UnityEngine;

public class CollisionBlock : MonoBehaviour
{
    public string modelName = "Blocks/PlayerHead";

    void Start()
    {
        LoadModel();
    }


    void LoadModel()
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
        }
        else
        {
            Debug.LogError("Modell nicht gefunden: " + modelName);
        }
    }
}