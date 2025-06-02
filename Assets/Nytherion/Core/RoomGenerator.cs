using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;

namespace Nytherion.Core
{
    public class RoomGenerator : MonoBehaviour
    {
        int offSet = 1;

        public static List<BoundsInt> BinarySpacePartitioning(int minWidth, int minHeight, BoundsInt startRect)//시작 큰 사각형에서 작은 사각형으로 방을 나눔
        {
            Queue<BoundsInt> rectQueue= new Queue<BoundsInt>();
            List<BoundsInt> rectPositions= new List<BoundsInt>();

            rectQueue.Enqueue(startRect);

            while(rectQueue.Count > 0)
            {
                BoundsInt rect= rectQueue.Dequeue();

                int length = rect.size.x > rect.size.y ? rect.size.x : rect.size.y;
                length=Mathf.RoundToInt(Random.Range(0.4f,0.7f)*length);

                if (rect.size.x>=minWidth && rect.size.y>=minHeight)
                {
                    if(rect.size.x>=rect.size.y && rect.size.x>minWidth*2)
                    {
                        BoundsInt divideRect1 = new BoundsInt(rect.min, new Vector3Int(length, rect.size.y, rect.size.z));
                        BoundsInt divideRect2 = new BoundsInt(new Vector3Int(rect.min.x + length, rect.min.y, rect.min.z),new Vector3Int(rect.size.x - length, rect.size.y, rect.size.z));
                        rectQueue.Enqueue(divideRect1);
                        rectQueue.Enqueue(divideRect2);
                    }
                    else if(rect.size.y>rect.size.x && rect.size.y>minHeight*2)
                    {
                        BoundsInt divideRect1 = new BoundsInt(rect.min, new Vector3Int(rect.size.x, length, rect.size.z));
                        BoundsInt divideRect2 = new BoundsInt(new Vector3Int(rect.min.x, rect.min.y + length, rect.min.z), new Vector3Int(rect.size.x, rect.size.y - length, rect.size.z));
                        rectQueue.Enqueue(divideRect1);
                        rectQueue.Enqueue(divideRect2);
                    }
                    else
                    {
                        rectPositions.Add(rect);
                    }

                }
            }
            return rectPositions;
        }

        public static HashSet<Vector3Int> RandomWalk(int length,Vector3Int startPosition)
        {
            Vector3Int currentPosition= startPosition;

            HashSet<Vector3Int> totalPositions= new HashSet<Vector3Int>();

            for(int i=0;i<length; ++i)
            {
                totalPositions.Add(currentPosition);
                currentPosition += RandomPosition.getRandomPosition();
            }
            return totalPositions;
        }

        public HashSet<Vector3Int> RandomWalkGenerate(List<BoundsInt> roomRects, int iteration, int length)
        {
            HashSet<Vector3Int> floorPositons= new HashSet<Vector3Int>();

            foreach(var roomRect in roomRects)
            {
                Vector3Int roomCenter = new Vector3Int(Mathf.RoundToInt(roomRect.center.x), Mathf.RoundToInt(roomRect.center.y), Mathf.RoundToInt(roomRect.center.z));
                HashSet<Vector3Int>randomWalk= RandomWalkIteration(iteration,length,roomCenter);

                foreach(var pos in randomWalk)
                {
                    if(roomRect.xMin+offSet< pos.x&&pos.x<roomRect.xMax-offSet&&roomRect.yMin+offSet<pos.y&&pos.y<roomRect.yMax-offSet)
                    {
                        floorPositons.Add(pos);
                    }
                }
            }

            return floorPositons;
        }

        public HashSet<Vector3Int> RandomWalkIteration(int iteration, int length, Vector3Int startPosition)
        {
            HashSet<Vector3Int> positions = new HashSet<Vector3Int>();
            Vector3Int currentPosition= startPosition;

            for(int i=0;i<iteration;++i)
            {
                HashSet<Vector3Int> walk=RandomWalk(length,currentPosition);
                positions.UnionWith(walk);

                if(walk.Count > 0 )
                {
                    currentPosition=walk.Last();
                }
            }

            return positions;   
        }




        //테스트
        public HashSet<Vector3Int> CentralizedRoomGenerate(List<BoundsInt> roomRects, int steps, float maxRadius = 12f)
        {
            HashSet<Vector3Int> floorPositions = new();

            foreach (var roomRect in roomRects)
            {
                Vector3Int center = new Vector3Int(Mathf.RoundToInt(roomRect.center.x), Mathf.RoundToInt(roomRect.center.y), 0);
                HashSet<Vector3Int> room = CentralizedRandomWalk(center, steps, maxRadius);

                foreach (var pos in room)
                {
                    if (roomRect.Contains(pos))
                        floorPositions.Add(pos);
                }
            }

            return floorPositions;
        }

        private HashSet<Vector3Int> CentralizedRandomWalk(Vector3Int start, int steps, float radius)
        {
            HashSet<Vector3Int> positions = new();
            Vector3Int current = start;

            for (int i = 0; i < steps; i++)
            {
                positions.Add(current);
                Vector3Int dir = RandomPosition.getRandomPosition();
                Vector3Int next = current + dir;
                float dist = Vector3Int.Distance(start, next);
                float probability = Mathf.Clamp01(1.0f - dist / radius);
                if (Random.value < probability)
                {
                    current = next;
                }
            }

            return positions;
        }
    }
}

    public class Kruskal
    {
        List<Edge> edges= new List<Edge>();
        List<TwoPos> twoPosList;
        int[] unionCheck;

        class Edge
        {
            public Vector3Int a, b;
            public int indA, indB;
            public float distance;

            public Edge(Vector3 a, Vector3 b, int i,int j)
            {
                this.a = new Vector3Int(Mathf.RoundToInt(a.x), Mathf.RoundToInt(a.y), Mathf.RoundToInt(a.z));
                this.b= new Vector3Int(Mathf.RoundToInt(b.x), Mathf.RoundToInt(b.y), Mathf.RoundToInt(b.z));
                this.distance=Vector3.Distance(a,b);
                this.indA=i;
                this.indB=j;
            }
        }

        void init(int numberOfRooms,List<BoundsInt> rooms)
        {
            twoPosList = new List<TwoPos>();
            unionCheck = new int[numberOfRooms];

            for(int i=0;i<numberOfRooms;++i)
            {
                for(int j=i+1;j<numberOfRooms;++j)
                {
                    Edge newEdge = new Edge(rooms[i].center, rooms[j].center, i, j);
                    edges.Add(newEdge);
                }
                unionCheck[i] = i;
            }
            edges=edges.OrderBy(x=>x.distance).ToList();
        }
        int Root(int a)
        {
            while(a!=unionCheck[a])
            {
                a=unionCheck[a];
            }
            return a;
        }
        bool isAllUnion()
        {
            int first=Root(unionCheck[0]);
            foreach(int item in unionCheck)
            {
                if(Root(item)!=first)
                    return false;
            }
            return true;
        }

        void Union(int a, int b)
        {
            a = Root(a);
            b=Root(b);
            unionCheck[a] = b;
        }

        public List<TwoPos> GetSortedTwoPositions(int numberOfRooms, List<BoundsInt> rooms)
        {
            List<TwoPos> corridorPairs = new List<TwoPos>();
            init(numberOfRooms, rooms);

            foreach(Edge edge in edges)
            {
                corridorPairs.Add(new TwoPos(edge.a, edge.b));
                Union(edge.indA, edge.indB);
                if(isAllUnion())
                {
                    break;
                }
            }    

            return corridorPairs;
        }

        public static HashSet<Vector3Int> Connect(TwoPos twoPos)
        {
            Vector3Int source = twoPos.a;
            Vector3Int destination = twoPos.b;
            
            HashSet<Vector3Int>corridors= new HashSet<Vector3Int>();

            while(source.x!=destination.x)
            {
                if(source.x>destination.x)
                {
                    source.x -= 1;
                }
                else
                {
                    source.x += 1;
                }
                corridors.Add(source);
            }
            while (source.y != destination.y)
            {
                if (source.y > destination.y)
                {
                    source.y -= 1;
                }
                else
                {
                    source.y += 1;
                }
                corridors.Add(source);
            }

            return corridors;
        }
    }

    public static class RandomPosition
    {
        public static readonly List<Vector3Int> directions = new List<Vector3Int>
        {
            Vector3Int.right,
            Vector3Int.left,
            Vector3Int.up,
            Vector3Int.down
        };
        public static Vector3Int getRandomPosition()
        {
            return directions[Random.Range(0, directions.Count)];
        }
    }

    public class TwoPos
    {
        public Vector3Int a;
        public Vector3Int b;

        public TwoPos(Vector3Int a, Vector3Int b)
        {
            this.a = a;
            this.b = b;
        }
    }

  /*  public static class Direction2D
    {
        public static readonly List<Vector3Int> eightDir = new List<Vector3Int>
        {
            Vector3Int.right,
            Vector3Int.left,
            Vector3Int.up,
            Vector3Int.down,
             new Vector3Int(-1, 1, 0), 
        new Vector3Int(1, 1, 0),   
        new Vector3Int(-1, -1, 0), 
        new Vector3Int(1, -1, 0)
        };
 
    }*/




