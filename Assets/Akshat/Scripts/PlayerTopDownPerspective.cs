// PlayerTopDownPerspective.cs
// Dynamically applies top-down perspective foreshortening (Y-axis compression),
// keystone tapering (broader top, narrower bottom), foot grounding, and Y-depth sorting
// without needing to redraw or re-export sprite frames.

using UnityEngine;

namespace Akshat
{
    /// <summary>
    /// Attach this component to 'PlayerChild' (the visual container with SpriteRenderer)
    /// or to 'Akshat_Player'.
    /// When entering TopDown mode/areas, this component:
    /// 1. Tapers the sprite so the top (head/shoulders) becomes broader and bottom (feet) narrower.
    /// 2. Foreshortens the sprite in the Y-axis.
    /// 3. Keeps feet planted on the ground plane.
    /// 4. Handles dynamic Y-depth sorting so the player walks in front of and behind objects.
    /// </summary>
    [AddComponentMenu("Akshat/Player Top-Down Perspective")]
    public class PlayerTopDownPerspective : MonoBehaviour
    {
        [Header("Target Visuals")]
        [Tooltip("The Transform that holds the visual sprite. Defaults to this Transform (PlayerChild).")]
        [SerializeField] private Transform visualTransform;

        [Tooltip("The SpriteRenderer of the player. Auto-located if unassigned.")]
        [SerializeField] private SpriteRenderer spriteRenderer;

        [Tooltip("The parent PlayerMovement component. Auto-located in parent if unassigned.")]
        [SerializeField] private PlayerMovement playerMovement;

        [Header("Keystone Taper (Broader Top, Narrower Bottom)")]
        [Tooltip("Drives shader property _TaperStrength via MaterialPropertyBlock for trapezoidal perspective.")]
        [SerializeField] private bool enableShaderTaper = true;

        [Range(0f, 1.0f)]
        [Tooltip("How much broader the top becomes relative to the bottom in Top-Down mode (0.25 - 0.45 recommended).")]
        [SerializeField] private float topDownTaperStrength = 0.35f;

        [Tooltip("Vertical center offset around which the sprite tapers (0 = center of sprite).")]
        [SerializeField] private float taperCenterY = 0.0f;

        [Header("Foreshortening (Vertical Compression)")]
        [Range(0.4f, 1.0f)]
        [Tooltip("Vertical scale multiplier in Top-Down mode (0.70 - 0.75 simulates a 45-degree angle).")]
        [SerializeField] private float yCompressionRatio = 0.72f;

        [Range(1.0f, 1.25f)]
        [Tooltip("Optional slight horizontal expansion in Top-Down mode to emphasize width (1.0 = unchanged).")]
        [SerializeField] private float xStretchRatio = 1.0f;

        [Tooltip("Duration in seconds to smoothly transition between Hallway and Top-Down.")]
        [Range(0f, 0.8f)]
        [SerializeField] private float transitionDuration = 0.25f;

        [Tooltip("Keep the bottom of the sprite anchored to the ground plane so feet don't float when compressed.")]
        [SerializeField] private bool keepFeetGrounded = true;

        [Header("Y-Depth Sorting (Top-Down Only)")]
        [Tooltip("Dynamically updates SpriteRenderer.sortingOrder based on player's Y position in top-down mode.")]
        [SerializeField] private bool enableDynamicYSorting = true;

        [Tooltip("Base sorting order in Hallway (side-scroller) mode.")]
        [SerializeField] private int hallwaySortingOrder = 50;

        [Tooltip("Multiplier for Y position to calculate sorting order (higher = finer depth slicing).")]
        [SerializeField] private int sortingPrecision = 100;

        [Tooltip("Vertical offset from player origin representing the feet/contact point on the ground.")]
        [SerializeField] private float footPointOffsetY = -0.9f;

        [Header("Optional Drop Shadow")]
        [Tooltip("Optional child GameObject or SpriteRenderer representing the drop shadow under the feet.")]
        [SerializeField] private GameObject dropShadowObject;

        [Tooltip("Shadow opacity multiplier in Top-Down mode vs Hallway mode.")]
        [Range(0f, 1f)]
        [SerializeField] private float topDownShadowAlpha = 0.6f;

        [Header("Manual Mode / Debugging")]
        [Tooltip("Override PlayerMovement to manually preview top-down perspective in Editor.")]
        [SerializeField] private bool manualOverride = false;
        [SerializeField] private bool forceTopDown = false;

        // Shader Property IDs
        private static readonly int TaperStrengthId = Shader.PropertyToID("_TaperStrength");
        private static readonly int YCompressionId = Shader.PropertyToID("_YCompression");
        private static readonly int TaperCenterYId = Shader.PropertyToID("_TaperCenterY");

        // Internal State
        private Vector3 initialScale;
        private Vector3 initialLocalPos;
        private float currentTransition = 0f; // 0 = Hallway, 1 = TopDown
        private float targetTransition = 0f;
        private MovementMode lastKnownMode = MovementMode.Hallway;
        private SpriteRenderer shadowRenderer;
        private MaterialPropertyBlock propBlock;

        public bool IsTopDownActive => targetTransition > 0.5f;

        private void Awake()
        {
            if (visualTransform == null) visualTransform = transform;

            if (spriteRenderer == null)
            {
                spriteRenderer = visualTransform.GetComponent<SpriteRenderer>();
                if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            }

            if (playerMovement == null) playerMovement = GetComponentInParent<PlayerMovement>();

            if (dropShadowObject != null) shadowRenderer = dropShadowObject.GetComponent<SpriteRenderer>();

            initialScale = visualTransform.localScale;
            initialLocalPos = visualTransform.localPosition;
            propBlock = new MaterialPropertyBlock();

            // Detect starting mode
            if (playerMovement != null)
            {
                lastKnownMode = playerMovement.currentMode;
                bool isTopDown = lastKnownMode == MovementMode.TopDown;
                currentTransition = isTopDown ? 1f : 0f;
                targetTransition = currentTransition;
                ApplyPerspective(currentTransition);
            }
        }

        private void OnEnable()
        {
            if (visualTransform != null) ApplyPerspective(currentTransition);
        }

        private void Update()
        {
            // 1. Determine target mode
            bool isTopDown;
            if (manualOverride)
            {
                isTopDown = forceTopDown;
            }
            else if (playerMovement != null)
            {
                isTopDown = playerMovement.currentMode == MovementMode.TopDown;
            }
            else
            {
                isTopDown = false;
            }

            targetTransition = isTopDown ? 1f : 0f;

            // 2. Smoothly interpolate between perspectives
            if (transitionDuration > 0.001f)
            {
                currentTransition = Mathf.MoveTowards(currentTransition, targetTransition, Time.deltaTime / transitionDuration);
            }
            else
            {
                currentTransition = targetTransition;
            }

            // 3. Apply scale, taper, and foot grounding
            ApplyPerspective(currentTransition);

            // 4. Update Y-Depth Sorting in Top-Down mode
            UpdateYSorting(isTopDown);
        }

        /// <summary>
        /// Applies foreshortening, keystone tapering, and foot position adjustment.
        /// </summary>
        private void ApplyPerspective(float t)
        {
            if (visualTransform == null) return;

            // ── A. Shader-based Keystone Taper (Broader Top, Narrower Bottom) ──
            if (enableShaderTaper && spriteRenderer != null)
            {
                spriteRenderer.GetPropertyBlock(propBlock);

                float currentTaper = Mathf.Lerp(0f, topDownTaperStrength, t);
                float currentShaderYComp = Mathf.Lerp(1f, yCompressionRatio, t);

                propBlock.SetFloat(TaperStrengthId, currentTaper);
                propBlock.SetFloat(YCompressionId, currentShaderYComp);
                propBlock.SetFloat(TaperCenterYId, taperCenterY);

                spriteRenderer.SetPropertyBlock(propBlock);
            }

            // ── B. Transform Scale Fallback / Horizontal Stretch ──
            float currentYScale = Mathf.Lerp(1.0f, yCompressionRatio, t);
            float currentXScale = Mathf.Lerp(1.0f, xStretchRatio, t);

            float signX = Mathf.Sign(visualTransform.localScale.x);
            float targetAbsX = Mathf.Abs(initialScale.x) * currentXScale;

            // If shader taper is active and handling Y compression, keep transform Y scale at 1 to prevent double squashing
            float targetY = enableShaderTaper ? initialScale.y : initialScale.y * currentYScale;
            float targetZ = initialScale.z;

            visualTransform.localScale = new Vector3(targetAbsX * signX, targetY, targetZ);

            // ── C. Keep Feet Grounded ──
            if (keepFeetGrounded)
            {
                float spriteHalfHeight = 1.0f;
                if (spriteRenderer != null && spriteRenderer.sprite != null)
                {
                    spriteHalfHeight = (spriteRenderer.sprite.rect.height / spriteRenderer.sprite.pixelsPerUnit) * 0.5f;
                }

                float lostHalfHeight = spriteHalfHeight * (1.0f - currentYScale) * initialScale.y;
                visualTransform.localPosition = new Vector3(
                    initialLocalPos.x,
                    initialLocalPos.y - lostHalfHeight,
                    initialLocalPos.z
                );
            }

            // ── D. Drop Shadow Opacity ──
            if (shadowRenderer != null)
            {
                Color c = shadowRenderer.color;
                c.a = Mathf.Lerp(0.2f, topDownShadowAlpha, t);
                shadowRenderer.color = c;
            }
        }

        private void UpdateYSorting(bool isTopDown)
        {
            if (spriteRenderer == null) return;

            if (isTopDown && enableDynamicYSorting)
            {
                float groundY = transform.position.y + footPointOffsetY;
                spriteRenderer.sortingOrder = -Mathf.RoundToInt(groundY * sortingPrecision);
            }
            else if (!isTopDown)
            {
                spriteRenderer.sortingOrder = hallwaySortingOrder;
            }
        }

        public void SetTopDownPerspective(bool isTopDown, bool instant = false)
        {
            targetTransition = isTopDown ? 1f : 0f;
            if (instant)
            {
                currentTransition = targetTransition;
                ApplyPerspective(currentTransition);
                UpdateYSorting(isTopDown);
            }
        }

        public void SetTaperStrength(float strength)
        {
            topDownTaperStrength = Mathf.Clamp(strength, 0f, 1f);
            ApplyPerspective(currentTransition);
        }

        public void SetCompressionRatio(float ratio)
        {
            yCompressionRatio = Mathf.Clamp(ratio, 0.3f, 1.0f);
            ApplyPerspective(currentTransition);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.green;
            Vector3 footPos = transform.position + new Vector3(0f, footPointOffsetY, 0f);
            Gizmos.DrawWireSphere(footPos, 0.15f);
        }
#endif
    }
}
