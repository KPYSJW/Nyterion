using UnityEngine;
using UnityEngine.Events;

public abstract class UIPanelBase : MonoBehaviour
{
    [Header("Panel Control")]
    [SerializeField] protected CanvasGroup controlledCanvasGroup;

    public bool IsOpen { get; private set; }

    public UnityEvent OnPanelOpened;
    public UnityEvent OnPanelClosed;

    protected virtual void Awake()
    {
        if (controlledCanvasGroup == null)
        {
            controlledCanvasGroup = GetComponent<CanvasGroup>();
        }

        if (controlledCanvasGroup == null)
        {
            Debug.LogError("UIPanelBase: 제어할 'Controlled Canvas Group'이 없거나 할당되지 않았습니다!", this.gameObject);
            return;
        }

        IsOpen = false;
        controlledCanvasGroup.alpha = 0f;
        controlledCanvasGroup.interactable = false;
        controlledCanvasGroup.blocksRaycasts = false;
    }

    public virtual void Toggle()
    {
        if (controlledCanvasGroup == null)
        {
            Debug.LogError($"[UIPanelBase] {gameObject.name}의 ControlledCanvasGroup이 null이어서 Toggle 불가!");
            return;
        }

        if (IsOpen)
        {
            Close();
        }
        else
        {
            Open();
        }
    }

    public virtual void Open()
    {
        if (controlledCanvasGroup == null)
        {
            Debug.LogError($"[UIPanelBase] {gameObject.name}의 ControlledCanvasGroup이 null이어서 Open 불가!");
            return;
        }

        if (IsOpen)
        {
            return;
        }

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