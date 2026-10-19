using Nytherion.Core.Enums;

public interface IInteractable
{
    InteractableType Type { get; }
    void Interact();
}

/// <summary>거리 외에 개별 조건을 만족한 대상만 F키 선택에 포함합니다.</summary>
public interface IConditionalInteractable : IInteractable
{
    bool CanInteract(UnityEngine.Transform interactor);
}
