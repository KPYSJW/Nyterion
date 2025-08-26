using UnityEngine;
using Nytherion.Services;
using Nytherion.Core.Data;
using Nytherion.Core.Interfaces;
using System.Collections;
using System.Collections.Generic;
using Zenject;

namespace Nytherion.Core.Managers
{
    public class SaveLoadManager : MonoBehaviour, IInitializable
    {
        private JsonSaveService saveService;
        private SaveData saveData;
        private bool isLoadingData = false;

        private List<ISaveable> saveableEntities;

        [Inject]
        public void Construct(
            List<ISaveable> saveableEntities)
        {
            this.saveableEntities = saveableEntities;
        }

        private void Awake()
        {
            saveService = new JsonSaveService();
        }

        public void Initialize()
        {
            LoadGame();
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
            isLoadingData = true;

            saveData = saveService.Load() ?? new SaveData();
            foreach (var entity in saveableEntities)
            {
                entity.LoadFromSaveData(saveData);
            }

            isLoadingData = false;
        }

        private void OnApplicationQuit()
        {
            if (!isLoadingData)
            {
                SaveGame();
            }
        }

    }
}