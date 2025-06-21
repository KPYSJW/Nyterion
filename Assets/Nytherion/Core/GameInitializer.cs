using UnityEngine;
using Nytherion.GamePlay.Characters.Player;
using Nytherion.UI.Inventory;
using Nytherion.UI.EngravingBoard;

namespace Nytherion.Core
{

    public class GameInitializer : MonoBehaviour
    {
        [Header("System Managers")]
        [SerializeField] private InputManager inputManager;
        [SerializeField] private AudioManager audioManager;
        [SerializeField] private CurrencyManager currencyManager;
        [SerializeField] private InventoryManager inventoryManager;
        [SerializeField] private EngravingManager engravingManager;
        [SerializeField] private PlayerManager playerManager;
        [SerializeField] private GachaManager gachaManager;

        [Header("UI Controllers & Presenters")]
        [SerializeField] private InventoryPresenter inventoryPresenter;
        [SerializeField] private InventoryUI inventoryUI;
        [SerializeField] private EngravingGridUI engravingGridUI;

        private void Start()
        {
            InitializeAllSystems();
        }

        private void InitializeAllSystems()
        {
            inputManager.Initialize();
            audioManager.Initialize();
            currencyManager.Initialize();
            inventoryManager.Initialize();
            engravingManager.Initialize();
            playerManager.Initialize();
            gachaManager.Initialize();
            inventoryPresenter.Initialize();
            inventoryUI.Initialize();
            inventoryUI.RefreshUI();
            if (engravingGridUI != null)
            {
                StartCoroutine(engravingGridUI.Initialize());
            }
        }
    }
}