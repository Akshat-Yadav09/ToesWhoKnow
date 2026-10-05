// EyelidController.cs
// Animates two full-screen Image panels (TopLid / BottomLid) to simulate eyes closing/opening.
// Both lids must be children of a Canvas set to Screen Space – Overlay (or the MainCanvas).
//
// Setup for TopLid:
//   AnchorMin = (0, 0.5), AnchorMax = (1, 1.5) → starts above screen
//   AnchoredPosition = (0, 0), SizeDelta = (0, 0)   [stretch fills top half + above]
//
// Setup for BottomLid:
//   AnchorMin = (0, -0.5), AnchorMax = (1, 0.5) → starts below screen
//   AnchoredPosition = (0, 0), SizeDelta = (0, 0)   [stretch fills bottom half + below]
//
// The script drives anchoredPosition.y to slide both lids inward to cover the screen.

using System.Collections;
using UnityEngine;

namespace Shaurya.MainMenu
{
    public class EyelidController : MonoBehaviour
    {
        // ── Inspector ────────────────────────────────────────────────────────

        [Header("Eyelid RectTransforms")]
        [Tooltip("Top eyelid — anchor set so it starts above the screen.")]
        [SerializeField] private RectTransform topLid;

        [Tooltip("Bottom eyelid — anchor set so it starts below the screen.")]
        [SerializeField] private RectTransform bottomLid;

        [Header("Timing")]
        [Tooltip("Seconds before the eyelids start closing after the video ends.")]
        [SerializeField] private float preCloseDelay = 0.5f;

        [Tooltip("Duration of the eye-closing animation.")]
        [SerializeField] private float closeDuration = 1.3f;

        [Tooltip("Duration to hold full black before opening.")]
        [SerializeField] private float blackHoldDuration = 0.6f;

        [Tooltip("Duration of the eye-opening animation.")]
        [SerializeField] private float openDuration = 1.3f;

        // ── Internal ─────────────────────────────────────────────────────────

        // Canvas reference resolution height (from CanvasScaler).
        // With Scale With Screen Size at 1920×1080, one "unit" = 1 canvas pixel.
        // We drive anchoredPosition to move the lids in/out.
        //
        // TopLid anchors at AnchorMin.y = 0.5, AnchorMax.y = 1.5  → so the panel
        //   sits from screen-centre to one full screen-height above.
        //   Sliding it DOWN by half-canvas-height brings it to cover the top half.
        //   (anchoredPosition.y goes from 0 → -halfH to close)
        //
        // BottomLid anchors at AnchorMin.y = -0.5, AnchorMax.y = 0.5 → panel
        //   sits from one full screen-height below to screen-centre.
        //   Sliding it UP by half-canvas-height brings it to cover the bottom half.
        //   (anchoredPosition.y goes from 0 → +halfH to close)

        private float canvasHalfHeight = 540f; // default 1080/2

        // ── Lifecycle ────────────────────────────────────────────────────────

        private void Awake()
        {
            // Park lids in open position (off-screen)
            SetLidPositions(0f);
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Plays full close → hold → open sequence.
        /// Yield this from the sequencer.
        /// </summary>
        public IEnumerator PlayEyelidSequence()
        {
            // Try to read reference resolution from the parent canvas
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                var scaler = canvas.GetComponent<UnityEngine.UI.CanvasScaler>();
                if (scaler != null)
                    canvasHalfHeight = scaler.referenceResolution.y * 0.5f;
                else
                    canvasHalfHeight = canvas.GetComponent<RectTransform>().rect.height * 0.5f;
            }

            // Ensure lids start in open position
            SetLidPositions(0f);

            yield return new WaitForSecondsRealtime(preCloseDelay);

            // Close: slide offset from 0 → canvasHalfHeight
            yield return StartCoroutine(AnimateLids(0f, canvasHalfHeight, closeDuration));

            // Hold black
            yield return new WaitForSecondsRealtime(blackHoldDuration);

            // Open: slide offset from canvasHalfHeight → 0
            yield return StartCoroutine(AnimateLids(canvasHalfHeight, 0f, openDuration));

            // Final snap to open
            SetLidPositions(0f);
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        /// <summary>
        /// Sets lids based on a "close amount" value.
        /// closeAmount = 0 → lids are in open (off-screen) position.
        /// closeAmount = canvasHalfHeight → lids are fully closed (meet at centre).
        /// TopLid moves DOWN (negative Y) to close.
        /// BottomLid moves UP (positive Y) to close.
        /// </summary>
        private void SetLidPositions(float closeAmount)
        {
            if (topLid != null)
            {
                Vector2 pos = topLid.anchoredPosition;
                pos.y = -closeAmount;
                topLid.anchoredPosition = pos;
            }

            if (bottomLid != null)
            {
                Vector2 pos = bottomLid.anchoredPosition;
                pos.y = closeAmount;
                bottomLid.anchoredPosition = pos;
            }
        }

        private IEnumerator AnimateLids(float from, float to, float duration)
        {
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float smooth = t * t * (3f - 2f * t); // smoothstep
                SetLidPositions(Mathf.Lerp(from, to, smooth));
                yield return null;
            }

            SetLidPositions(to);
        }
    }
}
