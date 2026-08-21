using UnityEngine;
using UnityEngine.Events;
using VContainer;
using Nytherion.UI;

public abstract class UIPanelBase : MonoBehaviour
{
    [Header("Panel Control")]
    [SerializeField] protected CanvasGroup controlledCanvasGroup;

    protected GlobalUIManager globalUIManager;
    public bool IsOpen { get; private set; }

    public UnityEvent OnPanelOpened;
    public UnityEvent OnPanelClosed;

    [Inject]
    public void ConstructParent(GlobalUIManager globalUIManager)
    {
        this.globalUIManager = globalUIManager;
    }

    protected virtual void Awake()
    // ... (이하 동일) ...
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
            Open(true);
        }
    }

    public virtual void Open(bool closeOthers = true)
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

        // --- GlobalUIManager 연동 로직 ---
        if (globalUIManager != null && closeOthers)
        {
            globalUIManager.RegisterOpenedPanel(this);
        }
        // ------------------------------------

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

        // --- GlobalUIManager 연동 로직 ---
        if (globalUIManager != null)
        {
            globalUIManager.RegisterClosedPanel(this);
        }
        // ------------------------------------

        IsOpen = false;

        controlledCanvasGroup.alpha = 0f;
        controlledCanvasGroup.interactable = false;
        controlledCanvasGroup.blocksRaycasts = false;

        OnPanelClosed?.Invoke();
        OnPanelStateChanged(false);
    }

    protected virtual void OnPanelStateChanged(bool isOpen) { }
}