using System;
using UnityEngine;
using UnityEngine.Events;
using Akshat.Inspection;

namespace Akshat.Interaction
{
    public class PasscodeLockInteractable : MonoBehaviour, IInteractable, IInspectable
    {
        public enum SubmitResult
        {
            Incomplete,
            Incorrect,
            Correct
        }

        private enum LockState
        {
            Locked,
            AwaitingInput,
            Solved,
            CompletionDispatched
        }

        [Header("Passcode Configuration")]
        [SerializeField] private string correctCode = "1234";
        [SerializeField] private string prompt = "Enter Code";
        
        [Header("Inspection Visuals")]
        [SerializeField] private Sprite inspectionSprite;
        [SerializeField] private string inspectionTitle = "Keypad Lock";
        [TextArea][SerializeField] private string inspectionDescription = "";
        [SerializeField] private InspectionConfig inspectionConfig;
        [SerializeField] private GameObject keypadCustomViewPrefab;
        
        [Header("Audio Feedback")]
        [SerializeField] private AudioClip successClip;
        [SerializeField] private AudioClip failureClip;
        [SerializeField] private AudioSource audioSource;
        
        [Header("Events")]
        [SerializeField] private UnityEvent onUnlocked;
        
        private LockState state = LockState.Locked;

        public int MaxCodeLength => correctCode.Length;

        // IInteractable properties
        public string InteractionPrompt => prompt;
        
        public bool CanInteract()
        {
            return state == LockState.Locked || state == LockState.AwaitingInput;
        }
        
        public void Interact()
        {
            if (CanInteract())
            {
                state = LockState.AwaitingInput;
                if (InspectionManager.Instance != null)
                {
                    InspectionManager.Instance.OnInspectionEnded += HandleInspectionEnded;
                    InspectionManager.Instance.StartInspection(this);
                }
            }
        }
        
        // IInspectable properties
        public Sprite InspectionSprite => inspectionSprite;
        public Color VisualColor => Color.white;
        public GameObject CustomVisualPrefab => keypadCustomViewPrefab;
        public Vector3 SourceWorldPosition => transform.position;
        public string InspectionTitle => inspectionTitle;
        public string InspectionDescription => inspectionDescription;
        public InspectionConfig Config => inspectionConfig;
        
        public SubmitResult TrySubmitCode(string enteredCode)
        {
            if (state == LockState.Solved || state == LockState.CompletionDispatched)
            {
                return SubmitResult.Correct;
            }
            
            if (enteredCode.Length < correctCode.Length)
            {
                return SubmitResult.Incomplete;
            }
            
            if (enteredCode == correctCode)
            {
                state = LockState.Solved;
                PlayAudio(successClip);
                return SubmitResult.Correct;
            }
            else
            {
                PlayAudio(failureClip);
                return SubmitResult.Incorrect;
            }
        }
        
        private void PlayAudio(AudioClip clip)
        {
            if (clip != null && audioSource != null)
            {
                audioSource.PlayOneShot(clip);
            }
        }
        
        private void HandleInspectionEnded(IInspectable inspectable)
        {
            if (inspectable as PasscodeLockInteractable == this)
            {
                if (InspectionManager.Instance != null)
                {
                    InspectionManager.Instance.OnInspectionEnded -= HandleInspectionEnded;
                }
                
                if (state == LockState.Solved)
                {
                    state = LockState.CompletionDispatched;
                    onUnlocked?.Invoke();
                }
                else if (state == LockState.AwaitingInput)
                {
                    state = LockState.Locked; // Reset state if closed without solving
                }
            }
        }

        private void OnValidate()
        {
            if (string.IsNullOrEmpty(correctCode))
            {
                Debug.LogWarning("PasscodeLockInteractable on " + gameObject.name + " requires a valid code.");
            }
            else
            {
                foreach (char c in correctCode)
                {
                    if (!char.IsDigit(c))
                    {
                        Debug.LogWarning("PasscodeLockInteractable on " + gameObject.name + " code must be numeric.");
                        break;
                    }
                }
            }
        }
    }
}
