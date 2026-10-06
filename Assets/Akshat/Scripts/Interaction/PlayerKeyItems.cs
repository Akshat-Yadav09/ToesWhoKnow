// PlayerKeyItems.cs
// Stores persistent key item ownership on the Player GameObject across room transitions.

using UnityEngine;

namespace Akshat.Interaction
{
    /// <summary>
    /// Attached to Akshat_Player. Holds puzzle key items (such as Black Tape)
    /// that survive room culling and transitions.
    /// </summary>
    [AddComponentMenu("Akshat/Player Key Items")]
    public class PlayerKeyItems : MonoBehaviour
    {
        [Header("Key Items (Debug View)")]
        [SerializeField] private bool hasBlackTape = false;

        [Header("Puzzle Progression")]
        [SerializeField] private bool hasInspectedDamagedWire = false;

        public bool HasBlackTape => hasBlackTape;
        public bool HasInspectedDamagedWire => hasInspectedDamagedWire;

        /// <summary>
        /// Marks that the player has discovered / inspected the damaged wire in the warehouse.
        /// </summary>
        public void MarkDamagedWireInspected()
        {
            hasInspectedDamagedWire = true;
        }

        /// <summary>
        /// Adds black tape to player inventory. Returns false if already owned.
        /// </summary>
        public bool AddBlackTape()
        {
            if (hasBlackTape) return false;
            hasBlackTape = true;
            return true;
        }

        /// <summary>
        /// Consumes black tape if owned. Returns true if consumed, false if player doesn't have it.
        /// </summary>
        public bool TryConsumeBlackTape()
        {
            if (!hasBlackTape) return false;
            hasBlackTape = false;
            return true;
        }
    }
}
