using UnityEngine;

// This class holds all the data related to a specific artwork in the museum
// The attribute called "Serializable" allows this class to be serialized by Unity,
// which means it can be saved and loaded, and its fields can be edited in the Unity
[System.Serializable]
public class ArtworkData
{
    [Header("Artwork ID")]
    public string artworkName;              // Artwork name for debugging and UI
    public string referenceImageName;       // Reference name that matches the one in the Reference Image Library.

    [Header("Artwork Details")]
    public string artistName;               // Artist's name
    public int yearCreated;                 // Year the artwork was created
    [TextArea(3, 5)]
    public string description;              // Description of the artwork
    
    [Header("Character")]
    public GameObject characterPrefab;      // The character's prefab that explains this artwork. It should be a GameObject with a CharacterDialogue script attached to it.
    public float characterScale = 1.0f;     // The scale of the character (some artworks may require larger or smaller characters).
    
    [Header("Position")]
    public Vector3 characterOffset = Vector3.zero;  // Offset with respect to the center of the artwork where the character should be placed.
    public float distanceFromWall = 1.5f;          // Distance from the wall where the artwork is hung.
    
    
    [Header("Dialogues")]
    [TextArea(5, 10)]
    public string[] dialogueLines;          // Set of dialogue lines that the character will say when the player interacts with the artwork.
}