using Nytherion.Core.Managers;
using Nytherion.GamePlay.Dungeon;
using Nytherion.UI.Map;
using Nytherion.UI.Inventory;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer;
using VContainer.Unity;

public class GameSceneInitializer : IStartable
{
    private readonly StageManager stageManager;
    private readonly DungeonManager dungeonManager;
    private readonly WorldmapController worldmapController;
    private readonly MinimapTileGenerator minimapGenerator;
    private readonly QuickSlotManager quickSlotManager;
    private readonly SaveLoadManager saveLoadManager;

    public GameSceneInitializer(
        StageManager stageManager,
        DungeonManager dungeonManager,
        WorldmapController worldmapController,
        MinimapTileGenerator minimapGenerator,
        QuickSlotManager quickSlotManager,
        SaveLoadManager saveLoadManager)
    {
        this.stageManager = stageManager;
        this.dungeonManager = dungeonManager;
        this.worldmapController = worldmapController;
        this.minimapGenerator = minimapGenerator;
        this.quickSlotManager = quickSlotManager;
        this.saveLoadManager = saveLoadManager;
    }

    public void Start()
    {
        if (SceneManager.GetActiveScene().name != "GameScene") return;

        stageManager.SetDungeonManager(dungeonManager);
        dungeonManager.SetStageManager(stageManager);

        if (dungeonManager != null && dungeonManager.roomFirstDungeonGenerator != null)
        {
            dungeonManager.SetControllers(worldmapController, minimapGenerator);
            dungeonManager.roomFirstDungeonGenerator.SetControllers(worldmapController, minimapGenerator);
        }
         InitializeGameData();
        dungeonManager.StartDungeonGeneration();

        // SaveLoadManager를 통한 정상적인 로드 (강제로드가 아님)
       
    }

    private void InitializeGameData()
    {

        if (saveLoadManager != null)
        {
            saveLoadManager.LoadGameIfNeeded();
        }
        else
        {
            Debug.LogError("[GameSceneInitializer] SaveLoadManager가 null입니다");
        }
    }
}