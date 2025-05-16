using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Nytherion.Core
{
    /// <summary>
    /// 게임의 전반적인 상태를 관리하는 중앙 관리자 클래스입니다.
    /// 싱글톤 패턴으로 구현되어 어디서든 접근이 가능합니다.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        /// <summary>
        /// GameManager의 싱글톤 인스턴스에 접근합니다.
        /// </summary>
        public static GameManager Instance { get; private set; }
        /// <summary>
        /// 컴포넌트가 활성화될 때 호출됩니다.
        /// 싱글톤 인스턴스를 초기화하고 씬 전환 시 파괴되지 않도록 설정합니다.
        /// </summary>
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
    }
}

