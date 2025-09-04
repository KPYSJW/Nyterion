using Nytherion.Core.Enums;
using Nytherion.Core.Managers;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BossPortal : MonoBehaviour,IInteractable
{
    public InteractableType Type => InteractableType.BossPortal;
    private StageManager _stageManager;
    public void Initialize(StageManager manager)
    {
        _stageManager = manager;
    }

    public void Interact()
    {
        Debug.Log("승리 포탈과 상호작용! 다음 스테이지로 이동합니다.");
        if (_stageManager != null)
        {
            _stageManager.AdvanceToNextStage();
            Destroy(gameObject);
        }
        else
        {
            Debug.LogError("StageManager가 BossPortal에 연결되지 않았습니다!");
        }
    }
}
