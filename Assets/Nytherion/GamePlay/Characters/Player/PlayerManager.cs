using Nytherion.Core;
using Nytherion.Data.ScriptableObjects.Player;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Nytherion.GamePlay.Characters.Player
{

    [System.Serializable]
    public class PlayerManager : MonoBehaviour
    {
        public static PlayerManager Instance { get; private set; }

        public PlayerHealth playerHealth;
        public float currentHP;

        [SerializeField]
        [Tooltip("플레이어의 전투 컴포넌트 참조")]
        private PlayerCombat _playerCombat;

        public PlayerCombat PlayerCombat
        {
            get
            {
                if (_playerCombat == null)
                {
                    _playerCombat = GetComponent<PlayerCombat>();
                    if (_playerCombat == null)
                    {
                        Debug.LogError("PlayerCombat 컴포넌트를 찾을 수 없습니다.", this);
                    }
                }
                return _playerCombat;
            }
        }

        public PlayerEngravingManager playerEngravingManager;
        public PlayerData playerData;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);

            _playerCombat = GetComponent<PlayerCombat>();
            playerEngravingManager = GetComponent<PlayerEngravingManager>();
            playerHealth = GetComponent<PlayerHealth>();
            currentHP = playerHealth.CurrentHealth;
        }
    }
}


