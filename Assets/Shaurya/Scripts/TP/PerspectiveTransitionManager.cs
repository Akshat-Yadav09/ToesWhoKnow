using System.Collections;
using UnityEngine;
using Unity.Cinemachine;
using Akshat;

namespace Shaurya
{
    using MovementMode = Akshat.MovementMode;

    /// <summary>
    /// Central manager for fade-to-black room and perspective transitions.
    /// Operates as a singleton service so doors do not require manual manager references.
    /// </summary>
    [DisallowMultipleComponent]
    public class PerspectiveTransitionManager : MonoBehaviour
    {
        public static PerspectiveTransitionManager Instance { get; private set; }

        // ── Inspector ─────────────────────────────────────────────────────────

        [Header("Required References (Auto-Located If Unassigned)")]
        [Tooltip("The ScreenFader script attached to the full-screen black Image panel.")]
        [SerializeField] private ScreenFader screenFader;

        [Tooltip("The PlayerMovement script on the Player GameObject.")]
        [SerializeField] private PlayerMovement playerMovement;

        [Header("Cinemachine Multi-Room Camera System")]
        [Tooltip("The initial CinemachineCamera active at scene start (e.g. Hallway 1).\n" +
                 "If assigned, it will automatically be activated on Start.")]
        [SerializeField] private CinemachineCamera startingCamera;

        [Tooltip("Optional list of cameras to disable at scene start. If empty, cameras are managed on-demand.")]
        [SerializeField] private CinemachineCamera[] allCameras;

        [Header("Legacy CameraController (Optional Fallback)")]
        [Tooltip("Leave empty if using Cinemachine.")]
        [SerializeField] private CameraController cameraController;
#pragma warning disable CS0414
        [SerializeField] private float hallwayCameraSize = 5f;
#pragma warning restore CS0414
        [SerializeField] private float topDownCameraSize = 8f;
        [SerializeField] private float cameraTransitionSpeed = 10f;

        [Header("Room Management (Culling)")]
        [Tooltip("The initial RoomZone active at scene start.")]
        [SerializeField] private Akshat.RoomSystem.RoomZone startingRoom;

        [Tooltip("Optional list of rooms to manage. Auto-located if empty.")]
        [SerializeField] private Akshat.RoomSystem.RoomZone[] allRooms;

        // ── State ─────────────────────────────────────────────────────────────

        public bool IsTransitioning { get; private set; }

        private CinemachineCamera currentCamera;
        private Akshat.RoomSystem.RoomZone currentRoom;
        private CinemachineBrain brain;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Awake()
        {
            // Singleton registration
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            // Auto-locate player if unassigned
            if (playerMovement == null)
            {
                playerMovement = FindAnyObjectByType<PlayerMovement>();
            }

            // Auto-locate screen fader if unassigned
            if (screenFader == null)
            {
                screenFader = FindAnyObjectByType<ScreenFader>();
            }

            // Cache the CinemachineBrain from the main camera
            var mainCam = Camera.main;
            if (mainCam != null)
                brain = mainCam.GetComponent<CinemachineBrain>();

            if (brain == null)
                Debug.LogWarning("[PerspectiveTransitionManager] No CinemachineBrain found on Main Camera. Instant cuts won't work.");
        }

        private void Start()
        {
            // Auto-locate rooms if not assigned
            if (allRooms == null || allRooms.Length == 0)
            {
                allRooms = FindObjectsByType<Akshat.RoomSystem.RoomZone>(FindObjectsInactive.Include);
            }

            // Disable all rooms except the starting room
            if (allRooms != null && allRooms.Length > 0)
            {
                foreach (var room in allRooms)
                {
                    if (room != null && room != startingRoom)
                    {
                        room.gameObject.SetActive(false);
                    }
                }
            }

            if (startingRoom != null)
            {
                startingRoom.gameObject.SetActive(true);
                currentRoom = startingRoom;
            }

            // Disable all cameras if explicitly configured, then enable only the starting one
            if (allCameras != null && allCameras.Length > 0)
            {
                foreach (var cam in allCameras)
                    if (cam != null) cam.gameObject.SetActive(false);
            }

            if (startingCamera != null)
            {
                startingCamera.gameObject.SetActive(true);
                currentCamera = startingCamera;
            }

            // Set the Brain's default blend to Cut so all transitions are instant
            if (brain != null)
                brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0f);
        }

        // ── Public API ────────────────────────────────────────────────────────

        public void BeginTransition(Transform destination, Akshat.MovementMode targetMode, CinemachineCamera targetCamera = null, Akshat.RoomSystem.RoomZone targetRoom = null)
        {
            if (destination == null)
            {
                Debug.LogError("[PerspectiveTransitionManager] Destination is null. Aborted.");
                return;
            }
            BeginTransition(destination.position, targetMode, targetCamera, targetRoom);
        }

        public void BeginTransition(Vector3 destinationPosition, Akshat.MovementMode targetMode, CinemachineCamera targetCamera = null, Akshat.RoomSystem.RoomZone targetRoom = null)
        {
            if (IsTransitioning)
            {
                Debug.LogWarning("[PerspectiveTransitionManager] Already transitioning. Ignored.");
                return;
            }

            StartCoroutine(TransitionCoroutine(destinationPosition, targetMode, targetCamera, targetRoom));
        }

        // ── Coroutine ─────────────────────────────────────────────────────────

        private IEnumerator TransitionCoroutine(Vector3 destinationPosition, Akshat.MovementMode targetMode, CinemachineCamera targetCamera, Akshat.RoomSystem.RoomZone targetRoom)
        {
            IsTransitioning = true;

            try
            {
                // ── 1. Disable player movement immediately to freeze input & cancel velocity ──
                if (playerMovement != null)
                    playerMovement.SetMovementEnabled(false);

                // ── 2. Fade to black ───────────────────────────────────────────────
                if (screenFader != null)
                    yield return StartCoroutine(screenFader.FadeOut());
                else
                    yield return new WaitForSecondsRealtime(0.35f);

                // ── 3. Room Culling & Teleport ──────────────────────────────────────
                // First: Turn ON the destination room so its floor and colliders exist
                if (targetRoom != null && targetRoom != currentRoom)
                {
                    targetRoom.gameObject.SetActive(true);
                }

                // Second: Teleport the player safely into the new room
                if (playerMovement != null)
                {
                    playerMovement.Teleport(destinationPosition);
                }

                // Third: Deactivate the old room now that the player is safely in the new room
                if (currentRoom != null && targetRoom != null && currentRoom != targetRoom)
                {
                    currentRoom.gameObject.SetActive(false);
                    currentRoom = targetRoom;
                }
                else if (targetRoom != null)
                {
                    currentRoom = targetRoom;
                }

                // ── 4. Switch movement mode (updates gravity scale & resets velocity) ──
                if (playerMovement != null)
                    playerMovement.SetMode(targetMode);

                // ── 5. Instant camera switch ───────────────────────────────────────
                if (targetCamera != null)
                    SwitchCameraInstant(targetCamera, destinationPosition);
                else
                    ApplyCameraForMode(targetMode, destinationPosition);

                // ── 6. Physics & Render Sync ───────────────────────────────────────
                Physics2D.SyncTransforms();
                yield return new WaitForFixedUpdate();
                yield return null;
                yield return new WaitForEndOfFrame();

                // Re-confirm player position & zero velocity after fixed update settling
                if (playerMovement != null)
                {
                    playerMovement.Teleport(destinationPosition);
                    playerMovement.SetMovementEnabled(true);
                }

                // ── 7. Fade back in ────────────────────────────────────────────────
                if (screenFader != null)
                    yield return StartCoroutine(screenFader.FadeIn());
            }
            finally
            {
                IsTransitioning = false;
                if (playerMovement != null)
                    playerMovement.SetMovementEnabled(true);
            }
        }

        // ── Camera Switching ──────────────────────────────────────────────────

        public void SwitchCameraInstant(CinemachineCamera newCamera, Vector3 snapPosition)
        {
            if (newCamera == null) return;

            if (currentCamera != null && currentCamera != newCamera)
                currentCamera.gameObject.SetActive(false);

            newCamera.gameObject.SetActive(true);

            Vector3 camPos = new Vector3(snapPosition.x, snapPosition.y, -10f);
            newCamera.ForceCameraPosition(camPos, Quaternion.identity);

            if (playerMovement != null)
            {
                CinemachineCore.OnTargetObjectWarped(
                    playerMovement.transform,
                    snapPosition - playerMovement.transform.position);
            }

            currentCamera = newCamera;
            Debug.Log($"[PerspectiveTransitionManager] <color=cyan>Cut</color> → <b>{newCamera.name}</b>");
        }

        // ── Legacy Fallback ───────────────────────────────────────────────────

        private void ApplyCameraForMode(Akshat.MovementMode mode, Vector3 playerPosition)
        {
            if (cameraController == null) return;
            if (mode == Akshat.MovementMode.TopDown)
                cameraController.EnterTriggerZone(playerPosition, topDownCameraSize, cameraTransitionSpeed);
            else
                cameraController.ExitTriggerZone(cameraTransitionSpeed);
        }
    }
}
