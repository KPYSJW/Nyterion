using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class dungeonTest : AbstractDungeonGenertor
{

    [SerializeField]
    private DungeonData dungeonData;




    protected override void RunProceduralGeneration()
    {
        HashSet<Vector2Int> floorPositions = RunRandomWalk(dungeonData);
        tilemapVisualizer.Clear();
        tilemapVisualizer.PaintFloorTiles(floorPositions);
        WallGenerator.CreateWalls(floorPositions, tilemapVisualizer);
    }

    protected HashSet<Vector2Int> RunRandomWalk(DungeonData dungeonData)
    {
        var currentPosition = startPosition;
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