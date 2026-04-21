using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using NavMeshPlus.Components;
using NavMeshPlus.Extensions;
public class DungeonNavMeshBuilder : MonoBehaviour
{
    [SerializeField] private NavMeshSurface navMeshSurface;
    [SerializeField] private CollectSources2d collectSources2D;

    private void Reset()
    {
        navMeshSurface=GetComponent<NavMeshSurface>();
        collectSources2D=GetComponent<CollectSources2d>();
        
    }

    public IEnumerator RebuildNavMeshCoroutine()
    {
        if(navMeshSurface==null)
        {
            Debug.LogError("[DungeonNavMeshBuilder] NavMeshSurface reference is missing.");
            yield break;
        }

        Debug.Log("[DungeonNavMeshBuilder] Rebuild start");
        yield return null;
        yield return new WaitForFixedUpdate();
        yield return new WaitForEndOfFrame();
        Physics2D.SyncTransforms();
        navMeshSurface.BuildNavMesh();
        var triangulation = UnityEngine.AI.NavMesh.CalculateTriangulation();
    Debug.Log($"[DungeonNavMeshBuilder] NavMesh verts={triangulation.vertices.Length}, tris={triangulation.indices.Length / 3}");

    }

    public void RebuildNow()
    {
        if (navMeshSurface == null)
        {
            Debug.LogError("[DungeonNavMeshBuilder] NavMeshSurface reference is missing.");
            return;
        }
        Physics2D.SyncTransforms();
        navMeshSurface.BuildNavMesh();
    }

}
