using UnityEngine;

namespace Akshat.Interaction
{
    [RequireComponent(typeof(Collider2D))]
    public class TestInteractable : MonoBehaviour, IInteractable
    {
        [Header("Interaction Settings")]
        [Tooltip("Text displayed on the screen-space interaction prompt (e.g. 'Interact').")]
        [SerializeField] private string promptMessage = "Interact";

        [Tooltip("Whether this object is currently interactable.")]
        [SerializeField] private bool isInteractable = true;

        [Header("Visual Feedback (Optional)")]
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private Color toggleColor = new Color(1f, 0.85f, 0.2f); // Gold / Yellow
        private Color defaultColor;
        private bool isToggled;

        public string InteractionPrompt => promptMessage;

        public bool CanInteract() => isInteractable;

        private void Awake()
        {
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            }

            if (spriteRenderer != null)
            {
                defaultColor = spriteRenderer.color;
            }

        }

        public void Interact()
        {
            if (!CanInteract()) return;

            isToggled = !isToggled;

            Debug.Log($"<color=cyan>[Interaction]</color> Successfully interacted with: <b>{gameObject.name}</b> (Toggled: {isToggled})");

            // Toggle visual state as immediate visual confirmation
            if (spriteRenderer != null)
            {
                spriteRenderer.color = isToggled ? toggleColor : defaultColor;
            }
        }
    }
}
