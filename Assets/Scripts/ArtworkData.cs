using UnityEngine;

// This class holds all the data related to a specific artwork in the museum
// The attribute called "Serializable" allows this class to be serialized by Unity,
// which means it can be saved and loaded, and its fields can be edited in the Unity
[System.Serializable]
public class ArtworkData
{
    [Header("Identificazione")]
    public string artworkName;              // Nome dell'opera per scopi di debug e UI
    public string referenceImageName;       // Nome che corrisponde a quello nella Reference Image Library
    
    [Header("Personaggio")]
    public GameObject characterPrefab;      // Il prefab del personaggio che spiega quest'opera
    public float characterScale = 1.0f;     // Scala del personaggio (alcune opere potrebbero richiedere personaggi più grandi o piccoli)
    
    [Header("Posizionamento")]
    public Vector3 characterOffset = Vector3.zero;  // Offset rispetto al centro del quadro dove posizionare il personaggio
    public float distanceFromWall = 1.5f;          // Distanza dalla parete su cui è appeso il quadro
    
    [Header("Informazioni Opera")]
    public string artistName;               // Nome dell'artista
    public int yearCreated;                 // Anno di creazione
    [TextArea(3, 5)]
    public string description;              // Descrizione dell'opera
    
    [Header("Dialoghi")]
    [TextArea(5, 10)]
    public string[] dialogueLines;          // Array di stringhe per il dialogo del personaggio
}