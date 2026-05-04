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
    /// �̴ϸʿ� ǥ�õ� ���� ������ ������ �����ϴ� ����ü�Դϴ�.
    /// </summary>
    [Serializable]
    public struct MinimapRoomColor
    {
        public RoomFirstDungeonGenerator.RoomType type;
        public Color color;
    }

    /// <summary>
    /// ��ֹ� �����հ� �� ũ��(Ÿ�� ����) ������ ��� Ŭ�����Դϴ�.
    /// </summary>
    [Serializable]
    public class ObstacleData
    {
        [Tooltip("��ֹ��� ���� ���� ������Ʈ �������Դϴ�.")]
        public GameObject prefab;
        [Tooltip("��ֹ��� �����ϴ� ������ ũ�� (Ÿ�� ����)�Դϴ�.")]
        public Vector2Int size = Vector2Int.one;
    }


    /// <summary>
    /// ������ ���� ������ �ʿ��� ��� �������� ��� �ִ� ScriptableObject�Դϴ�.
    /// �� ������ ���� ������ Ư���� �ڵ� ���� ���� �����Ϳ��� ���� ������ �� �ֽ��ϴ�.
    /// </summary>
    [CreateAssetMenu(fileName = "DungeonGenerationData", menuName = "Procedural Generation/Dungeon Generation Data")]
    public class DungeonData : ScriptableObject
    {
        [Header("Room Settings")]
        [Tooltip("�����ϰ��� �ϴ� ���� �� �����Դϴ�.")]
        public int desiredNumberOfRooms = 15;
        [Tooltip("������ ���� �ּ� ũ�� (����, ���� Ÿ�� ��)�Դϴ�.")]
        public Vector2Int minRoomSize = new Vector2Int(8, 8);
        [Tooltip("������ ���� �ִ� ũ�� (����, ���� Ÿ�� ��)�Դϴ�.")]
        public Vector2Int maxRoomSize = new Vector2Int(15, 15);
        [Tooltip("���� �ܼ��� �簢���� �ƴ�, ���� �簢���� ������ �������� ���·� ������ Ȯ���Դϴ�.")]
        [Range(0, 1)]
        public float compoundRoomChance = 0.7f;

        [Header("Special Room Settings")]
        [Tooltip("������ ���� ���� �����Դϴ�.")]
        public int numberOfShopRooms = 1;
        [Tooltip("������ ������ ���� �����Դϴ�.")]
        public int numberOfItemRooms = 2;

        [Header("Prefabricated Rooms")]
        [Tooltip("���� ������ ���� Ÿ�ϸ� �������Դϴ�. �������� ������ �Ϲ� ��ó�� �����˴ϴ�.")]
        public GameObject ShopRoomPrefab;
        public GameObject StartRoomPrefab;
        public GameObject ItemRoomPrefab;
        public GameObject bossRoomPrefab;

        [Tooltip("���� �濡 ������ ���� ������ �������Դϴ�.")]
        public EnemyData bossMonsterData;

        [Header("Obstacle Settings")]
        [Tooltip("�� �ȿ� ��ġ�� �� �ִ� ��ֹ� ������ ����Դϴ�.")]
        public ObstacleData[] obstacles;
        [Tooltip("�� �ϳ��� ������ ��ֹ��� �ּ� �����Դϴ�.")]
        public int minObstaclesPerRoom = 1;
        [Tooltip("�� �ϳ��� ������ ��ֹ��� �ִ� �����Դϴ�.")]
        public int maxObstaclesPerRoom = 3;

        [Header("Minimap Settings")]
        [Tooltip("�����/�̴ϸʿ��� �� �� �������� ǥ�õ� ���� �����Դϴ�.")]
        public MinimapRoomColor[] minimapRoomColors;

        [Header("Monster Settings")]
        [Tooltip("�� �������� ������ �� �ִ� ���� ������ ����Դϴ�.")]
        public List<EnemyData> dungeonMonsters;

        [Header("Wall Settings")]
        [Tooltip("������ ���� �β� (Ÿ�� ����)�Դϴ�.")]
        [Range(1, 5)]
        public int wallThickness = 1;

        [Header("Generation Algorithm Settings")]
        [Tooltip("��� �� ������ ������ �����ϴ� �����Դϴ�. ���� Ŭ���� ����� �ָ� �������ϴ�.")]
        [Range(1f, 2f)]
        public float roomSpacingMultiplier = 1.2f;
        [Tooltip("�� ��ħ�� �ذ��ϱ� ���� ��ġ�� �����ϴ� �˰������� �ݺ� Ƚ���Դϴ�.")]
        public int placementIterations = 50;
        [Tooltip("�䱸 ������ �����ϴ� ���� ���� ������ �������� ��, ��õ��� �ִ� Ƚ���Դϴ�.")]
        public int maxGenerationAttempts = 200;

        [Header("Portal Settings")]
        [Tooltip("���� óġ �� ������ �¸� ��Ż �������Դϴ�.")]
        public GameObject victoryPortalPrefab;
    }
}