using UnityEngine;
using Nytherion.GamePlay.Characters.Player;
using Nytherion.Core.Managers;
using Nytherion.Data.ScriptableObjects.Items;

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

        [Header("Databases")]
        [SerializeField] private ItemDatabaseSO itemDatabase;
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
            ItemDatabase.Initialize(itemDatabase);
            playerManager.Initialize();
            inputManager.Initialize();
            audioManager.Initialize();
            currencyManager.Initialize();
            inventoryManager.Initialize();
            engravingManager.Initialize();
            gachaManager.Initialize();
            shopManager.Initialize();
            saveLoadManager.Initialize();
        }
    }
}