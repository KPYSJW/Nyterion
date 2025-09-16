using Nytherion.Core.Enums;
using Nytherion.Core.Managers;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using VContainer;

public class VillagePortal : MonoBehaviour
{
    public InteractableType Type => InteractableType.None; // Ư���� Ÿ���� �ʿ� ���ٸ� None���� �ֵ� ��.

    private SceneTransitionManager _sceneTransitionManager;

    [Inject]
    public void Construct(SceneTransitionManager sceneTransitionManager)
    {
        _sceneTransitionManager = sceneTransitionManager;
        Debug.Log($"[VillagePortal] SceneTransitionManager injected: {_sceneTransitionManager != null}");
    }

    public void Interact()
    {
        Debug.Log($"[VillagePortal] Interact() called. SceneTransitionManager: {_sceneTransitionManager != null}");

        if (_sceneTransitionManager != null)
        {
            Debug.Log($"[VillagePortal] Calling LoadScene(GameScene)");
            _sceneTransitionManager.LoadScene("GameScene");
        }
        else
        {
            Debug.LogError("[VillagePortal] SceneTransitionManager is null!");
        }
    }
}
