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
            if (Input.GetKeyDown(KeyCode.F1))
            {
                CurrencyManager.Instance.AddCurrency(CurrencyType.Gold, 1000);
                Debug.Log("1000 골드가 추가되었습니다.");
            }
            if (Input.GetKeyDown(KeyCode.F2))
            {
                CurrencyManager.Instance.AddCurrency(CurrencyType.Token, 10);
                Debug.Log("10 토큰이 추가되었습니다.");
            }
        }
    }
}

