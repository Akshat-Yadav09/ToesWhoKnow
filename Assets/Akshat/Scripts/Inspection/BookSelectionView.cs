using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Akshat.Interaction;

namespace Akshat.Inspection
{
    /// <summary>
    /// Custom inspection view for searching the bookshelf.
    /// Provides an input field for book initials (e.g. "AY"),
    /// search and close buttons, book image swap, and feedback text.
    /// </summary>
    public class BookSelectionView : MonoBehaviour, IInspectionCustomView
    {
        [Header("UI Controls")]
        [Tooltip("Text input for entering book initials (auto-forced to uppercase and clamped to 2 chars).")]
        [SerializeField] private TMP_InputField initialsInput;

        [Tooltip("Button that triggers searching the book.")]
        [SerializeField] private Button searchButton;

        [Tooltip("Button that exits inspection.")]
        [SerializeField] private Button closeButton;

        [Header("Visual & Message Elements")]
        [Tooltip("Image component displaying the closed or open book.")]
        [SerializeField] private Image bookImage;

        [Tooltip("Text component showing thoughts / narrative clues upon searching.")]
        [SerializeField] private TMP_Text messageText;

        [Header("Feedback Styling")]
        [SerializeField] private Color normalTextColor = Color.white;
        [SerializeField] private Color errorTextColor = new Color(1f, 0.4f, 0.4f);
        [SerializeField] private Color successTextColor = new Color(0.4f, 1f, 0.5f);
        [SerializeField] private float shakeAmount = 8f;
        [SerializeField] private float shakeDuration = 0.25f;

        [Header("Input Field Styling")]
        [Tooltip("Font color of typed letters inside the input field (defaults to dark charcoal so it is clearly visible against the white input box).")]
        [SerializeField] private Color inputTextColor = new Color(0.12f, 0.12f, 0.12f, 1f);

        [Tooltip("Caret (blinking cursor) color in the input field.")]
        [SerializeField] private Color caretColor = new Color(0.12f, 0.12f, 0.12f, 1f);

        [Tooltip("Text color inside the input field when an incorrect book is submitted.")]
        [SerializeField] private Color errorInputTextColor = new Color(0.85f, 0.15f, 0.15f, 1f);

        [Tooltip("Text color inside the input field when the correct book is found.")]
        [SerializeField] private Color successInputTextColor = new Color(0.1f, 0.65f, 0.15f, 1f);

        [Tooltip("Optional override font asset for the input field to ensure all uppercase letters (A-Z) render reliably.")]
        [SerializeField] private TMP_FontAsset inputFontAsset;

        private BookshelfSearchInteractable currentInteractable;
        private InspectionManager currentManager;

        private Coroutine feedbackRoutine;
        private Vector3 originalInputPos;

        public void Bind(IInspectable inspectable, InspectionManager manager)
        {
            currentInteractable = inspectable as BookshelfSearchInteractable;
            currentManager = manager;

            if (currentInteractable == null)
            {
                Debug.LogWarning("BookSelectionView bound to an inspectable that is not BookshelfSearchInteractable.", this);
                return;
            }

            if (initialsInput != null)
            {
                originalInputPos = initialsInput.transform.localPosition;
                initialsInput.characterLimit = 2;
                initialsInput.onValidateInput = ValidateLetterInput;
                initialsInput.onValueChanged.AddListener(OnInputValueChanged);
                initialsInput.onSubmit.AddListener(OnInputSubmit);
                ConfigureInputField();
            }

            if (searchButton != null)
            {
                searchButton.onClick.AddListener(PerformSearch);
            }

            if (closeButton != null)
            {
                closeButton.onClick.AddListener(CloseView);
            }

            RefreshView();
        }

        public void Unbind()
        {
            if (feedbackRoutine != null)
            {
                StopCoroutine(feedbackRoutine);
                feedbackRoutine = null;
            }

            if (initialsInput != null)
            {
                initialsInput.onValidateInput = null;
                initialsInput.onValueChanged.RemoveListener(OnInputValueChanged);
                initialsInput.onSubmit.RemoveListener(OnInputSubmit);
                initialsInput.transform.localPosition = originalInputPos;
            }

            if (searchButton != null)
            {
                searchButton.onClick.RemoveListener(PerformSearch);
            }

            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(CloseView);
            }

            currentInteractable = null;
            currentManager = null;
        }

        private void ConfigureInputField()
        {
            if (initialsInput == null) return;

            // Safety guard: if inspector has transparent or pure white assigned, enforce readable dark color
            if (inputTextColor.a < 0.1f || (inputTextColor.r > 0.9f && inputTextColor.g > 0.9f && inputTextColor.b > 0.9f))
            {
                inputTextColor = new Color(0.12f, 0.12f, 0.12f, 1f);
            }

            if (caretColor.a < 0.1f || (caretColor.r > 0.9f && caretColor.g > 0.9f && caretColor.b > 0.9f))
            {
                caretColor = inputTextColor;
            }

            // Font asset: verify font can display uppercase letters A-Z without missing glyphs
            if (initialsInput.textComponent != null)
            {
                if (inputFontAsset != null)
                {
                    initialsInput.textComponent.font = inputFontAsset;
                }
                else if (initialsInput.textComponent.font == null || !initialsInput.textComponent.font.HasCharacter('A', true, true))
                {
                    TMP_FontAsset defaultFont = TMP_Settings.defaultFontAsset;
                    if (defaultFont != null)
                    {
                        initialsInput.textComponent.font = defaultFont;
                    }
                }

                initialsInput.textComponent.color = inputTextColor;
                initialsInput.textComponent.alignment = TextAlignmentOptions.Center;
                initialsInput.textComponent.overflowMode = TextOverflowModes.Overflow;
                initialsInput.textComponent.textWrappingMode = TextWrappingModes.NoWrap;
            }

            // Visible caret (cursor)
            initialsInput.customCaretColor = true;
            initialsInput.caretColor = caretColor;
            initialsInput.caretWidth = 2;

            // Selection highlight
            initialsInput.selectionColor = new Color(0.2f, 0.5f, 1f, 0.35f);

            // Ensure text area / viewport has comfortable padding and does not clip with RectMask2D
            if (initialsInput.textViewport != null)
            {
                initialsInput.textViewport.anchorMin = Vector2.zero;
                initialsInput.textViewport.anchorMax = Vector2.one;
                initialsInput.textViewport.offsetMin = new Vector2(8f, 2f);
                initialsInput.textViewport.offsetMax = new Vector2(-8f, -2f);
            }

            if (initialsInput.textComponent != null)
            {
                RectTransform textRect = initialsInput.textComponent.rectTransform;
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.offsetMin = Vector2.zero;
                textRect.offsetMax = Vector2.zero;
                textRect.anchoredPosition = Vector2.zero;
            }

            // Configure placeholder if present
            if (initialsInput.placeholder is TMP_Text placeholder)
            {
                placeholder.alignment = TextAlignmentOptions.Center;
                placeholder.color = new Color(0.5f, 0.5f, 0.5f, 0.6f);
                if (initialsInput.textComponent != null && initialsInput.textComponent.font != null)
                {
                    placeholder.font = initialsInput.textComponent.font;
                }
            }
        }

        private void OnInputValueChanged(string value)
        {
            if (string.IsNullOrEmpty(value)) return;

            string upper = value.ToUpperInvariant();
            if (upper != value)
            {
                initialsInput.text = upper;
                initialsInput.caretPosition = upper.Length;
            }

            if (initialsInput.textComponent != null)
            {
                initialsInput.textComponent.color = inputTextColor;
            }
        }

        private char ValidateLetterInput(string text, int charIndex, char addedChar)
        {
            if (char.IsLetter(addedChar))
            {
                return char.ToUpperInvariant(addedChar);
            }
            return '\0'; // Ignore non-letters
        }

        private void RefreshView()
        {
            if (currentInteractable == null) return;

            bool isSolved = currentInteractable.HasFoundCorrectBook;

            if (bookImage != null)
            {
                Sprite targetSprite = isSolved ? currentInteractable.OpenBookSprite : currentInteractable.ClosedBookSprite;
                bookImage.sprite = targetSprite;
                bookImage.gameObject.SetActive(targetSprite != null);
            }

            if (messageText != null)
            {
                messageText.text = isSolved ? currentInteractable.SuccessMessage : "";
                messageText.color = isSolved ? successTextColor : normalTextColor;
            }

            if (initialsInput != null)
            {
                if (initialsInput.textComponent != null)
                {
                    initialsInput.textComponent.color = isSolved ? successInputTextColor : inputTextColor;
                }

                if (isSolved)
                {
                    initialsInput.text = currentInteractable.CorrectBookInitials;
                    initialsInput.interactable = false;
                }
                else
                {
                    initialsInput.text = "";
                    initialsInput.interactable = true;

                    // Auto-select and focus so the player can type immediately
                    initialsInput.Select();
                    initialsInput.ActivateInputField();
                }
            }

            if (searchButton != null)
            {
                searchButton.interactable = !isSolved;
            }
        }

        private void OnInputSubmit(string value)
        {
            PerformSearch();
        }

        public void PerformSearch()
        {
            if (currentInteractable == null || initialsInput == null) return;
            if (currentInteractable.HasFoundCorrectBook) return;

            string entered = initialsInput.text;
            if (string.IsNullOrWhiteSpace(entered)) return;

            if (feedbackRoutine != null)
            {
                StopCoroutine(feedbackRoutine);
            }

            bool isCorrect = currentInteractable.SearchBook(entered);

            if (isCorrect)
            {
                feedbackRoutine = StartCoroutine(ShowSuccessRoutine());
            }
            else
            {
                feedbackRoutine = StartCoroutine(ShowFailureRoutine());
            }
        }

        private IEnumerator ShowSuccessRoutine()
        {
            if (initialsInput != null)
            {
                initialsInput.interactable = false;
                if (initialsInput.textComponent != null)
                {
                    initialsInput.textComponent.color = successInputTextColor;
                }
            }

            if (searchButton != null)
            {
                searchButton.interactable = false;
            }

            if (bookImage != null && currentInteractable.OpenBookSprite != null)
            {
                bookImage.sprite = currentInteractable.OpenBookSprite;
                bookImage.gameObject.SetActive(true);
            }

            if (messageText != null)
            {
                messageText.text = currentInteractable.SuccessMessage;
                messageText.color = successTextColor;
            }

            feedbackRoutine = null;
            yield return null;
        }

        private IEnumerator ShowFailureRoutine()
        {
            if (messageText != null)
            {
                messageText.text = currentInteractable.FailureMessage;
                messageText.color = errorTextColor;
            }

            if (initialsInput != null)
            {
                if (initialsInput.textComponent != null)
                {
                    initialsInput.textComponent.color = errorInputTextColor;
                }

                // Shake effect on the input box
                float elapsed = 0f;
                while (elapsed < shakeDuration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float offsetX = Random.Range(-shakeAmount, shakeAmount);
                    initialsInput.transform.localPosition = originalInputPos + new Vector3(offsetX, 0f, 0f);
                    yield return null;
                }
                initialsInput.transform.localPosition = originalInputPos;

                // Reset and clear for next attempt
                initialsInput.text = "";
                if (initialsInput.textComponent != null)
                {
                    initialsInput.textComponent.color = inputTextColor;
                }

                initialsInput.Select();
                initialsInput.ActivateInputField();
            }

            feedbackRoutine = null;
        }

        public void CloseView()
        {
            if (currentManager != null)
            {
                currentManager.ExitInspection();
            }
        }
    }
}
