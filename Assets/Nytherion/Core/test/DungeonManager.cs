using Nytherion.Core.Managers;
using System.Collections.Generic;
using UnityEngine;
using Nytherion.GamePlay.Dungeon;

public class DungeonManager : MonoBehaviour
{
    public static DungeonManager Instance { get; private set; }

    public List<RoomFirstDungeonGenerator.Room> AllDungeonRooms { get; private set; }
    private Dictionary<Vector3Int, Vector3Int> portalLinks = new Dictionary<Vector3Int, Vector3Int>();

    public GameObject playerObject;
    [SerializeField] private GameObject worldMapUI; 

    private void OnEnable()
    {
        RoomFirstDungeonGenerator.OnDungeonGenerated += SpawnPlayerAtStart;
        InputManager.Instance.onMap += WorldMapUI;
    }

    private void OnDisable()
    {
        RoomFirstDungeonGenerator.OnDungeonGenerated -= SpawnPlayerAtStart;
        InputManager.Instance.onMap -= WorldMapUI;
    }

    private void Awake()
    {
        if (worldMapUI != null)
        {
            worldMapUI.SetActive(false);
        }

        
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
            
        }
    }

    
    public void RegisterPortalPair(Vector3Int portalA, Vector3Int portalB)
    {
        portalLinks[portalA] = portalB;
        portalLinks[portalB] = portalA;
    }

    
    public bool TryGetDestination(Vector3Int currentPortalPos, out Vector3Int destinationPos)
    {
        return portalLinks.TryGetValue(currentPortalPos, out destinationPos);
    }

    public bool IsRoomCleared(Vector2Int roomCoord) 
    {
        return true;
    }

    
    public void ClearDungeonData()
    {
        portalLinks.Clear();
        AllDungeonRooms?.Clear();
    }

    private void SpawnPlayerAtStart(RoomFirstDungeonGenerator.Room startRoom)
    {
        if (playerObject == null)
        {
            playerObject = GameObject.FindGameObjectWithTag("Player");
        }

        if (playerObject != null)
        {
            
            playerObject.transform.position = new Vector3(startRoom.center.x, startRoom.center.y, 0);
            Debug.Log($"÷̾ {startRoom.center} ̵!");
        }
        else
        {
            Debug.LogError("Player  ã  ! 'Player'  Ȯ.");
        }
    }
    public void SetAllRooms(List<RoomFirstDungeonGenerator.Room> allRooms)
    {
        AllDungeonRooms = allRooms;
    }

    void WorldMapUI()
    {
        if (worldMapUI != null)
        {
            worldMapUI.SetActive(!worldMapUI.activeSelf);
        }
    }
}