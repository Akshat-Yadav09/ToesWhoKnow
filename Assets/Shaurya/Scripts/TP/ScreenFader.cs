// Assets/Shaurya/Scripts/ScreenFader.cs
// Handles full-screen fade-out and fade-in using a CanvasGroup over a black Image.
// Attach to the full-screen black Image panel inside the Fade Canvas.

using System.Collections;
using UnityEngine;

namespace Shaurya
{
    public class ScreenFader : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("The CanvasGroup on the full-screen black Image panel.")]
        [SerializeField] private CanvasGroup fadeCanvasGroup;

        [Header("Fade Settings")]
        [Tooltip("Duration of the fade-out (screen → black).")]
        [SerializeField] private float fadeOutDuration = 0.35f;

        [Tooltip("Duration of the fade-in (black → screen).")]
        [SerializeField] private float fadeInDuration = 0.35f;

        // True while any fade coroutine is running.
        public bool IsFading { get; private set; }

        private Coroutine activeFadeCoroutine;

        private void Awake()
        {
            // Auto-locate CanvasGroup if not assigned in Inspector.
            if (fadeCanvasGroup == null)
                fadeCanvasGroup = GetComponentInChildren<CanvasGroup>();

            if (fadeCanvasGroup == null)
                Debug.LogError("[ScreenFader] No CanvasGroup found. Assign it in the Inspector.", this);

            // Start fully transparent so it never blocks gameplay on scene load.
            SetAlpha(0f);
            SetBlocking(false);
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>Fade the screen to black. Use StartCoroutine to await.</summary>
        public IEnumerator FadeOut()
        {
            yield return RunFade(0f, 1f, fadeOutDuration);
        }

        /// <summary>Fade the screen back in from black. Use StartCoroutine to await.</summary>
        public IEnumerator FadeIn()
        {
            yield return RunFade(1f, 0f, fadeInDuration);
        }

        // ── Internal helpers ──────────────────────────────────────────────────

        private IEnumerator RunFade(float fromAlpha, float toAlpha, float duration)
        {
            // Cancel any previous fade to avoid conflicts.
            if (activeFadeCoroutine != null)
            {
                StopCoroutine(activeFadeCoroutine);
                activeFadeCoroutine = null;
            }

            IsFading = true;

            // Always block raycasts during a fade to prevent accidental UI clicks.
            SetBlocking(true);
            SetAlpha(fromAlpha);

            if (duration > 0.001f)
            {
                float elapsed = 0f;
                while (elapsed < duration)
                {
                    // Use unscaledDeltaTime so pausing Time.timeScale doesn't break the fade.
                    elapsed += Time.unscaledDeltaTime;
                    float t = Mathf.Clamp01(elapsed / duration);
                    // Smooth-step for a cinematic feel.
                    float smooth = t * t * (3f - 2f * t);
                    SetAlpha(Mathf.Lerp(fromAlpha, toAlpha, smooth));
                    yield return null;
                }
            }

            SetAlpha(toAlpha);

            // Once fully transparent again, stop blocking gameplay input.
            if (toAlpha < 0.01f)
                SetBlocking(false);

            IsFading = false;
        }

        private void SetAlpha(float alpha)
        {
            if (fadeCanvasGroup != null)
                fadeCanvasGroup.alpha = alpha;
        }

        private void SetBlocking(bool blocking)
        {
            if (fadeCanvasGroup == null) return;
            fadeCanvasGroup.blocksRaycasts = blocking;
            fadeCanvasGroup.interactable   = false; // Never interactable.
        }
    }
}
