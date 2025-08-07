// ScriptsArchive/DungeonData.cs

using Nytherion.Data.ScriptableObjects.Enemy;
using Nytherion.GamePlay.Dungeon;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Nytherion.Data.ScriptableObjects.Dungeon
{
    /// <summary>
    /// 미니맵에 표시될 방의 종류와 색상을 매핑하는 구조체입니다.
    /// </summary>
    [Serializable]
    public struct MinimapRoomColor
    {
        public RoomFirstDungeonGenerator.RoomType type;
        public Color color;
    }

    /// <summary>
    /// 장애물 프리팹과 그 크기(타일 단위) 정보를 담는 클래스입니다.
    /// </summary>
    [Serializable]
    public class ObstacleData
    {
        [Tooltip("장애물로 사용될 게임 오브젝트 프리팹입니다.")]
        public GameObject prefab;
        [Tooltip("장애물이 차지하는 공간의 크기 (타일 단위)입니다.")]
        public Vector2Int size = Vector2Int.one;
    }


    /// <summary>
    /// 절차적 던전 생성에 필요한 모든 설정값을 담고 있는 ScriptableObject입니다.
    /// 이 에셋을 통해 던전의 특성을 코드 수정 없이 에디터에서 쉽게 조절할 수 있습니다.
    /// </summary>
    [CreateAssetMenu(fileName = "DungeonGenerationData", menuName = "Procedural Generation/Dungeon Generation Data")]
    public class DungeonData : ScriptableObject
    {
        [Header("Room Settings")]
        [Tooltip("생성하고자 하는 방의 총 개수입니다.")]
        public int desiredNumberOfRooms = 15;
        [Tooltip("생성될 방의 최소 크기 (가로, 세로 타일 수)입니다.")]
        public Vector2Int minRoomSize = new Vector2Int(8, 8);
        [Tooltip("생성될 방의 최대 크기 (가로, 세로 타일 수)입니다.")]
        public Vector2Int maxRoomSize = new Vector2Int(15, 15);
        [Tooltip("방이 단순한 사각형이 아닌, 여러 사각형이 합쳐진 복합적인 형태로 생성될 확률입니다.")]
        [Range(0, 1)]
        public float compoundRoomChance = 0.7f;

        [Header("Special Room Settings")]
        [Tooltip("생성될 상점 방의 개수입니다.")]
        public int numberOfShopRooms = 1;
        [Tooltip("생성될 아이템 방의 개수입니다.")]
        public int numberOfItemRooms = 2;

        [Header("Prefabricated Rooms")]
        [Tooltip("보스 방으로 사용될 타일맵 프리팹입니다. 설정하지 않으면 일반 방처럼 생성됩니다.")]
        public Tilemap bossRoomPrefab;

        [Header("Obstacle Settings")]
        [Tooltip("방 안에 배치될 수 있는 장애물 종류의 목록입니다.")]
        public ObstacleData[] obstacles;
        [Tooltip("방 하나에 생성될 장애물의 최소 개수입니다.")]
        public int minObstaclesPerRoom = 1;
        [Tooltip("방 하나에 생성될 장애물의 최대 개수입니다.")]
        public int maxObstaclesPerRoom = 3;

        [Header("Minimap Settings")]
        [Tooltip("월드맵/미니맵에서 각 방 종류별로 표시될 색상 설정입니다.")]
        public MinimapRoomColor[] minimapRoomColors;

        [Header("Monster Settings")]
        [Tooltip("이 던전에서 스폰될 수 있는 몬스터 종류의 목록입니다.")]
        public List<EnemyData> dungeonMonsters;

        [Header("Wall Settings")]
        [Tooltip("생성될 벽의 두께 (타일 단위)입니다.")]
        [Range(1, 5)]
        public int wallThickness = 1;

        [Header("Generation Algorithm Settings")]
        [Tooltip("방과 방 사이의 간격을 조절하는 배율입니다. 값이 클수록 방들이 멀리 떨어집니다.")]
        [Range(1f, 2f)]
        public float roomSpacingMultiplier = 1.2f;
        [Tooltip("방 겹침을 해결하기 위해 위치를 조정하는 알고리즘의 반복 횟수입니다.")]
        public int placementIterations = 50;
        [Tooltip("요구 조건을 만족하는 던전 구조 생성에 실패했을 때, 재시도할 최대 횟수입니다.")]
        public int maxGenerationAttempts = 200;
    }
}