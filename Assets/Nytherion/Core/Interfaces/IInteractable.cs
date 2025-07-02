using Nytherion.Core.Enums;

public interface IInteractable
{
    InteractableType Type { get; }
    void Interact();
}