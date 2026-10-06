// LightSwitchInteractable.cs
// Interacts with room light switches, tied to RoomElectricity.

using UnityEngine;
using Akshat.RoomSystem;

namespace Akshat.Interaction
{
    /// <summary>
    /// Implements IInteractable for light switches in rooms.
    /// Synchronizes with RoomElectricity to ensure lights only shine when power is available.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    [AddComponentMenu("Akshat/Light Switch Interactable")]
    public class LightSwitchInteractable : MonoBehaviour, IInteractable
    {
        [Header("Electricity & Lights")]
        [Tooltip("The RoomElectricity component governing power in this room.")]
        [SerializeField] private RoomElectricity roomElectricity;

        [Tooltip("The light GameObjects (e.g. Light2D, bulb visuals) controlled by this switch.")]
        [SerializeField] private GameObject[] controlledLightObjects;

        [Tooltip("Whether the switch starts in the ON position.")]
        [SerializeField] private bool startsOn = true;

        [Header("Optional Clues & Narrative")]
        [Tooltip("Optional hidden text or clue object revealed on the first powerless attempt.")]
        [SerializeField] private GameObject hiddenWritingObject;

        [Tooltip("Message UI reference. Auto-located if unassigned.")]
        [SerializeField] private PlayerMessageUI messageUI;

        [SerializeField] private string noElectricityMessage = "There is no electricity in this room.";
        [SerializeField] private string repeatedNoPowerMessage = "The switch does nothing.";

        [Header("Audio")]
        [SerializeField] private AudioClip switchSound;
        [Range(0f, 1f)] [SerializeField] private float soundVolume = 0.8f;

        private bool isOn;
        private bool hasTriggeredNoPowerClue = false;

        public string InteractionPrompt
        {
            get
            {
                if (roomElectricity != null && !roomElectricity.HasElectricity)
                {
                    return "Try Light Switch";
                }
                return isOn ? "Turn Light Off" : "Turn Light On";
            }
        }

        public bool CanInteract() => true;

        private void Awake()
        {
            if (roomElectricity == null)
            {
                roomElectricity = GetComponentInParent<RoomElectricity>();
            }

            if (messageUI == null)
            {
                messageUI = FindAnyObjectByType<PlayerMessageUI>();
            }

            isOn = startsOn;
            ApplyLightState();
        }

        private void OnEnable()
        {
            if (roomElectricity != null)
            {
                roomElectricity.ElectricityChanged += HandleElectricityChanged;
            }
            ApplyLightState();
        }

        private void OnDisable()
        {
            if (roomElectricity != null)
            {
                roomElectricity.ElectricityChanged -= HandleElectricityChanged;
            }
        }

        private void HandleElectricityChanged(bool hasPower)
        {
            ApplyLightState();
        }

        private void ApplyLightState()
        {
            bool hasPower = roomElectricity == null || roomElectricity.HasElectricity;
            bool shouldBeLit = isOn && hasPower;

            if (controlledLightObjects != null)
            {
                for (int i = 0; i < controlledLightObjects.Length; i++)
                {
                    if (controlledLightObjects[i] != null)
                    {
                        controlledLightObjects[i].SetActive(shouldBeLit);
                    }
                }
            }
        }

        public void Interact()
        {
            PlaySwitchSound();

            bool hasPower = roomElectricity == null || roomElectricity.HasElectricity;

            if (!hasPower)
            {
                // No power
                if (!hasTriggeredNoPowerClue)
                {
                    hasTriggeredNoPowerClue = true;
                    if (hiddenWritingObject != null) hiddenWritingObject.SetActive(true);
                    ShowFeedback(noElectricityMessage);
                }
                else
                {
                    ShowFeedback(repeatedNoPowerMessage);
                }
                return;
            }

            // Power is active -> toggle switch
            isOn = !isOn;
            ApplyLightState();
        }

        private void PlaySwitchSound()
        {
            if (switchSound != null)
            {
                AudioSource.PlayClipAtPoint(switchSound, transform.position, soundVolume);
            }
        }

        private void ShowFeedback(string msg)
        {
            if (messageUI != null) messageUI.ShowMessage(msg);
            else PlayerMessageUI.Show(msg);
        }
    }
}
