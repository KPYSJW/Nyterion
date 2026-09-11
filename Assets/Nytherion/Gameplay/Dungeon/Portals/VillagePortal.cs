using Nytherion.Core.Enums;
using Nytherion.Core.Managers;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using VContainer;

public class VillagePortal : MonoBehaviour, IInteractable
{
    public InteractableType Type => InteractableType.None;

    private StageManager stageManager;

   /* [Inject]
    public void Construct(SceneTransitionManager sceneTransitionManager)
    {
        _sceneTransitionManager = sceneTransitionManager;
        Debug.Log($"[VillagePortal] SceneTransitionManager injected: {_sceneTransitionManager != null}");
    }*/

    public void Awake()
    {
        stageManager = FindObjectOfType<StageManager>();
    }
    public void Interact()
    {
       

        if (stageManager != null)
        {
            Debug.Log($"[VillagePortal] Calling LoadScene(GameScene)");
            stageManager.AdvanceToNextStage(); 
        }
        else
        {
            Debug.LogError("[VillagePortal] stageManager is null!");
        }
    }
}
