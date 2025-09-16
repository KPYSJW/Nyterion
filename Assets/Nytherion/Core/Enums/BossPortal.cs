using Nytherion.Core.Enums;
using Nytherion.Core.Managers;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using VContainer;

public class BossPortal : MonoBehaviour, IInteractable
{
    public InteractableType Type => InteractableType.BossPortal;
    public bool IsInteractable { get; set; } = true;

    private StageManager stageManager;

    [Inject]
    public void Construct(StageManager stageManager)
    {
        this.stageManager = stageManager;
    }

    // 레거시 호환을 위한 메서드
    public void Initialize(StageManager manager)
    {
        this.stageManager = manager;
    }

    public void Interact()
    {
        if (!IsInteractable) return;
        Debug.Log("�¸� ��Ż�� ��ȣ�ۿ�! ���� ���������� �̵��մϴ�.");
        // VContainer 주입이 없으면 직접 찾기
        if (stageManager == null)
        {
            stageManager = FindObjectOfType<StageManager>();
        }

        if (stageManager != null)
        {
            stageManager.AdvanceToNextStage();
            Destroy(gameObject);
        }
        else
        {
            Debug.LogError("StageManager�� BossPortal�� ������� �ʾҽ��ϴ�!");
        }
    }
}
