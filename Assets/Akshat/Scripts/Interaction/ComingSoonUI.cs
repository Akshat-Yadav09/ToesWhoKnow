using System.Collections;
using UnityEngine;
using TMPro;

namespace Akshat.Interaction
{
    /// <summary>
    /// Displays a cinematic 'Coming soon...' end-of-demo screen.
    /// Freezes player motion and interaction, fades in the UI, and plays an optional audio sting.
    /// Can be triggered from any UnityEvent (e.g. PasscodeLockInteractable.OnUnlocked, or LinkedDoor.OnDoorUsed).
    /// </summary>
    public class ComingSoonUI : MonoBehaviour
    {
        public static ComingSoonUI Instance { get; private set; }

        [Header("UI Components")]
        [Tooltip("The CanvasGroup controlling panel visibility and fade-in.")]
        [SerializeField] private CanvasGroup canvasGroup;

        [Tooltip("The main title text component (defaults to 'Coming soon...').")]
        [SerializeField] private TMP_Text mainText;

        [Tooltip("Optional subtitle text component (e.g. 'Thank you for playing!').")]
        [SerializeField] private TMP_Text subtitleText;

        [Header("Text & Animation Settings")]
        [SerializeField] private string titleMessage = "Coming soon...";
        [SerializeField] private string subtitleMessage = "Thank you for playing!";
        [SerializeField] private float delayBeforeFade = 0.3f;
        [SerializeField] private float fadeDuration = 1.2f;

        [Header("Player Locking (Auto-located if unassigned)")]
        [SerializeField] private PlayerMovement playerMovement;
        [SerializeField] private PlayerInteraction playerInteraction;

        [Header("Audio Feedback (Optional)")]
        [SerializeField] private AudioClip completionAudio;
        [SerializeField] private AudioSource audioSource;

        private bool hasTriggered = false;
        private Coroutine displayRoutine;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }

            AutoBindComponents();

            // Start completely invisible and non-blocking
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.blocksRaycasts = false;
                canvasGroup.interactable = false;
            }

            if (mainText != null && !string.IsNullOrEmpty(titleMessage))
            {
                mainText.text = titleMessage;
            }

            if (subtitleText != null && !string.IsNullOrEmpty(subtitleMessage))
            {
                subtitleText.text = subtitleMessage;
            }
        }

        private void Reset()
        {
            AutoBindComponents();
        }

        private void AutoBindComponents()
        {
            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
                if (canvasGroup == null) canvasGroup = GetComponentInChildren<CanvasGroup>(true);
            }

            var texts = GetComponentsInChildren<TMP_Text>(true);
            if (mainText == null && texts.Length > 0)
            {
                mainText = texts[0];
            }
            if (subtitleText == null && texts.Length > 1)
            {
                subtitleText = texts[1];
            }
        }

        /// <summary>
        /// Public parameterless trigger method for UnityEvent wiring in the Inspector.
        /// </summary>
        public void Show()
        {
            TriggerComingSoon();
        }

        /// <summary>
        /// Freezes player movement and displays the Coming Soon screen.
        /// </summary>
        public void TriggerComingSoon()
        {
            if (hasTriggered) return;
            hasTriggered = true;

            // 1. Freeze player movement and velocity
            if (playerMovement == null)
            {
                playerMovement = FindAnyObjectByType<PlayerMovement>();
            }

            if (playerMovement != null)
            {
                playerMovement.SetMovementEnabled(false);
                playerMovement.StopRigidbodyVelocity();
            }

            // 2. Hide and disable world interaction prompt
            if (playerInteraction == null)
            {
                playerInteraction = FindAnyObjectByType<PlayerInteraction>();
            }

            if (playerInteraction != null)
            {
                playerInteraction.enabled = false;
            }

            // 3. Play audio if assigned
            if (completionAudio != null)
            {
                if (audioSource == null) audioSource = GetComponent<AudioSource>();
                if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.PlayOneShot(completionAudio);
            }

            // 4. Begin fade-in
            if (displayRoutine != null) StopCoroutine(displayRoutine);
            displayRoutine = StartCoroutine(FadeInRoutine());
        }

        private IEnumerator FadeInRoutine()
        {
            if (delayBeforeFade > 0f)
            {
                yield return new WaitForSecondsRealtime(delayBeforeFade);
            }

            if (canvasGroup != null)
            {
                gameObject.SetActive(true);
                canvasGroup.blocksRaycasts = true;
                canvasGroup.interactable = true;

                float elapsed = 0f;
                float duration = Mathf.Max(0.05f, fadeDuration);

                while (elapsed < duration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    canvasGroup.alpha = Mathf.Clamp01(elapsed / duration);
                    yield return null;
                }

                canvasGroup.alpha = 1f;
            }

            displayRoutine = null;
        }
    }
}
