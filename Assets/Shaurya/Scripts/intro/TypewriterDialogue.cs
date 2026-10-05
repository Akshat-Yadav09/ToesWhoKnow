using System.Collections;
using TMPro;
using UnityEngine;

public class TypewriterDialogue : MonoBehaviour
{
    [Header("Text")]
    [Tooltip("The TextMeshProUGUI component where dialogue lines will be displayed.")]
    [SerializeField] private TMP_Text dialogueText;

    [Header("Audio")]
    [Tooltip("AudioSource used to play the typing audio.")]
    [SerializeField] private AudioSource audioSource;

    [Tooltip("Full typewriter typing sound clip. Plays continuously while text is being typed.")]
    [SerializeField] private AudioClip typewriterClip;

    [Tooltip("Legacy key sounds array (first clip will be used if typewriterClip is empty).")]
    [SerializeField] private AudioClip[] keySounds;

    [Range(0f, 1f)]
    [SerializeField] private float volume = 1.0f;

    [Header("Typing Speed")]
    [Tooltip("Characters typed per second. 25-30 is standard readable typing speed.")]
    [SerializeField] private float charactersPerSecond = 28f;

    [Header("Punctuation Pauses")]
    [SerializeField] private float commaPause = 0.20f;
    [SerializeField] private float periodPause = 0.50f;
    [SerializeField] private float questionPause = 0.55f;
    [SerializeField] private float exclamationPause = 0.50f;

    private void Awake()
    {
        if (dialogueText == null)
        {
            Debug.LogError("[TypewriterDialogue] 'Dialogue Text' is not assigned in the Inspector!", this);
            return;
        }
        dialogueText.text = "";

        // Resolve audio clip if not already set on audioSource
        if (audioSource != null && audioSource.clip == null)
        {
            if (typewriterClip != null)
            {
                audioSource.clip = typewriterClip;
            }
            else if (keySounds != null && keySounds.Length > 0 && keySounds[0] != null)
            {
                audioSource.clip = keySounds[0];
            }
        }
    }

    public IEnumerator PlayLine(string text)
    {
        if (dialogueText == null) yield break;

        dialogueText.text = "";

        // Start playing the full typewriter typing audio
        StartTypingAudio();

        float baseDelay = 1f / Mathf.Max(1f, charactersPerSecond);

        foreach (char character in text)
        {
            dialogueText.text += character;

            float pause = GetPunctuationPause(character);

            if (pause > 0f)
            {
                // Pause audio during punctuation pause
                PauseTypingAudio();
                yield return new WaitForSecondsRealtime(pause);
                ResumeTypingAudio();
            }
            else
            {
                yield return new WaitForSecondsRealtime(baseDelay);
            }
        }

        // Line finished — stop typing audio
        StopTypingAudio();
    }

    private float GetPunctuationPause(char c)
    {
        switch (c)
        {
            case ',': return commaPause;
            case '.': return periodPause;
            case '?': return questionPause;
            case '!': return exclamationPause;
            case '\n': return 0.5f;
            default: return 0f;
        }
    }

    private void StartTypingAudio()
    {
        if (audioSource == null) return;

        // Ensure clip is assigned
        if (audioSource.clip == null)
        {
            if (typewriterClip != null)
                audioSource.clip = typewriterClip;
            else if (keySounds != null && keySounds.Length > 0 && keySounds[0] != null)
                audioSource.clip = keySounds[0];
        }

        if (audioSource.clip != null)
        {
            audioSource.loop = true;
            audioSource.pitch = 1f;
            audioSource.volume = volume;
            if (!audioSource.isPlaying)
                audioSource.Play();
        }
    }

    private void PauseTypingAudio()
    {
        if (audioSource != null && audioSource.isPlaying)
        {
            audioSource.Pause();
        }
    }

    private void ResumeTypingAudio()
    {
        if (audioSource != null && audioSource.clip != null)
        {
            if (!audioSource.isPlaying)
                audioSource.UnPause();
        }
    }

    public void StopTypingAudio()
    {
        if (audioSource != null && audioSource.isPlaying)
        {
            audioSource.Stop();
        }
    }

    private void OnDisable()
    {
        StopTypingAudio();
    }
}