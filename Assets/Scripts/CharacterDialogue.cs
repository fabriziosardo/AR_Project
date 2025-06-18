using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Questo script gestisce il sistema di dialoghi del personaggio
// Va assegnato al prefab del personaggio
public class CharacterDialogue : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject dialogueBubble;              // Il GameObject che contiene la nuvoletta
    [SerializeField] private TextMeshProUGUI dialogueText;           // Il testo del dialogo
    [SerializeField] private Button nextButton;                     // Pulsante per passare al dialogo successivo
    [SerializeField] private Button previousButton;                 // Pulsante per tornare al dialogo precedente
    [SerializeField] private Button closeButton;                    // Pulsante per chiudere il dialogo
    
    [Header("Dialogue Settings")]
    [SerializeField] private float typingSpeed = 0.05f;             // Velocità di scrittura del testo (in secondi per carattere)
    [SerializeField] private float bubbleOffset = 2.0f;             // Altezza della nuvoletta rispetto al personaggio
    [SerializeField] private bool autoShowDialogue = true;          // Se mostrare automaticamente il dialogo quando il personaggio appare
    [SerializeField] private float autoShowDelay = 1.0f;            // Ritardo prima di mostrare automaticamente il dialogo
    
    [Header("Audio (Opzionale)")]
    [SerializeField] private AudioSource audioSource;               // Per eventuali effetti sonori
    [SerializeField] private AudioClip typingSoundEffect;           // Suono di scrittura
    [SerializeField] private AudioClip buttonClickSound;            // Suono di click sui pulsanti
    
    // Dati dell'opera d'arte corrente
    private ArtworkData currentArtwork;
    
    // Stato del dialogo
    private int currentDialogueIndex = 0;
    private bool isTyping = false;
    private Coroutine typingCoroutine;
    
    // Cache della camera per ottimizzazione
    private Camera mainCamera;
    
    void Start()
    {
        // Inizializziamo i riferimenti
        mainCamera = Camera.main;
        
        // Configuriamo i pulsanti se sono assegnati
        SetupButtons();
        
        // Nascondiamo inizialmente la nuvoletta
        if (dialogueBubble != null)
        {
            dialogueBubble.SetActive(false);
        }
        
        // Se dobbiamo mostrare automaticamente il dialogo, lo facciamo dopo un ritardo
        if (autoShowDialogue)
        {
            Invoke(nameof(ShowDialogue), autoShowDelay);
        }
    }
    
    void Update()
    {
        // Facciamo sempre guardare la nuvoletta verso la camera
        if (dialogueBubble != null && dialogueBubble.activeInHierarchy && mainCamera != null)
        {
            Vector3 lookDirection = mainCamera.transform.position - dialogueBubble.transform.position;
            dialogueBubble.transform.rotation = Quaternion.LookRotation(lookDirection);
        }
    }
    
    // Questo metodo viene chiamato dall'ImageDetectionManager per inizializzare il dialogo
    public void InitializeDialogue(ArtworkData artworkData)
    {
        currentArtwork = artworkData;
        currentDialogueIndex = 0;
        
        // Posizioniamo la nuvoletta sopra il personaggio
        if (dialogueBubble != null)
        {
            Vector3 bubblePosition = transform.position + Vector3.up * bubbleOffset;
            dialogueBubble.transform.position = bubblePosition;
        }
        
        Debug.Log($"Dialogo inizializzato per l'opera: {artworkData.artworkName}");
    }
    
    // Configuriamo gli eventi sui pulsanti
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
    
    // Mostra la nuvoletta di dialogo
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
    
    // Nasconde la nuvoletta di dialogo
    public void HideDialogue()
    {
        if (dialogueBubble != null)
        {
            dialogueBubble.SetActive(false);
        }
        
        // Fermiamo la coroutine di typing se è in corso
        if (typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
            typingCoroutine = null;
        }
        
        PlayButtonSound();
    }
    
    // Passa al dialogo successivo
    public void NextDialogue()
    {
        if (currentArtwork == null || currentArtwork.dialogueLines == null) return;
        
        // Se stiamo ancora scrivendo il testo corrente, completiamolo immediatamente
        if (isTyping)
        {
            CompleteCurrentText();
            return;
        }
        
        // Altrimenti passiamo al dialogo successivo
        if (currentDialogueIndex < currentArtwork.dialogueLines.Length - 1)
        {
            currentDialogueIndex++;
            DisplayCurrentDialogue();
        }
        
        PlayButtonSound();
    }
    
    // Torna al dialogo precedente
    public void PreviousDialogue()
    {
        if (currentArtwork == null || currentArtwork.dialogueLines == null) return;
        
        // Se stiamo ancora scrivendo il testo corrente, completiamolo immediatamente
        if (isTyping)
        {
            CompleteCurrentText();
            return;
        }
        
        // Altrimenti torniamo al dialogo precedente
        if (currentDialogueIndex > 0)
        {
            currentDialogueIndex--;
            DisplayCurrentDialogue();
        }
        
        PlayButtonSound();
    }
    
    // Mostra il dialogo corrente con effetto di scrittura
    private void DisplayCurrentDialogue()
    {
        if (currentArtwork == null || currentArtwork.dialogueLines == null || 
            currentDialogueIndex >= currentArtwork.dialogueLines.Length) return;
        
        string dialogueToShow = currentArtwork.dialogueLines[currentDialogueIndex];
        
        // Fermiamo qualsiasi animazione di scrittura in corso
        if (typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
        }
        
        // Iniziamo la nuova animazione di scrittura
        typingCoroutine = StartCoroutine(TypeText(dialogueToShow));
        
        // Aggiorniamo la visibilità dei pulsanti
        UpdateButtonStates();
    }
    
    // Coroutine per l'effetto di scrittura graduale del testo
    private IEnumerator TypeText(string textToType)
    {
        isTyping = true;
        
        if (dialogueText != null)
        {
            dialogueText.text = "";
            
            foreach (char character in textToType)
            {
                dialogueText.text += character;
                
                // Riproduciamo il suono di scrittura se disponibile
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
    
    // Completa immediatamente il testo corrente (utile quando l'utente clicca durante la scrittura)
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
    
    // Aggiorna lo stato dei pulsanti in base al dialogo corrente
    private void UpdateButtonStates()
    {
        if (currentArtwork == null || currentArtwork.dialogueLines == null) return;
        
        // Pulsante precedente: attivo solo se non siamo al primo dialogo
        if (previousButton != null)
        {
            previousButton.interactable = currentDialogueIndex > 0;
        }
        
        // Pulsante successivo: attivo solo se non siamo all'ultimo dialogo
        if (nextButton != null)
        {
            nextButton.interactable = currentDialogueIndex < currentArtwork.dialogueLines.Length - 1;
        }
    }
    
    // Riproduce il suono di click sui pulsanti
    private void PlayButtonSound()
    {
        if (audioSource != null && buttonClickSound != null)
        {
            audioSource.PlayOneShot(buttonClickSound, 0.3f);
        }
    }
    
    // Metodi pubblici per controllo esterno del dialogo
    
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
    
    // Metodo per far "parlare" il personaggio di una specifica linea di dialogo
    public void SpeakSpecificLine(int lineIndex)
    {
        if (currentArtwork != null && currentArtwork.dialogueLines != null && 
            lineIndex >= 0 && lineIndex < currentArtwork.dialogueLines.Length)
        {
            currentDialogueIndex = lineIndex;
            ShowDialogue();
        }
    }
    
    // Informazioni di debug
    public string GetCurrentArtworkInfo()
    {
        if (currentArtwork != null)
        {
            return $"Opera: {currentArtwork.artworkName}, Artista: {currentArtwork.artistName}, Anno: {currentArtwork.yearCreated}";
        }
        return "Nessuna opera assegnata";
    }
}