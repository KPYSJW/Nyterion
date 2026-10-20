using Nytherion.Core.Enums;
using Nytherion.Core.Managers;
using Nytherion.Data.ScriptableObjects.Items;
using Nytherion.Data.ScriptableObjects.Weapons;
using Nytherion.UI.Components;
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
            if (this.equipmentDataManager != null)
            {
                this.equipmentDataManager.OnEquipmentChanged -= HandleEquipmentChanged;
            }
            this.equipmentDataManager = equipmentDataManager;
            this.inventoryDataManager = inventoryDataManager;
            if (isActiveAndEnabled)
            {
                SubscribeAndRefreshEquipment();
            }
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

            SubscribeAndRefreshEquipment();
        }

        /// <summary>
        /// 데이터 매니저에서 현재 장착된 장비 정보를 가져와 UI를 초기화
        /// </summary>
        private void InitializeEquipmentState()
        {
            if (equipmentDataManager != null)
            {
                var currentEquipment = equipmentDataManager.GetEquipment(this.slotType);
                base.SetItem(currentEquipment, currentEquipment == null ? 0 : 1);
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

        protected override void UpdateVisuals(ItemData item, int count)
        {
            if (item != null && count > 0 && iconImage != null && iconImage.transform != transform)
                PixelPerfectItemIcon.Apply(iconImage, transform as RectTransform,
                    item is WeaponData weaponData ? weaponData.inventoryIconScale : 1f, item is WeaponData,
                    item is WeaponData weapon ? weapon.inventoryIconSizeMultiplier : 1f);
            base.UpdateVisuals(item, count);
        }

        protected override void ApplyIconLayout()
        {
            // 보관함과 같은 배율, 원본 방향, 픽셀 정렬을 사용한다.
            if (iconImage != null) iconImage.GetComponent<PixelPerfectItemIcon>()?.Refresh();
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
            SubscribeAndRefreshEquipment();
        }

        private void SubscribeAndRefreshEquipment()
        {
            if (equipmentDataManager != null)
            {
                // OnEnable 이후 주입되거나 Start에서 참조를 확보해도 구독을 보장한다.
                equipmentDataManager.OnEquipmentChanged -= HandleEquipmentChanged;
                equipmentDataManager.OnEquipmentChanged += HandleEquipmentChanged;
                InitializeEquipmentState();
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
        /// 장비 데이터가 변경되었을 때 UI를 갱신
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
            if (eventData.button != PointerEventData.InputButton.Left) return;
            // 드래그 중인 오브젝트가 있는지 확인, 없다면 종료
            if (eventData.pointerDrag == null) return;

            // 드래그해서 가져온 오브젝트가 BaseSlotUI 컴포넌트를 가지고 있는지 확인
            BaseSlotUI sourceBaseSlot = eventData.pointerDrag.GetComponent<BaseSlotUI>();

            // ㄱ가져온게 슬롯 UI가 아니거나 가져온 슬롯이 빈 칸이거나, 자기 자신을 클릭하고 제자리에 그대로 놓을 때 종료
            if (sourceBaseSlot == null || sourceBaseSlot.IsEmpty || sourceBaseSlot == this) return;

            // 아이템이 인벤토리 슬롯에서 왓는지 확인하고 장비 슬롯에 맞는 타입인지 검사
            if (sourceBaseSlot is InventorySlotUI sourceSlot && CanReceiveItem(sourceSlot.CurrentItem))
            {
                DragDropUIHandler.dropHandled = true;
                TryEquipFromInventory(sourceSlot);
            }
        }

        public bool TryEquipFromInventory(InventorySlotUI sourceSlot)
        {
            return sourceSlot != null && equipmentDataManager != null &&
                equipmentDataManager.TryEquipFromInventory(slotType, sourceSlot.SlotIndex, sourceSlot.CurrentItem as EquipmentData);
        }

        public bool TryMoveToInventory(InventorySlotUI targetSlot)
        {
            return targetSlot != null && equipmentDataManager != null &&
                equipmentDataManager.TryUnequipToInventory(slotType, CurrentItem as EquipmentData, targetSlot.SlotIndex);
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
                default:
                    return false;
            }
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
            if (equipmentDataManager != null &&
                equipmentDataManager.TryUnequipToInventory(slotType, CurrentItem as EquipmentData))
            {
                PlayUISound(UISoundType.Place);
            }
            else
            {
                Debug.LogWarning("[EquipmentSlotUI] 빈 인벤토리 슬롯이 없어 장비 해제를 취소했습니다.");
                PlayUISound(UISoundType.Error);
            }
        }
    }
}
