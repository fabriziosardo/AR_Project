// Script temporaneo per misurare il modello
using UnityEngine;

public class ModelMeasurer : MonoBehaviour
{
    void Start()
    {
        Renderer renderer = GetComponent<Renderer>();
        if (renderer != null)
        {
            Vector3 size = renderer.bounds.size;
            Debug.Log($"Dimensioni modello - Larghezza: {size.x}m, Altezza: {size.y}m, Profondità: {size.z}m");
        }
    }
}