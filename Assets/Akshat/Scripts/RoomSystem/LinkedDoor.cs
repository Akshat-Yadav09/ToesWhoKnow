using System.Collections;
using UnityEngine;
using Unity.Cinemachine;
using Akshat;
using Akshat.Interaction;

namespace Akshat.RoomSystem
{
    /// <summary>
    /// Modular, two-way linked door that connects two rooms seamlessly.
    /// Implements IInteractable so it responds directly to PlayerInteraction.
    /// Eliminates manual camera, mode, and destination wiring.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class LinkedDoor : MonoBehaviour, IInteractable
    {
        [Header("Door Pairing")]
        [Tooltip("Drag the counterpart LinkedDoor in the destination room here.")]
        [SerializeField] private LinkedDoor targetDoor;

        [Header("Room Association")]
        [Tooltip("The RoomZone this door belongs to. Auto-detected in parent if unassigned.")]
        [SerializeField] private RoomZone parentRoom;

        [Header("Player Spawn / Exit Point")]
        [Tooltip("Optional empty GameObject where the player appears when EXITING this door.\n" +
                 "If empty, automatically uses this door's position + spawnOffset.")]
        [SerializeField] private Transform exitSpawnPoint;

        [Tooltip("Offset applied from door center when spawning, to place the player safely outside the trigger.")]
        [SerializeField] private Vector2 defaultSpawnOffset = new Vector2(0f, -0.6f);

        [Header("Interaction & Audio")]
        [Tooltip("Prompt displayed on screen when near this door.")]
        [SerializeField] private string promptText = "Use Door";

        [Tooltip("Optional sound effect when opening this door.")]
        [SerializeField] private AudioClip doorSound;

        [Range(0f, 1f)]
        [SerializeField] private float soundVolume = 1f;

        // Internal State
        private AudioSource audioSource;

        public string InteractionPrompt => promptText;

        public bool CanInteract()
        {
            var mgr = Shaurya.PerspectiveTransitionManager.Instance;
            return mgr == null || !mgr.IsTransitioning;
        }

        public void Interact()
        {
            if (!CanInteract()) return;

            if (targetDoor == null)
            {
                Debug.LogWarning($"[LinkedDoor] <b>{gameObject.name}</b> has no Target Door assigned!", this);
                return;
            }

            // Play local door sound if available
            PlayDoorSound();

            // Extract target data directly from the counterpart door
            Vector3 destinationPos = GetDestinationPosition();
            MovementMode targetMode = targetDoor.GetRoomMovementMode();
            CinemachineCamera targetCam = targetDoor.GetRoomCamera();

            var mgr = Shaurya.PerspectiveTransitionManager.Instance;
            if (mgr != null)
            {
                RoomZone targetRoom = targetDoor.GetParentRoom();
                Debug.Log($"[LinkedDoor] <b>{gameObject.name}</b> → <b>{targetDoor.gameObject.name}</b> | Dest: {destinationPos} | Room: {(targetRoom != null ? targetRoom.name : "null")} | Mode: {targetMode}");
                mgr.BeginTransition(destinationPos, targetMode, targetCam, targetRoom);
            }
            else
            {
                Debug.LogError("[LinkedDoor] PerspectiveTransitionManager not found in scene!", this);
            }
        }

        public Vector3 GetDestinationPosition()
        {
            if (targetDoor == null)
            {
                return transform.position;
            }

            RoomZone targetRoom = targetDoor.GetParentRoom();
            RoomZone currentRoom = GetParentRoom();

            // Candidate 1: Check targetDoor's exitSpawnPoint
            if (targetDoor.exitSpawnPoint != null)
            {
                // Ensure it does not belong to the CURRENT room (e.g. user accidentally assigned current room spawn point to counterpart door)
                bool belongsToCurrentRoom = false;
                if (currentRoom != null && targetDoor.exitSpawnPoint.IsChildOf(currentRoom.transform))
                {
                    belongsToCurrentRoom = true;
                }
                else
                {
                    float distToTargetDoor = Vector2.Distance(targetDoor.exitSpawnPoint.position, targetDoor.transform.position);
                    float distToThisDoor = Vector2.Distance(targetDoor.exitSpawnPoint.position, transform.position);
                    if (distToThisDoor < distToTargetDoor && distToTargetDoor > 10f)
                    {
                        belongsToCurrentRoom = true;
                    }
                }

                if (!belongsToCurrentRoom)
                {
                    return targetDoor.exitSpawnPoint.position;
                }
                else
                {
                    Debug.LogWarning($"[LinkedDoor] '{targetDoor.name}.exitSpawnPoint' is located in source room '{currentRoom?.name}', not destination '{targetRoom?.name}'. Re-routing to target door position.", this);
                }
            }

            // Candidate 2: Check THIS door's exitSpawnPoint (if user assigned destination spawn point to the entering door)
            if (exitSpawnPoint != null)
            {
                bool belongsToTargetRoom = false;
                if (targetRoom != null && exitSpawnPoint.IsChildOf(targetRoom.transform))
                {
                    belongsToTargetRoom = true;
                }
                else
                {
                    float distToTargetDoor = Vector2.Distance(exitSpawnPoint.position, targetDoor.transform.position);
                    float distToThisDoor = Vector2.Distance(exitSpawnPoint.position, transform.position);
                    if (distToTargetDoor < distToThisDoor)
                    {
                        belongsToTargetRoom = true;
                    }
                }

                if (belongsToTargetRoom)
                {
                    return exitSpawnPoint.position;
                }
            }

            // Candidate 3: Target door position + safe offset
            Vector3 offset = (Vector3)targetDoor.defaultSpawnOffset;
            if (targetDoor.GetRoomMovementMode() == MovementMode.Hallway && offset.y < 0f)
            {
                offset.y = 0.1f; // Prevent sinking beneath floor in side-scroller hallway
            }
            return targetDoor.transform.position + offset;
        }

        public Vector3 GetSpawnPosition()
        {
            if (exitSpawnPoint != null)
            {
                return exitSpawnPoint.position;
            }
            Vector3 offset = (Vector3)defaultSpawnOffset;
            if (GetRoomMovementMode() == MovementMode.Hallway && offset.y < 0f)
            {
                offset.y = 0.1f;
            }
            return transform.position + offset;
        }

        public MovementMode GetRoomMovementMode()
        {
            if (parentRoom != null) return parentRoom.MovementMode;
            var room = GetComponentInParent<RoomZone>(true);
            return room != null ? room.MovementMode : MovementMode.TopDown;
        }

        public CinemachineCamera GetRoomCamera()
        {
            if (parentRoom != null) return parentRoom.RoomCamera;
            var room = GetComponentInParent<RoomZone>(true);
            return room != null ? room.RoomCamera : null;
        }

        public RoomZone GetParentRoom()
        {
            if (parentRoom != null) return parentRoom;
            return GetComponentInParent<RoomZone>(true);
        }

        private void PlayDoorSound()
        {
            if (doorSound == null) return;
            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
                if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
            }
            audioSource.PlayOneShot(doorSound, soundVolume);
        }

        private void Awake()
        {
            Collider2D col = GetComponent<Collider2D>();
            if (col != null) col.isTrigger = true;

            if (parentRoom == null)
            {
                parentRoom = GetComponentInParent<RoomZone>(true);
            }

            audioSource = GetComponent<AudioSource>();
        }

        private void Reset()
        {
            Collider2D col = GetComponent<Collider2D>();
            if (col != null) col.isTrigger = true;

            if (parentRoom == null)
            {
                parentRoom = GetComponentInParent<RoomZone>(true);
            }
        }

        /// <summary>
        /// Context menu utility: Right click the component in Inspector -> 'Link Bidirectionally with Target Door'.
        /// Sets targetDoor.targetDoor = this automatically!
        /// </summary>
        [ContextMenu("Link Bidirectionally With Target Door")]
        public void LinkBidirectionally()
        {
            if (targetDoor != null)
            {
                targetDoor.targetDoor = this;
#if UNITY_EDITOR
                UnityEditor.EditorUtility.SetDirty(targetDoor);
                UnityEditor.EditorUtility.SetDirty(this);
#endif
                Debug.Log($"[LinkedDoor] Successfully linked '{gameObject.name}' <---> '{targetDoor.gameObject.name}'!");
            }
            else
            {
                Debug.LogWarning("[LinkedDoor] Assign a Target Door first before linking bidirectionally.", this);
            }
        }

        private void OnDrawGizmos()
        {
            // Draw trigger box
            Collider2D col = GetComponent<Collider2D>();
            if (col != null)
            {
                Gizmos.color = targetDoor != null ? new Color(0f, 1f, 0.4f, 0.3f) : new Color(1f, 0.2f, 0.2f, 0.3f);
                Gizmos.DrawCube(col.bounds.center, col.bounds.size);
                Gizmos.color = targetDoor != null ? Color.green : Color.red;
                Gizmos.DrawWireCube(col.bounds.center, col.bounds.size);
            }

            // Draw spawn position
            Vector3 spawn = GetSpawnPosition();
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(spawn, 0.25f);
            Gizmos.DrawLine(transform.position, spawn);

            // Draw link to target door
            if (targetDoor != null)
            {
                Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.6f);
                Gizmos.DrawLine(transform.position, targetDoor.transform.position);
            }
        }
    }
}
