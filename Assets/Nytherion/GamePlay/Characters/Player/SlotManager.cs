using Nytherion.Characters.Player;
using Nytherion.Gameplay.Combat;
using Nytherion.Interfaces;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace Nytherion.Core
{
    public class SlotManager : MonoBehaviour
    {
        [SerializeField] public List<IUseableItem> quickSlots = new List<IUseableItem>(10);
        public static SlotManager Instance;

        
        public List<WeaponItem>weaponBases = new List<WeaponItem>();//테스트용 코드

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }
        void Start()
        {
            InputManager.Instance.onQuickSlotInput += UseSlot;
            for (int i = 0; i < 10; ++i) quickSlots.Add(null);
            SetItem(1, weaponBases[0]);//테스트용 코드
            SetItem(2, weaponBases[1]);//테스트용 코드
        }
        public void UseSlot(int index)
        {
            Debug.Log(index);
            if (index < 0 || quickSlots[index] == null || index > 9) return;

            quickSlots[index]?.Use();
        }

        public void SetItem(int index,IUseableItem Item)
        {
           
            if (index < 0 || quickSlots[index] != null || index > 9) return;
            quickSlots[index]=Item;
        }

        private void OnDisable()
        {
            InputManager.Instance.onQuickSlotInput -= UseSlot;
        }
    }

}
