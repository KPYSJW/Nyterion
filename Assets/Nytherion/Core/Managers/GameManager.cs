using UnityEngine;
using Zenject;

namespace Nytherion.Core.Managers
{
    public class GameManager : MonoBehaviour
    {
        private CurrencyManager currencyManager;
        [Inject]
        public void Construct(CurrencyManager currencyManager)
        {
            this.currencyManager = currencyManager;
        }
        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F1))
            {
                currencyManager.AddCurrency(CurrencyType.Gold, 1000);
                Debug.Log("1000 골드가 추가되었습니다.");
            }
            if (Input.GetKeyDown(KeyCode.F2))
            {
                currencyManager.AddCurrency(CurrencyType.Token, 10);
                Debug.Log("10 토큰이 추가되었습니다.");
            }
        }
    }
}

