using Nytherion.Core.Managers;
using Nytherion.GamePlay.Dungeon;
using Nytherion.UI.Map;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer.Unity;

public class GameSceneInitializer : IStartable
{
    private readonly StageManager stageManager;
    private readonly DungeonManager dungeonManager;
    private readonly WorldmapController worldmapController;
    private readonly MinimapTileGenerator minimapGenerator;
    private readonly SaveLoadManager saveLoadManager;

    public GameSceneInitializer(
        StageManager stageManager,
        DungeonManager dungeonManager,
        WorldmapController worldmapController,
        MinimapTileGenerator minimapGenerator,
        SaveLoadManager saveLoadManager)
    {
        this.stageManager = stageManager;
        this.dungeonManager = dungeonManager;
        this.worldmapController = worldmapController;
        this.minimapGenerator = minimapGenerator;
        this.saveLoadManager = saveLoadManager;
    }

    public void Start()
    {
        if (SceneManager.GetActiveScene().name != "GameScene") return;

        // 데이터 스코프의 초기화가 끝난 뒤 로드를 확정하고, 실패한 부분 상태로 던전을 시작하지 않는다.
        saveLoadManager.LoadGameIfNeeded();
        if (!saveLoadManager.HasLoadedData || saveLoadManager.IsSaveBlocked)
        {
            Debug.LogError($"[GameSceneInitializer] 저장 데이터를 준비하지 못해 던전 시작을 중단합니다: {saveLoadManager.LastError}");
            return;
        }

        stageManager.SetDungeonManager(dungeonManager);
        dungeonManager.SetStageManager(stageManager);

        if (dungeonManager != null && dungeonManager.roomFirstDungeonGenerator != null)
        {
            dungeonManager.SetControllers(worldmapController, minimapGenerator);
            dungeonManager.roomFirstDungeonGenerator.SetControllers(worldmapController, minimapGenerator);
        }
        dungeonManager.StartDungeonGeneration();
    }
}
