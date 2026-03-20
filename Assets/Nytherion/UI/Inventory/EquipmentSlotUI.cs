using Nytherion.Core.Enums;
using Nytherion.Core.Managers;
using Nytherion.Data.ScriptableObjects.Items;
using Nytherion.UI.Inventory.Utils;
using UnityEngine;
using UnityEngine.EventSystems;
using VContainer;
using VContainer.Unity;

namespace Nytherion.UI.Inventory
{
    /// <summary>
    /// 개별 장비 슬롯의 UI를 제어
    /// 아이템 드래그 앤 드롭 장착, 우클릭 해제 등의 상호작용 처리
    /// </summary>
    public class EquipmentSlotUI : BaseSlotUI, IDropHandler
    {
        [SerializeField] private EquipmentSlotType slotType;
        public EquipmentSlotType SlotType => slotType;

        private EquipmentDataManager equipmentDataManager;
        private InventoryDataManager inventoryDataManager;

        [Inject]
        public void Construct(EquipmentDataManager equipmentDataManager, InventoryDataManager inventoryDataManager)
        {
            this.equipmentDataManager = equipmentDataManager;
            this.inventoryDataManager = inventoryDataManager;
        }

        private void Start()
        {
            if (inventoryDataManager == null || equipmentDataManager == null)
            {
                var gameSceneScope = LifetimeScope.Find<GameSceneLifetimeScope>();
                if (gameSceneScope != null)
                {
                    if (inventoryDataManager == null && gameSceneScope.Container.TryResolve<InventoryDataManager>(out var invManager))
                    {
                        inventoryDataManager = invManager;
                    }

                    if (equipmentDataManager == null && gameSceneScope.Container.TryResolve<EquipmentDataManager>(out var equipManager))
                    {
                        equipmentDataManager = equipManager;
                    }
                }
            }

            InitializeEquipmentState();
        }

        /// <summary>
        /// 데이터 매니저에서 현재 장착된 장비 정보를 가져와 UI를 초기화
        /// </summary>
        private void InitializeEquipmentState()
        {
            if (equipmentDataManager != null)
            {
                var currentEquipment = equipmentDataManager.GetEquipment(this.slotType);
                if (currentEquipment != null)
                {
                    base.SetItem(currentEquipment, 1);
                }
            }
        }

        public void RefreshFromLoadedData()
        {
            InitializeEquipmentState();
        }

        protected override void Awake()
        {
            base.Awake();
            OnBeginDragEvent += (s, e) => DragDropUIHandler.HandleBeginDragShared(s);
            OnPointerClickEvent += HandlePointerClick;
            OnEndDragEvent += HandleEndDrag;
        }

        protected override void HandleEndDrag(BaseSlotUI slot, PointerEventData eventData)
        {
            base.HandleEndDrag(slot, eventData);

            DragDropUIHandler.HandleEndDragShared(slot, eventData);

            if (iconImage != null)
            {
                iconImage.enabled = !IsEmpty;
            }
        }

        public void OnEnable()
        {
            if (equipmentDataManager != null)
            {
                equipmentDataManager.OnEquipmentChanged += HandleEquipmentChanged;

                var currentEquipment = equipmentDataManager.GetEquipment(this.slotType);
                if (currentEquipment != null && base.IsEmpty)
                {
                    base.SetItem(currentEquipment, 1);
                }
            }
        }

        public void OnDisable()
        {
            if (equipmentDataManager != null)
            {
                equipmentDataManager.OnEquipmentChanged -= HandleEquipmentChanged;
            }
        }

        /// <summary>
        /// 장비 데이터가 변경되었을 때ㅑUI를 갱신
        /// </summary>
        private void HandleEquipmentChanged(EquipmentSlotType changedSlotType, EquipmentData newItem, EquipmentData oldItem)
        {
            if (changedSlotType == this.slotType)
            {
                base.SetItem(newItem, newItem == null ? 0 : 1);
            }
        }

        /// <summary>
        /// 다른 UI에서 아이템을 이 슬롯으로 드롭했을 때 호출
        /// </summary>
        public void OnDrop(PointerEventData eventData)
        {
            // 드래그 중인 오브젝트가 있는지 확인, 없다면 종료
            if (eventData.pointerDrag == null) return;

            // 드래그해서 가져온 오브젝트가 BaseSlotUI 컴포넌트를 가지고 있는지 확인
            BaseSlotUI sourceBaseSlot = eventData.pointerDrag.GetComponent<BaseSlotUI>();

            // ㄱ가져온게 슬롯 UI가 아니거나 가져온 슬롯이 빈 칸이거나, 자기 자신을 클릭하고 제자리에 그대로 놓을 때 종료
            if (sourceBaseSlot == null || sourceBaseSlot.IsEmpty || sourceBaseSlot == this) return;

            // 아이템이 인벤토리 슬롯에서 왓는지 확인하고 장비 슬롯에 맞는 타입인지 검사
            if (sourceBaseSlot is InventorySlotUI sourceSlot && CanReceiveItem(sourceSlot.CurrentItem))
            {
                // 아이템 정보 추출
                (ItemData itemToEquip, int count) = sourceSlot.GetItemInfo();
                if (itemToEquip == null) return;

                // 인벤토리에서 제거 후 장착
                if (inventoryDataManager.RemoveItemFromSlot(sourceSlot.SlotIndex, 1))
                {
                    // 기존에 장착된 아이템이 있으면 인벤토리로 반환 
                    if (!IsEmpty)
                    {
                        inventoryDataManager.AddItem(CurrentItem, 1);
                    }
                    SetItem(itemToEquip, 1);
                    DragDropUIHandler.dropHandled = true;
                }
            }
        }

        public override void SetItem(ItemData newItem, int count = 1)
        {
            base.SetItem(newItem, count);
            UpdateEquipment(newItem);
        }

        /// <summary>
        /// 해당 아이템이 장비 슬롯에 장착 가능한지 검사
        /// </summary>
        /// <param name="item">검사할 아이템 데이터</param>
        /// <returns>장착 가능 여부</returns>
        public override bool CanReceiveItem(ItemData item)
        {
            // 드래그해 온 아이템 데이터가 비어있거나 아이템이 장비 타입이 아닌 경우 종료
            if (item == null || !(item is EquipmentData equipment)) return false;

            switch (equipment.equipmentType)
            {
                case EquipmentType.Weapon:
                    return this.slotType == EquipmentSlotType.Weapon;
                case EquipmentType.Armor:
                    // 바어구 고유 데이터릴 읽기 위해 ArmorDat로 변환
                    if (equipment is ArmorData armor)
                    {
                        // 방어구 타입을 확인하고 해당하는 슬롯에만 들어가도록 맞춤
                        switch (armor.armorType)
                        {
                            case ArmorType.Helmet: return this.slotType == EquipmentSlotType.Helmet;
                            case ArmorType.Armor: return this.slotType == EquipmentSlotType.Armor;
                            case ArmorType.Boots: return this.slotType == EquipmentSlotType.Boots;
                            case ArmorType.Accessory: return this.slotType == EquipmentSlotType.Accessory;
                        }
                    }
                    return false;
                default:
                    return false;
            }
        }

        /// <summary>
        /// 실제 데이터 매니저에 장비 상태를 업데이트하도록 요청
        /// </summary>
        private void UpdateEquipment(ItemData itemToEquip)
        {
            if (equipmentDataManager == null)
            {
                Debug.LogError("equipmentDataManager가 null");
                return;
            }

            EquipmentData equipment = itemToEquip as EquipmentData;

            if (equipment != null)
            {
                // 인스턴스 ID가 없으면 고유 ID 부여
                if (string.IsNullOrEmpty(equipment.instanceId))
                {
                    equipment.instanceId = System.Guid.NewGuid().ToString();
                }
            }

            equipmentDataManager.SetEquipment(this.slotType, equipment, false);
        }

        /// <summary>
        /// 현재 장착된 아이템을 해제하고 인벤토리로 반환
        /// </summary>
        private void HandlePointerClick(BaseSlotUI slot, PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Right && !IsEmpty)
            {
                UnequipAndReturnToInventory();
            }
        }

        public void UnequipAndReturnToInventory()
        {
            if (IsEmpty)
            {
                Debug.Log($"{slotType} 슬롯이 비어있어 해제를 취소");
                return;
            }
            Debug.Log($"{slotType} 슬롯 해제 시도 시작");

            bool isAddedToInventory = inventoryDataManager.AddItem(CurrentItem, 1);

            if (isAddedToInventory)
            {
                Debug.Log("인벤토리 추가 성공. UI 슬롯을 비운다");
                ClearSlot();
            }
            else
            {
                Debug.LogError("인벤토리 추가 실패");
            }
        }

        public override void ClearSlot()
        {
            ItemData itemToClear = CurrentItem;
            base.ClearSlot();
            if (itemToClear != null)
            {
                UpdateEquipment(null);
            }
        }
    }
}