using UnityEngine;
using UnityEngine.InputSystem;

namespace Akshat
{
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerMovement : MonoBehaviour
    {
        [Header("Movement Settings")]
        [Tooltip("Horizontal movement speed in units per second.")]
        [SerializeField] private float moveSpeed = 8f;

        [Tooltip("Acceleration rate when speeding up.")]
        [SerializeField] private float acceleration = 60f;

        [Tooltip("Deceleration rate when stopping.")]
        [SerializeField] private float deceleration = 70f;

        [Tooltip("If true, velocity changes instantly without acceleration/deceleration smoothing.")]
        [SerializeField] private bool instantResponse = false;

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
        private float horizontalInput;
        private Vector3 initialScale;
        private int speedParamHash;
        private int isMovingParamHash;
        private bool hasSpeedParam;
        private bool hasIsMovingParam;

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();

            // Ensure player stays upright and doesn't rotate when moving
            rb.constraints |= RigidbodyConstraints2D.FreezeRotation;

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

                // 1D Axis Composite for Left (-1) and Right (+1)
                moveAction.AddCompositeBinding("1DAxis")
                    // Keyboard: A / D
                    .With("Negative", "<Keyboard>/a")
                    .With("Positive", "<Keyboard>/d")
                    // Keyboard: Left Arrow / Right Arrow
                    .With("Negative", "<Keyboard>/leftArrow")
                    .With("Positive", "<Keyboard>/rightArrow")
                    // Gamepad: D-Pad Left / Right
                    .With("Negative", "<Gamepad>/dpad/left")
                    .With("Positive", "<Gamepad>/dpad/right")
                    // Gamepad: Left Stick Left / Right
                    .With("Negative", "<Gamepad>/leftStick/left")
                    .With("Positive", "<Gamepad>/leftStick/right");
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
            ApplyHorizontalMovement();
        }

        private void ReadMovementInput()
        {
            if (moveAction != null && moveAction.enabled)
            {
                if (moveAction.expectedControlType == "Vector2")
                {
                    horizontalInput = moveAction.ReadValue<Vector2>().x;
                }
                else
                {
                    horizontalInput = moveAction.ReadValue<float>();
                }
            }
            else
            {
                // Fallback direct device polling if action is not active
                horizontalInput = 0f;
                if (Keyboard.current != null)
                {
                    if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) horizontalInput -= 1f;
                    if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) horizontalInput += 1f;
                }
                if (Gamepad.current != null && Mathf.Abs(horizontalInput) < 0.01f)
                {
                    horizontalInput = Gamepad.current.leftStick.x.ReadValue();
                }
            }
        }

        private void ApplyHorizontalMovement()
        {
            float targetVelocityX = horizontalInput * moveSpeed;
            float currentVelocityX;

#if UNITY_6000_0_OR_NEWER
            float currentX = rb.linearVelocity.x;
            float currentY = rb.linearVelocity.y;
#else
            float currentX = rb.velocity.x;
            float currentY = rb.velocity.y;
#endif

            if (instantResponse)
            {
                currentVelocityX = targetVelocityX;
            }
            else
            {
                float rate = Mathf.Abs(targetVelocityX) > 0.01f ? acceleration : deceleration;
                currentVelocityX = Mathf.MoveTowards(currentX, targetVelocityX, rate * Time.fixedDeltaTime);
            }

#if UNITY_6000_0_OR_NEWER
            rb.linearVelocity = new Vector2(currentVelocityX, currentY);
#else
            rb.velocity = new Vector2(currentVelocityX, currentY);
#endif
        }

        private void HandleFacingDirection()
        {
            if (Mathf.Abs(horizontalInput) > 0.05f)
            {
                bool movingLeft = horizontalInput < 0f;

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
            if (animator == null) return;

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
            if (animator == null) return;

            float absSpeed = Mathf.Abs(horizontalInput);
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
            horizontalInput = value.Get<Vector2>().x;
        }

        /// <summary>
        /// Called automatically if you wire Unity Events in the PlayerInput component.
        /// </summary>
        public void OnMove(InputAction.CallbackContext context)
        {
            usePlayerInputComponent = true;
            if (context.valueType == typeof(Vector2))
            {
                horizontalInput = context.ReadValue<Vector2>().x;
            }
            else
            {
                horizontalInput = context.ReadValue<float>();
            }
        }
        #endregion

        // Public getter for other scripts (e.g. state machines, animations)
        public float HorizontalInput => horizontalInput;
        public bool IsMoving => Mathf.Abs(horizontalInput) > 0.05f;
    }
}
