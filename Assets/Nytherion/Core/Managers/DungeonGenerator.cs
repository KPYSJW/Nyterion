using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using Nytherion.GamePlay.Dungeon;

namespace Nytherion.Core.Managers
{
    public class DungeonGenerator : RoomGenerator
    {
        [Header("Ÿ�ϸ�")]
        public Tilemap floorTilemap;
        public Tilemap wallTilemap;

        [Header("Ÿ�� ���̽�")]
        public TileBase floorTile;
        public TileBase wallTile;
  
        [Header("���� ����")]
        public int dungeonWidth;
        public int dungeonHeight;
        public int minRoomWidth;
        public int minRoomHeight;
        public int randomWalkLength;
        public int randomWalkItertions;
        HashSet<Vector3Int> aa;
        void Start() 
        {
            GenerateDungeon();
        }
        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                //DrawWall(aa);
            }
        }
        void GenerateDungeon()
        {
            BoundsInt startRect= new BoundsInt(Vector3Int.zero,new Vector3Int(dungeonWidth, dungeonHeight,1));
            List<BoundsInt> rooms=BinarySpacePartitioning(minRoomWidth, minRoomHeight, startRect);

            HashSet<Vector3Int> floorPosition= RandomWalkGenerate(rooms,randomWalkItertions,randomWalkLength);
            Kruskal kruskal= new Kruskal();
            List<TwoPos> corridors = kruskal.GetSortedTwoPositions(rooms.Count, rooms);

            foreach(TwoPos corridor in corridors)
            {
                HashSet<Vector3Int> path = Kruskal.Connect(corridor);
                floorPosition.UnionWith(path);
            }
            aa = floorPosition;
            DrawFloor(floorPosition);
            //DrawWall(floorPosition);
        }

        void DrawFloor(HashSet<Vector3Int> floorPositions)
        {
            foreach(Vector3Int pos in floorPositions)
            {
                floorTilemap.SetTile(pos, floorTile);
            }
        }

       /* void DrawWall(HashSet<Vector3Int> floorPositions)
        {
            foreach (Vector3Int pos in floorPositions)
            {
               foreach(Vector3Int dir in Direction2D.eightDir)
                {
                    Vector3Int neighbor = pos + dir;
                    if(!floorPositions.Contains(neighbor))
                    {
                        wallTilemap.SetTile(neighbor, wallTile);
                    }
                }
            }
        }*/
    }
}

