using UnityEngine;
using Nytherion.UI.EngravingBoard;
using Nytherion.UI.Controllers;
using Nytherion.UI.Presenters;
using Nytherion.Core.Data;
using Zenject;

namespace Nytherion.Core.Managers
{
    public class GameSceneUIManager : BaseManager
    {
        [Inject] private InventoryPresenter inventoryPresenter;
        [Inject] private InventoryUI inventoryUI;
        private EngravingGridUI engravingGridUI;

        [Inject]
        public void Construct(EngravingGridUI engravingGridUI)
        {
            this.engravingGridUI = engravingGridUI;
        }

        public override void Initialize()
        {
            InitializeSceneUI();
        }
        
        public override void PopulateSaveData(SaveData saveData)
        {
            
        }

        public override void LoadFromSaveData(SaveData saveData)
        {
            
        }

        private void InitializeSceneUI()
        {
            if (inventoryPresenter != null)
            {
                inventoryPresenter.Initialize();
            }

            if (inventoryUI != null)
            {
                inventoryUI.Initialize();
            }

            if (engravingGridUI != null)
            {
                engravingGridUI.Initialize();
            }
        }
    }
}