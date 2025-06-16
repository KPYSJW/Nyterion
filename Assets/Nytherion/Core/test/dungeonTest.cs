using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class dungeonTest : AbstractDungeonGenertor
{

    [SerializeField]
    protected DungeonData dungeonData;




    protected override void RunProceduralGeneration()
    {
        HashSet<Vector2Int> floorPositions = RunRandomWalk(dungeonData,startPosition);
        tilemapVisualizer.Clear();
        tilemapVisualizer.PaintFloorTiles(floorPositions);
        WallGenerator.CreateWalls(floorPositions, tilemapVisualizer);
    }

    protected HashSet<Vector2Int> RunRandomWalk(DungeonData dungeonData,Vector2Int position)
    {
        var currentPosition = position;
        HashSet<Vector2Int> floorPositions= new HashSet<Vector2Int>();
        for(int i = 0; i < dungeonData.iterations; i++)
        {
            var path = ProceduralGenerationAlgorithms.SimpleRandomWalk(currentPosition, dungeonData.walkLength);
            floorPositions.UnionWith(path);
            if(dungeonData.startRandomlyEachIteration)
                currentPosition=floorPositions.ElementAt(Random.Range(0,floorPositions.Count));
        }

        return floorPositions;
    }

}

public static class ProceduralGenerationAlgorithms
{
    public static HashSet<Vector2Int> SimpleRandomWalk(Vector2Int startPosition, int walkLength)
    {
        HashSet<Vector2Int> path = new HashSet<Vector2Int>();

        path.Add(startPosition);
        Vector2Int previousposition =startPosition;

        for (int i = 0; i < walkLength; i++) 
        {
            Vector2Int newPositon = previousposition + Direction2D.GetRandomCardinalDirection();
            path.Add(newPositon);
            previousposition = newPositon;
        }
        return path;

    }

    public static List<Vector2Int> RandomWalkCorridor(Vector2Int startPosition, int corridorLength)
    {
        List<Vector2Int> corridor=  new List<Vector2Int>();
        var direction =Direction2D.GetRandomCardinalDirection();
        var currentPosition=startPosition;
        corridor.Add(currentPosition);

        for (int i = 0;i < corridorLength;i++)
        {
            currentPosition += direction;
            corridor.Add(currentPosition);
        }

        return corridor;
    }

    public static List<BoundsInt> BinarySpacePartitioning(BoundsInt spaceToSplit,int minWidth,int minHeight)
    {
        Queue<BoundsInt> roomsQueue = new Queue<BoundsInt>();
        List<BoundsInt> roomsList = new List<BoundsInt>();
        roomsQueue.Enqueue(spaceToSplit);

        while (roomsQueue.Count > 0)
        {
            BoundsInt room = roomsQueue.Dequeue();
            if (room.size.x >= minWidth && room.size.y >= minHeight)
            {
                if (Random.value < 0.5f)
                {
                    if (room.size.y >= minHeight * 2)
                    {
                        SplitHorizontally( minHeight, roomsQueue, room);
                    }
                    else if (room.size.x >= minWidth * 2)
                    {
                        SplitVertically(minWidth, roomsQueue, room);
                    }
                    else
                    {
                        roomsList.Add(room);
                    }
                }
                else
                {
                    if (room.size.x >= minWidth * 2)
                    {
                        SplitVertically(minWidth, roomsQueue, room);
                    }
                    else if (room.size.y >= minHeight * 2)
                    {
                        SplitHorizontally(minHeight, roomsQueue, room);
                    }
                    else
                    {
                        roomsList.Add(room);
                    }
                }
            }
        }
            return roomsList;

     }

    private static void SplitVertically(int minWidth, Queue<BoundsInt> roomsQueue, BoundsInt room)
    {
        int xSplit = Random.Range(1, room.size.x);
        BoundsInt room1=new BoundsInt(room.min,new Vector3Int(xSplit, room.size.y, room.size.z));
        BoundsInt room2 = new BoundsInt(new Vector3Int(room.min.x+xSplit, room.min.y, room.min.z),new Vector3Int(room.size.x-xSplit, room.size.y, room.size.z));
        roomsQueue.Enqueue(room1);
        roomsQueue.Enqueue(room2);
    }

    private static void SplitHorizontally( int minHeight, Queue<BoundsInt> roomsQueue, BoundsInt room)
    {
        int ySplit = Random.Range(1, room.size.y);
        BoundsInt room1 = new BoundsInt(room.min, new Vector3Int(room.size.x, ySplit, room.size.z));
        BoundsInt room2 = new BoundsInt(new Vector3Int(room.min.x, room.min.y+ySplit, room.min.z), new Vector3Int(room.size.x, room.size.y-ySplit, room.size.z));
        roomsQueue.Enqueue(room1);
        roomsQueue.Enqueue(room2);
    }

    public static class Direction2D
    {
        public static List<Vector2Int> cardinalDirectionsList = new List<Vector2Int>
        {   
            Vector2Int.up,
            Vector2Int.down,
            Vector2Int.left,
            Vector2Int.right
        };

        public static Vector2Int GetRandomCardinalDirection()
        {
            return cardinalDirectionsList[Random.Range(0, cardinalDirectionsList.Count)];
        }
    }
}
