using Nytherion.Core.Managers;
using Nytherion.GamePlay.Dungeon;
using Nytherion.UI.Map;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer;
using VContainer.Unity;

public class GameSceneInitializer : IStartable
{
    private readonly StageManager _stageManager;
    private readonly DungeonManager _dungeonManager;
    private readonly WorldmapController _worldmapController;
    private readonly MinimapTileGenerator _minimapGenerator;
   
    public GameSceneInitializer(StageManager stageManager, DungeonManager dungeonManager, WorldmapController worldmapController, MinimapTileGenerator minimapGenerator)
    {
        _stageManager = stageManager;
        _dungeonManager = dungeonManager;
        _worldmapController = worldmapController;
        _minimapGenerator = minimapGenerator;
    }

    // 모든 준비가 끝나면 이 메서드가 딱 한 번 실행돼.
    public void Start()
    {
        if (SceneManager.GetActiveScene().name != "GameScene") return;

      
        _stageManager.SetDungeonManager(_dungeonManager);
        _dungeonManager.SetStageManager(_stageManager);

      
        if (_dungeonManager != null && _dungeonManager.roomFirstDungeonGenerator != null)
        {
           
            _dungeonManager.SetControllers(_worldmapController, _minimapGenerator);
            _dungeonManager.roomFirstDungeonGenerator.SetControllers(_worldmapController, _minimapGenerator);
        }
       
      
        _dungeonManager.StartDungeonGeneration();
    }
}