// Assets/Shaurya/Scripts/DoorTransition.cs
// Self-contained perspective-switching door.
// Does NOT depend on Akshat's PlayerInteraction, IInteractable, or any layer setup.
//
// HOW IT WORKS:
//   1. Player walks into this GameObject's trigger collider → door becomes "available".
//   2. Player presses E (or Gamepad South) → plays door sound + starts transition.
//   3. Player leaves the trigger without pressing E → nothing happens.
//
// SETUP:
//   1. Attach this script to the door GameObject.
//   2. Add a BoxCollider2D → set Is Trigger = true.
//      Size it to represent the interaction range (e.g. 2 wide × 3 tall).
//   3. The Player must be tagged "Player".
//   4. Fill in the Inspector fields below.

using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;

namespace Shaurya
{
    [RequireComponent(typeof(Collider2D))]
    public class DoorTransition : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────

        [Header("Transition Settings")]
        [Tooltip("The PerspectiveTransitionManager in the scene.")]
        [SerializeField] private PerspectiveTransitionManager transitionManager;

        [Tooltip("Empty GameObject the player teleports to after the transition.\n" +
                 "Place it away from this door so the player doesn't immediately re-trigger it.")]
        [SerializeField] private Transform destination;

        [Tooltip("Which movement mode the player will be in after using this door.")]
        [SerializeField] private MovementMode targetMode = MovementMode.TopDown;

        [Tooltip("The CinemachineCamera for the destination room/hallway.\n" +
                 "Drag the CinemachineCamera that covers the destination area here.\n" +
                 "Leave empty if staying on the same camera or not using Cinemachine.")]
        [SerializeField] private CinemachineCamera targetCamera;

        [Header("Input")]
        [Tooltip("Assign the 'Interact' action from PlayerInputActions.inputactions.\n" +
                 "Expand the asset in the Project window → drag Player/Interact here.\n\n" +
                 "If left empty, the script falls back to direct E key polling.")]
        [SerializeField] private InputActionReference interactActionReference;

        [Header("Audio")]
        [Tooltip("AudioClip to play when the player uses this door.\n" +
                 "Drag a clip from the Project window here. If empty, transition still works.")]
        [SerializeField] private AudioClip doorInteractionSound;

        [Tooltip("Volume of the door sound (0 = silent, 1 = full).")]
        [Range(0f, 1f)]
        [SerializeField] private float doorVolume = 1f;

        [Header("Debug")]
        [Tooltip("Logs trigger enter/exit and interaction events to the Console.")]
        [SerializeField] private bool debugLog = true;

        // ── Internal state ────────────────────────────────────────────────────

        private AudioSource audioSource;
        private bool playerInRange = false;
        private bool interactionInProgress = false;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Awake()
        {
            // Ensure collider is a trigger (interaction zone, not a wall).
            Collider2D col = GetComponent<Collider2D>();
            if (col != null) col.isTrigger = true;

            // Add AudioSource automatically if absent.
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
                audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.loop        = false;
        }

        private void OnEnable()
        {
            // Subscribe to the Interact action if a reference is provided.
            if (interactActionReference != null && interactActionReference.action != null)
            {
                interactActionReference.action.performed += OnInteractPerformed;
                interactActionReference.action.Enable();
            }
        }

        private void OnDisable()
        {
            if (interactActionReference != null && interactActionReference.action != null)
            {
                interactActionReference.action.performed -= OnInteractPerformed;
                // Do NOT Disable() the action — other scripts share the same asset.
            }

            playerInRange = false;
        }

        private void Update()
        {
            // Fallback: direct keyboard poll when no InputActionReference is assigned.
            if (interactActionReference == null || interactActionReference.action == null)
            {
                if (playerInRange && !interactionInProgress)
                {
                    if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
                    {
                        TryInteract();
                    }
                }
            }
        }

        // ── Trigger detection ─────────────────────────────────────────────────

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag("Player")) return;

            playerInRange = true;

            if (debugLog)
                Debug.Log($"[DoorTransition] <color=cyan>{gameObject.name}</color> — Player entered range. Press E to use.");
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (!other.CompareTag("Player")) return;

            playerInRange = false;

            if (debugLog)
                Debug.Log($"[DoorTransition] <color=grey>{gameObject.name}</color> — Player left range.");
        }

        // ── Input callback (InputActionReference path) ────────────────────────

        private void OnInteractPerformed(InputAction.CallbackContext context)
        {
            if (!playerInRange) return;
            TryInteract();
        }

        // ── Core interaction ──────────────────────────────────────────────────

        private void TryInteract()
        {
            if (interactionInProgress)
            {
                if (debugLog)
                    Debug.Log("[DoorTransition] Interaction already in progress — ignored.");
                return;
            }

            if (transitionManager != null && transitionManager.IsTransitioning)
            {
                if (debugLog)
                    Debug.Log("[DoorTransition] Transition already running — ignored.");
                return;
            }

            StartCoroutine(DoInteraction());
        }

        private IEnumerator DoInteraction()
        {
            interactionInProgress = true;

            if (debugLog)
                Debug.Log($"[DoorTransition] <color=green>{gameObject.name}</color> — Interaction started! Destination: '{(destination != null ? destination.name : "NULL")}', Target Camera: '{(targetCamera != null ? targetCamera.name : "NONE")}'");

            // Play door sound immediately (fire-and-forget).
            PlayDoorSound();

            if (transitionManager != null)
            {
                transitionManager.BeginTransition(destination, targetMode, targetCamera);

                // Wait until transition finishes before re-enabling interaction.
                while (transitionManager.IsTransitioning)
                    yield return null;
            }
            else
            {
                Debug.LogWarning(
                    $"[DoorTransition] <b>Transition Manager</b> is not assigned on <b>{gameObject.name}</b>!\n" +
                    "Drag the TransitionManager GameObject into the Transition Manager field.", this);

                yield return new WaitForSeconds(1f);
            }

            interactionInProgress = false;
        }

        private void PlayDoorSound()
        {
            if (audioSource == null || doorInteractionSound == null) return;
            audioSource.PlayOneShot(doorInteractionSound, doorVolume);
        }

        // ── Gizmos ────────────────────────────────────────────────────────────

        private void OnDrawGizmos()
        {
            // Draw the interaction range and destination in the Scene view.
            Collider2D col = GetComponent<Collider2D>();
            if (col != null)
            {
                Gizmos.color = playerInRange
                    ? new Color(0f, 1f, 0f, 0.25f)
                    : new Color(1f, 0.9f, 0f, 0.2f);
                Gizmos.DrawCube(transform.position, col.bounds.size);

                Gizmos.color = playerInRange ? Color.green : Color.yellow;
                Gizmos.DrawWireCube(transform.position, col.bounds.size);
            }

            if (destination != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(destination.position, 0.3f);
                Gizmos.DrawLine(transform.position, destination.position);
            }
        }
    }
}
