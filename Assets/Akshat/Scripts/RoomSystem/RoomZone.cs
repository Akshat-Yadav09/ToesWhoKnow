using UnityEngine;
using Unity.Cinemachine;
using Akshat;

namespace Akshat.RoomSystem
{
    /// <summary>
    /// Represents a self-contained room or hallway area.
    /// Holds the camera, movement mode, and optional ambience for this zone.
    /// </summary>
    [SelectionBase]
    [DisallowMultipleComponent]
    public class RoomZone : MonoBehaviour
    {
        [Header("Room Identity")]
        [Tooltip("Descriptive name for this room (e.g. 'Living Room', 'Hallway 1', 'Basement').")]
        [SerializeField] private string roomName = "New Room";

        [Header("Perspective & Movement")]
        [Tooltip("The movement mode the player uses inside this room.")]
        [SerializeField] private MovementMode movementMode = MovementMode.TopDown;

        [Header("Camera Configuration")]
        [Tooltip("The CinemachineCamera assigned to film this room.\nAuto-located in children if unassigned.")]
        [SerializeField] private CinemachineCamera roomCamera;

        [Header("Optional Room Ambience")]
        [Tooltip("Optional background music or ambient audio clip for this room.")]
        [SerializeField] private AudioClip ambientAudio;

        public string RoomName => roomName;
        public MovementMode MovementMode => movementMode;
        public CinemachineCamera RoomCamera => roomCamera;
        public AudioClip AmbientAudio => ambientAudio;

        private void Reset()
        {
            roomName = gameObject.name;
            if (roomCamera == null)
            {
                roomCamera = GetComponentInChildren<CinemachineCamera>();
            }
        }

        private void OnValidate()
        {
            if (roomCamera == null)
            {
                roomCamera = GetComponentInChildren<CinemachineCamera>();
            }
        }

        private void Awake()
        {
            if (roomCamera == null)
            {
                roomCamera = GetComponentInChildren<CinemachineCamera>();
            }
        }
    }
}
