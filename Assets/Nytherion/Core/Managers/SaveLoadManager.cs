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

                if (container.TryResolve<EngravingManager>(out var engravingManager))
                {
                    saveableEntities.Add(engravingManager);
                }

                if (container.TryResolve<EquipmentDataManager>(out var equipmentManager))
                {
                    saveableEntities.Add(equipmentManager);
                }

                if (container.TryResolve<ShopManager>(out var shopManager))
                {
                    saveableEntities.Add(shopManager);
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
            lastSaveTime = Time.time;
        }

        private void Update()
        {
            if (!isInitializationComplete || preventAutoSave) return;

            // 자동 저장 체크
            if (hasLoadedData && !isLoadingData && Time.time - lastSaveTime >= autoSaveInterval)
            {
                SaveGame();
                lastSaveTime = Time.time;
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

            yield return new WaitForSeconds(2f);
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
            if (isLoadingData)
            {
                return;
            }

            if (preventAutoSave)
            {
                return;
            }

            if (Time.time - lastSaveTime < SAVE_COOLDOWN)
            {
                return;
            }

            if (Application.isEditor && !Application.isPlaying)
            {
                return;
            }

            CollectSaveableEntities();

            if (saveableEntities == null || saveableEntities.Count == 0)
            {
                return;
            }

            if (saveData == null) saveData = new SaveData();

            int successCount = 0;
            foreach (var entity in saveableEntities)
            {
                if (entity != null)
                {
                    try
                    {
                        entity.PopulateSaveData(saveData);
                        successCount++;
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogError($"[SaveLoadManager] {entity.GetType().Name} 저장 중 오류: {e.Message}");
                    }
                }
            }

            try
            {
                saveService.Save(saveData);
                lastSaveTime = Time.time;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[SaveLoadManager] SaveService.Save() 실패: {e.Message}");
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
            CollectSaveableEntities();

            if (saveableEntities == null || saveableEntities.Count == 0)
            {
                Debug.LogWarning("[SaveLoadManager] LoadGame 실패 - ISaveable 엔티티가 없음");
                return;
            }

            isLoadingData = true;
            preventAutoSave = true;

            try
            {
                saveData = saveService.Load();

                if (saveData == null)
                {
                    saveData = new SaveData();
                    LoadNewGameData();
                }
                else
                {
                    LoadExistingGameData();
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[SaveLoadManager] LoadGame 중 예외 발생: {e.Message}");
                if (saveData == null)
                {
                    saveData = new SaveData();
                    LoadNewGameData();
                }
            }
            finally
            {
                isLoadingData = false;
                hasLoadedData = true;
            }

            StartCoroutine(NotifyUIAfterLoad());
        }

        private IEnumerator NotifyUIAfterLoad()
        {
            yield return null;

            var equipmentSlots = FindObjectsOfType<EquipmentSlotUI>();
            foreach (var slot in equipmentSlots)
            {
                slot.SendMessage("RefreshFromLoadedData", SendMessageOptions.DontRequireReceiver);
            }
        }

        private void LoadNewGameData()
        {
            foreach (var entity in saveableEntities)
            {
                if (entity != null)
                {
                    try
                    {
                        entity.LoadFromSaveData(saveData);
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogError($"[SaveLoadManager] {entity.GetType().Name} 새 게임 초기화 중 오류: {e.Message}");
                    }
                }
            }
        }

        private void LoadExistingGameData()
        {
            int successCount = 0;
            foreach (var entity in saveableEntities)
            {
                if (entity != null)
                {
                    try
                    {
                        entity.LoadFromSaveData(saveData);
                        successCount++;
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogError($"[SaveLoadManager] {entity.GetType().Name} 로드 중 오류: {e.Message}");
                        Debug.LogException(e);
                    }
                }
            }
        }

        private void OnApplicationQuit()
        {
            if (!isLoadingData && hasLoadedData)
            {
                SaveGame();
            }
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus && !isLoadingData && hasLoadedData)
            {
                SaveGame();
            }
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus && !isLoadingData && hasLoadedData)
            {
                SaveGame();
            }
        }

        protected override void OnDestroy()
        {
            if (!isLoadingData && hasLoadedData)
            {
                SaveGame();
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
    }
}