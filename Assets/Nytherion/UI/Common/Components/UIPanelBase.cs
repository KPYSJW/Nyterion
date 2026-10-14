using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using VContainer;
using Nytherion.UI;
using Nytherion.Core.Managers;
using Nytherion.Core.Enums;

public abstract class UIPanelBase : MonoBehaviour
{
    private static readonly HashSet<UIPanelBase> OpenPanels = new HashSet<UIPanelBase>();

    [Header("Panel Control")]
    [SerializeField] protected CanvasGroup controlledCanvasGroup;
    [Tooltip("패널을 열 때 공통 UI 효과음을 재생합니다.")]
    [SerializeField] private bool playOpenSound;
    [Tooltip("이 패널의 닫기 효과음입니다. 미할당 시 공통 닫기 효과음을 사용합니다.")]
    [SerializeField] private AudioClip closeSoundOverride;

    protected GlobalUIManager globalUIManager;
    protected AudioManager audioManager;
    public bool IsOpen { get; private set; }
    protected virtual bool UsesBookOpenSound => false;

    public UnityEvent OnPanelOpened;
    public UnityEvent OnPanelClosed;

    public static bool IsAnyPanelOpen
    {
        get
        {
            OpenPanels.RemoveWhere(panel => panel == null || !panel.isActiveAndEnabled || !panel.IsOpen);
            return OpenPanels.Count > 0;
        }
    }

    [Inject]
    public void ConstructParent(GlobalUIManager globalUIManager, AudioManager audioManager)
    {
        this.globalUIManager = globalUIManager;
        this.audioManager = audioManager;
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
        OpenPanels.Add(this);

        controlledCanvasGroup.alpha = 1f;
        controlledCanvasGroup.interactable = true;
        controlledCanvasGroup.blocksRaycasts = true;

        if (playOpenSound && UsesBookOpenSound) audioManager?.PlayUIOpenSound();
        else audioManager?.RequestUISound(UISoundType.Open);
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
        OpenPanels.Remove(this);

        controlledCanvasGroup.alpha = 0f;
        controlledCanvasGroup.interactable = false;
        controlledCanvasGroup.blocksRaycasts = false;

        audioManager?.RequestUISound(UISoundType.Close, closeSoundOverride);
        OnPanelClosed?.Invoke();
        OnPanelStateChanged(false);
    }

    protected virtual void OnPanelStateChanged(bool isOpen) { }

    public void PlayUISound(UISoundType sound) => audioManager?.RequestUISound(sound);
}
