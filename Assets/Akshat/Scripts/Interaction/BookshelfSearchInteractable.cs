using System;
using UnityEngine;
using UnityEngine.Events;
using Akshat.Inspection;

namespace Akshat.Interaction
{
    /// <summary>
    /// Interactable and inspectable component attached to the Bookshelf.
    /// Opens the book selection UI where the player enters book initials (e.g. "AY")
    /// to find a hidden clue or code.
    /// </summary>
    [SelectionBase]
    public class BookshelfSearchInteractable : MonoBehaviour, IInteractable, IInspectable
    {
        [Header("Interaction Settings")]
        [SerializeField] private string prompt = "Search Bookshelf";

        [Header("Book Puzzle Settings")]
        [Tooltip("The initials of the book that contains the secret code (e.g. AY).")]
        [SerializeField] private string correctBookInitials = "AY";

        [Tooltip("Sprite displayed when the book is closed / unexamined.")]
        [SerializeField] private Sprite closedBookSprite;

        [Tooltip("Sprite displayed when the correct book is discovered and opened.")]
        [SerializeField] private Sprite openBookSprite;

        [TextArea]
        [SerializeField] private string successMessage = "This book was marked for a reason... there is a code inside.";

        [TextArea]
        [SerializeField] private string failureMessage = "You searched through the book but found nothing.";

        [Header("Inspection Visuals")]
        [SerializeField] private string inspectionTitle = "Bookshelf";
        [TextArea] [SerializeField] private string inspectionDescription = "";
        [SerializeField] private InspectionConfig inspectionConfig;

        [Tooltip("The custom UI prefab (containing BookSelectionView) instantiated by InspectionManager.")]
        [SerializeField] private GameObject bookSelectionViewPrefab;

        [Header("Audio Feedback (Optional)")]
        [SerializeField] private AudioClip successClip;
        [SerializeField] private AudioClip failureClip;
        [SerializeField] private AudioSource audioSource;

        [Header("Events")]
        [Tooltip("Fires once when the player enters the correct initials and discovers the book.")]
        [SerializeField] private UnityEvent onCorrectBookFound;

        private bool hasFoundCorrectBook = false;

        // Public getters for the UI view
        public bool HasFoundCorrectBook => hasFoundCorrectBook;
        public string CorrectBookInitials => correctBookInitials;
        public Sprite ClosedBookSprite => closedBookSprite;
        public Sprite OpenBookSprite => openBookSprite;
        public string SuccessMessage => successMessage;
        public string FailureMessage => failureMessage;
        public UnityEvent OnCorrectBookFound => onCorrectBookFound;

        // IInteractable Implementation
        public string InteractionPrompt => prompt;

        public bool CanInteract()
        {
            return true; // Can always inspect or re-check the discovered clue
        }

        public void Interact()
        {
            if (InspectionManager.Instance != null)
            {
                InspectionManager.Instance.StartInspection(this);
            }
        }

        // IInspectable Implementation
        // Returning null ensures InspectionManager does not show a duplicate background book,
        // as BookSelectionPanel's BookImage owns the book display and handles opening.
        public Sprite InspectionSprite => null;
        public Color VisualColor => Color.white;
        public GameObject CustomVisualPrefab => bookSelectionViewPrefab;
        public Vector3 SourceWorldPosition => transform.position;
        public string InspectionTitle => inspectionTitle;
        public string InspectionDescription => inspectionDescription;
        public InspectionConfig Config => inspectionConfig;

        /// <summary>
        /// Attempts to validate entered book initials.
        /// Trims whitespace, ignores case, and triggers success events upon the first correct match.
        /// </summary>
        public bool SearchBook(string initials)
        {
            if (string.IsNullOrWhiteSpace(initials))
            {
                PlayAudio(failureClip);
                return false;
            }

            string cleanInput = initials.Trim().ToUpperInvariant();
            string cleanTarget = (correctBookInitials ?? "").Trim().ToUpperInvariant();

            if (cleanInput == cleanTarget)
            {
                if (!hasFoundCorrectBook)
                {
                    hasFoundCorrectBook = true;
                    onCorrectBookFound?.Invoke();
                }
                PlayAudio(successClip);
                return true;
            }
            else
            {
                PlayAudio(failureClip);
                return false;
            }
        }

        private void PlayAudio(AudioClip clip)
        {
            if (clip != null && audioSource != null)
            {
                audioSource.PlayOneShot(clip);
            }
        }

        private void OnValidate()
        {
            if (string.IsNullOrEmpty(correctBookInitials))
            {
                Debug.LogWarning($"BookshelfSearchInteractable on {gameObject.name} needs valid book initials.", this);
            }
        }
    }
}
