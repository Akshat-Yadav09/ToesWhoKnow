// BlackScreenController.cs
// Manages the opening "PRESS ANY KEY TO REMEMBER" black screen phase.
// Attach to: StartPrompt GameObject (or any persistent GameObject in the scene).
// This script drives STATE 1 of the MainMenu audio state machine.
//
// FIX: Uses CanvasGroup alpha to hide the panel instead of SetActive(false).
// This prevents the coroutine from killing itself mid-execution.

using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Shaurya.MainMenu
{
    public class BlackScreenController : MonoBehaviour
    {
        // ── Inspector ────────────────────────────────────────────────────────

        [Header("UI References")]
        [Tooltip("The CanvasGroup on the StartPrompt panel (add one if not present).")]
        [SerializeField] private CanvasGroup startPromptGroup;

        [Tooltip("The TMP text that reads 'PRESS ANY KEY TO REMEMBER'.")]
        [SerializeField] private TMP_Text promptText;

        [Header("Fade Settings")]
        [Tooltip("How long the text takes to fully fade in.")]
        [SerializeField] private float fadeInDuration = 2.0f;

        [Tooltip("How long the text stays fully visible before fading out.")]
        [SerializeField] private float holdDuration = 1.5f;

        [Tooltip("How long the text takes to fade out.")]
        [SerializeField] private float fadeOutDuration = 1.8f;

        [Header("Black Screen Audio")]
        [Tooltip("Rain AudioSource (child of Audio parent).")]
        [SerializeField] private AudioSource rainSource;

        [Tooltip("Environment AudioSource (child of Audio parent).")]
        [SerializeField] private AudioSource environmentSource;

        [Tooltip("Thunder AudioSource (child of Audio parent). Triggered randomly.")]
        [SerializeField] private AudioSource thunderSource;

        [Tooltip("AudioClips to use for random thunder. Assign thunder clips here.")]
        [SerializeField] private AudioClip[] thunderClips;

        [Header("Thunder Timing")]
        [Tooltip("Minimum seconds between thunder strikes.")]
        [SerializeField] private float thunderMinInterval = 8f;

        [Tooltip("Maximum seconds between thunder strikes.")]
        [SerializeField] private float thunderMaxInterval = 25f;

        [Header("Audio Fade")]
        [Tooltip("How long to fade out the black-screen audio when player presses a key.")]
        [SerializeField] private float audioFadeOutDuration = 1.5f;

        [Header("Audio Volumes")]
        [Range(0f, 1f)] [SerializeField] private float rainVolume = 1.0f;
        [Range(0f, 1f)] [SerializeField] private float environmentVolume = 1.0f;
        [Range(0f, 1f)] [SerializeField] private float thunderVolume = 1.0f;

        [Header("Sequencer Reference")]
        [Tooltip("The MainMenuSequencer that takes over after the prompt is dismissed.")]
        [SerializeField] private MainMenuSequencer sequencer;

        // ── State ────────────────────────────────────────────────────────────

        private bool keyPressed = false;
        private Coroutine thunderRoutine;
        private Coroutine fadeTextRoutine;

        // ── Lifecycle ────────────────────────────────────────────────────────

        private void Awake()
        {
            // Auto-locate CanvasGroup if not assigned
            if (startPromptGroup == null)
                startPromptGroup = GetComponent<CanvasGroup>();
            if (startPromptGroup == null)
                startPromptGroup = GetComponentInParent<CanvasGroup>();

            // Ensure prompt text starts invisible
            if (promptText != null)
            {
                Color c = promptText.color;
                c.a = 0f;
                promptText.color = c;
            }

            // Ensure the panel itself is fully opaque and visible at start
            if (startPromptGroup != null)
            {
                startPromptGroup.alpha = 1f;
                startPromptGroup.blocksRaycasts = true;
                startPromptGroup.interactable = false;
            }
        }

        private void Start()
        {
            // Start the black-screen audio immediately
            StartBlackScreenAudio();

            // Begin the pulsing text loop
            if (promptText != null)
                fadeTextRoutine = StartCoroutine(PulseTextLoop());
        }

        private void Update()
        {
            if (keyPressed) return;

            // Detect any keyboard key press using the New Input System
            if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame)
            {
                OnPlayerPressedKey();
            }
        }

        // ── Black Screen Audio ───────────────────────────────────────────────

        private void StartBlackScreenAudio()
        {
            if (rainSource != null)
            {
                rainSource.volume = rainVolume; // Always ensure rain plays at full volume when starting!
                rainSource.loop = true;
                if (!rainSource.isPlaying) rainSource.Play();
            }

            if (environmentSource != null)
            {
                environmentSource.volume = environmentVolume;
                environmentSource.loop = true;
                if (!environmentSource.isPlaying) environmentSource.Play();
            }

            if (thunderSource != null)
            {
                thunderSource.volume = thunderVolume;
                // If clips list is empty but thunderSource has a clip assigned, use that clip!
                if ((thunderClips != null && thunderClips.Length > 0) || thunderSource.clip != null)
                {
                    thunderRoutine = StartCoroutine(ThunderLoop());
                }
            }
        }

        private IEnumerator ThunderLoop()
        {
            while (true)
            {
                float waitTime = Random.Range(thunderMinInterval, thunderMaxInterval);
                yield return new WaitForSecondsRealtime(waitTime);

                if (keyPressed) yield break;

                AudioClip clip = null;
                if (thunderClips != null && thunderClips.Length > 0)
                    clip = thunderClips[Random.Range(0, thunderClips.Length)];
                else if (thunderSource != null)
                    clip = thunderSource.clip;

                if (clip != null && thunderSource != null)
                    thunderSource.PlayOneShot(clip, thunderVolume);
            }
        }

        // ── Text Pulse ───────────────────────────────────────────────────────

        private IEnumerator PulseTextLoop()
        {
            while (!keyPressed)
            {
                yield return StartCoroutine(FadeText(0f, 1f, fadeInDuration));
                if (keyPressed) yield break;

                yield return new WaitForSecondsRealtime(holdDuration);
                if (keyPressed) yield break;

                yield return StartCoroutine(FadeText(1f, 0f, fadeOutDuration));
                if (keyPressed) yield break;

                yield return new WaitForSecondsRealtime(0.3f);
            }
        }

        private IEnumerator FadeText(float from, float to, float duration)
        {
            if (promptText == null) yield break;

            float elapsed = 0f;
            Color c = promptText.color;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float smooth = t * t * (3f - 2f * t);
                c.a = Mathf.Lerp(from, to, smooth);
                promptText.color = c;
                yield return null;
            }

            c.a = to;
            promptText.color = c;
        }

        // ── Key Press ────────────────────────────────────────────────────────

        private void OnPlayerPressedKey()
        {
            keyPressed = true;

            if (fadeTextRoutine != null) StopCoroutine(fadeTextRoutine);
            if (thunderRoutine != null) StopCoroutine(thunderRoutine);

            StartCoroutine(TransitionSequence());
        }

        private IEnumerator TransitionSequence()
        {
            // 1. Quickly fade out only the prompt text (keep the black background solid!)
            float currentAlpha = promptText != null ? promptText.color.a : 1f;
            yield return StartCoroutine(FadeText(currentAlpha, 0f, 0.4f));

            // 2. Fade out black-screen audio
            yield return StartCoroutine(FadeOutBlackScreenAudio());

            // 3. Hand off to the sequencer while keeping screen solid black for dialogue/voice
            if (sequencer != null)
                sequencer.BeginDialogueSequence(this);
            else
                Debug.LogError("[BlackScreenController] MainMenuSequencer not assigned!", this);
        }

        /// <summary>Called by MainMenuSequencer right before the video begins playing.</summary>
        public IEnumerator FadeOutBlackPanel(float duration = 0.4f)
        {
            if (startPromptGroup != null)
            {
                float elapsed = 0f;
                float startAlpha = startPromptGroup.alpha;
                while (elapsed < duration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    startPromptGroup.alpha = Mathf.Lerp(startAlpha, 0f, elapsed / duration);
                    yield return null;
                }
                startPromptGroup.alpha = 0f;
                startPromptGroup.blocksRaycasts = false;
            }
        }

        private IEnumerator FadeOutBlackScreenAudio()
        {
            float elapsed = 0f;
            float rainStart = rainSource != null ? rainSource.volume : 0f;
            float envStart = environmentSource != null ? environmentSource.volume : 0f;

            while (elapsed < audioFadeOutDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / audioFadeOutDuration);
                float smooth = t * t * (3f - 2f * t);

                if (rainSource != null) rainSource.volume = Mathf.Lerp(rainStart, 0f, smooth);
                if (environmentSource != null) environmentSource.volume = Mathf.Lerp(envStart, 0f, smooth);

                yield return null;
            }

            if (rainSource != null) { rainSource.volume = 0f; rainSource.Stop(); }
            if (environmentSource != null) { environmentSource.volume = 0f; environmentSource.Stop(); }
            if (thunderSource != null) thunderSource.Stop();
        }

        // ── Public Accessors ──────────────────────────────────────────────────
        public AudioSource RainSource => rainSource;
        public AudioSource EnvironmentSource => environmentSource;
        public AudioSource ThunderSource => thunderSource;
        public AudioClip[] ThunderClips => thunderClips;
        public float ThunderVolume => thunderVolume;

        public bool IsBlackScreenAudioSource(AudioSource src)
        {
            return src != null && (src == rainSource || src == environmentSource || src == thunderSource);
        }
    }
}
