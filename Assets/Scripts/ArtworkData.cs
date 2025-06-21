using UnityEngine;

// This class holds all the data related to a specific artwork in the museum
// The attribute called "Serializable" allows this class to be serialized by Unity,
// which means it can be saved and loaded, and its fields can be edited in the Unity
[System.Serializable]
public class ArtworkData
{
    [Header("Artwork Details")]
    public string referenceImageName;       // Reference name that matches the one in the Reference Image Library.

    [TextArea(3, 5)]
    public string description;              // Description of the artwork
    
    [Header("Character")]
    public GameObject characterPrefab;      // The character's prefab that explains this artwork. It should be a GameObject with a CharacterDialogue script attached to it.
    public Vector3 characterOffset = Vector3.zero;  // Offset with respect to the center of the artwork where the character should be placed.
    
}