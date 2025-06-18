using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// This script manages the character dialogue system
// It should be assigned to the character prefab
public class CharacterDialogue : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject dialogueBubble;              // The GameObject that contains the dialogue bubble.
    [SerializeField] private TextMeshProUGUI dialogueText;           // The TextMeshProUGUI component for displaying dialogue text.
    [SerializeField] private Button nextButton;                     // Button to go to the next dialogue line
    [SerializeField] private Button previousButton;                 // Button to go to the previous dialogue line
    [SerializeField] private Button closeButton;                    // Button to close the dialogue bubble

    [Header("Dialogue Settings")]
    [SerializeField] private float typingSpeed = 0.05f;             // Text typing speed in seconds per character
    [SerializeField] private float bubbleOffset = 2.0f;             // Height offset for the dialogue bubble above the character
    [SerializeField] private bool autoShowDialogue = true;          // Whether to automatically show the dialogue bubble when the character is initialized
    [SerializeField] private float autoShowDelay = 1.0f;            // Delay before automatically showing the dialogue bubble

    [Header("Audio (Optional)")]
    [SerializeField] private AudioSource audioSource;               // For playing sound effects (optional)
    [SerializeField] private AudioClip typingSoundEffect;           // Sound effect for typing text (optional)
    [SerializeField] private AudioClip buttonClickSound;            // Sound effect for button clicks (optional)

    // Current artwork data
    private ArtworkData currentArtwork;

    // Dialogue state
    private int currentDialogueIndex = 0;
    private bool isTyping = false;
    private Coroutine typingCoroutine;

    // Camera chace for optimization
    private Camera mainCamera;

    void Start()
    {
        // Initializing the references
        mainCamera = Camera.main;

        // Setting up the dialogue bubble and buttons
        SetupButtons();

        // At first, we hide the dialogue bubble
        if (dialogueBubble != null)
        {
            dialogueBubble.SetActive(false);
        }

        // If we want to automatically show the dialogue bubble, we do it after a delay
        if (autoShowDialogue)
        {
            Invoke(nameof(ShowDialogue), autoShowDelay);
        }
    }

    void Update()
    {
        // We update the dialogue bubble's rotation to always face the camera
        if (dialogueBubble != null && dialogueBubble.activeInHierarchy && mainCamera != null)
        {
            Vector3 lookDirection = mainCamera.transform.position - dialogueBubble.transform.position;
            dialogueBubble.transform.rotation = Quaternion.LookRotation(lookDirection);
        }
    }

    // This method is called by the ImageDetectionManager to initialize the dialogue.
    public void InitializeDialogue(ArtworkData artworkData)
    {
        currentArtwork = artworkData;
        currentDialogueIndex = 0;

        // We position the dialogue bubble above the character.
        if (dialogueBubble != null)
        {
            Vector3 bubblePosition = transform.position + Vector3.up * bubbleOffset;
            dialogueBubble.transform.position = bubblePosition;
        }

        Debug.Log($"Dialogo inizializzato per l'opera: {artworkData.artworkName}");
    }

    // Configuring the events on the buttons.
    private void SetupButtons()
    {
        if (nextButton != null)
        {
            nextButton.onClick.AddListener(NextDialogue);
        }

        if (previousButton != null)
        {
            previousButton.onClick.AddListener(PreviousDialogue);
        }

        if (closeButton != null)
        {
            closeButton.onClick.AddListener(HideDialogue);
        }
    }

    // It shows the dialogue bubble.
    public void ShowDialogue()
    {
        if (currentArtwork == null || currentArtwork.dialogueLines == null || currentArtwork.dialogueLines.Length == 0)
        {
            Debug.LogWarning("Nessun dialogo disponibile per questa opera!");
            return;
        }

        if (dialogueBubble != null)
        {
            dialogueBubble.SetActive(true);
            DisplayCurrentDialogue();
        }
    }

    // It hides the dialogue bubble.
    public void HideDialogue()
    {
        if (dialogueBubble != null)
        {
            dialogueBubble.SetActive(false);
        }

        // We stop the typing coroutine if it's running.
        if (typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
            typingCoroutine = null;
        }

        PlayButtonSound();
    }

    // It goes to the next dialogue line.
    public void NextDialogue()
    {
        if (currentArtwork == null || currentArtwork.dialogueLines == null) return;

        // If we are still typing the current text, we complete it immediately.
        if (isTyping)
        {
            CompleteCurrentText();
            return;
        }

        // Otherwise, we go to the next dialogue line.
        if (currentDialogueIndex < currentArtwork.dialogueLines.Length - 1)
        {
            currentDialogueIndex++;
            DisplayCurrentDialogue();
        }

        PlayButtonSound();
    }

    // It returns to the previous dialogue line.
    public void PreviousDialogue()
    {
        if (currentArtwork == null || currentArtwork.dialogueLines == null) return;

        // If we are still typing the current text, we complete it immediately.
        if (isTyping)
        {
            CompleteCurrentText();
            return;
        }

        // Otherwise, we go to the previous dialogue line.
        if (currentDialogueIndex > 0)
        {
            currentDialogueIndex--;
            DisplayCurrentDialogue();
        }

        PlayButtonSound();
    }

    // It displays the current dialogue line with a typing effect.
    private void DisplayCurrentDialogue()
    {
        if (currentArtwork == null || currentArtwork.dialogueLines == null ||
            currentDialogueIndex >= currentArtwork.dialogueLines.Length) return;

        string dialogueToShow = currentArtwork.dialogueLines[currentDialogueIndex];

        // We stop any ongoing typing animation.
        if (typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
        }

        // We start a new typing animation.
        typingCoroutine = StartCoroutine(TypeText(dialogueToShow));

        // We update the visibility of the buttons.
        UpdateButtonStates();
    }

    // Coroutine for gradual text typing effect.
    private IEnumerator TypeText(string textToType)
    {
        isTyping = true;

        if (dialogueText != null)
        {
            dialogueText.text = "";

            foreach (char character in textToType)
            {
                dialogueText.text += character;

                // We play the typing sound effect if available.
                if (audioSource != null && typingSoundEffect != null)
                {
                    audioSource.PlayOneShot(typingSoundEffect, 0.1f);
                }

                yield return new WaitForSeconds(typingSpeed);
            }
        }

        isTyping = false;
        typingCoroutine = null;
    }

    // It completes the current text immediately (useful when the user clicks during typing).
    private void CompleteCurrentText()
    {
        if (typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
            typingCoroutine = null;
        }

        isTyping = false;

        if (dialogueText != null && currentArtwork != null &&
            currentDialogueIndex < currentArtwork.dialogueLines.Length)
        {
            dialogueText.text = currentArtwork.dialogueLines[currentDialogueIndex];
        }
    }

    // It updates the button state based on the current dialogue.
    private void UpdateButtonStates()
    {
        if (currentArtwork == null || currentArtwork.dialogueLines == null) return;

        // Previous button: active only if we are not at the first dialogue.
        if (previousButton != null)
        {
            previousButton.interactable = currentDialogueIndex > 0;
        }

        // Next button: active only if we are not at the last dialogue.
        if (nextButton != null)
        {
            nextButton.interactable = currentDialogueIndex < currentArtwork.dialogueLines.Length - 1;
        }
    }

    // It plays the button click sound effect.
    private void PlayButtonSound()
    {
        if (audioSource != null && buttonClickSound != null)
        {
            audioSource.PlayOneShot(buttonClickSound, 0.3f);
        }
    }

    // Public methods for external control of the dialogue.

    public bool IsDialogueVisible()
    {
        return dialogueBubble != null && dialogueBubble.activeInHierarchy;
    }

    public void ToggleDialogue()
    {
        if (IsDialogueVisible())
        {
            HideDialogue();
        }
        else
        {
            ShowDialogue();
        }
    }

    // It makes the character "say" a specific dialogue line.
    public void SpeakSpecificLine(int lineIndex)
    {
        if (currentArtwork != null && currentArtwork.dialogueLines != null &&
            lineIndex >= 0 && lineIndex < currentArtwork.dialogueLines.Length)
        {
            currentDialogueIndex = lineIndex;
            ShowDialogue();
        }
    }

    // Debug information about the current artwork.
    public string GetCurrentArtworkInfo()
    {
        if (currentArtwork != null)
        {
            return $"Opera: {currentArtwork.artworkName}, Artista: {currentArtwork.artistName}, Anno: {currentArtwork.yearCreated}";
        }
        return "Nessuna opera assegnata";
    }
}