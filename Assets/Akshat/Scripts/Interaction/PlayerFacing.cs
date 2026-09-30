using UnityEngine;

namespace Akshat.Interaction
{
    /// <summary>
    /// Common interface for any component providing the player's 2D facing direction.
    /// Decouples interaction detection from movement or animation systems.
    /// </summary>
    public interface IFacingProvider
    {
        Vector2 FacingDirection { get; }
        bool IsFacingRight { get; }
    }

    /// <summary>
    /// Modular component responsible for determining and exposing the player's facing direction.
    /// Can be observed by PlayerInteraction or replaced in the future with a full rotation system.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerFacing : MonoBehaviour, IFacingProvider
    {
        [Header("References (Auto-found if unassigned)")]
        [Tooltip("Optional reference to PlayerMovement to infer facing from movement input.")]
        [SerializeField] private PlayerMovement playerMovement;

        [Tooltip("Optional reference to visual SpriteRenderer to read flipX.")]
        [SerializeField] private SpriteRenderer visualRenderer;

        [Header("Facing Configuration")]
        [Tooltip("Default facing direction when game starts or when player is idle.")]
        [SerializeField] private Vector2 defaultFacingDirection = Vector2.right;

        private Vector2 currentFacingDirection = Vector2.right;

        public Vector2 FacingDirection => currentFacingDirection;
        public bool IsFacingRight => currentFacingDirection.x >= 0f;

        private void Awake()
        {
            currentFacingDirection = defaultFacingDirection.sqrMagnitude > 0.001f 
                ? defaultFacingDirection.normalized 
                : Vector2.right;

            if (playerMovement == null)
            {
                playerMovement = GetComponent<PlayerMovement>();
            }

            if (visualRenderer == null)
            {
                visualRenderer = GetComponentInChildren<SpriteRenderer>();
            }
        }

        private void Update()
        {
            UpdateFacingDirection();
        }

        private void UpdateFacingDirection()
        {
            // 1. Prefer reading movement input from PlayerMovement if active
            if (playerMovement != null && Mathf.Abs(playerMovement.HorizontalInput) > 0.05f)
            {
                currentFacingDirection = playerMovement.HorizontalInput > 0f ? Vector2.right : Vector2.left;
                return;
            }

            // 2. Read SpriteRenderer flip state if available
            if (visualRenderer != null)
            {
                currentFacingDirection = visualRenderer.flipX ? Vector2.left : Vector2.right;
                return;
            }

            // 3. Fallback to transform localScale X
            if (Mathf.Abs(transform.localScale.x) > 0.001f)
            {
                currentFacingDirection = transform.localScale.x >= 0f ? Vector2.right : Vector2.left;
            }
        }

        /// <summary>
        /// Explicitly override or set facing direction from external systems (e.g. dialogue, cutscenes).
        /// </summary>
        public void SetFacingDirection(Vector2 newDirection)
        {
            if (newDirection.sqrMagnitude > 0.001f)
            {
                currentFacingDirection = newDirection.normalized;
            }
        }
    }
}
