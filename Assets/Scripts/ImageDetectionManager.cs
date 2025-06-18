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
    [SerializeField] private ARAnchorManager anchorManager;


    [Header("Artworks Database")]
    [SerializeField] private ArtworkData[] artworksDatabase;

    [Header("Spawning Information")]
    [SerializeField] private LayerMask groundLayerMask = 1; // Ground plane layer for raycasting.

    [Header("Stability Settings")]
    [SerializeField] private float minTrackingTime = 2.0f; // Minimum time before creating an anchor.
    [SerializeField] private bool useAnchors = true; // Flag to enable/disable anchors.


    // Dictionary for quick lookup to artwork data by reference name.
    private Dictionary<string, ArtworkData> artworkLookup;

    // Dictionary per tenere traccia dei personaggi già spawnati per ogni immagine
    // private Dictionary<TrackableId, GameObject> spawnedCharacters;

    // Private class for managing character states
    [System.Serializable]
    private class CharacterState
    {
        public GameObject character;
        public ARAnchor anchor;
        public bool isAnchored;
        public float trackingStartTime;
        public Vector3 lastKnownPosition;

        public CharacterState(GameObject characterObj)
        {
            character = characterObj;
            anchor = null;
            isAnchored = false;
            trackingStartTime = Time.time;
            lastKnownPosition = Vector3.zero;
        }
    }

    // Dictionary for managing character states by trackable ID
    private Dictionary<TrackableId, CharacterState> characterStates;


    void Start()
    {
        // Data structures initialization
        InitializeArtworkLookup();
        //spawnedCharacters = new Dictionary<TrackableId, GameObject>();
        characterStates = new Dictionary<TrackableId, CharacterState>();


        // Se non è stato assegnato il TrackedImageManager o l'AnchorManager nell'inspector, proviamo a trovarli
        if (trackedImageManager == null)
        {
            trackedImageManager = FindAnyObjectByType<ARTrackedImageManager>();
            if (trackedImageManager == null)
            {
                Debug.LogError("ARTrackedImageManager non trovato! Assicurati che sia presente nella scena.");
                return;
            }
        }

        
        if (anchorManager == null && useAnchors)
        {
            anchorManager = FindAnyObjectByType<ARAnchorManager>();
            if (anchorManager == null)
            {
                Debug.LogWarning("ARAnchorManager non trovato! Gli anchor non saranno disponibili. Aggiungi un ARAnchorManager alla scena per maggiore stabilità.");
                useAnchors = false;
            }
        }


        // Ci registriamo agli eventi di tracking delle immagini
        //trackedImageManager.trackedImagesChanged += OnTrackedImagesChanged;

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
            //trackedImageManager.trackedImagesChanged -= OnTrackedImagesChanged;
            OnDisable();
        }

        // Pulisci tutti gli anchor creati
        CleanupAllCharacters();

    }


    // Metodo chiamato quando una nuova immagine viene rilevata
    private void HandleImageDetected(ARTrackedImage trackedImage)
    {
        string imageName = trackedImage.referenceImage.name;
        Debug.Log($"Immagine rilevata: {imageName}");

        // Cerchiamo i dati dell'opera corrispondente
        if (artworkLookup.TryGetValue(imageName, out ArtworkData artworkData))
        {
            // Se non abbiamo già spawnato un personaggio per questa immagine, lo creiamo
            //if (!spawnedCharacters.ContainsKey(trackedImage.trackableId))
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

    // Metodo chiamato quando un'immagine già tracciata viene aggiornata
    /*
    private void HandleImageUpdated(ARTrackedImage trackedImage)
    {
        // Se l'immagine è ancora tracciata bene, aggiorniamo la posizione del personaggio
        if (trackedImage.trackingState == TrackingState.Tracking)
        {
            if (spawnedCharacters.TryGetValue(trackedImage.trackableId, out GameObject character))
            {
                UpdateCharacterPosition(trackedImage, character);
            }
        }
        // Se l'immagine non è più tracciata bene, nascondiamo il personaggio
        else if (trackedImage.trackingState == TrackingState.Limited ||
                 trackedImage.trackingState == TrackingState.None)
        {
            if (spawnedCharacters.TryGetValue(trackedImage.trackableId, out GameObject character))
            {
                character.SetActive(false);
            }
        }
    }
    */
    private void HandleImageUpdated(ARTrackedImage trackedImage)
    {
        if (!characterStates.TryGetValue(trackedImage.trackableId, out CharacterState characterState))
            return;

        // Se il personaggio è già ancorato, non facciamo nulla
        if (characterState.isAnchored && useAnchors)
        {
            // L'anchor mantiene automaticamente la posizione stabile
            return;
        }

        // Gestione basata sullo stato di tracking
        if (trackedImage.trackingState == TrackingState.Tracking)
        {
            // Se stiamo usando gli anchor e abbiamo tracciato abbastanza a lungo, creiamo l'anchor
            if (useAnchors && !characterState.isAnchored &&
                Time.time - characterState.trackingStartTime >= minTrackingTime)
            {
                CreateAnchorForCharacter(trackedImage, characterState);
            }
            else if (!useAnchors)
            {
                // Se non usiamo anchor, aggiorna manualmente la posizione
                UpdateCharacterPosition(trackedImage, characterState.character);
            }

            // Assicurati che il personaggio sia visibile
            if (!characterState.character.activeInHierarchy)
            {
                characterState.character.SetActive(true);
            }
        }
        else if (trackedImage.trackingState == TrackingState.Limited)
        {
            // Se il tracking è limitato ma abbiamo un anchor, mantieni il personaggio visibile
            if (useAnchors && characterState.isAnchored)
            {
                // Non nascondere il personaggio, l'anchor mantiene la posizione
                return;
            }
            else
            {
                // Senza anchor, nascondi temporaneamente il personaggio
                characterState.character.SetActive(false);
            }
        }
        else // TrackingState.None
        {
            // Se il tracking è completamente perso
            if (useAnchors && characterState.isAnchored)
            {
                // Con anchor, mantieni il personaggio visibile
                return;
            }
            else
            {
                // Senza anchor, nascondi il personaggio
                characterState.character.SetActive(false);
            }
        }
    }


    // Metodo chiamato quando un'immagine non è più rilevata --- non lo uso perché è cambiata la libreria ARFoundation
    /*
    private void HandleImageLost(ARTrackedImage trackedImage)
    {
        if (spawnedCharacters.TryGetValue(trackedImage.trackableId, out GameObject character))
        {
            // Distruggiamo il personaggio quando l'immagine non è più tracciata
            Destroy(character);
            spawnedCharacters.Remove(trackedImage.trackableId);

            // Handle removed event nella documentazione di Unity
            //TrackableId removedImageTrackableId = trackedImage.Key;
            //ARTrackedImage removedImage = trackedImage.Value;

            Debug.Log($"Personaggio rimosso per immagine: {trackedImage.referenceImage.name}");
        }
    }
    */

    private void HandleImageRemoved(TrackableId trackableId)
    {
        if (characterStates.TryGetValue(trackableId, out CharacterState characterState))
        {
            // Se usiamo anchor, il personaggio può rimanere anche dopo che l'immagine è persa
            if (useAnchors && characterState.isAnchored)
            {
                Debug.Log($"Immagine persa ma personaggio mantenuto tramite anchor");
                // Opzionalmente, potresti voler nascondere il personaggio dopo un certo tempo
                // StartCoroutine(HideCharacterAfterDelay(characterState, 10.0f));
            }
            else
            {
                // Senza anchor, rimuovi completamente il personaggio
                CleanupCharacter(trackableId);
            }
        }
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
        GameObject character = Instantiate(artworkData.characterPrefab, characterPosition, Quaternion.identity);

        // Applichiamo la scala
        character.transform.localScale = Vector3.one * artworkData.characterScale;

        // Crea un anchor per stabilizzare la posizione
        /*
        ARAnchor anchor = GetComponent<ARAnchorManager>()?.AddAnchor(new Pose(characterPosition, character.transform.rotation));
        if (anchor != null)
        {
            character.transform.SetParent(anchor.transform);
        }
        */

        /*
        // Facciamo guardare il personaggio verso la camera
        Vector3 lookDirection = Camera.main.transform.position - character.transform.position;
        lookDirection.y = 0; // Manteniamo solo la rotazione orizzontale
        if (lookDirection != Vector3.zero)
        {
            character.transform.rotation = Quaternion.LookRotation(lookDirection);
        }

        // Registriamo il personaggio nella nostra dictionary
        spawnedCharacters[trackedImage.trackableId] = character;
        */

        // Orientamento verso la camera
        OrientCharacterTowardsCamera(character);

        // Crea lo stato del personaggio
        CharacterState characterState = new CharacterState(character);
        characterStates[trackedImage.trackableId] = characterState;


        // Se il personaggio ha un componente per gestire i dialoghi, inizializziamolo
        CharacterDialogue dialogueComponent = character.GetComponent<CharacterDialogue>();
        if (dialogueComponent != null)
        {
            dialogueComponent.InitializeDialogue(artworkData);
        }

        Debug.Log($"Personaggio spawnato per l'opera: {artworkData.artworkName}");
    }

    private async Task CreateAnchorForCharacter(ARTrackedImage trackedImage, CharacterState characterState)
    {
        if (anchorManager == null || characterState.isAnchored)
            return;

        // Crea l'anchor nella posizione attuale del personaggio
        Pose anchorPose = new Pose(characterState.character.transform.position,
                                  characterState.character.transform.rotation);

        // ARFoundation 6.2
        var anchorRequest = await anchorManager.TryAddAnchorAsync(anchorPose);
        if (anchorRequest.status.IsSuccess())
        {
            // L'anchor è stato creato con successo
            characterState.anchor = anchorRequest.value;
            characterState.isAnchored = true;

            // Rendi il personaggio figlio dell'anchor per la stabilità
            characterState.character.transform.SetParent(characterState.anchor.transform, true);

            Debug.Log($"Anchor creato per il personaggio di {trackedImage.referenceImage.name}");
        }
        else
        {
            Debug.LogWarning($"Impossibile creare anchor per il personaggio di {trackedImage.referenceImage.name}");
        }
    }


    // Questo metodo calcola dove posizionare il personaggio rispetto al quadro
    private Vector3 CalculateCharacterPosition(ARTrackedImage trackedImage, ArtworkData artworkData)
    {
        // Partiamo dalla posizione del quadro rilevato
        Vector3 imagePosition = trackedImage.transform.position;
        Vector3 imageForward = trackedImage.transform.forward; // z axis
        Vector3 imageRight = trackedImage.transform.right; // x axis

        // Applichiamo l'offset specificato nei dati dell'opera
        Vector3 offsetPosition = imagePosition +
                                (imageRight * artworkData.characterOffset.x) +
                                (trackedImage.transform.up * artworkData.characterOffset.y) +
                                (imageForward * artworkData.characterOffset.z);

        // Spostiamo il personaggio davanti al quadro della distanza specificata
        Vector3 characterPosition = offsetPosition + (imageForward * artworkData.distanceFromWall);

        // Ora dobbiamo trovare il pavimento usando un raycast verso il basso
        /*
        RaycastHit hit;
        Vector3 rayOrigin = characterPosition + Vector3.up * 2.0f; // Partiamo da 2 metri sopra

        if (Physics.Raycast(rayOrigin, Vector3.down, out hit, 5.0f, groundLayerMask))
        {
            // Se troviamo il pavimento, posizioniamo il personaggio lì
            characterPosition.y = hit.point.y;
        }
        else
        {
            // Se non troviamo il pavimento, usiamo una posizione di default
            characterPosition.y = imagePosition.y - 1.0f; // 1 metro sotto il quadro
            //characterPosition.y = 0.0; // hardcoding for debugging purposes
            Debug.LogWarning("Pavimento non trovato per il posizionamento del personaggio. Usando posizione di default.");
        }
        */

        // Usa il metodo per trovare la posizione del pavimento
        Vector3 groundPosition = FindBestGroundPosition(characterPosition);
        characterPosition.y = groundPosition.y;


        return characterPosition;
    }

    /*
    // Questo metodo aggiorna la posizione del personaggio quando l'immagine si muove
    private void UpdateCharacterPosition(ARTrackedImage trackedImage, GameObject character)
    {
        string imageName = trackedImage.referenceImage.name;

        if (artworkLookup.TryGetValue(imageName, out ArtworkData artworkData))
        {
            Vector3 newPosition = CalculateCharacterPosition(trackedImage, artworkData);
            character.transform.position = newPosition;

            // Aggiorniamo anche la rotazione per far guardare sempre il personaggio verso la camera
            Vector3 lookDirection = Camera.main.transform.position - character.transform.position;
            lookDirection.y = 0;
            if (lookDirection != Vector3.zero)
            {
                character.transform.rotation = Quaternion.LookRotation(lookDirection);
            }
        }
    }
    */


    // Aggiungi questo metodo al tuo ImageDetectionManager
    private Vector3 FindBestGroundPosition(Vector3 targetPosition)
    {
        // Prima prova con il raycast come già fai
        RaycastHit hit;
        Vector3 rayOrigin = targetPosition + Vector3.up * 2.0f;

        if (Physics.Raycast(rayOrigin, Vector3.down, out hit, 5.0f, groundLayerMask))
        {
            Debug.Log("Pavimento trovato tramite raycast");
            return hit.point;
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
        return new Vector3(targetPosition.x, 0.0f, targetPosition.z);
    }

    private ARPlane FindClosestARPlane(Vector3 position)
    {
        ARPlane closestPlane = null;
        float closestDistance = float.MaxValue;

        // Trova tutti i plane AR attivi
        ARPlane[] allPlanes = FindObjectsOfType<ARPlane>();

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

    private void UpdateCharacterPosition(ARTrackedImage trackedImage, GameObject character)
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
            character.transform.rotation = Quaternion.LookRotation(lookDirection);
        }
    }
    
    private void CleanupCharacter(TrackableId trackableId)
    {
        if (characterStates.TryGetValue(trackableId, out CharacterState characterState))
        {
            // Distruggi l'anchor se presente
            if (characterState.anchor != null)
            {
                anchorManager.TryRemoveAnchor(characterState.anchor);
            }
            
            // Distruggi il personaggio
            if (characterState.character != null)
            {
                Destroy(characterState.character);
            }
            
            characterStates.Remove(trackableId);
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