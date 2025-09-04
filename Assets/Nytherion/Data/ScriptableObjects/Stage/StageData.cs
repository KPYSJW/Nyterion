using Nytherion.Data.ScriptableObjects.Dungeon;
using Nytherion.Data.ScriptableObjects.Enemy;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Nytherion.Data.ScriptableObjects.Stage
{
    [CreateAssetMenu(fileName = "NewStageData", menuName = "Data/Stage")]
    public class StageData : ScriptableObject
    {
        public string stageName;
        public int chapterNumber;
        public int stageNumber;
        public bool isBossStage;
        public List<EnemyData> enemyList;
        public List<Transform> fixedSpawnPoints;
        public bool useRandomSpawn = true;
        public int enemyCount;
        public Sprite stageBackground;

        [Header("스테이지 흐름 설정")]
        [Tooltip("이 스테이지에서 사용할 던전 생성 데이터입니다.")]
        public DungeonData dungeonData; // 이 스테이지는 어떤 던전 지도를 쓸 것인가?

        [Tooltip("이 스테이지를 클리어하면 넘어갈 다음 스테이지 데이터입니다.")]
        public StageData nextStageData; // 이 스테이지를 깨면 다음 계획은 무엇인가?

        [Tooltip("이 스테이지를 클리어하면 로드할 씬의 이름입니다. (예: Village)")]
        public string victorySceneName = "GameScene"; // 클리어 후 '마을' 같은 특별한 곳으로 가는가?
    }
}
