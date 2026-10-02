// Assets/Shaurya/Scripts/PlayerMovementMode.cs
// Fully self-contained player movement controller.
// No dependency on Akshat's PlayerMovement or any other external script.
//
// Attach to the Player GameObject (alongside a Rigidbody2D).
//
// HALLWAY MODE (side-view)
//   Left / Right only. Up / Down / diagonal — ignored entirely.
//   Smooth acceleration and deceleration.
//
// TOP-DOWN MODE (room)
//   Up / Down / Left / Right — cardinal only, no diagonal.
//   Horizontal input takes priority when both axes are held simultaneously.
//   Immediate velocity (no acceleration smoothing — snappy top-down feel).
//
// TRANSITIONS
//   SetMovementEnabled(false) → zeroes velocity and stops input processing.
//   SetMode(mode)             → switches between modes (called while screen is black).

using UnityEngine;
using UnityEngine.InputSystem;

namespace Shaurya
{
    public enum MovementMode
    {
        Hallway,
        TopDown
    }

    [RequireComponent(typeof(Rigidbody2D))]
    [DisallowMultipleComponent]
    public class PlayerMovementMode : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────

        [Header("Starting Mode")]
        [Tooltip("Which movement mode is active when the scene starts.")]
        [SerializeField] private MovementMode startingMode = MovementMode.Hallway;

        [Header("Hallway Movement")]
        [Tooltip("Maximum horizontal speed in the side-view hallway (units/second).")]
        [SerializeField] private float hallwaySpeed = 8f;

        [Tooltip("Acceleration rate while a direction key is held (units/second²).")]
        [SerializeField] private float hallwayAcceleration = 60f;

        [Tooltip("Deceleration rate when no direction key is held (units/second²).")]
        [SerializeField] private float hallwayDeceleration = 70f;

        [Header("Top-Down Movement")]
        [Tooltip("Movement speed in top-down room mode (units/second).")]
        [SerializeField] private float topDownSpeed = 6f;

        [Header("Visuals & Facing")]
        [Tooltip("SpriteRenderer used for horizontal flip. Auto-found in children if unassigned.")]
        [SerializeField] private SpriteRenderer spriteRenderer;

        [Tooltip("If true, flips the SpriteRenderer's flipX. If false, flips the transform's local scale X.")]
        [SerializeField] private bool flipUsingSpriteRenderer = true;

        [Header("Input")]
        [Tooltip(
            "Assign the 'Move' InputActionReference from PlayerInputActions.inputactions.\n\n" +
            "In the Project window, expand PlayerInputActions.inputactions → drag Player/Move here.\n" +
            "If left empty, the script falls back to direct keyboard polling.")]
        [SerializeField] private InputActionReference moveActionReference;

        // ── Internal state ────────────────────────────────────────────────────

        private Rigidbody2D rb;
        private MovementMode currentMode;
        private bool movementEnabled = true;

        // Raw Vector2 input read each Update frame.
        private Vector2 rawInput;

        // Cached initial local scale for correct scale-flip math.
        private Vector3 initialScale;

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>The currently active movement mode.</summary>
        public MovementMode CurrentMode => currentMode;

        /// <summary>
        /// Switch between Hallway and TopDown modes.
        /// Called by PerspectiveTransitionManager while the screen is fully black.
        /// </summary>
        public void SetMode(MovementMode mode)
        {
            currentMode = mode;
            StopRigidbodyVelocity();
        }

        /// <summary>
        /// Pause or resume all player movement.
        /// Called by PerspectiveTransitionManager at the start/end of a transition.
        /// </summary>
        public void SetMovementEnabled(bool enabled)
        {
            movementEnabled = enabled;

            if (!enabled)
                StopRigidbodyVelocity();
        }

        // ── Unity lifecycle ───────────────────────────────────────────────────

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            rb.constraints |= RigidbodyConstraints2D.FreezeRotation;

            if (spriteRenderer == null)
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();

            initialScale  = transform.localScale;
            currentMode   = startingMode;
        }

        private void OnEnable()
        {
            if (moveActionReference != null && moveActionReference.action != null)
                moveActionReference.action.Enable();
        }

        private void OnDisable()
        {
            // Do NOT disable the action here — other scripts (e.g. Akshat's PlayerInteraction)
            // may share the same InputActionAsset. Disabling a shared action would break them.
            // The action is managed at the asset level by Unity's PlayerInput or the asset itself.
        }

        private void Update()
        {
            if (!movementEnabled) return;

            rawInput = ReadMoveInput();

            // Sprite facing is only meaningful when there is horizontal intent.
            if (currentMode == MovementMode.Hallway || currentMode == MovementMode.TopDown)
                HandleFacingDirection();
        }

        private void FixedUpdate()
        {
            if (!movementEnabled) return;

            if (currentMode == MovementMode.Hallway)
                ApplyHallwayMovement();
            else
                ApplyTopDownMovement();
        }

        // ── Hallway movement (horizontal only) ────────────────────────────────

        private void ApplyHallwayMovement()
        {
            // Only the X axis is used — Y input is discarded entirely.
            float targetVelocityX = rawInput.x * hallwaySpeed;

#if UNITY_6000_0_OR_NEWER
            float currentX = rb.linearVelocity.x;
            float currentY = rb.linearVelocity.y;
#else
            float currentX = rb.velocity.x;
            float currentY = rb.velocity.y;
#endif

            // Accelerate toward target, decelerate when no input.
            float rate = Mathf.Abs(targetVelocityX) > 0.01f ? hallwayAcceleration : hallwayDeceleration;
            float newX = Mathf.MoveTowards(currentX, targetVelocityX, rate * Time.fixedDeltaTime);

            // Preserve Y so gravity (if any) is unaffected; we never touch it.
#if UNITY_6000_0_OR_NEWER
            rb.linearVelocity = new Vector2(newX, currentY);
#else
            rb.velocity = new Vector2(newX, currentY);
#endif
        }

        // ── Top-down movement (cardinal only, no diagonal) ────────────────────

        private void ApplyTopDownMovement()
        {
            // Priority rule: horizontal input wins when both axes are held simultaneously.
            // This guarantees strictly cardinal movement — never a diagonal.
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
                // No input → stop immediately (top-down snappy feel).
                velocity = Vector2.zero;
            }

#if UNITY_6000_0_OR_NEWER
            rb.linearVelocity = velocity;
#else
            rb.velocity = velocity;
#endif
        }

        // ── Sprite facing ─────────────────────────────────────────────────────

        private void HandleFacingDirection()
        {
            // Only flip based on horizontal movement intent.
            if (Mathf.Abs(rawInput.x) < 0.05f) return;

            bool movingLeft = rawInput.x < 0f;

            if (flipUsingSpriteRenderer && spriteRenderer != null)
            {
                spriteRenderer.flipX = movingLeft;
            }
            else
            {
                // Flip local scale X instead (affects the whole transform).
                Transform target = spriteRenderer != null ? spriteRenderer.transform : transform;
                float scaleX = Mathf.Abs(initialScale.x) * (movingLeft ? -1f : 1f);
                target.localScale = new Vector3(scaleX, target.localScale.y, target.localScale.z);
            }
        }

        // ── Input reading ─────────────────────────────────────────────────────

        private Vector2 ReadMoveInput()
        {
            // Prefer the InputActionReference assigned in the Inspector (shared asset).
            if (moveActionReference != null && moveActionReference.action != null
                && moveActionReference.action.enabled)
            {
                return moveActionReference.action.ReadValue<Vector2>();
            }

            // Fallback: direct keyboard polling (used if no reference is assigned).
            Vector2 input = Vector2.zero;
            if (Keyboard.current != null)
            {
                if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) input.x += 1f;
                if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed)  input.x -= 1f;
                if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed)    input.y += 1f;
                if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed)  input.y -= 1f;
            }
            return input;
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private void StopRigidbodyVelocity()
        {
#if UNITY_6000_0_OR_NEWER
            if (rb != null) rb.linearVelocity = Vector2.zero;
#else
            if (rb != null) rb.velocity = Vector2.zero;
#endif
        }
    }
}
