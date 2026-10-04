using System;
using UnityEngine;

namespace Akshat.Inspection
{
    /// <summary>
    /// Supported rotation modes for inspected objects.
    /// </summary>
    public enum InspectionRotationMode
    {
        [Tooltip("Object cannot be rotated.")]
        None,

        [Tooltip("2D rotation around Z axis (e.g. rotating a photo, sheet of paper, or flat clue).")]
        Roll2D,

        [Tooltip("3D tilt around X and Y axes (pitch/yaw to inspect angles, lighting sheen, or bevels).")]
        Tilt3D,

        [Tooltip("Full 3D rotation around all three axes.")]
        Full3D
    }

    /// <summary>
    /// Configuration data container for inspection behavior and limits.
    /// Fully configurable per inspectable in the Unity Inspector.
    /// </summary>
    [Serializable]
    public class InspectionConfig
    {
        [Header("Rotation Behavior")]
        [Tooltip("Rotation behavior allowed during inspection.")]
        [SerializeField] private InspectionRotationMode rotationMode = InspectionRotationMode.Roll2D;

        [Tooltip("Sensitivity of rotation when dragging with mouse or keys.")]
        [SerializeField] private float rotationSpeed = 2.5f;

        [Tooltip("If true, rotation is clamped between limits instead of rotating infinitely.")]
        [SerializeField] private bool clampRotation = false;

        [Tooltip("Rotation angle limits (Min / Max degrees) when clampRotation is enabled.")]
        [SerializeField] private Vector2 rotationLimits = new Vector2(-45f, 45f);

        [Tooltip("Initial Euler angles applied to the visual when inspection starts.")]
        [SerializeField] private Vector3 initialRotation = Vector3.zero;

        [Header("Zoom Behavior")]
        [Tooltip("Whether the player can zoom in and out on this object.")]
        [SerializeField] private bool allowZoom = true;

        [Tooltip("Sensitivity of zoom when using the mouse scroll wheel.")]
        [SerializeField] private float zoomSpeed = 0.15f;

        [Tooltip("Minimum allowable zoom scale factor.")]
        [SerializeField] private float minZoom = 0.75f;

        [Tooltip("Maximum allowable zoom scale factor.")]
        [SerializeField] private float maxZoom = 2.0f;

        [Tooltip("Starting zoom scale factor when presentation begins.")]
        [SerializeField] private float defaultZoom = 1.0f;

        [Header("Presentation Sizing")]
        [Tooltip("Preferred UI display dimensions (Width x Height). If set to 0x0, auto-scales from source aspect ratio.")]
        [SerializeField] private Vector2 preferredSize = new Vector2(450f, 450f);

        [Tooltip("Whether to preserve aspect ratio when presenting the image.")]
        [SerializeField] private bool preserveAspect = true;

        // Public getters
        public InspectionRotationMode RotationMode => rotationMode;
        public float RotationSpeed => rotationSpeed;
        public bool ClampRotation => clampRotation;
        public Vector2 RotationLimits => rotationLimits;
        public Vector3 InitialRotation => initialRotation;

        public bool AllowZoom => allowZoom;
        public float ZoomSpeed => zoomSpeed;
        public float MinZoom => minZoom;
        public float MaxZoom => maxZoom;
        public float DefaultZoom => defaultZoom;

        public Vector2 PreferredSize => preferredSize;
        public bool PreserveAspect => preserveAspect;
    }

    /// <summary>
    /// Generic abstraction for any object that can be inspected in inspection mode.
    /// InspectionManager consumes this interface without knowing concrete object types.
    /// </summary>
    public interface IInspectable
    {
        /// <summary>
        /// Sprite representation to inspect (photos, documents, portraits, notes, etc.).
        /// </summary>
        Sprite InspectionSprite { get; }

        /// <summary>
        /// Tint color applied to the visual presentation.
        /// </summary>
        Color VisualColor { get; }

        /// <summary>
        /// Optional custom visual prefab for 3D or composite objects.
        /// </summary>
        GameObject CustomVisualPrefab { get; }

        /// <summary>
        /// World position of the object before inspection, used for smooth spatial expand transitions.
        /// </summary>
        Vector3 SourceWorldPosition { get; }

        /// <summary>
        /// Title displayed in the inspection UI (e.g. "Family Photograph", "Burnt Letter").
        /// </summary>
        string InspectionTitle { get; }

        /// <summary>
        /// Detailed narrative description, transcription, or clue details.
        /// </summary>
        string InspectionDescription { get; }

        /// <summary>
        /// Configuration describing rotation, zoom, and sizing behavior for this object.
        /// </summary>
        InspectionConfig Config { get; }
    }

    /// <summary>
    /// Abstraction for background dimming or blur treatment during inspection.
    /// </summary>
    public interface IBackgroundDimmer
    {
        void SetDimAmount(float normalizedAmount);
    }
}
