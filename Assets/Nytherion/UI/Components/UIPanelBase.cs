using UnityEngine;
using UnityEngine.Events;

public abstract class UIPanelBase : MonoBehaviour
{
    [Header("Panel Control")]
    [SerializeField] protected CanvasGroup controlledCanvasGroup;

    public bool IsOpen;

    public UnityEvent OnPanelOpened;
    public UnityEvent OnPanelClosed;

    protected virtual void Awake()
    {
        if (controlledCanvasGroup == null)
        {
            Debug.LogError("UIPanelBase: 제어할 'Controlled Canvas Group'이 할당되지 않았습니다!", this.gameObject);
            return;
        }

        controlledCanvasGroup.alpha = 0f;
        controlledCanvasGroup.interactable = false;
        controlledCanvasGroup.blocksRaycasts = false;
        IsOpen = false;
    }

    public virtual void Toggle()
    {
        if (controlledCanvasGroup == null) return;
        if (IsOpen) Close();
        else Open();
    }

    public virtual void Open()
    {
        if (controlledCanvasGroup == null || IsOpen) return;
        IsOpen = true;

        controlledCanvasGroup.alpha = 1f;
        controlledCanvasGroup.interactable = true;
        controlledCanvasGroup.blocksRaycasts = true;

        OnPanelOpened?.Invoke();
        OnPanelStateChanged(true);
    }

    public virtual void Close()
    {
        if (controlledCanvasGroup == null || !IsOpen)
        {
            Debug.LogWarning(this.name + ": Close() 호출되었으나, 이미 닫혀있거나 CanvasGroup이 없음.");
            return;
        }
        IsOpen = false;

        controlledCanvasGroup.alpha = 0f;
        controlledCanvasGroup.interactable = false;
        controlledCanvasGroup.blocksRaycasts = false;

        OnPanelClosed?.Invoke();
        OnPanelStateChanged(false);
    }

    protected virtual void OnPanelStateChanged(bool isOpen) { }
}