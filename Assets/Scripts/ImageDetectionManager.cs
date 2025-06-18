using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

// This script manages the detection of images in AR and spawns characters based on the detected images.
public class ImageDetectionManager : MonoBehaviour
{
    [Header("AR Configuration")]
    [SerializeField] private ARTrackedImageManager trackedImageManager;


    [Header("Artworks Database")]
    [SerializeField] private ArtworkData[] artworksDatabase;

    [Header("Spawning Information")]
    [SerializeField] private LayerMask groundLayerMask; // Ground plane layer for raycasting.


    // Dictionary for quick lookup to artwork data by reference name.
    private Dictionary<string, ArtworkData> artworkLookup;


    // Dictionary for managing character states by trackable ID
    private Dictionary<TrackableId, GameObject> characterStates;


    void Start()
    {
        // Data structures initialization
        InitializeArtworkLookup();
        characterStates = new Dictionary<TrackableId, GameObject>();

        if (trackedImageManager == null)
        {
            trackedImageManager = FindAnyObjectByType<ARTrackedImageManager>();
            if (trackedImageManager == null)
            {
                Debug.LogError("ARTrackedImageManager non trovato! Assicurati che sia presente nella scena.");
                return;
            }
        }

        // Ci registriamo agli eventi di tracking delle immagini
        OnEnable();

    }

    // Questo metodo ausiliario crea una lookup table per accesso rapido ai dati delle opere
    private void InitializeArtworkLookup()
    {
        artworkLookup = new Dictionary<string, ArtworkData>();

        foreach (ArtworkData artwork in artworksDatabase)
        {
            if (!string.IsNullOrEmpty(artwork.referenceImageName))
            {
                artworkLookup[artwork.referenceImageName] = artwork;
                Debug.Log($"Registrata opera: {artwork.artworkName} con immagine di riferimento: {artwork.referenceImageName}");
            }
            else
            {
                Debug.LogWarning($"Opera {artwork.artworkName} non ha un nome di immagine di riferimento valido!");
            }
        }
    }

    // Event methods

    void OnEnable() => trackedImageManager.trackablesChanged.AddListener(OnChanged);

    void OnDisable() => trackedImageManager.trackablesChanged.RemoveListener(OnChanged);

    void OnChanged(ARTrackablesChangedEventArgs<ARTrackedImage> eventArgs)
    {
        // Handling the addition of new tracked images
        foreach (ARTrackedImage trackedImage in eventArgs.added)
        {
            HandleImageDetected(trackedImage);
        }

        // Handling updated tracked images
        foreach (ARTrackedImage trackedImage in eventArgs.updated)
        {
            HandleImageUpdated(trackedImage);
        }

        // Handling removed tracked images
        foreach (var trackedImage in eventArgs.removed)
        {
            //TrackableId removedImageTrackableId = trackedImage.Key;
            //ARTrackedImage removedImage = trackedImage.Value;
            HandleImageRemoved(trackedImage.Key);
        }
    }

    void OnDestroy()
    {
        // Importante: deregistriamo gli eventi per evitare memory leaks
        if (trackedImageManager != null)
        {
            OnDisable();
        }

        CleanupAllCharacters();
    }


    // Metodo chiamato quando una nuova immagine viene rilevata
    private void HandleImageDetected(ARTrackedImage trackedImage)
    {
        string imageName = trackedImage.referenceImage.name;
        if (string.IsNullOrEmpty(imageName))
        {
            Debug.LogWarning("Immagine rilevata senza nome valido!");
            return;
        }

        Debug.Log($"Immagine rilevata: {imageName}");

        // Cerchiamo i dati dell'opera corrispondente
        if (artworkLookup.TryGetValue(imageName, out ArtworkData artworkData))
        {
            // Se non abbiamo già spawnato un personaggio per questa immagine, lo creiamo
            if (!characterStates.ContainsKey(trackedImage.trackableId))
            {
                SpawnCharacterForArtwork(trackedImage, artworkData);
            }
        }
        else
        {
            Debug.LogWarning($"Nessuna opera trovata per l'immagine: {imageName}");
        }
    }

    private void HandleImageUpdated(ARTrackedImage trackedImage)
    {
        if (!characterStates.TryGetValue(trackedImage.trackableId, out GameObject character))
            return;

        Debug.Log($"Tracking state: {trackedImage.trackingState} per l'immagine: {trackedImage.referenceImage.name}");

        // Gestione basata sullo stato di tracking
        if (trackedImage.trackingState == TrackingState.Tracking)
        {
            UpdateCharacterPose(trackedImage, character);
            // Assicurati che il personaggio sia visibile
            if (!character.activeInHierarchy)
            {
                character.SetActive(true);
            }
        }
        //else if (trackedImage.trackingState == TrackingState.None)
        else
        {
            character.SetActive(false);
        }
    }

    // Metodo chiamato quando un'immagine viene rimossa o persa
    private void HandleImageRemoved(TrackableId trackableId)
    {
        CleanupCharacter(trackableId);
    }


    // Questo metodo si occupa di creare e posizionare il personaggio
    private void SpawnCharacterForArtwork(ARTrackedImage trackedImage, ArtworkData artworkData)
    {
        if (artworkData.characterPrefab == null)
        {
            Debug.LogWarning($"Nessun prefab personaggio assegnato per l'opera: {artworkData.artworkName}");
            return;
        }

        // Calcoliamo la posizione del personaggio
        Vector3 characterPosition = CalculateCharacterPosition(trackedImage, artworkData);

        // Creiamo il personaggio
        GameObject character = Instantiate(artworkData.characterPrefab, trackedImage.transform);
        //GameObject character = Instantiate(artworkData.characterPrefab, characterPosition, Quaternion.identity);

        // Applichiamo la scala
        character.transform.localScale = Vector3.one * artworkData.characterScale;

        // Orientamento verso la camera
        OrientCharacterTowardsCamera(character);

        // Crea lo stato del personaggio
        characterStates[trackedImage.trackableId] = character;

        // Se il personaggio ha un componente per gestire i dialoghi, inizializziamolo
        CharacterDialogue dialogueComponent = character.GetComponent<CharacterDialogue>();
        if (dialogueComponent != null)
        {
            dialogueComponent.InitializeDialogue(artworkData);
        }

        Debug.Log($"Personaggio spawnato per l'opera: {artworkData.artworkName}");
    }

    // Questo metodo calcola dove posizionare il personaggio rispetto al quadro
    private Vector3 CalculateCharacterPosition(ARTrackedImage trackedImage, ArtworkData artworkData)
    {
        // Applichiamo l'offset specificato nei dati dell'opera
        Vector3 characterPosition = trackedImage.transform.position +
                                    (trackedImage.transform.right * artworkData.characterOffset.x) +
                                    (trackedImage.transform.up * artworkData.characterOffset.z) +
                                    (trackedImage.transform.forward * artworkData.characterOffset.y);
             
        //Vector3 characterPosition = trackedImage.transform.position;
        // Usa il metodo per trovare la posizione del pavimento
        Vector3 groundPosition = FindBestGroundPosition(characterPosition);

        return groundPosition;
    }

    private Vector3 FindBestGroundPosition(Vector3 targetPosition)
    {
        // Prima prova con il raycast
        RaycastHit hit_down;
        RaycastHit hit_up;

        Physics.Raycast(targetPosition, Vector3.down, out hit_down, 5.0f, groundLayerMask);
        Physics.Raycast(targetPosition, Vector3.up, out hit_up, 5.0f, groundLayerMask);

        if (hit_down.collider != null && hit_up.collider != null)
        {
            return (Vector3.Distance(targetPosition, hit_down.point) < Vector3.Distance(targetPosition, hit_up.point)) ? hit_down.point : hit_up.point;
        }
        else if (hit_down.collider != null)
        {
            return hit_down.point;
        }
        else if (hit_up.collider != null)
        {
            return hit_up.point;
        }
        
        // Se il raycast fallisce, cerca il plane AR più vicino
        ARPlane closestPlane = FindClosestARPlane(targetPosition);
        if (closestPlane != null)
        {
            // Proietta la posizione target sul plane più vicino
            Vector3 planePosition = closestPlane.transform.position;
            Vector3 planeNormal = closestPlane.transform.up;

            // Calcola la proiezione del punto target sul piano
            Vector3 toTarget = targetPosition - planePosition;
            float distanceToPlane = Vector3.Dot(toTarget, planeNormal);
            Vector3 projectedPosition = targetPosition - (planeNormal * distanceToPlane);

            Debug.Log("Pavimento trovato tramite plane AR più vicino");
            return projectedPosition;
        }

        // Come ultima risorsa, usa una posizione di default
        Debug.LogWarning("Pavimento non trovato, usando posizione default");
        return new Vector3(targetPosition.x, targetPosition.y, targetPosition.z);
    }

    private ARPlane FindClosestARPlane(Vector3 position)
    {
        ARPlane closestPlane = null;
        float closestDistance = float.MaxValue;

        // Trova tutti i plane AR attivi
        ARPlane[] allPlanes = FindObjectsByType<ARPlane>(FindObjectsSortMode.None);

        foreach (ARPlane plane in allPlanes)
        {
            if (plane.gameObject.activeInHierarchy && plane.trackingState == TrackingState.Tracking)
            {
                float distance = Vector3.Distance(position, plane.transform.position);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestPlane = plane;
                }
            }
        }

        return closestPlane;
    }

    private void UpdateCharacterPose(ARTrackedImage trackedImage, GameObject character)
    {
        string imageName = trackedImage.referenceImage.name;
        
        if (artworkLookup.TryGetValue(imageName, out ArtworkData artworkData))
        {
            Vector3 newPosition = CalculateCharacterPosition(trackedImage, artworkData);
            character.transform.position = newPosition;
            OrientCharacterTowardsCamera(character);
        }
    }
    
    private void OrientCharacterTowardsCamera(GameObject character)
    {
        Vector3 lookDirection = Camera.main.transform.position - character.transform.position;
        lookDirection.y = 0;
        if (lookDirection != Vector3.zero)
        {
            character.transform.rotation = Quaternion.LookRotation(lookDirection) * Quaternion.Euler(0, 90, 0); 
        }
    }
    
    private void CleanupCharacter(TrackableId trackableId)
    {
        if (characterStates.TryGetValue(trackableId, out GameObject character))
        {
            Debug.Log($"Immagine persa, rimuovo il personaggio per trackableId: {trackableId}");
            
            // Distruggi il personaggio
            if (character != null)
            {
                Destroy(character);
            }
            
        }
    }
    
    private void CleanupAllCharacters()
    {
        foreach (var kvp in characterStates)
        {
            CleanupCharacter(kvp.Key);
        }
        characterStates.Clear();
    }


}