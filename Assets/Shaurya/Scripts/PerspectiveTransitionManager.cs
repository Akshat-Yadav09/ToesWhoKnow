// Assets/Shaurya/Scripts/PerspectiveTransitionManager.cs
// Central coordinator for fade-to-black perspective transitions.
// Place on a dedicated empty GameObject called "TransitionManager".
//
// Transition sequence:
//   1. Fade to black
//   2. Disable player movement
//   3. Teleport player
//   4. Switch camera (INSTANT CUT — Brain.DefaultBlend forced to Cut for one frame)
//   5. Switch movement mode
//   6. Re-enable movement
//   7. Fade back in

using System.Collections;
using UnityEngine;
using Unity.Cinemachine;

namespace Shaurya
{
    public class PerspectiveTransitionManager : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────

        [Header("Required References")]
        [Tooltip("The ScreenFader script attached to the full-screen black Image panel.")]
        [SerializeField] private ScreenFader screenFader;

        [Tooltip("The PlayerMovementMode script on the Player GameObject.")]
        [SerializeField] private PlayerMovementMode playerMovementMode;

        [Header("Cinemachine Multi-Room Camera System")]
        [Tooltip("The initial CinemachineCamera active at scene start (e.g. Hallway 1).\n" +
                 "If assigned, it will automatically be activated on Start.")]
        [SerializeField] private CinemachineCamera startingCamera;

        [Tooltip("All CinemachineCamera objects used in this scene.\n" +
                 "Drag every hallway and room camera here. They will all start disabled except startingCamera.")]
        [SerializeField] private CinemachineCamera[] allCameras;

        [Header("Legacy CameraController (Optional Fallback)")]
        [Tooltip("Leave empty if using Cinemachine.")]
        [SerializeField] private CameraController cameraController;
        [SerializeField] private float hallwayCameraSize = 5f;
        [SerializeField] private float topDownCameraSize = 8f;
        [SerializeField] private float cameraTransitionSpeed = 10f;

        // ── State ─────────────────────────────────────────────────────────────

        public bool IsTransitioning { get; private set; }

        private CinemachineCamera currentCamera;
        private CinemachineBrain brain;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Awake()
        {
            // Cache the CinemachineBrain from the main camera.
            var mainCam = Camera.main;
            if (mainCam != null)
                brain = mainCam.GetComponent<CinemachineBrain>();

            if (brain == null)
                Debug.LogWarning("[PerspectiveTransitionManager] No CinemachineBrain found on Main Camera. Instant cuts won't work.");
        }

        private void Start()
        {
            // Disable all cameras, then enable only the starting one.
            if (allCameras != null)
                foreach (var cam in allCameras)
                    if (cam != null) cam.gameObject.SetActive(false);

            if (startingCamera != null)
            {
                startingCamera.gameObject.SetActive(true);
                currentCamera = startingCamera;
            }

            // Set the Brain's default blend to Cut so all transitions are instant.
            // Individual smooth motion is handled by Position Composer damping, not camera blends.
            if (brain != null)
                brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0f);
        }

        // ── Public API ────────────────────────────────────────────────────────

        public void BeginTransition(Transform destination, MovementMode targetMode, CinemachineCamera targetCamera = null)
        {
            if (IsTransitioning)
            {
                Debug.LogWarning("[PerspectiveTransitionManager] Already transitioning. Ignored.");
                return;
            }
            if (destination == null)
            {
                Debug.LogError("[PerspectiveTransitionManager] Destination is null. Aborted.");
                return;
            }
            StartCoroutine(TransitionCoroutine(destination.position, targetMode, targetCamera));
        }

        // ── Coroutine ─────────────────────────────────────────────────────────

        private IEnumerator TransitionCoroutine(Vector3 destinationPosition, MovementMode targetMode, CinemachineCamera targetCamera)
        {
            IsTransitioning = true;

            // ── 1. Fade to black ───────────────────────────────────────────────
            if (screenFader != null)
                yield return StartCoroutine(screenFader.FadeOut());
            else
                yield return new WaitForSecondsRealtime(0.35f);

            // ── 2. Disable player movement ─────────────────────────────────────
            if (playerMovementMode != null)
                playerMovementMode.SetMovementEnabled(false);

            // ── 3. Teleport player ─────────────────────────────────────────────
            if (playerMovementMode != null)
                playerMovementMode.transform.position = destinationPosition;

            // ── 4. Instant camera switch ───────────────────────────────────────
            // Screen is fully black here. Switch camera and snap instantly.
            if (targetCamera != null)
                SwitchCameraInstant(targetCamera, destinationPosition);
            else
                ApplyCameraForMode(targetMode, destinationPosition);

            // ── 5. Switch movement mode ────────────────────────────────────────
            if (playerMovementMode != null)
                playerMovementMode.SetMode(targetMode);

            // ── 6. Wait for Cinemachine to process the cut in LateUpdate ────────
            // (Brain runs in LateUpdate so we need at least one full frame after
            //  the camera switch before the scene is revealed.)
            yield return null;
            yield return new WaitForEndOfFrame();

            // ── 7. Re-enable movement ──────────────────────────────────────────
            if (playerMovementMode != null)
                playerMovementMode.SetMovementEnabled(true);

            // ── 8. Fade back in ────────────────────────────────────────────────
            if (screenFader != null)
                yield return StartCoroutine(screenFader.FadeIn());

            IsTransitioning = false;
        }

        // ── Camera Switching ──────────────────────────────────────────────────

        public void SwitchCameraInstant(CinemachineCamera newCamera, Vector3 snapPosition)
        {
            if (newCamera == null) return;

            // Disable the old camera. With only one camera active, Cinemachine cannot blend.
            if (currentCamera != null && currentCamera != newCamera)
                currentCamera.gameObject.SetActive(false);

            // Enable new camera.
            newCamera.gameObject.SetActive(true);

            // ForceCameraPosition resets internal damping history so damping
            // starts fresh from the player's NEW position, not the old one.
            // The Z=-10 keeps the 2D camera at the correct depth.
            Vector3 camPos = new Vector3(snapPosition.x, snapPosition.y, -10f);
            newCamera.ForceCameraPosition(camPos, Quaternion.identity);

            // Notify all Cinemachine cameras that the tracked object warped.
            if (playerMovementMode != null)
            {
                CinemachineCore.OnTargetObjectWarped(
                    playerMovementMode.transform,
                    snapPosition - playerMovementMode.transform.position);
            }

            currentCamera = newCamera;
            Debug.Log($"[PerspectiveTransitionManager] <color=cyan>Cut</color> → <b>{newCamera.name}</b>");
        }

        // ── Legacy Fallback ───────────────────────────────────────────────────

        private void ApplyCameraForMode(MovementMode mode, Vector3 playerPosition)
        {
            if (cameraController == null) return;
            if (mode == MovementMode.TopDown)
                cameraController.EnterTriggerZone(playerPosition, topDownCameraSize, cameraTransitionSpeed);
            else
                cameraController.ExitTriggerZone(cameraTransitionSpeed);
        }
    }
}
