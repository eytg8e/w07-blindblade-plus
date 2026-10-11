/// <summary>Defines the prompt, availability, and action for a player interaction target.</summary>
public interface IInteractable
{
    bool CanInteract { get; }
    string InteractionPrompt { get; }

    /// <summary>Performs the target's interaction without input parameters and updates its own state.</summary>
    void Interact();
}
