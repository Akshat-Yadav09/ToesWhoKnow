// TopDownAreaTrigger.cs
// Place on a 2D Trigger Collider to seamlessly switch the player into Top-Down mode
// when walking into an open room or area without needing a door.

using UnityEngine;
using Unity.Cinemachine;

namespace Akshat.RoomSystem
{
    [RequireComponent(typeof(Collider2D))]
    [AddComponentMenu("Akshat/Top-Down Area Trigger")]
    public class TopDownAreaTrigger : MonoBehaviour
    {
        [Header("Mode Configuration")]
        [Tooltip("The movement mode to activate when player enters this area.")]
        [SerializeField] private MovementMode enterMode = MovementMode.TopDown;

        [Tooltip("The movement mode to restore when player exits this area.")]
        [SerializeField] private MovementMode exitMode = MovementMode.Hallway;

        [Header("Optional Camera")]
        [Tooltip("Optional CinemachineCamera to prioritize when inside this area.")]
        [SerializeField] private CinemachineCamera areaCamera;

        [Tooltip("Priority to assign to areaCamera when player is inside.")]
        [SerializeField] private int activePriority = 20;

        [Tooltip("Priority when player leaves the area.")]
        [SerializeField] private int inactivePriority = 10;

        private Collider2D triggerCollider;

        private void Awake()
        {
            triggerCollider = GetComponent<Collider2D>();
            if (triggerCollider != null)
            {
                triggerCollider.isTrigger = true;
            }
        }

        private void OnTriggerEnter2D(Collider2D collision)
        {
            if (!collision.CompareTag("Player")) return;

            PlayerMovement movement = collision.GetComponent<PlayerMovement>();
            if (movement != null)
            {
                movement.SetMode(enterMode);
            }

            if (areaCamera != null)
            {
                areaCamera.Priority = activePriority;
            }
        }

        private void OnTriggerExit2D(Collider2D collision)
        {
            if (!collision.CompareTag("Player")) return;

            PlayerMovement movement = collision.GetComponent<PlayerMovement>();
            if (movement != null)
            {
                movement.SetMode(exitMode);
            }

            if (areaCamera != null)
            {
                areaCamera.Priority = inactivePriority;
            }
        }
    }
}
