using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using UnityEngine.InputSystem;

namespace Akshat.Inspection
{
    using Akshat.Interaction;

    public class PasscodeKeypadView : MonoBehaviour, IInspectionCustomView
    {
        [Header("UI References")]
        [Tooltip("The simple TMP Input Field the user types into.")]
        [SerializeField] private TMP_InputField inputField;
        
        [Tooltip("Optional label to show success/error status.")]
        [SerializeField] private TMP_Text statusLabel;
        
        [Tooltip("Optional CanvasGroup to disable input when successful.")]
        [SerializeField] private CanvasGroup keypadGroup;
        
        [Header("Buttons (Optional)")]
        [SerializeField] private Button submitButton;
        [SerializeField] private Button closeButton;
        
        [Header("Feedback")]
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color errorColor = Color.red;
        [SerializeField] private Color successColor = Color.green;
        [SerializeField] private float shakeAmount = 10f;
        [SerializeField] private float shakeDuration = 0.3f;
        
        private PasscodeLockInteractable currentLock;
        private InspectionManager currentManager;
        
        private Coroutine feedbackRoutine;
        private Vector3 originalDisplayPos;

        public void Bind(IInspectable inspectable, InspectionManager manager)
        {
            currentLock = inspectable as PasscodeLockInteractable;
            currentManager = manager;
            
            if (currentLock == null)
            {
                Debug.LogError("PasscodeKeypadView bound to an inspectable that is not a PasscodeLockInteractable.");
                return;
            }
            
            if (inputField != null)
            {
                inputField.characterLimit = currentLock.MaxCodeLength;
                inputField.text = "";
                inputField.onSubmit.AddListener(OnInputSubmit);
                originalDisplayPos = inputField.transform.localPosition;
                
                // Auto focus so the player can immediately start typing
                inputField.Select();
                inputField.ActivateInputField();
            }

            if (submitButton != null) submitButton.onClick.AddListener(Submit);
            if (closeButton != null) closeButton.onClick.AddListener(CloseView);
            
            if (statusLabel != null)
            {
                statusLabel.text = "";
                statusLabel.color = normalColor;
            }
        }
        
        public void Unbind()
        {
            if (inputField != null) inputField.onSubmit.RemoveAllListeners();
            if (submitButton != null) submitButton.onClick.RemoveAllListeners();
            if (closeButton != null) closeButton.onClick.RemoveAllListeners();
            
            currentLock = null;
            currentManager = null;
        }

        private void OnInputSubmit(string text)
        {
            Submit();
        }

        public void Submit()
        {
            if (currentLock == null || inputField == null) return;
            
            string currentEntry = inputField.text;
            if (string.IsNullOrEmpty(currentEntry)) return;

            var result = currentLock.TrySubmitCode(currentEntry);
            
            if (feedbackRoutine != null) StopCoroutine(feedbackRoutine);

            if (result == PasscodeLockInteractable.SubmitResult.Correct)
            {
                feedbackRoutine = StartCoroutine(ShowSuccess());
            }
            else if (result == PasscodeLockInteractable.SubmitResult.Incorrect)
            {
                feedbackRoutine = StartCoroutine(ShowError());
            }
        }

        private IEnumerator ShowError()
        {
            if (inputField != null && inputField.textComponent != null) inputField.textComponent.color = errorColor;
            if (statusLabel != null)
            {
                statusLabel.text = "ERROR";
                statusLabel.color = errorColor;
            }
            
            if (inputField != null)
            {
                float elapsed = 0f;
                while (elapsed < shakeDuration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float offsetX = Random.Range(-shakeAmount, shakeAmount);
                    inputField.transform.localPosition = originalDisplayPos + new Vector3(offsetX, 0, 0);
                    yield return null;
                }
                inputField.transform.localPosition = originalDisplayPos;
                
                // Clear and reset on fail
                inputField.text = "";
                if (inputField.textComponent != null) inputField.textComponent.color = normalColor;
                
                // Re-focus
                inputField.Select();
                inputField.ActivateInputField();
            }
            
            if (statusLabel != null) statusLabel.text = "";
            
            feedbackRoutine = null;
        }

        private IEnumerator ShowSuccess()
        {
            if (keypadGroup != null) keypadGroup.interactable = false;
            if (inputField != null)
            {
                inputField.interactable = false;
                if (inputField.textComponent != null) inputField.textComponent.color = successColor;
            }
            
            if (statusLabel != null)
            {
                statusLabel.text = "ACCESS GRANTED";
                statusLabel.color = successColor;
            }
            
            yield return new WaitForSecondsRealtime(1.0f);
            
            if (currentManager != null)
            {
                currentManager.ExitInspection();
            }
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
