using Nytherion.Data.ScriptableObjects.Dungeon;
using Nytherion.Data.ScriptableObjects.Enemy;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Nytherion.Core.Utils;

namespace Nytherion.Data.ScriptableObjects.Stage
{
    [CreateAssetMenu(fileName = "NewStageData", menuName = "Data/Stage")]
    public class StageData : ScriptableObject
    {
        public string stageName;
        public string DisplayName => LocalizationText.Get(
            LocalizationTables.World,
            LocalizationKeys.StageName(name),
            stageName,
            stageName);
        public int chapterNumber;
        public int stageNumber;
        public bool isBossStage;
        public List<EnemyData> enemyList;
        public List<Transform> fixedSpawnPoints;
        public bool useRandomSpawn = true;
        public int enemyCount;
        public Sprite stageBackground;

        [Header("스테이지 흐름 설정")]
        [Tooltip("이 스테이지에서 사용할 던전 데이터입니다.")]
        public DungeonData dungeonData; 

        [Tooltip("이 스테이지를 클리어하면 넘어갈 다음 스테이지 데이터입니다.")]
        public StageData nextStageData;

        [Tooltip("이 스테이지를 클리어하면 로드할 씬 이름입니다. (예: Village)")]
        public string victorySceneName = "GameScene"; 
    }
}
