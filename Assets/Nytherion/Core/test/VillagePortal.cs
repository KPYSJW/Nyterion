using Nytherion.Core.Enums;
using Nytherion.Core.Managers;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Zenject;

public class VillagePortal : MonoBehaviour
{
    public InteractableType Type => InteractableType.None; // 특별한 타입이 필요 없다면 None으로 둬도 돼.

    private SceneTransitionManager _sceneTransitionManager;

    [Inject]
    public void Construct(SceneTransitionManager sceneTransitionManager)
    {
        _sceneTransitionManager = sceneTransitionManager;
    }

    public void Interact()
    {
        Debug.Log("마을 포탈과 상호작용! 다음 스테이지가 있는 GameScene으로 이동합니다.");

        if (_sceneTransitionManager != null)
        {
            // 이 포탈의 유일한 임무! 그냥 GameScene을 로드하는 것!
            _sceneTransitionManager.LoadScene("GameScene");
        }
        else
        {
            Debug.LogError("SceneTransitionManager를 찾을 수 없습니다!");
        }
    }
}
