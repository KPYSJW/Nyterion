using UnityEngine;
using Nytherion.GamePlay.Characters.Player;
using Nytherion.Core.Managers;

namespace Nytherion.Core.Systems
{

    public class GameInitializer : MonoBehaviour
    {
        public static GameInitializer Instance { get; private set; }

        [Header("System Managers")]
        [SerializeField] private InputManager inputManager;
        [SerializeField] private AudioManager audioManager;
        [SerializeField] private CurrencyManager currencyManager;
        [SerializeField] private InventoryManager inventoryManager;
        [SerializeField] private EngravingManager engravingManager;
        [SerializeField] private PlayerManager playerManager;
        [SerializeField] private GachaManager gachaManager;
        [SerializeField] private ShopManager shopManager;
        [SerializeField] private SaveLoadManager saveLoadManager;
        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }
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
            shopManager.Initialize();

            saveLoadManager.Initialize();
        }

    }
}