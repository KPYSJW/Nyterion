using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Nytherion.Data
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
    }
}
