using UnityEngine.EventSystems;
using Nytherion.Data.ScriptableObjects.Items;
using Nytherion.Core.Managers;
using Nytherion.UI.Inventory.Utils;
using Nytherion.Data.ScriptableObjects.Weapons;
using Nytherion.GamePlay.Characters.Player;
using Nytherion.GamePlay.Combat;

namespace Nytherion.UI.Inventory
{
    public class EquipmentSlotUI : BaseSlotUI, IDropHandler
    {
        protected override void Awake()
        {
            base.Awake();
            OnBeginDragEvent += (s, e) => DragDropUIHandler.HandleBeginDragShared(s);
            OnEndDragEvent += (s, e) => DragDropUIHandler.HandleEndDragShared(s, e);
            OnPointerClickEvent += HandlePointerClick;
        }

        public void OnDrop(PointerEventData eventData)
        {
            if (eventData.pointerDrag == null) return;

            BaseSlotUI sourceSlot = eventData.pointerDrag.GetComponent<BaseSlotUI>();
            if (sourceSlot == null || sourceSlot.IsEmpty || sourceSlot == this)
            {
                return;
            }

            if (CanReceiveItem(sourceSlot.CurrentItem))
            {
                ItemData previousItem = CurrentItem;
                int previousCount = CurrentCount;

                SetItem(sourceSlot.CurrentItem, sourceSlot.CurrentCount);

                if (previousItem != null)
                {
                    sourceSlot.SetItem(previousItem, previousCount);
                }
                else
                {
                    sourceSlot.ClearSlot();
                }
            }
        }

        public override void SetItem(ItemData newItem, int count = 1)
        {
            base.SetItem(newItem, count);
            UpdatePlayerEquipment(newItem);
        }

        public override bool CanReceiveItem(ItemData item)
        {
            if (item is WeaponData)
            {
                return true;
            }
            return false;
        }

        private void UpdatePlayerEquipment(ItemData itemToEquip)
        {
            WeaponBase weaponPrefab = null;
            if (itemToEquip is WeaponData weaponData)
            {
                weaponPrefab = weaponData.weaponPrefab;
            }
            PlayerManager.Instance.PlayerCombat.EquipWeapon(weaponPrefab);
        }
        
        private void HandlePointerClick(BaseSlotUI slot, PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Right && !IsEmpty)
            {
                UnequipAndReturnToInventory();
            }
        }

        private void UnequipAndReturnToInventory()
        {
            if (IsEmpty) return;

            if (InventoryManager.Instance.AddItem(CurrentItem, CurrentCount))
            {
                ClearSlot();
            }
        }

        public override void ClearSlot()
        {
            ItemData itemToClear = CurrentItem;
            base.ClearSlot();
            if (itemToClear != null)
            {
                UpdatePlayerEquipment(null);
            }
        }
    }
}