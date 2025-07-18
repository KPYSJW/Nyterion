using UnityEngine;
using Nytherion.UI.EngravingBoard;
using Nytherion.UI.Controllers;
using Nytherion.UI.Presenters;
using Zenject;

namespace Nytherion.Core.Managers
{
    public class GameSceneUIManager : MonoBehaviour
    {
        [Inject] private InventoryPresenter inventoryPresenter;
        [Inject] private InventoryUI inventoryUI;
        [SerializeField] private EngravingGridUI engravingGridUI;

        [Inject]
        public void Initialize()
        {
            InitializeSceneUI();
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
                StartCoroutine(engravingGridUI.Initialize());
            }
        }
    }
}