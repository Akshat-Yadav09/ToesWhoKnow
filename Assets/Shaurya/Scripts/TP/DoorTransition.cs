using System.Collections;
using UnityEngine;
using Unity.Cinemachine;
using Akshat.Interaction;

namespace Akshat.Interaction
{
    [RequireComponent(typeof(Collider2D))]
    public class DoorTransition : MonoBehaviour, IInteractable
    {
        // ── Inspector ─────────────────────────────────────────────────────────

        [Header("Transition Settings")]
        [Tooltip("The PerspectiveTransitionManager in the scene.")]
        [SerializeField] private Shaurya.PerspectiveTransitionManager transitionManager;

        [Tooltip("Empty GameObject the player teleports to after the transition.\n" +
                 "Place it away from this door so the player doesn't immediately re-trigger it.")]
        [SerializeField] private Transform destination;

        [Tooltip("Which movement mode the player will be in after using this door.")]
        [SerializeField] private Akshat.MovementMode targetMode = Akshat.MovementMode.TopDown;

        [Tooltip("The CinemachineCamera for the destination room/hallway.\n" +
                 "Drag the CinemachineCamera that covers the destination area here.\n" +
                 "Leave empty if staying on the same camera or not using Cinemachine.")]
        [SerializeField] private CinemachineCamera targetCamera;

        [Tooltip("The RoomZone this door leads to. Assign this to enable room culling (activating this room and deactivating the old one).")]
        [SerializeField] private Akshat.RoomSystem.RoomZone targetRoom;

        [Header("Audio")]
        [Tooltip("AudioClip to play when the player uses this door.\n" +
                 "Drag a clip from the Project window here. If empty, transition still works.")]
        [SerializeField] private AudioClip doorInteractionSound;

        [Tooltip("Volume of the door sound (0 = silent, 1 = full).")]
        [Range(0f, 1f)]
        [SerializeField] private float doorVolume = 1f;

        [Header("Debug")]
        [Tooltip("Logs interaction events to the Console.")]
        [SerializeField] private bool debugLog = true;

        // ── Internal state ────────────────────────────────────────────────────

        private AudioSource audioSource;

        public string InteractionPrompt => "Use Door";

        public bool CanInteract()
        {
            var mgr = transitionManager != null ? transitionManager : Shaurya.PerspectiveTransitionManager.Instance;
            return mgr == null || !mgr.IsTransitioning;
        }

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

        public void Interact()
        {
            TryInteract();
        }

        // ── Core interaction ──────────────────────────────────────────────────

        private void TryInteract()
        {
            if (!CanInteract()) return;

            if (debugLog)
                Debug.Log($"[DoorTransition] <color=green>{gameObject.name}</color> — Interaction started! Destination: '{(destination != null ? destination.name : "NULL")}', Target Camera: '{(targetCamera != null ? targetCamera.name : "NONE")}'");

            PlayDoorSound();

            var mgr = transitionManager != null ? transitionManager : Shaurya.PerspectiveTransitionManager.Instance;
            if (mgr != null)
            {
                mgr.BeginTransition(destination, targetMode, targetCamera, targetRoom);
            }
            else
            {
                Debug.LogWarning(
                    $"[DoorTransition] <b>Transition Manager</b> was not found in the scene for <b>{gameObject.name}</b>!", this);
            }
        }

        private void PlayDoorSound()
        {
            if (audioSource == null || doorInteractionSound == null) return;
            audioSource.PlayOneShot(doorInteractionSound, doorVolume);
        }

        // ── Gizmos ────────────────────────────────────────────────────────────

        private void OnDrawGizmos()
        {
            if (destination != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(destination.position, 0.3f);
                Gizmos.DrawLine(transform.position, destination.position);
            }
        }
    }
}
