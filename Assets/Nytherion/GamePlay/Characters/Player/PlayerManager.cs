using Nytherion.Core;
using Nytherion.Data.ScriptableObjects.Player;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Nytherion.GamePlay.Characters.Player
{
    /// <summary>
    /// 플레이어 관련 전역 상태와 기능을 관리하는 싱글톤 매니저 클래스입니다.
    /// 이 클래스는 게임 전반에서 플레이어 인스턴스와 그 컴포넌트들에 대한 접근을 제공합니다.
    /// </summary>
    [System.Serializable]
    public class PlayerManager : MonoBehaviour
    {
        private static PlayerManager _instance;
        public float currentHP;
        /// <summary>
        /// PlayerManager의 싱글톤 인스턴스에 접근합니다.
        /// 인스턴스가 없을 경우 씬에서 자동으로 찾아 할당합니다.
        /// </summary>
        /// <exception cref="System.NullReferenceException">씬에 PlayerManager가 없는 경우</exception>
        public static PlayerManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<PlayerManager>();
                    if (_instance == null)
                    {
                        Debug.LogError("PlayerManager 인스턴스를 찾을 수 없습니다. 씬에 PlayerManager가 있는지 확인해주세요.");
                    }
                }
                return _instance;
            }
        }

        [SerializeField] 
        [Tooltip("플레이어의 전투 컴포넌트 참조")]
        private PlayerCombat _playerCombat;
        
        /// <summary>
        /// 플레이어의 전투 컴포넌트에 접근합니다.
        /// 필요한 경우 자동으로 컴포넌트를 찾아 할당합니다.
        /// </summary>
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
        /// <summary>
        /// 컴포넌트 초기화 시 호출됩니다.
        /// 싱글톤 인스턴스를 설정하고, 필요한 컴포넌트들을 초기화합니다.
        /// </summary>
        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
            
            // PlayerCombat 초기화
            if (_playerCombat == null)
            {
                _playerCombat = GetComponent<PlayerCombat>();
            }
            playerEngravingManager=GetComponent<PlayerEngravingManager>();
            currentHP = playerData.maxHealth;
        }

    }
}


