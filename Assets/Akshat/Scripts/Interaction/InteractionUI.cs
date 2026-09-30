using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Akshat.Interaction
{
    /// <summary>
    /// Central Screen-Space Overlay UI component for displaying interaction prompts.
    /// Handles smooth fade-in/out and micro-scaling transitions using a CanvasGroup.
    /// Reusable across all interactables in the game without creating per-object Canvases.
    /// Supports both TextMeshPro (standard in Unity 6) and legacy UI Text.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class InteractionUI : MonoBehaviour
    {
        [Header("UI References")]
        [Tooltip("The CanvasGroup controlling prompt opacity. Auto-assigned if left empty.")]
        [SerializeField] private CanvasGroup canvasGroup;

        [Tooltip("The RectTransform to scale during fade transitions (typically the panel or background).")]
        [SerializeField] private RectTransform promptPanel;

        [Tooltip("The TextMeshPro UI Text component (default when creating UI > Text in Unity 6).")]
        [SerializeField] private TMP_Text promptText;

        [Tooltip("Optional legacy UI Text component (if using UI > Legacy > Text).")]
        [SerializeField] private Text legacyPromptText;

        [Header("Key Binding Display")]
        [Tooltip("Key prefix displayed before the interaction action text (e.g. '[E] ').")]
        [SerializeField] private string keyPrefix = "[E] ";

        [Tooltip("Fallback text if an interactable returns an empty prompt string.")]
        [SerializeField] private string defaultPromptText = "Interact";

        [Header("Animation Settings")]
        [Tooltip("Duration in seconds for fade-in and fade-out transitions.")]
        [SerializeField] private float fadeDuration = 0.15f;

        [Tooltip("Initial scale when prompt begins fading in.")]
        [SerializeField] private Vector3 hiddenScale = new Vector3(0.9f, 0.9f, 1f);

        [Tooltip("Target scale when prompt is fully visible.")]
        [SerializeField] private Vector3 visibleScale = Vector3.one;

        [Header("Optional Direct Connection")]
        [Tooltip("Optional PlayerInteraction reference. If assigned or found, automatically subscribes to detection events.")]
        [SerializeField] private PlayerInteraction playerInteraction;

        private Coroutine transitionCoroutine;
        private bool isVisible;

        private void Awake()
        {
            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
            }

            if (promptPanel == null)
            {
                promptPanel = GetComponent<RectTransform>();
            }

            // Auto-locate TextMeshPro or legacy Text in children if unassigned
            if (promptText == null)
            {
                promptText = GetComponentInChildren<TMP_Text>(true);
            }

            if (legacyPromptText == null)
            {
                legacyPromptText = GetComponentInChildren<Text>(true);
            }

            // Ensure prompt does not block raycasts / clicks
            if (canvasGroup != null)
            {
                canvasGroup.blocksRaycasts = false;
                canvasGroup.interactable = false;
                canvasGroup.alpha = 0f;
            }

            if (promptPanel != null)
            {
                promptPanel.localScale = hiddenScale;
            }

            // Auto-locate PlayerInteraction if not explicitly assigned
            if (playerInteraction == null)
            {
                playerInteraction = FindAnyObjectByType<PlayerInteraction>();
            }
        }

        private void OnEnable()
        {
            if (playerInteraction != null)
            {
                playerInteraction.OnInteractableDetected += HandleInteractableDetected;
                playerInteraction.OnInteractableLost += HandleInteractableLost;

                // Sync with current state if player is already near an interactable
                if (playerInteraction.CurrentInteractable != null)
                {
                    HandleInteractableDetected(playerInteraction.CurrentInteractable);
                }
            }
        }

        private void OnDisable()
        {
            if (playerInteraction != null)
            {
                playerInteraction.OnInteractableDetected -= HandleInteractableDetected;
                playerInteraction.OnInteractableLost -= HandleInteractableLost;
            }

            if (transitionCoroutine != null)
            {
                StopCoroutine(transitionCoroutine);
                transitionCoroutine = null;
            }
        }

        private void HandleInteractableDetected(IInteractable interactable)
        {
            ShowPrompt(interactable);
        }

        private void HandleInteractableLost()
        {
            HidePrompt();
        }

        /// <summary>
        /// Displays the interaction prompt for the specified interactable with smooth animation.
        /// </summary>
        public void ShowPrompt(IInteractable interactable)
        {
            string actionText = interactable != null && !string.IsNullOrEmpty(interactable.InteractionPrompt)
                ? interactable.InteractionPrompt
                : defaultPromptText;

            ShowPrompt(actionText);
        }

        /// <summary>
        /// Displays the interaction prompt with raw action text (e.g. 'Open Door').
        /// </summary>
        public void ShowPrompt(string actionText)
        {
            string fullMessage = $"{keyPrefix}{actionText}";

            if (promptText != null)
            {
                promptText.text = fullMessage;
            }

            if (legacyPromptText != null)
            {
                legacyPromptText.text = fullMessage;
            }

            isVisible = true;

            if (transitionCoroutine != null)
            {
                StopCoroutine(transitionCoroutine);
            }

            transitionCoroutine = StartCoroutine(AnimateTransition(1f, visibleScale));
        }

        /// <summary>
        /// Smoothly hides the interaction prompt.
        /// </summary>
        public void HidePrompt()
        {
            if (!isVisible) return;
            isVisible = false;

            if (transitionCoroutine != null)
            {
                StopCoroutine(transitionCoroutine);
            }

            transitionCoroutine = StartCoroutine(AnimateTransition(0f, hiddenScale));
        }

        /// <summary>
        /// Smoothly interpolates CanvasGroup alpha and RectTransform scale without GC allocations.
        /// Handles rapid interruption gracefully without snapping.
        /// </summary>
        private IEnumerator AnimateTransition(float targetAlpha, Vector3 targetScale)
        {
            float startAlpha = canvasGroup != null ? canvasGroup.alpha : 0f;
            Vector3 startScale = promptPanel != null ? promptPanel.localScale : visibleScale;

            if (fadeDuration <= 0.001f)
            {
                if (canvasGroup != null) canvasGroup.alpha = targetAlpha;
                if (promptPanel != null) promptPanel.localScale = targetScale;
                transitionCoroutine = null;
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / fadeDuration);

                // Smooth step curve for polished feel
                float smoothT = t * t * (3f - 2f * t);

                if (canvasGroup != null)
                {
                    canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, smoothT);
                }

                if (promptPanel != null)
                {
                    promptPanel.localScale = Vector3.Lerp(startScale, targetScale, smoothT);
                }

                yield return null;
            }

            if (canvasGroup != null)
            {
                canvasGroup.alpha = targetAlpha;
            }

            if (promptPanel != null)
            {
                promptPanel.localScale = targetScale;
            }

            transitionCoroutine = null;
        }

        /// <summary>
        /// Configure the displayed key prefix dynamically (e.g. if player rebinds keys or switches to gamepad).
        /// </summary>
        public void SetKeyPrefix(string newPrefix)
        {
            keyPrefix = newPrefix;
        }
    }
}
