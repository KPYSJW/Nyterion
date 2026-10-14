using UnityEngine;
using Nytherion.Services;
using Nytherion.Core.Data;
using Nytherion.Core.Interfaces;
using Nytherion.Core.Enums;
using System.Collections;
using Nytherion.UI.Inventory;
using System.Collections.Generic;
using VContainer;
using Nytherion.Core.Systems;
using Nytherion.UI.Skill;

namespace Nytherion.Core.Managers
{
    public class SaveLoadManager : BaseManager
    {
        private JsonSaveService saveService;
        private SaveData saveData;
        private bool isLoadingData = false;
        private bool hasLoadedData = false;

        private bool isInitializationComplete = false;

        private List<ISaveable> saveableEntities = new List<ISaveable>();

        // 자동 저장 관련
        private float autoSaveInterval = 30f; // 30초마다 자동 저장
        private float lastSaveTime = 0f;

        private bool preventAutoSave = false;
        private const float SAVE_COOLDOWN = 1f; // 저장 간 최소 간격
        private bool isSaveBlocked = false;
        private float lastAutoSaveAttemptTime = float.NegativeInfinity;

        public bool IsSaveBlocked => isSaveBlocked;
        public string LastError { get; private set; }
        public SaveLoadStatus LastLoadStatus { get; private set; } = SaveLoadStatus.Missing;

        private void CollectSaveableEntities()
        {
            saveableEntities.Clear();

            if (DataLifetimeScope.Instance != null && DataLifetimeScope.Instance.Container != null)
            {
                var container = DataLifetimeScope.Instance.Container;

                if (container.TryResolve<CurrencyDataManager>(out var currencyManager))
                {
                    saveableEntities.Add(currencyManager);
                }

                if (container.TryResolve<InventoryDataManager>(out var inventoryManager))
                {
                    saveableEntities.Add(inventoryManager);
                }

                if (container.TryResolve<RelicManager>(out var relicManager))
                {
                    saveableEntities.Add(relicManager);
                }

                if (container.TryResolve<EquipmentDataManager>(out var equipmentManager))
                {
                    saveableEntities.Add(equipmentManager);
                }

                if (container.TryResolve<ShopManager>(out var shopManager))
                {
                    saveableEntities.Add(shopManager);
                }
                if (container.TryResolve<SkillDataManager>(out var skillDataManager))
                {
                    saveableEntities.Add(skillDataManager);
                }
                if (container.TryResolve<IProgressionManager>(out var progressionManager))
                {
                    if (progressionManager is ISaveable saveableProgression)
                    {
                        saveableEntities.Add(saveableProgression);
                    }
                }
            }
            else
            {
                Debug.LogWarning("[SaveLoadManager] DataLifetimeScope.Instance 또는 Container가 null입니다.");
            }

            CollectGameSceneISaveableEntities();

        }

        private void CollectGameSceneISaveableEntities()
        {
            string currentSceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

            var gameSceneScope = FindObjectOfType<GameSceneLifetimeScope>();
            if (gameSceneScope != null && gameSceneScope.Container != null)
            {
                var gameContainer = gameSceneScope.Container;

                try
                {
                    if (gameContainer.TryResolve<QuickSlotManager>(out var quickSlotManager))
                    {
                        if (!saveableEntities.Contains(quickSlotManager))
                        {
                            saveableEntities.Add(quickSlotManager);
                        }
                    }
                    else
                    {
                        Debug.LogWarning("[SaveLoadManager] GameSceneLifetimeScope에서 QuickSlotManager를 찾지 못했습니다");
                    }

                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[SaveLoadManager] GameScene ISaveable 수집 중 오류: {e.Message}");
                }
            }
            else if (currentSceneName == "GameScene")
            {
                var quickSlotManagerInScene = FindObjectOfType<QuickSlotManager>();
                if (quickSlotManagerInScene != null)
                {
                    if (!saveableEntities.Contains(quickSlotManagerInScene))
                    {
                        saveableEntities.Add(quickSlotManagerInScene);
                        Debug.Log("[SaveLoadManager] 직접 탐색으로 QuickSlotManager를 찾아 추가했습니다");
                    }
                }
            }
        }

        protected override void Awake()
        {
            base.Awake();
            saveService = new JsonSaveService();
        }

        protected override void OnInitializeInternal()
        {
            preventAutoSave = true;

            CollectSaveableEntities();
            StartCoroutine(DelayedLoadCoroutine());
        }

        private void Update()
        {
            if (!isInitializationComplete || preventAutoSave || isSaveBlocked) return;

            // 실패 시 성공 시각을 갱신하지 않되, 매 프레임 파일 쓰기를 재시도하지 않습니다.
            if (hasLoadedData && !isLoadingData && Time.unscaledTime - lastSaveTime >= autoSaveInterval &&
                Time.unscaledTime - lastAutoSaveAttemptTime >= SAVE_COOLDOWN)
            {
                lastAutoSaveAttemptTime = Time.unscaledTime;
                SaveGame();
            }
        }

        private IEnumerator DelayedLoadCoroutine()
        {
            yield return null;

            string currentSceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

            if (currentSceneName == "GameScene")
            {
                var gameSceneScope = FindObjectOfType<GameSceneLifetimeScope>();
                if (gameSceneScope != null && gameSceneScope.Container != null)
                {
                    if (GameManager.IsVerboseLogging()) Debug.Log("[SaveLoadManager] GameSceneLifetimeScope 초기화 확인됨, 로드 시작");
                }
                else
                {
                    if (GameManager.IsVerboseLogging()) Debug.Log("[SaveLoadManager] GameSceneLifetimeScope가 아직 준비되지 않았지만 로드 진행");
                }
            }
            else
            {
                if (GameManager.IsVerboseLogging()) Debug.Log($"[SaveLoadManager] 현재 씬({currentSceneName})에서는 GameSceneLifetimeScope를 찾지 않고 바로 로드 진행");
            }

            LoadGame();

            yield return new WaitForSecondsRealtime(2f);
            isInitializationComplete = true;
            preventAutoSave = false;
        }

        public void LoadGameIfNeeded()
        {
            if (!hasLoadedData)
            {
                LoadGame();
            }
        }

        public void SaveGame()
        {
            SaveGameInternal(false);
        }

        public void ForceSaveGame()
        {
            SaveGameInternal(true);
        }

        private void SaveGameInternal(bool ignoreCooldown)
        {
            if (isLoadingData || preventAutoSave || isSaveBlocked || !hasLoadedData) return;
            if (!ignoreCooldown && Time.unscaledTime - lastSaveTime < SAVE_COOLDOWN) return;
            if (Application.isEditor && !Application.isPlaying) return;

            CollectSaveableEntities();
            if (saveableEntities == null || saveableEntities.Count == 0) return;

            try
            {
                // 씬 밖 퀵슬롯과 던전이 직접 기록한 맵을 보존하고, 수집 실패로 기존 상태를 훼손하지 않습니다.
                SaveData snapshot = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(CurrentSaveData));
                foreach (ISaveable entity in saveableEntities)
                {
                    if (entity != null) entity.PopulateSaveData(snapshot);
                }

                if (saveService.Save(snapshot))
                {
                    saveData = snapshot;
                    lastSaveTime = Time.unscaledTime;
                    LastError = null;
                }
                else
                {
                    LastError = saveService.LastError;
                }
            }
            catch (System.Exception exception)
            {
                LastError = exception.Message;
                Debug.LogError($"[SaveLoadManager] 저장 데이터 수집 실패. 기존 파일을 보존합니다: {LastError}");
            }
        }

        public void LoadGame()
        {
            if (hasLoadedData)
            {
                return;
            }

            LoadGameInternal();
        }

        public void ForceLoadGame()
        {
            LoadGameInternal();
        }

        private void LoadGameInternal()
        {
            if (isLoadingData) return;
            CollectSaveableEntities();
            if (saveableEntities == null || saveableEntities.Count == 0)
            {
                Debug.LogWarning("[SaveLoadManager] LoadGame 실패 - ISaveable 엔티티가 없음");
                return;
            }

            bool previousPreventAutoSave = preventAutoSave;
            bool loadSucceeded = false;
            isLoadingData = true;
            preventAutoSave = true;
            try
            {
                SaveLoadResult result = saveService.LoadWithResult();
                LastLoadStatus = result.Status;
                if (!result.IsSuccess && result.Status != SaveLoadStatus.Missing)
                {
                    isSaveBlocked = true;
                    LastError = result.Error;
                    Debug.LogError($"[SaveLoadManager] 세이브 복구 실패. 기존 파일을 보호하기 위해 저장을 차단합니다: {LastError}");
                    return;
                }

                SaveData loadedData = result.Status == SaveLoadStatus.Missing ? new SaveData() : result.Data;
                // 적용 도중 예외가 발생한 부분 상태도 파일에 기록하지 않습니다.
                isSaveBlocked = true;
                if (!ApplyLoadedData(loadedData))
                {
                    hasLoadedData = false;
                    return;
                }

                saveData = loadedData;
                hasLoadedData = true;
                isSaveBlocked = false;
                LastError = null;
                loadSucceeded = true;
                if (result.Status == SaveLoadStatus.RecoveredBackup)
                    Debug.LogWarning("[SaveLoadManager] 정상 백업에서 세이브를 복구했습니다. 손상 원본은 다음 저장 시 별도 보관합니다.");
            }
            catch (System.Exception exception)
            {
                isSaveBlocked = true;
                LastError = exception.Message;
                Debug.LogError($"[SaveLoadManager] LoadGame 실패. 저장 파일을 보존합니다: {LastError}");
            }
            finally
            {
                isLoadingData = false;
                // 최초 초기화의 차단 상태와 실행 중 강제 로드의 해제 상태를 각각 복원합니다.
                preventAutoSave = previousPreventAutoSave;
            }

            if (loadSucceeded) StartCoroutine(NotifyUIAfterLoad());
        }

        private IEnumerator NotifyUIAfterLoad()
        {
            yield return null;

            var equipmentSlots = FindObjectsOfType<EquipmentSlotUI>();
            foreach (var slot in equipmentSlots)
            {
                slot.SendMessage("RefreshFromLoadedData", SendMessageOptions.DontRequireReceiver);
            }

            var skillUI = FindObjectOfType<SkillUIController>(); // (실제 클래스명으로 변경)
            if (skillUI != null)
            {
                skillUI.SyncUIFromData();
            }
        }

        private bool ApplyLoadedData(SaveData loadedData)
        {
            bool success = true;
            foreach (ISaveable entity in saveableEntities)
            {
                if (entity == null) continue;
                try
                {
                    entity.LoadFromSaveData(loadedData);
                }
                catch (System.Exception exception)
                {
                    success = false;
                    LastError = exception.Message;
                    Debug.LogError($"[SaveLoadManager] {entity.GetType().Name} 로드 중 오류. 저장을 차단합니다: {LastError}");
                }
            }
            return success;
        }

        private void OnApplicationQuit()
        {
            if (!isLoadingData && hasLoadedData)
            {
                ForceSaveGame();
            }
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus && !isLoadingData && hasLoadedData)
            {
                ForceSaveGame();
            }
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus && !isLoadingData && hasLoadedData)
            {
                ForceSaveGame();
            }
        }

        protected override void OnDestroy()
        {
            if (!isLoadingData && hasLoadedData)
            {
                ForceSaveGame();
            }
            base.OnDestroy();
        }

        public override void PopulateSaveData(SaveData saveData)
        {
            // SaveLoadManager 자체는 저장할 데이터가 없음 (다른 매니저들의 저장을 관리)
        }

        public override void LoadFromSaveData(SaveData saveData)
        {
            // SaveLoadManager 자체는 로드할 데이터가 없음 (다른 매니저들의 로드를 관리)
        }

        public SaveData CurrentSaveData
        {
            get
            {
                if (saveData == null)
                    saveData = new SaveData();

                return saveData;
            }
        }
    }
}
