using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Akshat.Interaction
{
    /// <summary>
    /// Modular UI component responsible for displaying interaction prompts.
    /// Can be used as a World Space prompt above the object/player or connected to Screen Space UI.
    /// </summary>
    public class InteractionPromptUI : MonoBehaviour
    {
        [Header("UI References")]
        [Tooltip("The root visual GameObject to enable/disable when an interactable is in range.")]
        [SerializeField] private GameObject promptContainer;

        [Tooltip("Optional TextMeshPro UI Text component.")]
        [SerializeField] private TMP_Text tmpPromptText;

        [Tooltip("Optional standard UI Text component for displaying '[E] Open', etc.")]
        [SerializeField] private Text promptText;

        [Tooltip("Optional 3D TextMesh component (useful for world-space 2D prompts without Canvas).")]
        [SerializeField] private TextMesh textMesh;

        [Header("Settings")]
        [Tooltip("Prefix for the prompt (e.g. '[E] ').")]
        [SerializeField] private string keyPrefix = "[E] ";

        [Tooltip("Default prompt text if the interactable doesn't provide one.")]
        [SerializeField] private string defaultActionText = "Interact";

        private void Awake()
        {
            if (promptContainer == null)
            {
                // Default to first child if container is unassigned, or this GameObject
                promptContainer = transform.childCount > 0 ? transform.GetChild(0).gameObject : gameObject;
            }

            if (tmpPromptText == null)
            {
                tmpPromptText = GetComponentInChildren<TMP_Text>(true);
            }

            if (promptText == null)
            {
                promptText = GetComponentInChildren<Text>(true);
            }

            if (textMesh == null)
            {
                textMesh = GetComponentInChildren<TextMesh>(true);
            }

            Hide();
        }

        /// <summary>
        /// Displays the prompt with the specified interactable's text.
        /// </summary>
        public virtual void Show(IInteractable interactable)
        {
            if (promptContainer != null)
            {
                promptContainer.SetActive(true);
            }

            string actionText = !string.IsNullOrEmpty(interactable?.InteractionPrompt)
                ? interactable.InteractionPrompt
                : defaultActionText;

            string fullMessage = $"{keyPrefix}{actionText}";

            if (tmpPromptText != null)
            {
                tmpPromptText.text = fullMessage;
            }

            if (promptText != null)
            {
                promptText.text = fullMessage;
            }

            if (textMesh != null)
            {
                textMesh.text = fullMessage;
            }
        }

        /// <summary>
        /// Hides the prompt when no interactable is in range.
        /// </summary>
        public virtual void Hide()
        {
            if (promptContainer != null)
            {
                promptContainer.SetActive(false);
            }
        }
    }
}
