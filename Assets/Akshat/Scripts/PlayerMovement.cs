using UnityEngine;
using UnityEngine.InputSystem;

namespace Akshat
{
    public enum MovementMode
    {
        Hallway,
        TopDown
    }

    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerMovement : MonoBehaviour
    {
        [Header("Movement Mode")]
        public MovementMode currentMode = MovementMode.Hallway;

        [Header("Hallway Settings (Side-scroller)")]
        [Tooltip("Horizontal movement speed in units per second.")]
        [SerializeField] private float moveSpeed = 8f;

        [Tooltip("Acceleration rate when speeding up.")]
        [SerializeField] private float acceleration = 60f;

        [Tooltip("Deceleration rate when stopping.")]
        [SerializeField] private float deceleration = 70f;

        [Tooltip("Gravity scale when in Hallway (side-scroller) mode. Set to Rigidbody2D's default or custom value.")]
        [SerializeField] private float hallwayGravityScale = 1f;

        [Header("Top-Down Settings")]
        [Tooltip("Movement speed in top-down mode (units/second).")]
        [SerializeField] private float topDownSpeed = 6f;

        [Header("New Input System Settings")]
        [Tooltip("Optional custom action. If left empty, default bindings (A/D, Arrow keys, Gamepad Stick & D-Pad) are auto-generated.")]
        [SerializeField] private InputAction moveAction;

        [Tooltip("Enable if using the PlayerInput component with 'Send Messages' or Unity Events.")]
        [SerializeField] private bool usePlayerInputComponent = false;

        [Header("Visuals & Facing Direction")]
        [Tooltip("Flip the SpriteRenderer directly, or flip the local scale X.")]
        [SerializeField] private bool flipUsingSpriteRenderer = true;

        [Tooltip("Reference to the SpriteRenderer (automatically found in children if unassigned).")]
        [SerializeField] private SpriteRenderer spriteRenderer;

        [Tooltip("Optional Animator (automatically found in children if unassigned).")]
        [SerializeField] private Animator animator;

        // Internal references & state
        private Rigidbody2D rb;
        private Vector2 rawInput;
        private Vector3 initialScale;
        private int speedParamHash;
        private int isMovingParamHash;
        private bool hasSpeedParam;
        private bool hasIsMovingParam;
        private bool movementEnabled = true;

        // Public getter for other scripts (e.g. state machines, animations)
        public float HorizontalInput => rawInput.x;
        public bool IsMoving => rawInput.magnitude > 0.05f;

        public void SetMode(MovementMode mode)
        {
            currentMode = mode;
            ApplyGravityForMode(mode);
            StopRigidbodyVelocity();
        }

        private void ApplyGravityForMode(MovementMode mode)
        {
            if (rb == null) return;
            rb.gravityScale = (mode == MovementMode.TopDown) ? 0f : hallwayGravityScale;
        }

        public void SetMovementEnabled(bool enabled)
        {
            movementEnabled = enabled;
            if (!enabled)
            {
                StopRigidbodyVelocity();
            }
        }

        public void Teleport(Vector3 newPosition)
        {
            transform.position = newPosition;
            if (rb != null)
            {
                rb.position = newPosition;
                StopRigidbodyVelocity();
            }
            Physics2D.SyncTransforms();
        }

        public void StopRigidbodyVelocity()
        {
            if (rb == null) return;
#if UNITY_6000_0_OR_NEWER
            rb.linearVelocity = Vector2.zero;
#else
            rb.velocity = Vector2.zero;
#endif
            rb.angularVelocity = 0f;
        }

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();

            // Ensure player stays upright and doesn't rotate when moving
            rb.constraints |= RigidbodyConstraints2D.FreezeRotation;

            // Continuous collision detection prevents falling through floors when transitioning
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            // Apply starting gravity scale
            ApplyGravityForMode(currentMode);

            // Auto-locate visual components if not assigned
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            }

            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }

            initialScale = transform.localScale;

            // Cache animator parameters if present
            CheckAnimatorParameters();

            // Set up default input bindings for A/D, Arrows, and Gamepad if not already assigned
            InitializeInputActions();
        }

        private void InitializeInputActions()
        {
            if (moveAction == null || moveAction.bindings.Count == 0)
            {
                moveAction = new InputAction("Move", InputActionType.Value);
                moveAction.expectedControlType = "Vector2";

                // 2D Vector Composite for Up/Down/Left/Right
                moveAction.AddCompositeBinding("2DVector")
                    // Keyboard: W A S D
                    .With("Up", "<Keyboard>/w")
                    .With("Down", "<Keyboard>/s")
                    .With("Left", "<Keyboard>/a")
                    .With("Right", "<Keyboard>/d")
                    // Keyboard: Arrows
                    .With("Up", "<Keyboard>/upArrow")
                    .With("Down", "<Keyboard>/downArrow")
                    .With("Left", "<Keyboard>/leftArrow")
                    .With("Right", "<Keyboard>/rightArrow")
                    // Gamepad: D-Pad
                    .With("Up", "<Gamepad>/dpad/up")
                    .With("Down", "<Gamepad>/dpad/down")
                    .With("Left", "<Gamepad>/dpad/left")
                    .With("Right", "<Gamepad>/dpad/right")
                    // Gamepad: Left Stick
                    .With("Up", "<Gamepad>/leftStick/up")
                    .With("Down", "<Gamepad>/leftStick/down")
                    .With("Left", "<Gamepad>/leftStick/left")
                    .With("Right", "<Gamepad>/leftStick/right");
            }
        }

        private void OnEnable()
        {
            if (!usePlayerInputComponent && moveAction != null)
            {
                moveAction.Enable();
            }
        }

        private void OnDisable()
        {
            if (!usePlayerInputComponent && moveAction != null)
            {
                moveAction.Disable();
            }
        }

        private void Update()
        {
            if (!movementEnabled) return;

            // 1. Read input unless PlayerInput component is feeding values
            if (!usePlayerInputComponent)
            {
                ReadMovementInput();
            }

            // 2. Handle sprite facing direction (Left / Right)
            HandleFacingDirection();

            // 3. Update animator parameters if available
            UpdateAnimator();
        }

        private void FixedUpdate()
        {
            if (!movementEnabled) return;

            if (currentMode == MovementMode.Hallway)
            {
                ApplyHallwayMovement();
            }
            else
            {
                ApplyTopDownMovement();
            }
        }

        private void ReadMovementInput()
        {
            if (moveAction != null && moveAction.enabled)
            {
                if (moveAction.expectedControlType == "Vector2")
                {
                    rawInput = moveAction.ReadValue<Vector2>();
                }
                else
                {
                    rawInput = new Vector2(moveAction.ReadValue<float>(), 0f);
                }
            }
            else
            {
                // Fallback direct device polling if action is not active
                rawInput = Vector2.zero;
                if (Keyboard.current != null)
                {
                    if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) rawInput.x -= 1f;
                    if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) rawInput.x += 1f;
                    if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) rawInput.y += 1f;
                    if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) rawInput.y -= 1f;
                }
                if (Gamepad.current != null && rawInput.magnitude < 0.01f)
                {
                    rawInput = Gamepad.current.leftStick.ReadValue();
                }
            }
        }

        private void ApplyHallwayMovement()
        {
            float targetVelocityX = rawInput.x * moveSpeed;

#if UNITY_6000_0_OR_NEWER
            float currentX = rb.linearVelocity.x;
            float currentY = rb.linearVelocity.y;
#else
            float currentX = rb.velocity.x;
            float currentY = rb.velocity.y;
#endif

            float rate = Mathf.Abs(targetVelocityX) > 0.01f ? acceleration : deceleration;
            float currentVelocityX = Mathf.MoveTowards(currentX, targetVelocityX, rate * Time.fixedDeltaTime);

#if UNITY_6000_0_OR_NEWER
            rb.linearVelocity = new Vector2(currentVelocityX, currentY);
#else
            rb.velocity = new Vector2(currentVelocityX, currentY);
#endif
        }

        private void ApplyTopDownMovement()
        {
            Vector2 velocity;

            if (Mathf.Abs(rawInput.x) > 0.1f)
            {
                // Horizontal intent → move left or right, vertical is ignored.
                velocity = new Vector2(Mathf.Sign(rawInput.x) * topDownSpeed, 0f);
            }
            else if (Mathf.Abs(rawInput.y) > 0.1f)
            {
                // No horizontal intent → move up or down.
                velocity = new Vector2(0f, Mathf.Sign(rawInput.y) * topDownSpeed);
            }
            else
            {
                // No input → stop immediately
                velocity = Vector2.zero;
            }

#if UNITY_6000_0_OR_NEWER
            rb.linearVelocity = velocity;
#else
            rb.velocity = velocity;
#endif
        }

        private void HandleFacingDirection()
        {
            if (Mathf.Abs(rawInput.x) > 0.05f)
            {
                bool movingLeft = rawInput.x < 0f;

                if (flipUsingSpriteRenderer && spriteRenderer != null)
                {
                    spriteRenderer.flipX = movingLeft;
                }
                else
                {
                    // Flip the entire transform (or child) scale X
                    Transform targetTransform = (spriteRenderer != null) ? spriteRenderer.transform : transform;
                    float scaleX = Mathf.Abs(targetTransform.localScale.x) * (movingLeft ? -1f : 1f);
                    targetTransform.localScale = new Vector3(scaleX, targetTransform.localScale.y, targetTransform.localScale.z);
                }
            }
        }

        private void CheckAnimatorParameters()
        {
            if (animator == null || animator.runtimeAnimatorController == null) return;

            speedParamHash = Animator.StringToHash("Speed");
            isMovingParamHash = Animator.StringToHash("IsMoving");

            foreach (var param in animator.parameters)
            {
                if (param.nameHash == speedParamHash) hasSpeedParam = true;
                if (param.nameHash == isMovingParamHash) hasIsMovingParam = true;
            }
        }

        private void UpdateAnimator()
        {
            if (animator == null || animator.runtimeAnimatorController == null) return;

            float absSpeed = rawInput.magnitude; // or just rawInput.x if you prefer, but magnitude works for both modes
            if (hasSpeedParam)
            {
                animator.SetFloat(speedParamHash, absSpeed);
            }
            if (hasIsMovingParam)
            {
                animator.SetBool(isMovingParamHash, absSpeed > 0.05f);
            }
        }

        #region PlayerInput Component Messages
        /// <summary>
        /// Called automatically if you attach Unity's PlayerInput component with 'Send Messages' mode.
        /// </summary>
        public void OnMove(InputValue value)
        {
            usePlayerInputComponent = true;
            rawInput = value.Get<Vector2>();
        }

        /// <summary>
        /// Called automatically if you wire Unity Events in the PlayerInput component.
        /// </summary>
        public void OnMove(InputAction.CallbackContext context)
        {
            usePlayerInputComponent = true;
            if (context.valueType == typeof(Vector2))
            {
                rawInput = context.ReadValue<Vector2>();
            }
            else
            {
                rawInput = new Vector2(context.ReadValue<float>(), 0f);
            }
        }
        #endregion
    }
}
