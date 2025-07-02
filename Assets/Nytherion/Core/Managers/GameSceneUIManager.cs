using UnityEngine;
using Nytherion.UI.Inventory;
using Nytherion.UI.EngravingBoard;
using Nytherion.UI.Controllers;
using Nytherion.UI.Presenters;

namespace Nytherion.Core.Managers
{
    public class GameSceneUIManager : MonoBehaviour
    {
        [SerializeField] private InventoryPresenter inventoryPresenter;
        [SerializeField] private InventoryUI inventoryUI;
        [SerializeField] private QuickSlotManager quickSlotManager;
        [SerializeField] private EngravingGridUI engravingGridUI;

        private void Start()
        {
            InitializeSceneUI();
        }

        private void InitializeSceneUI()
        {
            inventoryPresenter.Initialize();
            inventoryUI.Initialize();

            if (engravingGridUI != null)
            {
                StartCoroutine(engravingGridUI.Initialize());
            }
        }
    }
}