using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Nytherion.Core.Enums;
using Nytherion.Core.Managers;
using Nytherion.Data.ScriptableObjects.Items;
using Nytherion.Data.ScriptableObjects.Weapons;
using Nytherion.Core.Data;
using Nytherion.Core.Systems;
using Nytherion.Services;
using Nytherion.UI.Inventory;
using Nytherion.UI.Inventory.Utils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Nytherion.Editor
{
    [InitializeOnLoad]
    public static class InventoryEquipmentSwapVerification
    {
        private const string Output = "output/inventory-equipment-swap";
        private static readonly MethodInfo Click = typeof(InventorySlotUI).GetMethod(
            "HandlePointerClick", BindingFlags.Instance | BindingFlags.NonPublic);

        static InventoryEquipmentSwapVerification()
        {
            EditorApplication.update += Update;
        }

        private static void Update()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(Output + "/verify.request")) return;
            File.Delete(Output + "/verify.request");
            Verify();
        }

        [MenuItem("Tools/Nytherion/Inventory/Verify Equipment Swap")]
        public static void Verify()
        {
            Directory.CreateDirectory(Output);
            var results = new List<string>();
            try
            {
                VerifyCase(false, true);
                results.Add("PASS 앞쪽 빈 슬롯이 있어도 기존 장비는 클릭한 7번 슬롯으로 반환, 같은 ID의 다른 인스턴스 보존");
                VerifyCase(true, true);
                results.Add("PASS 인벤토리가 가득 차도 교환, 기존 무기 인스턴스/등급 유지, 중복/유실 없음");
                VerifyCase(false, false);
                results.Add("PASS 빈 장착 칸에 장착 후 보관 슬롯 비움");
                VerifyCase(true, true, 1);
                results.Add("PASS 인벤토리 → 장착 칸 드롭: 교체 완료 후에만 알림, 각 인스턴스 1개 보존");
                VerifyCase(true, true, 2);
                results.Add("PASS 장착 칸 → 인벤토리 드롭: 교체 완료 후에만 알림, 각 인스턴스 1개 보존");
                VerifyCase(true, true, 3);
                VerifyCase(true, true, 4);
                results.Add("PASS 드롭 이벤트 누락 시 양방향 폴백도 데이터 교환 경로 사용");
                VerifyRejectedTransfersAndUnequip();
                results.Add("PASS 가득 찬 상태 해제·오래된 선택·분해 예약·잘못된 슬롯 거부, 실패 시 상태와 알림 보존");
                VerifyEquipmentGrant();
                results.Add("PASS 공간 부족·0/음수·일부 수용만 가능한 지급 거부: 복제본·알림 증가 없음, 전체 지급 성공 시 알림 1회");
                results.Add("PASS OnEnable 이후 주입 시 장착 UI 즉시 갱신, 반복 구독 시 이벤트 1회, 비활성 중 변경 후 재활성/해제 동기화");
            }
            catch (Exception error)
            {
                results.Add("FAIL " + error);
                Debug.LogException(error);
            }
            File.WriteAllLines(Output + "/verification.txt", results);
        }

        private static void VerifyCase(bool full, bool occupied, int transferPath = 0)
        {
            Scene scene = EditorSceneManager.NewPreviewScene();
            var assets = new List<WeaponData>();
            try
            {
                var managers = CreateObject(scene, "검증 매니저");
                InventoryDataManager inventory = managers.AddComponent<InventoryDataManager>();
                inventory.Initialize();
                EquipmentDataManager equipment = managers.AddComponent<EquipmentDataManager>();
                equipment.Initialize();
                equipment.Construct(inventory, null);
                WeaponData a = CreateWeapon(assets, "a", Rarity.Rare);
                WeaponData b = CreateWeapon(assets, "b", Rarity.Epic);
                WeaponData sameId = Object.Instantiate(a);
                sameId.instanceId = Guid.NewGuid().ToString();
                assets.Add(sameId);
                inventory.AddItemToSlot(sameId, 1, 1);
                inventory.AddItemToSlot(a, 1, 7);
                if (full)
                {
                    for (int i = 0; i < inventory.MaxSlotCount; i++)
                    {
                        if (inventory.GetSlot(i).item == null)
                            inventory.AddItemToSlot(CreateWeapon(assets, "기타 " + i, Rarity.Common), 1, i);
                    }
                }
                if (occupied) equipment.SetEquipment(EquipmentSlotType.Weapon, b, false);
                int originalCount = inventory.GetAllItems().Count + (occupied ? 1 : 0);
                var equippedSlot = CreateObject(scene, "장착 슬롯").AddComponent<EquipmentSlotUI>();
                equippedSlot.OnEnable(); // 의존성 주입보다 먼저 활성화되는 순서를 재현한다.
                equippedSlot.Construct(equipment, inventory);
                Require(ReferenceEquals(equippedSlot.CurrentItem, occupied ? b : null), "주입 직후 장착 칸 동기화");
                equippedSlot.OnEnable();
                equippedSlot.OnEnable();
                int equipmentUiUpdates = 0;
                equippedSlot.OnSlotUpdated += _ => equipmentUiUpdates++;
                var source = CreateObject(scene, "보관 슬롯").AddComponent<InventorySlotUI>();
                source.Construct(equipment, null, inventory, null, null);
                source.Initialize(7);
                source.SetItem(a, 1);
                inventory.OnDataChanged += _ =>
                {
                    var slot = inventory.GetSlot(7);
                    source.SetItem(slot.item, slot.count);
                };
                int inventoryNotifications = 0;
                int equipmentNotifications = 0;
                Action checkCommittedState = () =>
                {
                    Require(ReferenceEquals(equipment.GetEquipment(EquipmentSlotType.Weapon), a), "알림 시점 장착 데이터 확정");
                    Require(ReferenceEquals(inventory.GetSlot(7).item, occupied ? b : null), "알림 시점 보관 데이터 확정");
                    SaveData snapshot = Capture(inventory, equipment);
                    RequireUniqueInstances(snapshot, originalCount);
                };
                inventory.OnDataChanged += _ => { inventoryNotifications++; checkCommittedState(); };
                Action<EquipmentSlotType, EquipmentData, EquipmentData> equipmentStateCheck = (_, __, ___) =>
                { equipmentNotifications++; checkCommittedState(); };
                equipment.OnEquipmentChanged += equipmentStateCheck;
                if (transferPath == 0)
                    Click.Invoke(source, new object[] { source,
                        new PointerEventData(null) { button = PointerEventData.InputButton.Right } });
                else if (transferPath == 1)
                    equippedSlot.OnDrop(new PointerEventData(null) { button = PointerEventData.InputButton.Left, pointerDrag = source.gameObject });
                else if (transferPath == 2)
                    source.OnDrop(new PointerEventData(null) { button = PointerEventData.InputButton.Left, pointerDrag = equippedSlot.gameObject });
                else if (transferPath == 3)
                    SlotTransferHelper.TransferItem(source, equippedSlot);
                else
                    SlotTransferHelper.TransferItem(equippedSlot, source);
                Require(inventoryNotifications == 1 && equipmentNotifications == 1, "완료 알림 각각 1회");
                // 이후 UI 구독 검증에서는 다른 장비를 임의 설정하므로 위 상태 관찰을 해제한다.
                equipment.OnEquipmentChanged -= equipmentStateCheck;
                Require(ReferenceEquals(equipment.GetEquipment(EquipmentSlotType.Weapon), a), "a 장착 데이터");
                Require(ReferenceEquals(equippedSlot.CurrentItem, a), "a 장착 칸 표시");
                Require(equipmentUiUpdates == 1, "장비 변경당 UI 갱신 1회");
                Require(ReferenceEquals(inventory.GetSlot(7).item, occupied ? b : null), "b 원래 보관 위치 반환");
                Require(ReferenceEquals(source.CurrentItem, occupied ? b : null), "보관 칸 표시 동기화");
                Require(ReferenceEquals(inventory.GetSlot(1).item, sameId), "같은 ID의 다른 무기 보존");
                Require(inventory.GetAllItems().Count + 1 == originalCount, "아이템 총수 보존");
                Require(b.rarity == Rarity.Epic && a.rarity == Rarity.Rare, "등급 보존");
                string path = Path.GetFullPath(Output + $"/snapshot-{full}-{occupied}-{transferPath}.json");
                var service = new JsonSaveService(path);
                Require(service.Save(Capture(inventory, equipment)), "교체 직후 격리 파일 저장");
                SaveLoadResult loaded = service.LoadWithResult();
                Require(loaded.IsSuccess, "저장 파일 다시 읽기");
                RequireUniqueInstances(loaded.Data, originalCount);
                Require(loaded.Data.equippedItemsData[0].instanceId == a.instanceId &&
                    loaded.Data.equippedItemsData[0].rarity == a.rarity, "파일의 장착 인스턴스와 등급");
                VerifyRestoredState(scene, assets, loaded.Data);
                equippedSlot.OnDisable();
                equipment.SetEquipment(EquipmentSlotType.Weapon, b, false);
                equippedSlot.OnEnable();
                Require(ReferenceEquals(equippedSlot.CurrentItem, b), "재활성화 시 이전 아이콘 갱신");
                equippedSlot.OnDisable();
                equipment.SetEquipment(EquipmentSlotType.Weapon, null, false);
                equippedSlot.OnEnable();
                Require(equippedSlot.IsEmpty, "재활성화 시 해제된 슬롯 비움");
                equippedSlot.OnDisable();
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
                foreach (WeaponData asset in assets) Object.DestroyImmediate(asset);
            }
        }

        private static void VerifyRejectedTransfersAndUnequip()
        {
            Scene scene = EditorSceneManager.NewPreviewScene();
            var assets = new List<WeaponData>();
            try
            {
                var managers = CreateObject(scene, "실패 경로 검증");
                var inventory = managers.AddComponent<InventoryDataManager>();
                inventory.Initialize();
                var equipment = managers.AddComponent<EquipmentDataManager>();
                equipment.Initialize();
                equipment.Construct(inventory, null);
                WeaponData equipped = CreateWeapon(assets, "착용", Rarity.Epic);
                equipment.SetEquipment(EquipmentSlotType.Weapon, equipped, false);
                for (int i = 0; i < inventory.MaxSlotCount; i++)
                    inventory.AddItemToSlot(CreateWeapon(assets, "보관" + i, Rarity.Common), 1, i);
                string before = JsonUtility.ToJson(Capture(inventory, equipment));
                int notifications = 0;
                inventory.OnDataChanged += _ => notifications++;
                equipment.OnEquipmentChanged += (_, __, ___) => notifications++;
                Require(!equipment.TryUnequipToInventory(EquipmentSlotType.Weapon, equipped), "가득 찬 보관함 해제 거부");
                Require(!equipment.TryEquipFromInventory(EquipmentSlotType.Weapon, 0, equipped), "오래된 선택 거부");
                Require(!equipment.TryUnequipToInventory(EquipmentSlotType.Weapon, equipped, inventory.MaxSlotCount), "잘못된 대상 거부");
                Require(before == JsonUtility.ToJson(Capture(inventory, equipment)) && notifications == 0, "실패 시 상태·알림 불변");

                EquipmentData reserved = inventory.GetSlot(0).item as EquipmentData;
                Require(inventory.StageForDismantle(0, reserved), "분해 예약 설정");
                notifications = 0;
                Require(!equipment.TryEquipFromInventory(EquipmentSlotType.Weapon, 0, reserved), "예약 장비 장착 거부");
                Require(!equipment.TryUnequipToInventory(EquipmentSlotType.Weapon, equipped, 0), "예약 슬롯 해제 거부");
                Require(inventory.InventoryModel.GetStoredItemAt(0).item == reserved && notifications == 0, "예약 장비 보존");
                inventory.ResetDismantle();
                inventory.RemoveItemFromSlot(3);
                notifications = 0;
                Require(equipment.TryUnequipToInventory(EquipmentSlotType.Weapon, equipped), "빈 슬롯 확보 후 해제");
                Require(equipment.GetEquipment(EquipmentSlotType.Weapon) == null && inventory.GetSlot(3).item == equipped,
                    "해제 시 기존 인스턴스 유지");
                Require(notifications == 2, "해제 완료 알림 각각 1회");
                RequireUniqueInstances(Capture(inventory, equipment), inventory.MaxSlotCount);
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
                foreach (WeaponData asset in assets) Object.DestroyImmediate(asset);
            }
        }

        private static void VerifyEquipmentGrant()
        {
            Scene scene = EditorSceneManager.NewPreviewScene();
            var assets = new List<WeaponData>();
            try
            {
                var inventory = CreateObject(scene, "장비 지급 검증").AddComponent<InventoryDataManager>();
                inventory.Initialize();
                // AddItem은 원본 에셋으로 신규 지급하고, 등급 결정 인스턴스는 AddEquipmentInstance를 사용한다.
                WeaponData template = CreateWeapon(assets, "지급 원본", Rarity.Common);
                for (int i = 0; i < inventory.MaxSlotCount; i++)
                    inventory.AddItemToSlot(CreateWeapon(assets, "채움" + i, Rarity.Common), 1, i);
                int notifications = 0;
                inventory.OnDataChanged += _ => notifications++;
                int originalObjects = Resources.FindObjectsOfTypeAll<WeaponData>().Length;
                for (int i = 0; i < 10; i++) Require(!inventory.AddItem(template), "가득 찬 상태 지급 실패");
                Require(!inventory.AddItem(template, 0) && !inventory.AddItem(template, -1), "양수가 아닌 지급 거부");
                Require(notifications == 0 && Resources.FindObjectsOfTypeAll<WeaponData>().Length == originalObjects,
                    "전체 실패 시 복제본·알림 없음");
                inventory.RemoveItemFromSlot(0);
                notifications = 0;
                Require(!inventory.AddItem(template, 2) && inventory.GetSlot(0).item == null && notifications == 0,
                    "일부 수용만 가능하면 전체 거부");
                inventory.RemoveItemFromSlot(1);
                notifications = 0;
                Require(inventory.AddItem(template, 2), "두 개 전체 지급");
                var first = inventory.GetSlot(0).item as WeaponData;
                var second = inventory.GetSlot(1).item as WeaponData;
                assets.Add(first);
                assets.Add(second);
                Require(first != template && second != template && first != second && first.instanceId != second.instanceId,
                    "신규 지급은 각각 새 인스턴스 생성");
                Require(first.rarity == template.rarity && second.rarity == template.rarity && notifications == 1,
                    "지급 등급 유지 및 알림 한 번");
                inventory.RemoveItemFromSlot(0);
                WeaponData rolled = CreateWeapon(assets, "등급 결정 인스턴스", Rarity.Legendary);
                Require(inventory.AddEquipmentInstance(rolled) && inventory.GetSlot(0).item == rolled && rolled.rarity == Rarity.Legendary,
                    "등급 결정된 인스턴스는 복제 없이 지급");
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
                foreach (WeaponData asset in assets) if (asset != null) Object.DestroyImmediate(asset);
            }
        }

        private static SaveData Capture(InventoryDataManager inventory, EquipmentDataManager equipment)
        {
            var snapshot = new SaveData();
            inventory.PopulateSaveData(snapshot);
            equipment.PopulateSaveData(snapshot);
            return snapshot;
        }

        private static void VerifyRestoredState(Scene scene, List<WeaponData> templates, SaveData saved)
        {
            // 전역 데이터베이스와 실제 세이브를 변경하지 않고 기존 로드 구현을 확인한다.
            FieldInfo tableField = typeof(ItemDatabase).GetField("itemTable", BindingFlags.Static | BindingFlags.NonPublic);
            FieldInfo initializedField = typeof(ItemDatabase).GetField("isInitialized", BindingFlags.Static | BindingFlags.NonPublic);
            object previousTable = tableField.GetValue(null);
            object previousInitialized = initializedField.GetValue(null);
            var restored = new List<EquipmentData>();
            try
            {
                var table = new Dictionary<string, ItemData>();
                foreach (WeaponData template in templates) table[template.ID] = template;
                tableField.SetValue(null, table);
                initializedField.SetValue(null, true);
                GameObject managers = CreateObject(scene, "저장 복원 검증");
                var inventory = managers.AddComponent<InventoryDataManager>();
                inventory.Initialize();
                var equipment = managers.AddComponent<EquipmentDataManager>();
                equipment.Initialize();
                equipment.Construct(inventory, null);
                inventory.LoadFromSaveData(saved);
                equipment.LoadFromSaveData(saved);
                foreach (var slot in inventory.GetAllItems()) restored.Add((EquipmentData)slot.item);
                foreach (EquipmentData item in equipment.EquippedItems.Values) restored.Add(item);
                Require(JsonUtility.ToJson(Capture(inventory, equipment)) == JsonUtility.ToJson(saved),
                    "새 매니저에 저장 복원 후 위치·ID·등급·수량 동일");
            }
            finally
            {
                tableField.SetValue(null, previousTable);
                initializedField.SetValue(null, previousInitialized);
                foreach (EquipmentData item in restored) Object.DestroyImmediate(item);
            }
        }

        private static void RequireUniqueInstances(SaveData snapshot, int expectedCount)
        {
            var instances = new HashSet<string>();
            foreach (ItemEntry entry in snapshot.inventoryData)
                Require(entry.count == 1 && !string.IsNullOrEmpty(entry.instanceId) && instances.Add(entry.instanceId), "보관 장비 인스턴스 유일성");
            foreach (EquippedItemEntry entry in snapshot.equippedItemsData)
                Require(!string.IsNullOrEmpty(entry.instanceId) && instances.Add(entry.instanceId), "장착 장비 인스턴스 유일성");
            Require(instances.Count == expectedCount, "저장 스냅샷 장비 수량 보존");
        }

        private static GameObject CreateObject(Scene scene, string name)
        {
            var instance = new GameObject(name);
            SceneManager.MoveGameObjectToScene(instance, scene);
            return instance;
        }

        private static WeaponData CreateWeapon(List<WeaponData> assets, string name, Rarity rarity)
        {
            WeaponData weapon = ScriptableObject.CreateInstance<WeaponData>();
            weapon.name = name;
            typeof(ItemData).GetField("uniqueID", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(weapon, "검증-" + name);
            weapon.equipmentType = EquipmentType.Weapon;
            weapon.instanceId = Guid.NewGuid().ToString();
            weapon.ApplyRarityStats(rarity);
            assets.Add(weapon);
            return weapon;
        }

        private static void Require(bool condition, string description)
        {
            if (!condition) throw new InvalidOperationException(description);
        }
    }
}
