using Nytherion.Data.ScriptableObjects.Enemy;
using Nytherion.GamePlay.Dungeon;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

[Serializable]
public struct MinimapRoomColor
{
    public RoomFirstDungeonGenerator.RoomType type;
    public Color color;
}


[CreateAssetMenu(fileName = "DungeonGenerationData", menuName = "Procedural Generation/Dungeon Generation Data")]
public class DungeonData : ScriptableObject
{
    [Header("Room Settings")]
    public int desiredNumberOfRooms = 15;
    public Vector2Int minRoomSize = new Vector2Int(8, 8);
    public Vector2Int maxRoomSize = new Vector2Int(15, 15);
    [Range(0, 1)]
    public float compoundRoomChance = 0.7f;

    [Header("Special Room Settings")]
    public int numberOfShopRooms = 1;
    public int numberOfItemRooms = 2;

    [Header("Prefabricated Rooms")]
    public Tilemap bossRoomPrefab;

    [Header("Obstacle Settings")]
    [Tooltip("Obstacle data array.")]
    public ObstacleData[] obstacles;
    [Tooltip("Minimum number of obstacles per room.")]
    public int minObstaclesPerRoom = 1;
    [Tooltip("Maximum number of obstacles per room.")]
    public int maxObstaclesPerRoom = 3;

    
    [Header("Minimap Settings")]
    [Tooltip("Minimap room colors array.")]
    public MinimapRoomColor[] minimapRoomColors;

    [Header("Monster Settings")] // [2]
    [Tooltip("이 던전에서 스폰될 몬스터 종류")] // [2]
    public List<EnemyData> dungeonMonsters; // [2]

    [Header("Wall Settings")]
    [Tooltip("벽의 두께를 설정합니다. (기본값: 1)")]
    [Range(1, 5)]
    public int wallThickness = 1;
}


[Serializable]
public class ObstacleData
{
    [Tooltip("Obstacle prefab.")]
    public GameObject prefab;
    [Tooltip("Obstacle size (in grid units).")]
    public Vector2Int size = Vector2Int.one;
}

