namespace Akshat.Interaction
{
    /// <summary>
    /// Generic interface for any world object that can be interacted with by the player.
    /// Objects define their own interaction logic (e.g. Doors, Stoves, Photos, Chests).
    /// </summary>
    public interface IInteractable
    {
        /// <summary>
        /// Executes the interaction logic for this specific object.
        /// </summary>
        void Interact();

        /// <summary>
        /// Prompt text displayed to the player (e.g. "Open", "Inspect", "Talk").
        /// </summary>
        string InteractionPrompt { get; }

        /// <summary>
        /// Returns whether this object can currently be interacted with.
        /// </summary>
        bool CanInteract();
    }
}
