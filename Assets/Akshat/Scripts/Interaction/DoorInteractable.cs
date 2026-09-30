using UnityEngine;

namespace Akshat.Interaction
{
    /// <summary>
    /// Practical example of an interactable object (Door).
    /// Implements IInteractable without modifying PlayerInteraction.cs.
    /// Demonstrates dynamic prompt text ("Open Door" / "Close Door") and state changes.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class DoorInteractable : MonoBehaviour, IInteractable
    {
        [Header("Door State")]
        [Tooltip("Whether the door is currently open.")]
        [SerializeField] private bool isOpen = false;

        [Tooltip("If true, interaction is temporarily disabled (e.g. key required).")]
        [SerializeField] private bool isLocked = false;

        [Header("Prompt Customization")]
        [SerializeField] private string openPrompt = "Open Door";
        [SerializeField] private string closePrompt = "Close Door";
        [SerializeField] private string lockedPrompt = "Door Locked";

        [Header("Visual Feedback (Optional)")]
        [SerializeField] private SpriteRenderer doorRenderer;
        [SerializeField] private Color openColor = new Color(0.4f, 0.8f, 0.4f, 1f); // Tint green when open
        private Color defaultColor = Color.white;

        public string InteractionPrompt
        {
            get
            {
                if (isLocked) return lockedPrompt;
                return isOpen ? closePrompt : openPrompt;
            }
        }

        public bool CanInteract()
        {
            return !isLocked;
        }

        private void Awake()
        {
            if (doorRenderer == null)
            {
                doorRenderer = GetComponentInChildren<SpriteRenderer>();
            }

            if (doorRenderer != null)
            {
                defaultColor = doorRenderer.color;
                if (isOpen)
                {
                    doorRenderer.color = openColor;
                }
            }

            // Ensure collider is marked as a trigger so the player can approach it smoothly
            Collider2D col = GetComponent<Collider2D>();
            if (col != null)
            {
                col.isTrigger = true;
            }
        }

        public void Interact()
        {
            if (!CanInteract())
            {
                Debug.Log($"<color=orange>[Door]</color> Cannot open <b>{gameObject.name}</b>; the door is locked!");
                return;
            }

            isOpen = !isOpen;

            Debug.Log($"<color=green>[Door]</color> Interacted with <b>{gameObject.name}</b>. Door is now <b>{(isOpen ? "OPEN" : "CLOSED")}</b>.");

            if (doorRenderer != null)
            {
                doorRenderer.color = isOpen ? openColor : defaultColor;
            }
        }

        /// <summary>
        /// Lock or unlock door from external systems (keys, puzzle switches, events).
        /// </summary>
        public void SetLocked(bool locked)
        {
            isLocked = locked;
        }
    }
}
