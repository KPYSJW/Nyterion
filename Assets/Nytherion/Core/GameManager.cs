using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Nytherion.Core;

namespace Nytherion.Core
{
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }
        private void Update()
        {
            // F1 키를 누르면 1000 골드를 추가하는 테스트 코드
            if (Input.GetKeyDown(KeyCode.F1))
            {
                // AddGold 대신 AddCurrency를 사용합니다.
                CurrencyManager.Instance.AddCurrency(CurrencyType.Gold, 1000);
                Debug.Log("1000 골드가 추가되었습니다.");
            }
        }
    }
}

