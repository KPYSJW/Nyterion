using Nytherion.Core.Data;
using Nytherion.Core.Managers;
using Nytherion.Data.ScriptableObjects.Dungeon;
using Nytherion.GamePlay.Characters.Enemy;
using Nytherion.UI.Map;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.UI;
using VContainer;
using Nytherion.GamePlay.Relics;
using Nytherion.Core.Systems;
using Nytherion.Core.Enums;

namespace Nytherion.GamePlay.Dungeon
{
    /// <summary>
    /// 생성된 던전의 전반적인 상태와 로직을 관리하는 중앙 관리자 클래스입니다.
    /// </summary>
    public class DungeonManager : MonoBehaviour
    {
        #region Public 프로퍼티

        public List<RoomFirstDungeonGenerator.Room> AllDungeonRooms { get; private set; }
        public List<EnemyBase> AllActiveEnemies => AllDungeonRooms?.SelectMany(room => room.enemies).ToList() ?? new List<EnemyBase>();
        public GameObject playerObject;

        #endregion

        #region Private 변수

        private readonly Dictionary<Vector3Int, Vector3Int> portalLinks = new Dictionary<Vector3Int, Vector3Int>();
        private Dictionary<Vector2Int, RoomFirstDungeonGenerator.Room> roomLookup;
        private Dictionary<RoomFirstDungeonGenerator.Room, HashSet<Vector2Int>> roomFloorData;

        [Header("컴포넌트 참조")]
        [SerializeField] public RoomFirstDungeonGenerator roomFirstDungeonGenerator;
        [SerializeField] public TilemapVisualizer tilemapVisualizer;

        [Header("Test Options")]
        [Tooltip("체크하면 저장된 던전 맵을 무시하고 새 던전을 생성합니다.")]
        [SerializeField] private bool ignoreSavedDungeonMapForTest = false;

        [Header("Boss Intro")]
        [SerializeField] private bool playBossIntro = true;
        [SerializeField] private bool disablePlayerControlDuringBossIntro = true;
        [SerializeField] private string bossIntroTitle = "WARNING";
        [SerializeField] private float bossIntroFadeDuration = 0.25f;
        [SerializeField] private float bossIntroHoldDuration = 1.25f;
        [SerializeField] private float bossIntroPostDelay = 0.25f;

        private Tilemap portalTilemap;
        private TilemapCollider2D portalCollider;

        private RoomFirstDungeonGenerator.Room currentPlayerRoom = null;
        public RoomFirstDungeonGenerator.Room CurrentPlayerRoom => currentPlayerRoom;
        private RoomFirstDungeonGenerator.Room previousPlayerRoom = null;
        private bool hasBossSpawned = false;

        private EventManager _eventManager;
        private SaveLoadManager _saveLoadManager;
        public ObjectPoolManager _objectPoolManager;
        private InputManager _inputManager;
        private WorldmapController _worldmapController;
        private MinimapTileGenerator _minimapGenerator;
        public StageManager _stageManager;
        private DungeonData currentDungeonData;
        public DungeonData CurrentDungeonData => currentDungeonData;
        private bool isBossIntroPlaying = false;
        private Tilemap floorTilemap;
        private Tilemap wallTilemap;
        private Tilemap portalTilemapInstance;

        private CompositeCollider2D floorCollider;
        #endregion

        #region 의존성 주입

        [Inject]
        public void Construct(
           EventManager eventManager,
           ObjectPoolManager objectPoolManager,
           Characters.Player.PlayerController playerController,
           InputManager inputManager,
           SaveLoadManager saveLoadManager
           /*WorldmapController worldmapController*/) // 월드맵 토글 기능을 위해 주입받습니다.
        {
            _eventManager = eventManager;
            _objectPoolManager = objectPoolManager;
            playerObject = playerController.gameObject;
            _inputManager = inputManager;
            //_worldmapController = worldmapController;
            _saveLoadManager = saveLoadManager;
            FindTilemapReferences();
        }

        private void FindTilemapReferences()
        {
            GameObject floorTilemapObj = GameObject.Find("FloorTilemap");
            GameObject wallTilemapObj = GameObject.Find("WallTilemap");
            GameObject portalTilemapObj = GameObject.Find("PortalTilemap");

            if (floorTilemapObj != null)
            {
                this.floorTilemap = floorTilemapObj.GetComponent<Tilemap>();
                this.floorCollider = floorTilemapObj.GetComponent<CompositeCollider2D>();
            }
            if (wallTilemapObj != null) this.wallTilemap = wallTilemapObj.GetComponent<Tilemap>();
            if (portalTilemapObj != null)
            {
                this.portalTilemapInstance = portalTilemapObj.GetComponent<Tilemap>();
                this.portalTilemap = this.portalTilemapInstance;
            }
        }
        public void SetCurrentMapSaveData(DungeonMapSaveData mapData)
        {
            if (_saveLoadManager == null) return;

            SaveData saveData = _saveLoadManager.CurrentSaveData;
            saveData.dungeonMapData = mapData;
        }

        public void InitializeDungeonCheckpoint(RoomFirstDungeonGenerator.Room startRoom)
        {
            DungeonMapSaveData mapData = _saveLoadManager?.CurrentSaveData?.dungeonMapData;
            if (mapData == null || mapData.hasCheckpoint || startRoom == null)
                return;

            SaveDungeonCheckpoint(startRoom, startRoom.center, true);
        }

        public void SetStageManager(StageManager stageManager)
        {
            this._stageManager = stageManager;
            if (tilemapVisualizer != null)
            {
                tilemapVisualizer.InitializeTilemaps(floorTilemap, wallTilemap, portalTilemapInstance);
            }
        }

        private bool RefreshDungeonDataFromStage()
        {
            if (_stageManager == null)
            {
                Debug.LogError("[DungeonManager] StageManager가 없습니다.");
                return false;
            }

            if (_stageManager.CurrentStage == null)
            {
                Debug.LogError("[DungeonManager] CurrentStage가 없습니다.");
                return false;
            }

            if (_stageManager.CurrentStage.dungeonData == null)
            {
                Debug.LogError($"[DungeonManager] 현재 스테이지({_stageManager.CurrentStage.stageName})에 DungeonData가 없습니다.");
                return false;
            }

            currentDungeonData = _stageManager.CurrentStage.dungeonData;

            if (roomFirstDungeonGenerator != null)
            {
                roomFirstDungeonGenerator.dungeonData = currentDungeonData;
            }

            return true;
        }

        public void SetControllers(WorldmapController worldmapController, MinimapTileGenerator minimapGenerator)
        {
            _worldmapController = worldmapController;
            _minimapGenerator = minimapGenerator;
        }

        #endregion

        #region Unity 생명주기 메서드

        private void Awake()
        {
            if (portalTilemap != null)
            {
                portalCollider = portalTilemap.GetComponent<TilemapCollider2D>();
            }
            roomFirstDungeonGenerator = GetComponentInChildren<RoomFirstDungeonGenerator>();
        }

        private void Start()
        {
            if (_eventManager != null)
            {
                _eventManager.RegisterEnemyDeathListener(HandleEnemyDeath);
            }
            RoomFirstDungeonGenerator.OnDungeonGenerated += SpawnPlayerAtStart;

            if (_inputManager != null)
            {
                _inputManager.onMap += ToggleWorldMap;
            }
        }

        private void OnDestroy()
        {
            if (_eventManager != null)
            {
                _eventManager.UnregisterEnemyDeathListener(HandleEnemyDeath);
            }
            RoomFirstDungeonGenerator.OnDungeonGenerated -= SpawnPlayerAtStart;

            if (_inputManager != null)
            {
                _inputManager.onMap -= ToggleWorldMap;
            }
        }

        private void Update()
        {
            if (playerObject == null || AllDungeonRooms == null || AllDungeonRooms.Count == 0) return;
            RoomFirstDungeonGenerator.Room detectedRoom = FindCurrentPlayerRoom();
            if (detectedRoom != null && detectedRoom != currentPlayerRoom)
            {
                previousPlayerRoom = currentPlayerRoom;
                currentPlayerRoom = detectedRoom;
                OnPlayerEnteredRoom(currentPlayerRoom, previousPlayerRoom);
            }
        }

        public void StartDungeonGeneration()
        {
            if (!RefreshDungeonDataFromStage())
                return;

            DungeonMapSaveData savedMap = _saveLoadManager?.CurrentSaveData?.dungeonMapData;

            if (!ignoreSavedDungeonMapForTest && savedMap != null && savedMap.hasMap)
            {
                roomFirstDungeonGenerator.LoadDungeonFromSave(savedMap);
            }
            else
            {
                if (ignoreSavedDungeonMapForTest && _saveLoadManager != null)
                {
                    _saveLoadManager.CurrentSaveData.dungeonMapData = new DungeonMapSaveData();
                }

                roomFirstDungeonGenerator.GenerateNewDungeonAndCreateSnapshot();
            }
        }

        public void RegenerateDungeon()
        {
            Debug.Log("[DungeonManager] 던전 재생성을 시작합니다...");
            StartDungeonGeneration();
        }

        #endregion

        #region Public 데이터 설정 메서드

        public void SetAllRooms(List<RoomFirstDungeonGenerator.Room> allRooms)
        {
            AllDungeonRooms = allRooms;
        }

        public void SetRoomLookup(Dictionary<Vector2Int, RoomFirstDungeonGenerator.Room> newLookup)
        {
            roomLookup = newLookup;
        }

        public void SetRoomFloorData(Dictionary<RoomFirstDungeonGenerator.Room, HashSet<Vector2Int>> newFloorData)
        {
            roomFloorData = newFloorData;
        }

        public void ClearDungeonData()
        {
            portalLinks.Clear();
            AllDungeonRooms?.Clear();
            roomLookup?.Clear();
            roomFloorData?.Clear();
            currentPlayerRoom = null;
            previousPlayerRoom = null;
            hasBossSpawned = false;
        }

        #endregion

        #region 포탈 관련 메서드

        public void RegisterPortalPair(Vector3Int portalA, Vector3Int portalB)
        {
            portalLinks[portalA] = portalB;
            portalLinks[portalB] = portalA;
        }

        public bool TryGetDestination(Vector3Int currentPortalPos, out Vector3Int destinationPos)
        {
            if (portalLinks.Count == 0)
            {
                Debug.LogError("[포탈 확인] DungeonManager에 등록된 포탈이 하나도 없습니다!");
            }
            return portalLinks.TryGetValue(currentPortalPos, out destinationPos);

        }

        private void LockPortals()
        {
            if (portalCollider != null)
            {
                portalCollider.isTrigger = false;
            }
        }

        private void UnlockPortals()
        {
            if (portalCollider != null)
            {
                portalCollider.isTrigger = true;
            }
        }

        #endregion

        #region 방 탐색 및 상태 관련 메서드

        public RoomFirstDungeonGenerator.Room FindCurrentPlayerRoom()
        {
            if (playerObject == null) return null;
            Vector2Int playerTilePos = Vector2Int.FloorToInt(playerObject.transform.position);
            return FindRoomAt(playerTilePos);
        }

        public RoomFirstDungeonGenerator.Room FindRoomAt(Vector2Int position)
        {
            if (roomLookup != null && roomLookup.TryGetValue(position, out RoomFirstDungeonGenerator.Room room))
            {
                return room;
            }
            return null;
        }

        public bool IsRoomCleared(RoomFirstDungeonGenerator.Room room)
        {
            if (room == null) return true;
            if (room.cleared) return true;

            if (room.type == RoomFirstDungeonGenerator.RoomType.Start ||
                room.type == RoomFirstDungeonGenerator.RoomType.Shop ||
                room.type == RoomFirstDungeonGenerator.RoomType.Item)
            {
                return true;
            }

            return room.enemies.All(monster => monster == null || monster.isDead);
        }

        public void SaveDungeonCheckpoint(RoomFirstDungeonGenerator.Room room, Vector2 playerPosition, bool forceSave)
        {
            DungeonMapSaveData mapData = _saveLoadManager?.CurrentSaveData?.dungeonMapData;
            if (mapData == null || !mapData.hasMap || room == null)
                return;

            room.visited = true;
            mapData.hasCheckpoint = true;
            mapData.currentRoomId = room.id;
            mapData.lastSafeRoomId = room.id;
            mapData.lastSafeX = playerPosition.x;
            mapData.lastSafeY = playerPosition.y;
            mapData.portalsUnlocked = IsRoomCleared(room);
            mapData.hasBossSpawned = hasBossSpawned;

            UpdateRoomStatesInSaveData(mapData);

            if (forceSave)
            {
                _saveLoadManager?.ForceSaveGame();
            }
            else
            {
                _saveLoadManager?.SaveGame();
            }
        }

        public void SaveCheckpointAtCurrentPlayerPosition(bool forceSave)
        {
            RoomFirstDungeonGenerator.Room room = FindCurrentPlayerRoom();
            if (room == null || playerObject == null)
                return;

            if (!IsRoomCleared(room))
            {
                SaveDungeonRoomStates(forceSave);
                return;
            }

            SaveDungeonCheckpoint(room, playerObject.transform.position, forceSave);
        }

        public void SaveDungeonRoomStates(bool forceSave)
        {
            DungeonMapSaveData mapData = _saveLoadManager?.CurrentSaveData?.dungeonMapData;
            if (mapData == null || !mapData.hasMap)
                return;

            mapData.hasBossSpawned = hasBossSpawned;
            UpdateRoomStatesInSaveData(mapData);

            if (forceSave)
            {
                _saveLoadManager?.ForceSaveGame();
            }
            else
            {
                _saveLoadManager?.SaveGame();
            }
        }

        private void UpdateRoomStatesInSaveData(DungeonMapSaveData mapData)
        {
            if (mapData == null || mapData.rooms == null || AllDungeonRooms == null)
                return;

            Dictionary<int, DungeonRoomSaveData> savedRoomsById = mapData.rooms.ToDictionary(room => room.id, room => room);
            foreach (RoomFirstDungeonGenerator.Room room in AllDungeonRooms)
            {
                if (savedRoomsById.TryGetValue(room.id, out DungeonRoomSaveData roomData))
                {
                    roomData.visited = room.visited;
                    roomData.cleared = room.cleared;
                }
            }
        }

        #endregion

        #region 이벤트 핸들러 및 콜백

        private void ToggleWorldMap()
        {
            _worldmapController?.Toggle();
        }

        private void SpawnPlayerAtStart(RoomFirstDungeonGenerator.Room startRoom)
        {
            if (playerObject == null || startRoom == null)
                return;

            DungeonMapSaveData mapData = _saveLoadManager?.CurrentSaveData?.dungeonMapData;
            if (mapData != null && mapData.hasCheckpoint)
            {
                playerObject.transform.position = new Vector2(mapData.lastSafeX, mapData.lastSafeY);
                hasBossSpawned = mapData.hasBossSpawned;
                return;
            }

            playerObject.transform.position = startRoom.center;
        }

        private void HandleEnemyDeath(EnemyBase deadEnemy)
        {
            RoomFirstDungeonGenerator.Room room = deadEnemy.homeRoom;
            if (room != null && IsRoomCleared(room))
            {
                room.cleared = true;

                // 꼬인 실타래 (TangledYarn) 활성 링크 3개 이상 상태로 방 클리어 업적 연동
                RelicManager relicManager = UnityEngine.Object.FindObjectOfType<RelicManager>();
                if (relicManager != null)
                {
                    bool isTangledYarnMet = false;
                    foreach (KeyValuePair<string, Vector2Int> pair in relicManager.GetPlacedBlocks())
                    {
                        RelicBlock block = relicManager.GetBlockAt(pair.Value.y, pair.Value.x);
                        if (block != null && block.RelicId == "TangledYarn" && !block.SourceData.isDisabled)
                        {
                            // 꼬인 실타래 조건 만족 여부 확인
                            HashSet<string> seriesIds = new HashSet<string>();
                            foreach (KeyValuePair<string, Vector2Int> p in relicManager.GetPlacedBlocks())
                            {
                                RelicBlock b = relicManager.GetBlockAt(p.Value.y, p.Value.x);
                                if (b != null && b.SourceData != null && !string.IsNullOrEmpty(b.SourceData.synergySeriesId))
                                {
                                    seriesIds.Add(b.SourceData.synergySeriesId);
                                }
                            }

                            int totalLinks = 0;
                            foreach (string seriesId in seriesIds)
                            {
                                int length = relicManager.GetMaxChainLength(seriesId);
                                if (length >= 2)
                                {
                                    totalLinks += (length - 1);
                                }
                            }

                            if (totalLinks >= 3)
                            {
                                isTangledYarnMet = true;
                                break;
                            }
                        }
                    }

                    if (isTangledYarnMet)
                    {
                        ProgressionManager progressionManager = DataLifetimeScope.Instance != null ? DataLifetimeScope.Instance.GetDataManager<ProgressionManager>() : null;
                        if (progressionManager != null)
                        {
                            progressionManager.ProcessAction(ProgressionType.TangledYarnRoomClear, 1);
                        }
                    }
                }

                if (room.type == RoomFirstDungeonGenerator.RoomType.Boss)
                {
                    _stageManager?.SpawnBossPortal(deadEnemy.transform.position);
                    UnlockPortals();
                }
                else
                {
                    UnlockPortals();
                }

                SaveDungeonCheckpoint(room, playerObject != null ? playerObject.transform.position : room.center, true);
            }
        }

        private void OnPlayerEnteredRoom(RoomFirstDungeonGenerator.Room newRoom, RoomFirstDungeonGenerator.Room oldRoom)
        {
            oldRoom?.DeactivateEnemies();
            newRoom.visited = true;
            newRoom?.ActivateEnemies();

            // 네잎클로버 전투 내 대쉬 초기화 횟수 리셋
            if (playerObject != null)
            {
                PlayerManager playerManager = playerObject.GetComponent<PlayerManager>();
                if (playerManager != null)
                {
                    playerManager.ResetLuckyCloverResetCount();
                }
            }

            if (newRoom.type == RoomFirstDungeonGenerator.RoomType.Boss && !hasBossSpawned)
            {
                if (!isBossIntroPlaying)
                {
                    StartCoroutine(PlayBossIntroAndSpawn(newRoom));
                }
            }

            if (!IsRoomCleared(newRoom))
            {
                LockPortals();
                SaveDungeonRoomStates(true);
            }
            else
            {
                UnlockPortals();
                SaveDungeonCheckpoint(newRoom, playerObject != null ? playerObject.transform.position : newRoom.center, true);
            }
        }

        private IEnumerator PlayBossIntroAndSpawn(RoomFirstDungeonGenerator.Room bossRoom)
        {
            isBossIntroPlaying = true;
            LockPortals();

            if (disablePlayerControlDuringBossIntro)
            {
                _inputManager?.DisableMovement();
            }

            if (playBossIntro)
            {
                string bossName = currentDungeonData?.bossMonsterData != null
                    ? currentDungeonData.bossMonsterData.enemyName
                    : "Boss";

                yield return StartCoroutine(ShowBossIntroOverlay(bossName));
            }

            

            if (bossIntroPostDelay > 0f)
            {
                yield return new WaitForSeconds(bossIntroPostDelay);
            }

            SpawnBoss(bossRoom);

            if (disablePlayerControlDuringBossIntro)
            {
                _inputManager?.EnableMovement();
            }
            

            isBossIntroPlaying = false;
        }

        private IEnumerator ShowBossIntroOverlay(string bossName)
        {
            CanvasGroup canvasGroup = CreateBossIntroOverlay(bossName);
            if (canvasGroup == null)
                yield break;

            yield return StartCoroutine(FadeBossIntroOverlay(canvasGroup, 0f, 1f, bossIntroFadeDuration));

            if (bossIntroHoldDuration > 0f)
            {
                yield return new WaitForSeconds(bossIntroHoldDuration);
            }

            yield return StartCoroutine(FadeBossIntroOverlay(canvasGroup, 1f, 0f, bossIntroFadeDuration));

            if (canvasGroup != null)
            {
                Destroy(canvasGroup.gameObject);
            }
        }

        private CanvasGroup CreateBossIntroOverlay(string bossName)
        {
            GameObject canvasObject = new GameObject("BossIntroCanvas");
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 9000;

            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            canvasObject.AddComponent<GraphicRaycaster>();

            CanvasGroup canvasGroup = canvasObject.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;

            GameObject dimObject = new GameObject("Dim");
            dimObject.transform.SetParent(canvasObject.transform, false);
            Image dimImage = dimObject.AddComponent<Image>();
            dimImage.color = new Color(0f, 0f, 0f, 0.65f);
            RectTransform dimRect = dimObject.GetComponent<RectTransform>();
            dimRect.anchorMin = Vector2.zero;
            dimRect.anchorMax = Vector2.one;
            dimRect.offsetMin = Vector2.zero;
            dimRect.offsetMax = Vector2.zero;

            GameObject titleObject = new GameObject("Title");
            titleObject.transform.SetParent(canvasObject.transform, false);
            TextMeshProUGUI titleText = titleObject.AddComponent<TextMeshProUGUI>();
            titleText.text = bossIntroTitle;
            titleText.fontSize = 72f;
            titleText.alignment = TextAlignmentOptions.Center;
            titleText.color = new Color(1f, 0.2f, 0.16f, 1f);
            titleText.fontStyle = FontStyles.Bold;
            RectTransform titleRect = titleObject.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0.1f, 0.52f);
            titleRect.anchorMax = new Vector2(0.9f, 0.68f);
            titleRect.offsetMin = Vector2.zero;
            titleRect.offsetMax = Vector2.zero;

            GameObject nameObject = new GameObject("BossName");
            nameObject.transform.SetParent(canvasObject.transform, false);
            TextMeshProUGUI nameText = nameObject.AddComponent<TextMeshProUGUI>();
            nameText.text = bossName;
            nameText.fontSize = 44f;
            nameText.alignment = TextAlignmentOptions.Center;
            nameText.color = Color.white;
            RectTransform nameRect = nameObject.GetComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0.1f, 0.42f);
            nameRect.anchorMax = new Vector2(0.9f, 0.52f);
            nameRect.offsetMin = Vector2.zero;
            nameRect.offsetMax = Vector2.zero;

            return canvasGroup;
        }

        private IEnumerator FadeBossIntroOverlay(CanvasGroup canvasGroup, float from, float to, float duration)
        {
            if (canvasGroup == null)
                yield break;

            if (duration <= 0f)
            {
                canvasGroup.alpha = to;
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                canvasGroup.alpha = Mathf.Lerp(from, to, elapsed / duration);
                yield return null;
            }

            canvasGroup.alpha = to;
        }

        private void SpawnBoss(RoomFirstDungeonGenerator.Room bossRoom)
        {
            DungeonData dungeonData = currentDungeonData;
            if (dungeonData.bossMonsterData == null)
            {
                Debug.LogWarning("DungeonData에 보스 몬스터 데이터가 할당되지 않았습니다.");
                return;
            }

            if (!bossRoom.bossSpawnPoint.HasValue)
            {
                Debug.LogError("보스 방에 스폰 위치가 지정되지 않았습니다! RoomFirstDungeonGenerator를 확인하세요.");
                return;
            }

            Vector3 spawnPosition = bossRoom.bossSpawnPoint.Value;
            GameObject bossObj = _objectPoolManager.SpawnFromPool(
                dungeonData.bossMonsterData.enemyName,
                spawnPosition,
                Quaternion.identity);

            if (bossObj != null && bossObj.TryGetComponent<EnemyBase>(out var bossEnemy))
            {
                bossEnemy.Initialize(dungeonData.bossMonsterData);
                bossEnemy.homeRoom = bossRoom;
                bossRoom.enemies.Add(bossEnemy);
                hasBossSpawned = true;

                Debug.Log($"<color=red><b>보스 등장!</b></color> '{dungeonData.bossMonsterData.enemyName}' 스폰 완료!");
            }
            else
            {
                Debug.LogError($"오브젝트 풀에서 '{dungeonData.bossMonsterData.enemyName}' 태그를 가진 오브젝트를 스폰하는데 실패했습니다. ObjectPoolManager 설정을 확인하세요.");
            }
        }
        #endregion
    }
}
