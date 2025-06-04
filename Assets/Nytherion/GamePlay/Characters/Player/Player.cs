using Nytherion.Core;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Nytherion.GamePlay.Characters.Player
{
    /// <summary>
    /// 플레이어 캐릭터를 나타내는 메인 클래스입니다.
    /// 싱글톤 패턴을 사용하여 게임 내에서 단일 인스턴스로 관리됩니다.
    /// </summary>
    public class Player : MonoBehaviour
    {
        [Header("Player Components")]
        [Tooltip("플레이어의 싱글톤 인스턴스")]
        public static Player Instance { get; private set; }
        
        [Tooltip("플레이어의 전투 시스템을 관리하는 컴포넌트")]
        public PlayerCombat playerCombat;

        /// <summary>
        /// 게임 오브젝트가 활성화될 때 호출됩니다.
        /// 싱글톤 인스턴스를 설정하고 필요한 컴포넌트를 초기화합니다.
        /// </summary>
        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            playerCombat = GetComponent<PlayerCombat>();
        }

    }
}


