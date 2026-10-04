using UnityEngine;
using Akshat.Interaction;

namespace Akshat.Inspection
{
    /// <summary>
    /// Generic, reusable component that makes any GameObject inspectable.
    /// Bridges the existing interaction system (IInteractable) with the inspection system (IInspectable).
    /// Works with photos, portraits, documents, notes, keys, or any future inspectable target.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    [DisallowMultipleComponent]
    public class InspectableObject : MonoBehaviour, IInteractable, IInspectable
    {
        [Header("Interaction Settings")]
        [Tooltip("Prompt displayed on screen when player approaches (e.g. 'Inspect Photo', 'Read Note').")]
        [SerializeField] private string interactionPrompt = "Inspect";

        [Tooltip("Whether this object is currently available for interaction and inspection.")]
        [SerializeField] private bool isInspectable = true;

        [Header("Inspection Visual Source")]
        [Tooltip("Sprite to present during inspection. If unassigned, automatically taken from attached SpriteRenderer.")]
        [SerializeField] private Sprite inspectionSprite;

        [Tooltip("Tint color applied to the visual presentation.")]
        [SerializeField] private Color visualColor = Color.white;

        [Tooltip("Optional custom 3D or composite prefab for complex inspection targets.")]
        [SerializeField] private GameObject customVisualPrefab;

        [Header("Narrative Information (Optional)")]
        [Tooltip("Title shown in the inspection UI (e.g. 'Old Family Photograph').")]
        [SerializeField] private string objectTitle;

        [Tooltip("Readable text transcription, diary entry, or clues.")]
        [TextArea(3, 8)]
        [SerializeField] private string objectDescription;

        [Header("Inspection Behavior Configuration")]
        [Tooltip("Per-object rotation, zoom, and sizing settings.")]
        [SerializeField] private InspectionConfig inspectionConfig = new InspectionConfig();

        [Header("Manager Reference (Optional)")]
        [Tooltip("Direct reference to InspectionManager. If unassigned, automatically uses InspectionManager.Instance.")]
        [SerializeField] private InspectionManager inspectionManager;

        // IInteractable Implementation
        public string InteractionPrompt => interactionPrompt;
        public bool CanInteract() => isInspectable;

        public void Interact()
        {
            if (!CanInteract()) return;

            var manager = inspectionManager != null ? inspectionManager : InspectionManager.Instance;
            if (manager != null)
            {
                manager.StartInspection(this);
            }
            else
            {
                Debug.LogWarning($"<color=orange>[InspectableObject]</color> No InspectionManager found to inspect <b>{gameObject.name}</b>!");
            }
        }

        // IInspectable Implementation
        public Sprite InspectionSprite => inspectionSprite;
        public Color VisualColor => visualColor;
        public GameObject CustomVisualPrefab => customVisualPrefab;
        public Vector3 SourceWorldPosition => transform.position;
        public string InspectionTitle => objectTitle;
        public string InspectionDescription => objectDescription;
        public InspectionConfig Config => inspectionConfig;

        private void Awake()
        {
            // Auto-fallback: grab sprite and color from attached SpriteRenderer if not explicitly set
            if (inspectionSprite == null)
            {
                var sr = GetComponentInChildren<SpriteRenderer>();
                if (sr != null)
                {
                    inspectionSprite = sr.sprite;
                    visualColor = sr.color;
                }
            }
        }

        /// <summary>
        /// Enable or disable inspectability dynamically from external quest or puzzle scripts.
        /// </summary>
        public void SetInspectable(bool state)
        {
            isInspectable = state;
        }

        /// <summary>
        /// Update inspection text dynamically (e.g. after finding a clue).
        /// </summary>
        public void UpdateNarrativeDetails(string newTitle, string newDescription)
        {
            objectTitle = newTitle;
            objectDescription = newDescription;
        }
    }
}
