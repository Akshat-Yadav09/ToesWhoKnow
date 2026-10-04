using UnityEngine;
using UnityEngine.UI;

namespace Akshat.Inspection
{
    /// <summary>
    /// Component controlling background dimming and visual subduing during inspection mode.
    /// Implements IBackgroundDimmer so InspectionManager remains decoupled from specific UI/URP implementations.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class UIBackgroundDimmer : MonoBehaviour, IBackgroundDimmer
    {
        [Header("References (Auto-found if unassigned)")]
        [Tooltip("CanvasGroup controlling opacity of the dim/blur layer.")]
        [SerializeField] private CanvasGroup canvasGroup;

        [Tooltip("Optional background image (e.g. solid dark tint or vignette).")]
        [SerializeField] private Image dimmerImage;

        [Tooltip("Optional child GameObject or layer for blur/post-processing effects.")]
        [SerializeField] private GameObject blurEffectLayer;

        [Header("Settings")]
        [Tooltip("Maximum opacity reached when fully dimmed.")]
        [Range(0f, 1f)]
        [SerializeField] private float maxOpacity = 0.85f;

        private void Awake()
        {
            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
            }

            if (dimmerImage == null)
            {
                dimmerImage = GetComponent<Image>();
            }

            SetDimAmount(0f);
        }

        /// <summary>
        /// Updates dimming level from 0 (completely clear) to 1 (fully dimmed).
        /// </summary>
        public void SetDimAmount(float normalizedAmount)
        {
            float clamped = Mathf.Clamp01(normalizedAmount);

            if (canvasGroup != null)
            {
                canvasGroup.alpha = Mathf.Lerp(0f, maxOpacity, clamped);
                canvasGroup.blocksRaycasts = clamped > 0.01f;
                canvasGroup.interactable = clamped > 0.01f;
            }

            if (blurEffectLayer != null)
            {
                blurEffectLayer.SetActive(clamped > 0.05f);
            }
        }
    }
}
