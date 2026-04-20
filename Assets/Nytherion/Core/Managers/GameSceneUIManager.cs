using Nytherion.UI.EngravingBoard;
using Nytherion.UI.Controllers;
using Nytherion.UI.Presenters;
using Nytherion.Core.Data;
using VContainer;
using Nytherion.UI.Progression;
using Nytherion.UI.Skill;

namespace Nytherion.Core.Managers
{
    public class GameSceneUIManager : BaseManager
    {
        private GameSceneUIRefs gameSceneUIRefs;
        private InventoryPresenter inventoryPresenter;
        private InventoryUI inventoryUI;
        private EngravingGridUI engravingGridUI;
        private MilestoneUIController milestoneUI;
        private SkillUIController skillUI;

        [Inject]
        public void Construct(GameSceneUIRefs gameSceneUIRefs,
            InventoryPresenter inventoryPresenter,
            InventoryUI inventoryUI,
            EngravingGridUI engravingGridUI,
            MilestoneUIController milestoneUI,
            SkillUIController skillUI)
        {
            this.gameSceneUIRefs = gameSceneUIRefs;
            this.inventoryPresenter = inventoryPresenter;
            this.inventoryUI = inventoryUI;
            this.engravingGridUI = engravingGridUI;
            this.milestoneUI = milestoneUI;
            this.skillUI = skillUI;
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