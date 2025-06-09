using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Nytherion.UI.Shop
{
    public class ShopUI : MonoBehaviour
    {
        public static ShopUI Instance;

        public GameObject shopPanel;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
            }

            shopPanel.SetActive(false);
        }

        public void OpenShop()
        {
            shopPanel.SetActive(true);
        }

        public void CloseShop()
        {
            shopPanel.SetActive(false);
        }
    }
}
