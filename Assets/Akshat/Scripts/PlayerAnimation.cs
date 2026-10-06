// PlayerAnimation.cs
// Controls the player's visual Animator (Walk and Idle) on PlayerChild.
// Attach this script directly to PlayerChild.

using UnityEngine;

namespace Akshat
{
    /// <summary>
    /// Attach this component to the 'PlayerChild' GameObject.
    /// It communicates with the parent's PlayerMovement / Rigidbody2D to drive
    /// Walk and Idle animations on the Animator.
    /// Supports both parameter-driven transitions (IsMoving, Speed) and direct state playback (fallback).
    /// </summary>
    [RequireComponent(typeof(Animator))]
    [AddComponentMenu("Akshat/Player Animation")]
    public class PlayerAnimation : MonoBehaviour
    {
        [Header("Components")]
        [Tooltip("The Animator component on PlayerChild (automatically found if unassigned).")]
        [SerializeField] private Animator animator;

        [Tooltip("The SpriteRenderer component on PlayerChild (automatically found if unassigned).")]
        [SerializeField] private SpriteRenderer spriteRenderer;

        [Tooltip("The parent PlayerMovement script (automatically found in parent if unassigned).")]
        [SerializeField] private PlayerMovement playerMovement;

        [Tooltip("The parent Rigidbody2D (automatically found in parent if unassigned).")]
        [SerializeField] private Rigidbody2D parentRigidbody;

        [Header("Animator Parameters (Case-Insensitive)")]
        [Tooltip("Name of the boolean parameter in your Animator for movement (e.g. 'IsMoving').")]
        [SerializeField] private string isMovingParam = "IsMoving";

        [Tooltip("Name of the float parameter in your Animator for speed (e.g. 'Speed').")]
        [SerializeField] private string speedParam = "Speed";

        [Header("State Names (Fallback if no parameters exist)")]
        [Tooltip("Name of the idle state in the Animator.")]
        [SerializeField] private string idleStateName = "idle";

        [Tooltip("Name of the walk state in the Animator.")]
        [SerializeField] private string walkStateName = "walk";

        [Tooltip("Crossfade duration when switching states directly (in seconds).")]
        [Range(0f, 0.3f)]
        [SerializeField] private float stateCrossfadeDuration = 0.08f;

        [Header("Sprite Flipping")]
        [Tooltip("Let this script handle SpriteRenderer.flipX based on movement direction. Set to false if PlayerMovement already handles flipping.")]
        [SerializeField] private bool handleSpriteFlip = false;

        [Tooltip("Invert flipping if your default sprite faces left instead of right.")]
        [SerializeField] private bool invertFacing = false;

        [Header("Movement Sensitivity")]
        [Tooltip("Threshold above which the player is considered moving.")]
        [SerializeField] private float moveThreshold = 0.05f;

        // Cached hashes & resolved states
        private int isMovingHash;
        private int speedHash;
        private bool hasIsMovingParam;
        private bool hasSpeedParam;

        private int resolvedIdleHash;
        private int resolvedWalkHash;
        private int currentDirectStateHash = 0;

        private void Awake()
        {
            // 1. Auto-wire components on PlayerChild
            if (animator == null)
            {
                animator = GetComponent<Animator>();
            }

            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponent<SpriteRenderer>();
            }

            // 2. Auto-wire parent components
            if (playerMovement == null)
            {
                playerMovement = GetComponentInParent<PlayerMovement>();
            }

            if (parentRigidbody == null)
            {
                parentRigidbody = GetComponentInParent<Rigidbody2D>();
            }

            // 3. Ensure the Animator is enabled and healthy
            ValidateAnimatorSetup();

            // 4. Cache parameter hashes and state hashes
            RefreshAnimatorBindings();
        }

        private void OnEnable()
        {
            // Re-validate upon being enabled
            ValidateAnimatorSetup();
        }

        private void Start()
        {
            // Force evaluate base layer weight to 1 so animations are not muted
            if (animator != null && animator.layerCount > 0)
            {
                if (animator.GetLayerWeight(0) <= 0f)
                {
                    animator.SetLayerWeight(0, 1f);
                }
            }

            RefreshAnimatorBindings();
        }

        /// <summary>
        /// Validates that the Animator doesn't have broken settings or missing Avatars.
        /// </summary>
        private void ValidateAnimatorSetup()
        {
            if (animator == null) return;

            // Ensure the component is active
            if (!animator.enabled)
            {
                animator.enabled = true;
            }

            // 2D sprites do NOT use 3D avatars; an invalid avatar will disable the Animator
            if (animator.avatar != null)
            {
                Debug.LogWarning($"[PlayerAnimation] '{gameObject.name}' has an Avatar assigned ('{animator.avatar.name}'). 2D Sprite animators do not need an Avatar and it can cause Unity to disable the component. Setting Avatar to None is recommended.", this);
            }
        }

        /// <summary>
        /// Scans the AnimatorController for matching parameters and states.
        /// </summary>
        public void RefreshAnimatorBindings()
        {
            if (animator == null || animator.runtimeAnimatorController == null) return;

            hasIsMovingParam = false;
            hasSpeedParam = false;

            // Check parameters case-insensitively
            foreach (var param in animator.parameters)
            {
                if (string.Equals(param.name, isMovingParam, System.StringComparison.OrdinalIgnoreCase))
                {
                    isMovingHash = param.nameHash;
                    hasIsMovingParam = true;
                }
                else if (string.Equals(param.name, speedParam, System.StringComparison.OrdinalIgnoreCase))
                {
                    speedHash = param.nameHash;
                    hasSpeedParam = true;
                }
            }

            // Resolve state hashes (tries exact, capitalized, lowercase, uppercase)
            resolvedIdleHash = ResolveStateHash(idleStateName);
            resolvedWalkHash = ResolveStateHash(walkStateName);
        }

        private void Update()
        {
            if (animator == null || animator.runtimeAnimatorController == null) return;

            // 1. Calculate movement state
            bool isMoving = CheckIsMoving(out float speed, out float horizontalInput);

            // 2. Handle sprite flipping if configured
            if (handleSpriteFlip && spriteRenderer != null && Mathf.Abs(horizontalInput) > 0.01f)
            {
                bool faceLeft = horizontalInput < 0f;
                if (invertFacing) faceLeft = !faceLeft;
                spriteRenderer.flipX = faceLeft;
            }

            // 3. Drive Animator
            if (hasIsMovingParam || hasSpeedParam)
            {
                // Method A: Parameter-driven (Standard Unity Animator transitions)
                if (hasIsMovingParam)
                {
                    animator.SetBool(isMovingHash, isMoving);
                }

                if (hasSpeedParam)
                {
                    animator.SetFloat(speedHash, speed);
                }
            }
            else
            {
                // Method B: Direct state playback (Fallback if transitions/parameters aren't wired)
                UpdateDirectState(isMoving);
            }
        }

        /// <summary>
        /// Reads movement from PlayerMovement or Rigidbody2D.
        /// </summary>
        private bool CheckIsMoving(out float speed, out float horizontalInput)
        {
            speed = 0f;
            horizontalInput = 0f;

            // Primary: Use PlayerMovement
            if (playerMovement != null && playerMovement.enabled)
            {
                horizontalInput = playerMovement.HorizontalInput;
                speed = Mathf.Abs(horizontalInput);

                // Check physics velocity if moving
                if (parentRigidbody != null)
                {
#if UNITY_6000_0_OR_NEWER
                    Vector2 vel = parentRigidbody.linearVelocity;
#else
                    Vector2 vel = parentRigidbody.velocity;
#endif
                    speed = Mathf.Max(speed, vel.magnitude);
                }

                return playerMovement.IsMoving || speed > moveThreshold;
            }

            // Secondary: Fallback to Rigidbody2D directly
            if (parentRigidbody != null)
            {
#if UNITY_6000_0_OR_NEWER
                Vector2 vel = parentRigidbody.linearVelocity;
#else
                Vector2 vel = parentRigidbody.velocity;
#endif
                horizontalInput = vel.x;
                speed = vel.magnitude;
                return speed > moveThreshold;
            }

            return false;
        }

        /// <summary>
        /// Directly plays or crossfades between idle and walk states by name.
        /// </summary>
        private void UpdateDirectState(bool isMoving)
        {
            int targetHash = isMoving ? resolvedWalkHash : resolvedIdleHash;
            if (targetHash == 0 || targetHash == currentDirectStateHash) return;

            if (animator.HasState(0, targetHash))
            {
                currentDirectStateHash = targetHash;

                if (stateCrossfadeDuration > 0f)
                {
                    animator.CrossFade(targetHash, stateCrossfadeDuration, 0);
                }
                else
                {
                    animator.Play(targetHash, 0, 0f);
                }
            }
        }

        /// <summary>
        /// Resolves a state name to a valid Animator state hash, trying multiple casing formats.
        /// </summary>
        private int ResolveStateHash(string stateName)
        {
            if (string.IsNullOrEmpty(stateName) || animator == null) return 0;

            // 1. Exact match
            int hash = Animator.StringToHash(stateName);
            if (animator.HasState(0, hash)) return hash;

            // 2. Capitalized (e.g. "Walk")
            string capitalized = char.ToUpperInvariant(stateName[0]) + (stateName.Length > 1 ? stateName.Substring(1) : "");
            int capHash = Animator.StringToHash(capitalized);
            if (animator.HasState(0, capHash)) return capHash;

            // 3. Lowercase (e.g. "walk")
            string lower = stateName.ToLowerInvariant();
            int lowerHash = Animator.StringToHash(lower);
            if (animator.HasState(0, lowerHash)) return lowerHash;

            // 4. Uppercase (e.g. "WALK")
            string upper = stateName.ToUpperInvariant();
            int upperHash = Animator.StringToHash(upper);
            if (animator.HasState(0, upperHash)) return upperHash;

            return hash;
        }

        /// <summary>
        /// Manually triggers direct playback of an animation state.
        /// </summary>
        public void PlayAnimation(string stateName, float crossfade = 0.05f)
        {
            int hash = ResolveStateHash(stateName);
            if (hash != 0 && animator != null && animator.HasState(0, hash))
            {
                currentDirectStateHash = hash;
                if (crossfade > 0f)
                {
                    animator.CrossFade(hash, crossfade, 0);
                }
                else
                {
                    animator.Play(hash, 0, 0f);
                }
            }
        }
    }
}
