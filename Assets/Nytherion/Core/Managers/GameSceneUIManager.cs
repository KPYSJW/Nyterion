using UnityEngine;
using Nytherion.UI.EngravingBoard;
using Nytherion.UI.Controllers;
using Nytherion.UI.Presenters;
using Nytherion.Core.Data;
using VContainer;
using VContainer.Unity;

namespace Nytherion.Core.Managers
{
    public class GameSceneUIManager : BaseManager
    {
        private readonly GameSceneUIRefs gameSceneUIRefs;
        private readonly InventoryPresenter inventoryPresenter;
        private readonly InventoryUI inventoryUI;
        private EngravingGridUI engravingGridUI;

        [Inject]
        public GameSceneUIManager(GameSceneUIRefs gameSceneUIRefs,
            InventoryPresenter inventoryPresenter,
            InventoryUI inventoryUI,
            EngravingGridUI engravingGridUI)
        {
            this.gameSceneUIRefs = gameSceneUIRefs;
            this.inventoryPresenter = inventoryPresenter;
            this.inventoryUI = inventoryUI;
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