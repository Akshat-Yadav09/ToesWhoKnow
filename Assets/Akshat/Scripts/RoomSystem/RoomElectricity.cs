// RoomElectricity.cs
// Manages power state for a specific RoomZone.

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Akshat.RoomSystem
{
    /// <summary>
    /// Tracks and controls whether a room currently has electrical power.
    /// Acts as the single source of truth for lights and switches in the room.
    /// </summary>
    [AddComponentMenu("Akshat/Room Electricity")]
    public class RoomElectricity : MonoBehaviour
    {
        [Header("Electricity Configuration")]
        [Tooltip("True for normal rooms; false for rooms that start without power (e.g. Warehouse).")]
        [SerializeField] private bool startsWithElectricity = true;

        [Header("Room Lights")]
        [Tooltip("Specific light GameObjects to toggle with power. If empty, automatically finds lights under this room.")]
        [SerializeField] private GameObject[] roomLights;

        [Tooltip("Automatically finds Light2D and 'lighting' objects in children if roomLights is empty.")]
        [SerializeField] private bool autoFindLightsInChildren = true;

        private bool hasElectricity;
        private readonly List<GameObject> activeControlledLights = new List<GameObject>();
        private readonly List<Light2D> activeLight2DComponents = new List<Light2D>();
        private bool isInitialized = false;

        public bool HasElectricity => hasElectricity;
        public event Action<bool> ElectricityChanged;

        private void Awake()
        {
            if (!isInitialized)
            {
                hasElectricity = startsWithElectricity;
                isInitialized = true;
            }
            CollectRoomLights();
            ApplyLightState();
        }

        private void OnEnable()
        {
            // Reapply state when room becomes active after a transition
            ApplyLightState();
        }

        private void CollectRoomLights()
        {
            activeControlledLights.Clear();
            activeLight2DComponents.Clear();

            if (roomLights != null && roomLights.Length > 0)
            {
                for (int i = 0; i < roomLights.Length; i++)
                {
                    if (roomLights[i] != null)
                    {
                        activeControlledLights.Add(roomLights[i]);
                    }
                }
            }
            else if (autoFindLightsInChildren)
            {
                // Find all Light2D components under this room
                var foundLights = GetComponentsInChildren<Light2D>(true);
                for (int i = 0; i < foundLights.Length; i++)
                {
                    if (foundLights[i] != null)
                    {
                        activeLight2DComponents.Add(foundLights[i]);
                    }
                }

                // Also find any GameObject named 'lighting'
                var allTransforms = GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < allTransforms.Length; i++)
                {
                    if (allTransforms[i] != null && allTransforms[i].gameObject.name.Equals("lighting", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!activeControlledLights.Contains(allTransforms[i].gameObject))
                        {
                            activeControlledLights.Add(allTransforms[i].gameObject);
                        }
                    }
                }
            }
        }

        public void RestoreElectricity()
        {
            SetElectricity(true);
        }

        public void CutElectricity()
        {
            SetElectricity(false);
        }

        public void SetElectricity(bool value)
        {
            if (hasElectricity == value && isInitialized) return;
            hasElectricity = value;
            isInitialized = true;
            ApplyLightState();
            ElectricityChanged?.Invoke(hasElectricity);
        }

        private void ApplyLightState()
        {
            for (int i = 0; i < activeControlledLights.Count; i++)
            {
                if (activeControlledLights[i] != null)
                {
                    activeControlledLights[i].SetActive(hasElectricity);
                }
            }

            for (int i = 0; i < activeLight2DComponents.Count; i++)
            {
                if (activeLight2DComponents[i] != null)
                {
                    activeLight2DComponents[i].enabled = hasElectricity;
                }
            }
        }

        [ContextMenu("Toggle Electricity")]
        private void ToggleElectricity()
        {
            SetElectricity(!hasElectricity);
        }
    }
}
