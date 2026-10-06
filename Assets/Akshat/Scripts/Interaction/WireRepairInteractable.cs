// WireRepairInteractable.cs
// Handles inspecting the damaged wire and repairing it with black tape in the Warehouse.

using UnityEngine;
using Akshat.RoomSystem;

namespace Akshat.Interaction
{
    /// <summary>
    /// Placed on the Wire GameObject in Room_Warehouse.
    /// Implements IInteractable.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    [AddComponentMenu("Akshat/Wire Repair Interactable")]
    public class WireRepairInteractable : MonoBehaviour, IInteractable
    {
        [Header("References")]
        [Tooltip("Reference to the player's key items. Auto-located if unassigned.")]
        [SerializeField] private PlayerKeyItems playerKeyItems;

        [Tooltip("Reference to the message UI. Auto-located if unassigned.")]
        [SerializeField] private PlayerMessageUI messageUI;

        [Tooltip("The disabled BlackTape child GameObject under Wire.")]
        [SerializeField] private GameObject repairedTapeVisual;

        [Tooltip("The RoomElectricity component for the Warehouse.")]
        [SerializeField] private RoomElectricity warehouseElectricity;

        [Tooltip("Optional fallback GameObject to enable if RoomElectricity is not used (e.g. warehouse light).")]
        [SerializeField] private GameObject fallbackObjectToEnableAfterRepair;

        [Header("Prompts & Messages")]
        [SerializeField] private string inspectPrompt = "Inspect Damaged Wire";
        [SerializeField] private string repairPrompt = "Repair Wire";
        [SerializeField] private string missingTapeMessage = "The wire is damaged. I need something to insulate it.";
        [SerializeField] private string repairSuccessMessage = "That should hold. The power is back on.";

        [Header("Audio")]
        [SerializeField] private AudioClip repairSound;
        [Range(0f, 1f)] [SerializeField] private float soundVolume = 1f;

        private bool isRepaired = false;

        public string InteractionPrompt
        {
            get
            {
                if (isRepaired) return string.Empty;
                if (playerKeyItems == null) playerKeyItems = FindAnyObjectByType<PlayerKeyItems>();
                bool hasTape = playerKeyItems != null && playerKeyItems.HasBlackTape;
                return hasTape ? repairPrompt : inspectPrompt;
            }
        }

        public bool CanInteract()
        {
            return !isRepaired;
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

            if (warehouseElectricity == null)
            {
                warehouseElectricity = GetComponentInParent<RoomElectricity>();
            }

            if (repairedTapeVisual == null)
            {
                Transform tapeChild = transform.Find("BlackTape");
                if (tapeChild != null) repairedTapeVisual = tapeChild.gameObject;
            }

            // Ensure repair visual starts disabled only if not yet repaired
            if (!isRepaired && repairedTapeVisual != null)
            {
                repairedTapeVisual.SetActive(false);
            }
        }

        public void Interact()
        {
            if (!CanInteract()) return;

            // Ensure playerKeyItems reference is active
            if (playerKeyItems == null)
            {
                playerKeyItems = FindAnyObjectByType<PlayerKeyItems>();
            }

            // Record that player has inspected the damaged wire
            if (playerKeyItems != null)
            {
                playerKeyItems.MarkDamagedWireInspected();
            }

            bool hasTape = playerKeyItems != null && playerKeyItems.HasBlackTape;

            if (!hasTape)
            {
                // Player does not have tape -> show hint
                ShowFeedback(missingTapeMessage);
                return;
            }

            // Player has tape -> perform repair
            bool consumed = playerKeyItems.TryConsumeBlackTape();
            if (!consumed) return;

            // 1. Mark repaired immediately to prevent duplicate input
            isRepaired = true;

            // 2. Enable repaired tape visual
            if (repairedTapeVisual != null)
            {
                repairedTapeVisual.SetActive(true);
            }

            // 3. Restore warehouse electricity
            if (warehouseElectricity != null)
            {
                warehouseElectricity.RestoreElectricity();
            }
            else if (fallbackObjectToEnableAfterRepair != null)
            {
                fallbackObjectToEnableAfterRepair.SetActive(true);
            }

            // 4. Play sound
            if (repairSound != null)
            {
                AudioSource.PlayClipAtPoint(repairSound, transform.position, soundVolume);
            }

            // 5. Show success message
            ShowFeedback(repairSuccessMessage);
        }

        private void ShowFeedback(string msg)
        {
            if (messageUI != null)
            {
                messageUI.ShowMessage(msg);
            }
            else
            {
                PlayerMessageUI.Show(msg);
            }
        }
    }
}
