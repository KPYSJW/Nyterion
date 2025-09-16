using UnityEngine;
using Nytherion.Services;
using Nytherion.Core.Data;
using Nytherion.Core.Interfaces;
using System.Collections;
using Nytherion.UI.Inventory;
using System.Collections.Generic;
using VContainer;
using VContainer.Unity;

namespace Nytherion.Core.Managers
{
    public class SaveLoadManager : MonoBehaviour, IInitializable
    {
        private JsonSaveService saveService;
        private SaveData saveData;
        private bool isLoadingData = false;
        private bool hasLoadedData = false;

        private IReadOnlyList<ISaveable> saveableEntities;

        [Inject]
        public void Construct(
            IReadOnlyList<ISaveable> saveableEntities)
        {
            this.saveableEntities = saveableEntities;
        }

        private void Awake()
        {
            saveService = new JsonSaveService();
        }

        public void Initialize()
        {
            StartCoroutine(DelayedLoadCoroutine());
        }

        private System.Collections.IEnumerator DelayedLoadCoroutine()
        {
            yield return null;
            yield return new UnityEngine.WaitForSeconds(0.1f);

            LoadGame();
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
            if (isLoadingData) return;
            if (saveData == null) saveData = new SaveData();


            foreach (var entity in saveableEntities)
            {
                entity.PopulateSaveData(saveData);
            }
            saveService.Save(saveData);
        }
       
        public void LoadGame()
        {
            if (hasLoadedData)
            {
                return;
            }

            isLoadingData = true;


            saveData = saveService.Load() ?? new SaveData();
            foreach (var entity in saveableEntities)
            {
                entity.LoadFromSaveData(saveData);
            }

            isLoadingData = false;
            hasLoadedData = true;


            StartCoroutine(NotifyUIAfterLoad());
        }

        private System.Collections.IEnumerator NotifyUIAfterLoad()
        {
            yield return null;

            var equipmentSlots = FindObjectsOfType<EquipmentSlotUI>();
            foreach (var slot in equipmentSlots)
            {
                slot.SendMessage("RefreshFromLoadedData", SendMessageOptions.DontRequireReceiver);
            }
        }

        private void OnApplicationQuit()
        {
            if (!isLoadingData)
            {
                SaveGame();
            }
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus && !isLoadingData)
            {
                SaveGame();
            }
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus && !isLoadingData)
            {
                SaveGame();
            }
        }

    }
}