using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using TMPro;

public class ArtworkCharacterManager : MonoBehaviour
{

    [Header("AR Configuration")]
    [SerializeField] public List<ArtworkData> artworksDatabase;


    private ARTrackedImageManager _trackedImageManager;
    private Dictionary<string, GameObject> _characters;
    private Dictionary<string, string> _artworks_descriptions;


    void Start()
    {
        _trackedImageManager = GetComponent<ARTrackedImageManager>();
        if (_trackedImageManager == null)
        {
            Debug.LogError("ARTrackedImageManager component is missing from this GameObject.");
            return;
        }
        _trackedImageManager.trackablesChanged.AddListener(OnTrackedImagesChanged);

        _characters = new Dictionary<string, GameObject>();
        _artworks_descriptions = new Dictionary<string, string>();
        InstantiateCharacters();
    }

    private void InstantiateCharacters()
    {
        foreach (var artwork in artworksDatabase)
        {
            if (artwork.characterPrefab == null)
            {
                Debug.LogWarning($"Character prefab is missing for artwork: {artwork.artworkName}");
                continue;
            }

            GameObject characterInstance = Instantiate(artwork.characterPrefab, Vector3.zero, Quaternion.identity);
            characterInstance.name = artwork.characterPrefab.name + "_" + artwork.artworkName; // Unique name for the character instance
            characterInstance.gameObject.SetActive(false);
            _characters.Add(artwork.referenceImageName, characterInstance);

            if (artwork.description == null)
            {
                Debug.LogWarning($"Description is missing for the artwork: {artwork.artworkName}");
                continue;
            }

            _artworks_descriptions.Add(artwork.referenceImageName, artwork.description);
        }
    }

    private void OnDestroy()
    {
        _trackedImageManager.trackablesChanged.RemoveListener(OnTrackedImagesChanged);
    }

    private void OnTrackedImagesChanged(ARTrackablesChangedEventArgs<ARTrackedImage> eventArgs)
    {
        foreach (var trackedImage in eventArgs.added)
        {
            Debug.Log($"Image added: {trackedImage.referenceImage.name}");
            // Handle the addition of a new tracked image
            HandleTrackedImage(trackedImage);
        }

        foreach (var trackedImage in eventArgs.updated)
        {
            Debug.Log($"Image updated: {trackedImage.referenceImage.name}");
            // Handle the update of an existing tracked image
            HandleTrackedImage(trackedImage);
        }

        foreach (var trackedImage in eventArgs.removed)
        {
            Debug.Log($"Image removed: {trackedImage.Value.referenceImage.name}");
            // Handle the removal of a tracked image
            HandleTrackedImage(trackedImage.Value);
        }
    }

    private void HandleTrackedImage(ARTrackedImage trackedImage)
    {
        if (trackedImage == null)
        {
            Debug.LogWarning("Tracked image is null.");
            return;
        }

        if (trackedImage.referenceImage.name == null)
        {
            Debug.LogWarning("Tracked image is not present in the current library.");
            return;
        }

        _characters.TryGetValue(trackedImage.referenceImage.name, out var characterInstance);

        if (trackedImage.trackingState is TrackingState.Limited or TrackingState.None)
        {
            if (characterInstance != null)
            {
                characterInstance.gameObject.SetActive(false);
            }
            return;
        }

        if (characterInstance != null)
        {
            characterInstance.gameObject.SetActive(true);
            //characterInstance.transform.position = trackedImage.transform.position;
            characterInstance.transform.position = CalculateCharacterPosition(trackedImage, artworksDatabase.Find(a => a.referenceImageName == trackedImage.referenceImage.name));

            //characterInstance.transform.rotation = trackedImage.transform.rotation;
            OrientCharacterTowardsCamera(characterInstance);

            StartCoroutine(ShowText(characterInstance, trackedImage.referenceImage.name));
        }
    }

    private Vector3 CalculateCharacterPosition(ARTrackedImage trackedImage, ArtworkData artworkData)
    {
        // Applichiamo l'offset specificato nei dati dell'opera
        Vector3 characterPosition = trackedImage.transform.position +
                                    (trackedImage.transform.right * artworkData.characterOffset.x) +
                                    (trackedImage.transform.up * artworkData.characterOffset.z) +
                                    (trackedImage.transform.forward * artworkData.characterOffset.y); // up e forward sono invertiti perché l'immagine è appesa

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

        Physics.Raycast(targetPosition, Vector3.down, out hit_down, 5.0f);
        Physics.Raycast(targetPosition, Vector3.up, out hit_up, 5.0f);

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
        /*         
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
        } */

        // Come ultima risorsa, usa una posizione di default
        Debug.LogWarning("Pavimento non trovato, usando posizione default");
        return new Vector3(targetPosition.x, targetPosition.y, targetPosition.z);
    }

    private void OrientCharacterTowardsCamera(GameObject character)
    {
        Vector3 lookDirection = Camera.main.transform.position - character.transform.position;
        lookDirection.y = 0;
        if (lookDirection != Vector3.zero)
        {
            character.transform.rotation = Quaternion.LookRotation(lookDirection); //* Quaternion.Euler(0, 90, 0); 
        }
    }

    private IEnumerator ShowText(GameObject characterInstance, string referenceImageName)
    {
        float delayBeforeStart = 1.0f;
        float typingSpeed = 0.4f;
        yield return new WaitForSeconds(delayBeforeStart);

        //_characters.TryGetValue(trackedImage.referenceImage.name, out var characterInstance);
        TMP_Text textField = characterInstance.GetComponentInChildren<TMP_Text>();

        _artworks_descriptions.TryGetValue(referenceImageName, out var fullText);
        //Debug.Log($"Reference Image Name --{referenceImageName}--; value: ---{fullText}---");

        // separare il fulltext in chunks

        if (fullText != null)
        {
            Debug.Log($"Full Text: *{fullText}*");
        }


        if (textField != null)
        {
            Debug.Log($"Textfield: *{textField}*");
        }


        if (fullText != null && textField != null)
        {
            // Questo per far vedere i caratteri uno alla volta
/*             textField.text = "";
            foreach (char c in fullText)
            {
                textField.text += c;
                yield return new WaitForSeconds(typingSpeed);
            }
 */     
            textField.text = fullText;

        }

    }


}
