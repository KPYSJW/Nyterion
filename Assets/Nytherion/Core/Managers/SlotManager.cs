using Nytherion.GamePlay.Characters.Items;
using Nytherion.Core.Interfaces;
using System.Collections.Generic;
using UnityEngine;

namespace Nytherion.Core.Managers
{
    public class SlotManager : MonoBehaviour
    {
        [SerializeField] public List<IUseableItem> quickSlots = new List<IUseableItem>(10);
        public static SlotManager Instance { get; private set; }
        
        public List<WeaponItem>weaponBases = new List<WeaponItem>();

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
            SetItem(1, weaponBases[0]);
            SetItem(2, weaponBases[1]);
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
