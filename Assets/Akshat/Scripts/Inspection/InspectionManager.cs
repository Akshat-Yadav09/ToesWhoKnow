using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;
using Akshat.Interaction;

namespace Akshat.Inspection
{
    /// <summary>
    /// Central manager controlling the inspection mode lifecycle, visual presentation,
    /// background dimming, rotation, zoom, and gameplay pause/resume.
    /// Completely decoupled from concrete object types (Portraits, Notes, Photos, etc.).
    /// </summary>
    [DisallowMultipleComponent]
    public class InspectionManager : MonoBehaviour
    {
        public static InspectionManager Instance { get; private set; }

        [Header("Hierarchy & Presentation References")]
        [Tooltip("The root Canvas or GameObject for the inspection view.")]
        [SerializeField] private GameObject inspectionRoot;

        [Tooltip("The background dimmer component controlling world dimming/blur.")]
        [SerializeField] private UIBackgroundDimmer backgroundDimmer;

        [Tooltip("The container RectTransform that moves, scales, and rotates during inspection.")]
        [SerializeField] private RectTransform presentationContainer;

        [Tooltip("The UI Image component where the inspected 2D sprite is displayed.")]
        [SerializeField] private Image presentationImage;

        [Tooltip("Optional mount Transform for instantiating custom 3D or composite visual prefabs.")]
        [SerializeField] private Transform customVisualMount;

        [Header("Narrative & Text UI (Optional)")]
        [Tooltip("Optional panel containing title and description text.")]
        [SerializeField] private GameObject narrativePanel;

        [Tooltip("Text component displaying the object's title.")]
        [SerializeField] private TMP_Text titleText;

        [Tooltip("Text component displaying the object's transcription or narrative details.")]
        [SerializeField] private TMP_Text descriptionText;

        [Tooltip("Text component showing control hints (e.g. '[LMB Drag] Rotate  [Scroll] Zoom  [Esc] Put Down').")]
        [SerializeField] private TMP_Text controlsHintText;

        [Header("Transition Settings")]
        [Tooltip("Duration in seconds for the enter expand/fade transition.")]
        [SerializeField] private float enterDuration = 0.35f;

        [Tooltip("Duration in seconds for the exit restore/fade transition.")]
        [SerializeField] private float exitDuration = 0.25f;

        [Tooltip("Easing curve used for movement, scaling, and dimming transitions.")]
        [SerializeField] private AnimationCurve transitionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Gameplay Locking References")]
        [Tooltip("Reference to the player's movement controller to lock during inspection.")]
        [SerializeField] private PlayerMovement playerMovement;

        [Tooltip("Reference to the player's interaction detector to pause during inspection.")]
        [SerializeField] private PlayerInteraction playerInteraction;

        [Tooltip("Optional additional behaviours to disable during inspection (e.g. flashlight, enemies, timers).")]
        [SerializeField] private Behaviour[] additionalBehavioursToDisable;

        [Header("Camera & Input References")]
        [Tooltip("Camera used for world-to-screen coordinate conversion. Auto-found if unassigned.")]
        [SerializeField] private Camera sceneCamera;

        [Tooltip("Optional InputActionReference for exiting inspection (defaults to Escape / E / Gamepad East).")]
        [SerializeField] private InputActionReference exitActionReference;

        // Public inspection events
        public event Action<IInspectable> OnInspectionStarted;
        public event Action<IInspectable> OnInspectionEnded;

        // Runtime state
        private bool isInspecting;
        private IInspectable currentInspectable;
        private Coroutine activeTransition;
        private Vector2 originScreenLocalPos;
        private Vector3 currentRotationEuler;
        private float currentZoom = 1.0f;
        private GameObject spawnedCustomVisual;
        private Canvas rootCanvas;

        public bool IsInspecting => isInspecting;
        public IInspectable CurrentInspectable => currentInspectable;

        private void Awake()
        {
            // Singleton management with scene persistence safety
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            if (sceneCamera == null)
            {
                sceneCamera = Camera.main;
            }

            if (inspectionRoot != null)
            {
                rootCanvas = inspectionRoot.GetComponentInParent<Canvas>();
            }

            // Auto-locate player components if unassigned
            if (playerMovement == null)
            {
                playerMovement = FindAnyObjectByType<PlayerMovement>();
            }

            if (playerInteraction == null)
            {
                playerInteraction = FindAnyObjectByType<PlayerInteraction>();
            }

            // Ensure inspection view starts hidden
            if (inspectionRoot != null)
            {
                inspectionRoot.SetActive(false);
            }

            if (backgroundDimmer != null)
            {
                backgroundDimmer.SetDimAmount(0f);
            }
        }

        private void OnEnable()
        {
            if (exitActionReference != null && exitActionReference.action != null)
            {
                exitActionReference.action.performed += OnExitActionPerformed;
                exitActionReference.action.Enable();
            }
        }

        private void OnDisable()
        {
            if (exitActionReference != null && exitActionReference.action != null)
            {
                exitActionReference.action.performed -= OnExitActionPerformed;
                exitActionReference.action.Disable();
            }
        }

        private void Update()
        {
            if (!isInspecting || activeTransition != null) return;

            HandleExitInput();
            HandleRotationInput();
            HandleZoomInput();
        }

        /// <summary>
        /// Begins inspection mode for the specified IInspectable.
        /// </summary>
        public void StartInspection(IInspectable inspectable)
        {
            if (isInspecting || inspectable == null) return;

            isInspecting = true;
            currentInspectable = inspectable;

            // 1. Lock gameplay controls
            LockGameplay(true);

            // 2. Prepare visual and narrative content
            SetupInspectionContent(inspectable);

            // 3. Activate inspection hierarchy
            if (inspectionRoot != null)
            {
                inspectionRoot.SetActive(true);
            }

            // 4. Calculate starting screen position from world object
            CalculateOriginPosition(inspectable.SourceWorldPosition);

            // 5. Begin smooth enter transition
            if (activeTransition != null) StopCoroutine(activeTransition);
            activeTransition = StartCoroutine(AnimateEnterTransition(inspectable.Config));

            OnInspectionStarted?.Invoke(inspectable);
        }

        /// <summary>
        /// Exits inspection mode and restores gameplay state.
        /// </summary>
        public void ExitInspection()
        {
            if (!isInspecting || activeTransition != null) return;

            if (activeTransition != null) StopCoroutine(activeTransition);
            activeTransition = StartCoroutine(AnimateExitTransition());
        }

        private void SetupInspectionContent(IInspectable inspectable)
        {
            var config = inspectable.Config ?? new InspectionConfig();

            // Setup 2D Sprite presentation
            if (presentationImage != null)
            {
                if (inspectable.InspectionSprite != null)
                {
                    presentationImage.gameObject.SetActive(true);
                    presentationImage.sprite = inspectable.InspectionSprite;
                    presentationImage.color = inspectable.VisualColor;

                    // Set presentation sizing
                    if (config.PreferredSize.x > 1f && config.PreferredSize.y > 1f)
                    {
                        presentationImage.rectTransform.sizeDelta = config.PreferredSize;
                    }
                    else if (inspectable.InspectionSprite != null)
                    {
                        // Auto-calculate size from sprite aspect ratio
                        float aspect = inspectable.InspectionSprite.rect.width / Mathf.Max(1f, inspectable.InspectionSprite.rect.height);
                        presentationImage.rectTransform.sizeDelta = new Vector2(400f * aspect, 400f);
                    }

                    presentationImage.preserveAspect = config.PreserveAspect;
                }
                else
                {
                    presentationImage.gameObject.SetActive(false);
                }
            }

            // Setup custom visual prefab if provided
            if (spawnedCustomVisual != null)
            {
                Destroy(spawnedCustomVisual);
                spawnedCustomVisual = null;
            }

            if (inspectable.CustomVisualPrefab != null && customVisualMount != null)
            {
                spawnedCustomVisual = Instantiate(inspectable.CustomVisualPrefab, customVisualMount);
                spawnedCustomVisual.transform.localPosition = Vector3.zero;
                spawnedCustomVisual.transform.localRotation = Quaternion.identity;
            }

            // Setup Narrative / Clue Text
            bool hasTitle = !string.IsNullOrEmpty(inspectable.InspectionTitle);
            bool hasDesc = !string.IsNullOrEmpty(inspectable.InspectionDescription);

            if (titleText != null)
            {
                titleText.gameObject.SetActive(hasTitle);
                titleText.text = inspectable.InspectionTitle ?? string.Empty;
            }

            if (descriptionText != null)
            {
                descriptionText.gameObject.SetActive(hasDesc);
                descriptionText.text = inspectable.InspectionDescription ?? string.Empty;
            }

            if (narrativePanel != null)
            {
                narrativePanel.SetActive(hasTitle || hasDesc);
            }

            // Initialize control hints
            if (controlsHintText != null)
            {
                string hint = "[Esc / E] Put Down";
                if (config.RotationMode != InspectionRotationMode.None) hint = "[LMB Drag] Rotate   " + hint;
                if (config.AllowZoom) hint = "[Scroll] Zoom   " + hint;
                controlsHintText.text = hint;
            }

            // Reset transform state
            currentRotationEuler = config.InitialRotation;
            currentZoom = config.DefaultZoom;
        }

        private void CalculateOriginPosition(Vector3 worldPos)
        {
            originScreenLocalPos = Vector2.zero;

            Camera cam = sceneCamera != null ? sceneCamera : Camera.main;
            if (cam == null || presentationContainer == null) return;

            Vector3 screenPoint = cam.WorldToScreenPoint(worldPos);

            RectTransform canvasRect = rootCanvas != null ? rootCanvas.transform as RectTransform : presentationContainer.parent as RectTransform;
            if (canvasRect != null)
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPoint, null, out originScreenLocalPos);
            }
        }

        private IEnumerator AnimateEnterTransition(InspectionConfig config)
        {
            Vector2 startPos = originScreenLocalPos;
            Vector2 targetPos = Vector2.zero;

            Vector3 startScale = Vector3.one * 0.15f;
            Vector3 targetScale = Vector3.one * (config != null ? config.DefaultZoom : 1.0f);

            Quaternion startRot = Quaternion.identity;
            Quaternion targetRot = Quaternion.Euler(config != null ? config.InitialRotation : Vector3.zero);

            float elapsed = 0f;
            float duration = Mathf.Max(0.01f, enterDuration);

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float curveT = transitionCurve.Evaluate(t);

                if (presentationContainer != null)
                {
                    presentationContainer.anchoredPosition = Vector2.Lerp(startPos, targetPos, curveT);
                    presentationContainer.localScale = Vector3.Lerp(startScale, targetScale, curveT);
                    presentationContainer.localRotation = Quaternion.Slerp(startRot, targetRot, curveT);
                }

                if (backgroundDimmer != null)
                {
                    backgroundDimmer.SetDimAmount(curveT);
                }

                yield return null;
            }

            if (presentationContainer != null)
            {
                presentationContainer.anchoredPosition = targetPos;
                presentationContainer.localScale = targetScale;
                presentationContainer.localRotation = targetRot;
            }

            if (backgroundDimmer != null)
            {
                backgroundDimmer.SetDimAmount(1f);
            }

            activeTransition = null;
        }

        private IEnumerator AnimateExitTransition()
        {
            Vector2 startPos = presentationContainer != null ? presentationContainer.anchoredPosition : Vector2.zero;
            Vector2 targetPos = originScreenLocalPos;

            Vector3 startScale = presentationContainer != null ? presentationContainer.localScale : Vector3.one;
            Vector3 targetScale = Vector3.one * 0.15f;

            float elapsed = 0f;
            float duration = Mathf.Max(0.01f, exitDuration);

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float curveT = transitionCurve.Evaluate(t);

                if (presentationContainer != null)
                {
                    presentationContainer.anchoredPosition = Vector2.Lerp(startPos, targetPos, curveT);
                    presentationContainer.localScale = Vector3.Lerp(startScale, targetScale, curveT);
                }

                if (backgroundDimmer != null)
                {
                    backgroundDimmer.SetDimAmount(1f - curveT);
                }

                yield return null;
            }

            // Cleanup & complete exit
            if (backgroundDimmer != null)
            {
                backgroundDimmer.SetDimAmount(0f);
            }

            if (spawnedCustomVisual != null)
            {
                Destroy(spawnedCustomVisual);
                spawnedCustomVisual = null;
            }

            if (inspectionRoot != null)
            {
                inspectionRoot.SetActive(false);
            }

            var finishedInspectable = currentInspectable;
            currentInspectable = null;
            isInspecting = false;
            activeTransition = null;

            // Restore gameplay
            LockGameplay(false);

            OnInspectionEnded?.Invoke(finishedInspectable);
        }

        private void HandleRotationInput()
        {
            if (currentInspectable?.Config == null) return;
            var config = currentInspectable.Config;

            if (config.RotationMode == InspectionRotationMode.None || presentationContainer == null) return;

            // Mouse Drag Detection
            if (Mouse.current != null && Mouse.current.leftButton.isPressed)
            {
                Vector2 delta = Mouse.current.delta.ReadValue();

                if (delta.sqrMagnitude > 0.001f)
                {
                    float speed = config.RotationSpeed;

                    switch (config.RotationMode)
                    {
                        case InspectionRotationMode.Roll2D:
                            currentRotationEuler.z -= delta.x * speed * 0.5f;
                            if (config.ClampRotation)
                            {
                                currentRotationEuler.z = Mathf.Clamp(currentRotationEuler.z, config.RotationLimits.x, config.RotationLimits.y);
                            }
                            break;

                        case InspectionRotationMode.Tilt3D:
                        case InspectionRotationMode.Full3D:
                            currentRotationEuler.y -= delta.x * speed * 0.5f;
                            currentRotationEuler.x += delta.y * speed * 0.5f;

                            if (config.ClampRotation)
                            {
                                currentRotationEuler.x = Mathf.Clamp(currentRotationEuler.x, config.RotationLimits.x, config.RotationLimits.y);
                                currentRotationEuler.y = Mathf.Clamp(currentRotationEuler.y, config.RotationLimits.x, config.RotationLimits.y);
                            }
                            break;
                    }

                    presentationContainer.localRotation = Quaternion.Euler(currentRotationEuler);
                }
            }
        }

        private void HandleZoomInput()
        {
            if (currentInspectable?.Config == null || presentationContainer == null) return;
            var config = currentInspectable.Config;

            if (!config.AllowZoom || Mouse.current == null) return;

            float scrollY = Mouse.current.scroll.ReadValue().y;
            if (Mathf.Abs(scrollY) > 0.01f)
            {
                float zoomDelta = Mathf.Sign(scrollY) * config.ZoomSpeed;
                currentZoom = Mathf.Clamp(currentZoom + zoomDelta, config.MinZoom, config.MaxZoom);
                presentationContainer.localScale = Vector3.one * currentZoom;
            }
        }

        private void HandleExitInput()
        {
            // Fallback keyboard polling (Escape or E)
            if (Keyboard.current != null)
            {
                if (Keyboard.current.escapeKey.wasPressedThisFrame || Keyboard.current.eKey.wasPressedThisFrame)
                {
                    ExitInspection();
                    return;
                }
            }

            // Gamepad polling (East / B button or South / A)
            if (Gamepad.current != null)
            {
                if (Gamepad.current.buttonEast.wasPressedThisFrame)
                {
                    ExitInspection();
                }
            }
        }

        private void OnExitActionPerformed(InputAction.CallbackContext context)
        {
            if (isInspecting && activeTransition == null)
            {
                ExitInspection();
            }
        }

        private void LockGameplay(bool locked)
        {
            // 1. Lock Movement
            if (playerMovement != null)
            {
                playerMovement.enabled = !locked;

                // Stop existing velocity when locking
                if (locked && playerMovement.TryGetComponent<Rigidbody2D>(out var rb))
                {
#if UNITY_6000_0_OR_NEWER
                    rb.linearVelocity = Vector2.zero;
#else
                    rb.velocity = Vector2.zero;
#endif
                }
            }

            // 2. Lock Interaction Detection & hide interaction prompts
            if (playerInteraction != null)
            {
                playerInteraction.enabled = !locked;
            }

            // 3. Lock any custom additional behaviours
            if (additionalBehavioursToDisable != null)
            {
                for (int i = 0; i < additionalBehavioursToDisable.Length; i++)
                {
                    if (additionalBehavioursToDisable[i] != null)
                    {
                        additionalBehavioursToDisable[i].enabled = !locked;
                    }
                }
            }
        }
    }
}
