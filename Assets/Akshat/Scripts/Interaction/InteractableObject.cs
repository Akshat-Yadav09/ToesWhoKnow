using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace Akshat.Interaction
{
    /// <summary>
    /// Generic, modular component that turns ANY GameObject into an interactable object.
    /// Responds directly to PlayerInteraction.cs and exposes a UnityEvent so you can wire
    /// cutscenes, end-of-demo screens (ComingSoonUI), locks, doors, lights, or custom logic in the Inspector.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    [DisallowMultipleComponent]
    [AddComponentMenu("Akshat/Interaction/Interactable Object")]
    public class InteractableObject : MonoBehaviour, IInteractable
    {
        [Header("Prompt & Display")]
        [Tooltip("The prompt text displayed when the player approaches this object (e.g. 'Use Door', 'Pick Up', 'Examine', 'Open Gate').")]
        [SerializeField] private string promptText = "Interact";

        [Tooltip("Whether this object is currently available for interaction.")]
        [SerializeField] private bool isInteractable = true;

        [Header("Lock Settings (Optional)")]
        [Tooltip("If true, the object is locked and requires an Unlock() call or event.")]
        [SerializeField] private bool isLocked = false;

        [Tooltip("Prompt displayed when near the object while it is locked (e.g. 'Door Locked', 'Locked').")]
        [SerializeField] private string lockedPrompt = "Locked";

        [Tooltip("If true, pressing interact while locked plays the locked sound and fires OnLockedAttempt.")]
        [SerializeField] private bool allowInteractWhenLocked = true;

        [Header("Interaction Behavior")]
        [Tooltip("If true, this object can only be interacted with once, then automatically disables further interactions.")]
        [SerializeField] private bool singleUse = false;

        [Tooltip("Cooldown in seconds between interactions to prevent spam.")]
        [SerializeField] private float cooldownDuration = 0.3f;

        [Header("End of Demo / Coming Soon (Optional)")]
        [Tooltip("If enabled, automatically triggers the ComingSoonUI end-of-demo screen upon successful interaction.")]
        [SerializeField] private bool triggerComingSoon = false;

        [Header("Audio Feedback (Optional)")]
        [Tooltip("Sound played upon successful interaction.")]
        [SerializeField] private AudioClip interactionSound;

        [Tooltip("Sound played when attempting to interact while locked.")]
        [SerializeField] private AudioClip lockedSound;

        [Range(0f, 1f)]
        [SerializeField] private float soundVolume = 1f;

        [Tooltip("AudioSource used to play clips. Auto-created if unassigned.")]
        [SerializeField] private AudioSource audioSource;

        [Header("Events")]
        [Tooltip("Invoked when the player successfully interacts with this object.")]
        [SerializeField] private UnityEvent onInteracted;

        [Tooltip("Invoked when the player tries to interact while this object is locked.")]
        [SerializeField] private UnityEvent onLockedAttempt;

        private float lastInteractTime = -10f;
        private bool hasBeenUsed = false;

        // IInteractable Implementation
        public string InteractionPrompt => isLocked ? lockedPrompt : promptText;

        public bool IsLocked => isLocked;
        public bool IsInteractable => isInteractable;

        private void Awake()
        {
            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
            }

            // Ensure collider is marked as a trigger so the player can approach smoothly
            Collider2D col = GetComponent<Collider2D>();
            if (col != null && !col.isTrigger)
            {
                col.isTrigger = true;
            }
        }

        private void Reset()
        {
            // Auto-configure trigger collider on add
            Collider2D col = GetComponent<Collider2D>();
            if (col != null)
            {
                col.isTrigger = true;
            }

            // Suggest or auto-assign to Interactable layer (Layer 6) if currently Default
            int interactableLayer = LayerMask.NameToLayer("Interactable");
            if (interactableLayer != -1 && gameObject.layer == 0)
            {
                gameObject.layer = interactableLayer;
            }
        }

        public bool CanInteract()
        {
            if (!isInteractable) return false;
            if (singleUse && hasBeenUsed) return false;
            if (Time.time < lastInteractTime + cooldownDuration) return false;

            // Block interaction if locked and locked interactions are disabled
            if (isLocked && !allowInteractWhenLocked) return false;

            // Ensure player isn't mid-camera/perspective transition
            var transitionMgr = Shaurya.PerspectiveTransitionManager.Instance;
            if (transitionMgr != null && transitionMgr.IsTransitioning)
            {
                return false;
            }

            return true;
        }

        public void Interact()
        {
            if (!CanInteract()) return;

            // Handle Locked State
            if (isLocked)
            {
                PlayAudio(lockedSound);
                onLockedAttempt?.Invoke();
                return;
            }

            // Successful Interaction
            lastInteractTime = Time.time;
            hasBeenUsed = true;

            PlayAudio(interactionSound);

            // 1. Invoke Inspector UnityEvent
            onInteracted?.Invoke();

            // 2. Trigger Coming Soon if enabled
            if (triggerComingSoon)
            {
                if (ComingSoonUI.Instance != null)
                {
                    ComingSoonUI.Instance.TriggerComingSoon();
                }
                else
                {
                    // Fallback: search scene in case Instance hasn't initialized
                    var comingSoon = FindAnyObjectByType<ComingSoonUI>();
                    if (comingSoon != null)
                    {
                        comingSoon.TriggerComingSoon();
                    }
                    else
                    {
                        Debug.LogWarning("[InteractableObject] Trigger Coming Soon is enabled, but no ComingSoonUI was found in the scene!", this);
                    }
                }
            }

            // 3. Handle single use
            if (singleUse)
            {
                isInteractable = false;
            }
        }

        private void PlayAudio(AudioClip clip)
        {
            if (clip == null) return;

            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
                if (audioSource == null)
                {
                    audioSource = gameObject.AddComponent<AudioSource>();
                }
            }

            audioSource.PlayOneShot(clip, soundVolume);
        }

        #region Public Control API (Can be called from UnityEvents / Scripts)

        public void Unlock()
        {
            isLocked = false;
        }

        public void Lock()
        {
            isLocked = true;
        }

        public void SetLocked(bool locked)
        {
            isLocked = locked;
        }

        public void SetInteractable(bool state)
        {
            isInteractable = state;
        }

        public void SetPromptText(string newPrompt)
        {
            promptText = newPrompt;
        }

        /// <summary>
        /// Manually triggers this interaction through code or another event.
        /// </summary>
        public void TriggerInteraction()
        {
            Interact();
        }

        #endregion
    }
}
