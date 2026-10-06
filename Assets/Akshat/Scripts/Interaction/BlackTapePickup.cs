// BlackTapePickup.cs
// Interacting with this object gives the Black Tape to the player and hides the pickup.

using UnityEngine;

namespace Akshat.Interaction
{
    /// <summary>
    /// Placed on the Black Tape pickup GameObject in the Kitchen.
    /// Implements IInteractable.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    [AddComponentMenu("Akshat/Black Tape Pickup")]
    public class BlackTapePickup : MonoBehaviour, IInteractable
    {
        [Header("References")]
        [Tooltip("Reference to the player's key items component. Auto-located if unassigned.")]
        [SerializeField] private PlayerKeyItems playerKeyItems;

        [Tooltip("Reference to the message UI. Auto-located if unassigned.")]
        [SerializeField] private PlayerMessageUI messageUI;

        [Header("Prerequisite Settings")]
        [Tooltip("If true, player must inspect the damaged wire first before they can pick up the black tape.")]
        [SerializeField] private bool requireInspectedDamagedWire = true;
        [SerializeField] private string unneededPrompt = "Examine Tape";
        [SerializeField] private string unneededMessage = "Just some black electrical tape. I don't have a use for it right now.";

        [Header("Interaction Settings")]
        [SerializeField] private string promptText = "Pick Up Black Tape";
        [SerializeField] private string pickupMessage = "Picked up black tape.";

        [Header("Audio")]
        [SerializeField] private AudioClip pickupSound;
        [Range(0f, 1f)] [SerializeField] private float soundVolume = 1f;

        private bool isCollected = false;

        public string InteractionPrompt
        {
            get
            {
                if (isCollected) return string.Empty;
                if (requireInspectedDamagedWire)
                {
                    if (playerKeyItems == null) playerKeyItems = FindAnyObjectByType<PlayerKeyItems>();
                    bool knowsAboutWire = playerKeyItems != null && playerKeyItems.HasInspectedDamagedWire;
                    if (!knowsAboutWire) return unneededPrompt;
                }
                return promptText;
            }
        }

        public bool CanInteract()
        {
            return !isCollected && gameObject.activeSelf;
        }

        private void Awake()
        {
            if (playerKeyItems == null)
            {
                playerKeyItems = FindAnyObjectByType<PlayerKeyItems>();
            }

            if (messageUI == null)
            {
                messageUI = FindAnyObjectByType<PlayerMessageUI>();
            }
        }

        public void Interact()
        {
            if (!CanInteract()) return;

            // Find playerKeyItems if not cached yet
            if (playerKeyItems == null)
            {
                playerKeyItems = FindAnyObjectByType<PlayerKeyItems>();
            }

            // Check if broken cable has been inspected first
            if (requireInspectedDamagedWire)
            {
                bool knowsAboutWire = playerKeyItems != null && playerKeyItems.HasInspectedDamagedWire;
                if (!knowsAboutWire)
                {
                    // Player has not discovered the broken wire yet
                    if (messageUI != null) messageUI.ShowMessage(unneededMessage);
                    else PlayerMessageUI.Show(unneededMessage);
                    return;
                }
            }

            if (playerKeyItems != null)
            {
                bool added = playerKeyItems.AddBlackTape();
                if (!added) return; // Already has it
            }

            isCollected = true;

            // Play audio if assigned
            if (pickupSound != null)
            {
                AudioSource.PlayClipAtPoint(pickupSound, transform.position, soundVolume);
            }

            // Show feedback message
            if (messageUI != null)
            {
                messageUI.ShowMessage(pickupMessage);
            }
            else
            {
                PlayerMessageUI.Show(pickupMessage);
            }

            // Hide pickup (do not destroy, so SetActive(false) stays persistent while room toggles)
            gameObject.SetActive(false);
        }
    }
}
