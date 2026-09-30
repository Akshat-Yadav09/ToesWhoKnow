using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Akshat.Interaction
{
    /// <summary>
    /// Player component responsible for detecting nearby interactables in front of the player
    /// and routing user interaction input to the detected IInteractable.
    /// Completely decoupled from specific object logic (doors, stoves, photos, etc.).
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerInteraction : MonoBehaviour
    {
        [Header("Detection Transform & Area")]
        [Tooltip("The pivot point from which detection originates. If unassigned, automatically finds child 'InteractionPoint' or uses Player transform.")]
        [SerializeField] private Transform interactionPoint;

        [Tooltip("Radius around the detection center to search for interactable objects.")]
        [SerializeField] private float interactionRadius = 1.2f;

        [Tooltip("Shifts detection area forward along the player's facing direction to prioritize objects in front.")]
        [SerializeField] private float forwardOffset = 0.5f;

        [Header("Filtering & Facing")]
        [Tooltip("LayerMask containing interactable world objects (default: Interactable layer).")]
        [SerializeField] private LayerMask interactableLayer;

        [Tooltip("If true, requires the interactable to be in front of the player's facing direction.")]
        [SerializeField] private bool requireFacingObject = true;

        [Tooltip("Minimum dot product with facing direction (-1 = 360°, 0 = 180° cone in front, 0.5 = 120° cone).")]
        [Range(-1f, 1f)]
        [SerializeField] private float minFacingDot = 0f;

        [Tooltip("How often (in seconds) to scan for interactables to avoid per-frame physics overhead.")]
        [SerializeField] private float detectionInterval = 0.04f;

        [Header("Screen-Space UI Reference")]
        [Tooltip("Reference to the central Screen-Space InteractionUI overlay. Auto-found if unassigned.")]
        [SerializeField] private InteractionUI interactionUI;

        [Header("Input System (Reusing Existing Actions)")]
        [Tooltip("Optional reference to an existing InputAction (e.g. from PlayerInputActions.inputactions).")]
        [SerializeField] private InputActionReference interactActionReference;

        [Tooltip("Custom embedded action used if interactActionReference is unassigned. Defaults to 'E' and Gamepad South button.")]
        [SerializeField] private InputAction fallbackInteractAction;

        [Tooltip("Enable if using the PlayerInput component with 'Send Messages' or Unity Events.")]
        [SerializeField] private bool usePlayerInputComponent = false;

        [Header("Decoupled Facing Provider (Optional)")]
        [Tooltip("Optional component providing facing direction. Auto-located on Player or children if unassigned.")]
        [SerializeField] private MonoBehaviour facingProviderComponent;

        [Tooltip("Optional fallback SpriteRenderer to read flipX if no facing provider exists.")]
        [SerializeField] private SpriteRenderer visualSpriteRenderer;

        // Events for modular listeners (e.g. dialogue, UI, audio, quest systems)
        public event Action<IInteractable> OnInteractableDetected;
        public event Action OnInteractableLost;

        // Internal detection state & non-allocating buffers
        private readonly Collider2D[] hitBuffer = new Collider2D[16];
        private ContactFilter2D contactFilter;
        private IInteractable currentInteractable;
        private IFacingProvider facingProvider;
        private float nextDetectionTime;

        public IInteractable CurrentInteractable => currentInteractable;

        private void Awake()
        {
            // 1. Resolve InteractionPoint
            if (interactionPoint == null)
            {
                Transform foundPoint = transform.Find("InteractionPoint");
                interactionPoint = foundPoint != null ? foundPoint : transform;
            }

            // 2. Resolve Facing Provider (modular interface)
            if (facingProviderComponent is IFacingProvider provider)
            {
                facingProvider = provider;
            }
            else
            {
                facingProvider = GetComponent<IFacingProvider>() 
                              ?? GetComponentInChildren<IFacingProvider>() 
                              ?? GetComponentInParent<IFacingProvider>();
            }

            // 3. Resolve visual SpriteRenderer fallback
            if (visualSpriteRenderer == null)
            {
                visualSpriteRenderer = GetComponentInChildren<SpriteRenderer>();
            }

            // 4. Resolve default LayerMask if not configured (Layer 6: 'Interactable')
            if (interactableLayer.value == 0)
            {
                int layerIndex = LayerMask.NameToLayer("Interactable");
                if (layerIndex >= 0)
                {
                    interactableLayer = 1 << layerIndex;
                }
            }

            // 5. Initialize non-allocating ContactFilter2D
            contactFilter = new ContactFilter2D();
            contactFilter.SetLayerMask(interactableLayer);
            contactFilter.useLayerMask = true;
            contactFilter.useTriggers = true;

            // 6. Resolve InteractionUI if not explicitly wired in Inspector
            if (interactionUI == null)
            {
                interactionUI = FindAnyObjectByType<InteractionUI>();
            }

            // 7. Setup fallback input action if no reference is assigned
            InitializeInputActions();
        }

        private void InitializeInputActions()
        {
            if (interactActionReference == null && (fallbackInteractAction == null || fallbackInteractAction.bindings.Count == 0))
            {
                fallbackInteractAction = new InputAction("Interact", InputActionType.Button);
                fallbackInteractAction.AddBinding("<Keyboard>/e");
                fallbackInteractAction.AddBinding("<Gamepad>/buttonSouth");
            }
        }

        private void OnEnable()
        {
            if (!usePlayerInputComponent)
            {
                if (interactActionReference != null && interactActionReference.action != null)
                {
                    interactActionReference.action.performed += OnInteractActionPerformed;
                    interactActionReference.action.Enable();
                }
                else if (fallbackInteractAction != null)
                {
                    fallbackInteractAction.performed += OnInteractActionPerformed;
                    fallbackInteractAction.Enable();
                }
            }
        }

        private void OnDisable()
        {
            if (!usePlayerInputComponent)
            {
                if (interactActionReference != null && interactActionReference.action != null)
                {
                    interactActionReference.action.performed -= OnInteractActionPerformed;
                    interactActionReference.action.Disable();
                }
                else if (fallbackInteractAction != null)
                {
                    fallbackInteractAction.performed -= OnInteractActionPerformed;
                    fallbackInteractAction.Disable();
                }
            }

            ClearCurrentInteractable();
        }

        private void Update()
        {
            // Periodic non-allocating physics check
            if (Time.time >= nextDetectionTime)
            {
                nextDetectionTime = Time.time + detectionInterval;
                ScanForInteractables();
            }
        }

        /// <summary>
        /// Scans for the closest valid interactable using non-allocating 2D physics.
        /// Prioritizes interactables positioned in front of the player.
        /// </summary>
        public void ScanForInteractables()
        {
            Vector2 origin = interactionPoint != null ? (Vector2)interactionPoint.position : (Vector2)transform.position;
            Vector2 facingDir = GetFacingDirection();
            Vector2 detectionCenter = origin + (facingDir * forwardOffset);

            // Modern Unity non-allocating 2D overlap query
            int hitCount = Physics2D.OverlapCircle(detectionCenter, interactionRadius, contactFilter, hitBuffer);

            IInteractable bestInteractable = null;
            float closestDistanceSqr = float.MaxValue;

            for (int i = 0; i < hitCount; i++)
            {
                Collider2D hitCollider = hitBuffer[i];
                if (hitCollider == null) continue;

                // Locate IInteractable on collider or parent
                IInteractable interactable = hitCollider.GetComponentInParent<IInteractable>();
                if (interactable == null || !interactable.CanInteract()) continue;

                Vector2 toCollider = ((Vector2)hitCollider.bounds.center - origin);
                float sqrDist = toCollider.sqrMagnitude;

                // Check forward facing condition if enabled
                if (requireFacingObject && sqrDist > 0.001f)
                {
                    Vector2 dirToCollider = toCollider.normalized;
                    float dot = Vector2.Dot(facingDir, dirToCollider);

                    if (dot < minFacingDot)
                    {
                        continue; // Interactable is behind or outside the facing cone
                    }
                }

                // Choose the closest valid interactable
                if (sqrDist < closestDistanceSqr)
                {
                    closestDistanceSqr = sqrDist;
                    bestInteractable = interactable;
                }
            }

            UpdateCurrentInteractable(bestInteractable);
        }

        private void UpdateCurrentInteractable(IInteractable newInteractable)
        {
            if (ReferenceEquals(currentInteractable, newInteractable)) return;

            currentInteractable = newInteractable;

            if (currentInteractable != null)
            {
                OnInteractableDetected?.Invoke(currentInteractable);
                if (interactionUI != null)
                {
                    interactionUI.ShowPrompt(currentInteractable);
                }
            }
            else
            {
                OnInteractableLost?.Invoke();
                if (interactionUI != null)
                {
                    interactionUI.HidePrompt();
                }
            }
        }

        private void ClearCurrentInteractable()
        {
            if (currentInteractable != null)
            {
                currentInteractable = null;
                OnInteractableLost?.Invoke();
                if (interactionUI != null)
                {
                    interactionUI.HidePrompt();
                }
            }
        }

        /// <summary>
        /// Executes interaction logic on the current interactable.
        /// </summary>
        public void ExecuteInteraction()
        {
            if (currentInteractable == null || !currentInteractable.CanInteract()) return;

            // Trigger polymorphic interaction
            currentInteractable.Interact();

            // Rescan immediately in case interaction modified interactable state
            ScanForInteractables();
        }

        /// <summary>
        /// Gets the player's 2D facing direction, querying the decoupled IFacingProvider
        /// or falling back to SpriteRenderer / transform localScale.
        /// </summary>
        public Vector2 GetFacingDirection()
        {
            if (facingProvider != null)
            {
                return facingProvider.FacingDirection;
            }

            if (visualSpriteRenderer != null)
            {
                return visualSpriteRenderer.flipX ? Vector2.left : Vector2.right;
            }

            return transform.localScale.x >= 0f ? Vector2.right : Vector2.left;
        }

        #region Input Handlers
        private void OnInteractActionPerformed(InputAction.CallbackContext context)
        {
            ExecuteInteraction();
        }

        /// <summary>
        /// Called automatically by Unity's PlayerInput component when Send Messages mode is active.
        /// </summary>
        public void OnInteract(InputValue value)
        {
            if (value.isPressed)
            {
                ExecuteInteraction();
            }
        }

        /// <summary>
        /// Called automatically when wired through Unity Events in PlayerInput.
        /// </summary>
        public void OnInteract(InputAction.CallbackContext context)
        {
            if (context.performed)
            {
                ExecuteInteraction();
            }
        }
        #endregion

        #region Gizmos & Debugging
        private void OnDrawGizmosSelected()
        {
            Vector3 origin = interactionPoint != null ? interactionPoint.position : transform.position;
            Vector3 facing = GetFacingDirection();
            Vector3 center = origin + (facing * forwardOffset);

            // Detection area wireframe
            Gizmos.color = currentInteractable != null ? new Color(0.2f, 1f, 0.2f, 0.9f) : new Color(1f, 0.9f, 0.1f, 0.6f);
            Gizmos.DrawWireSphere(center, interactionRadius);

            // Forward facing arrow
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(origin, origin + facing * (interactionRadius + forwardOffset));

            // Facing cone boundaries
            if (requireFacingObject && minFacingDot > -0.99f)
            {
                float angle = Mathf.Acos(Mathf.Clamp(minFacingDot, -1f, 1f)) * Mathf.Rad2Deg;
                Vector3 leftBoundary = Quaternion.Euler(0, 0, angle) * facing;
                Vector3 rightBoundary = Quaternion.Euler(0, 0, -angle) * facing;

                Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.35f);
                Gizmos.DrawLine(origin, origin + leftBoundary * (interactionRadius + forwardOffset));
                Gizmos.DrawLine(origin, origin + rightBoundary * (interactionRadius + forwardOffset));
            }
        }
        #endregion
    }
}
