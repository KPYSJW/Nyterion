using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Nytherion.Core.Enums;
using Nytherion.Core.Managers;
using Nytherion.Data.ScriptableObjects.Items;
using Nytherion.Data.ScriptableObjects.Weapons;
using Nytherion.UI.Inventory;
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
                results.Add("PASS OnEnable 이후 주입 시 장착 UI 즉시 갱신, 반복 구독 시 이벤트 1회, 비활성 중 변경 후 재활성/해제 동기화");
            }
            catch (Exception error)
            {
                results.Add("FAIL " + error);
                Debug.LogException(error);
            }
            File.WriteAllLines(Output + "/verification.txt", results);
        }

        private static void VerifyCase(bool full, bool occupied)
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
                Click.Invoke(source, new object[] { source,
                    new PointerEventData(null) { button = PointerEventData.InputButton.Right } });
                Require(ReferenceEquals(equipment.GetEquipment(EquipmentSlotType.Weapon), a), "a 장착 데이터");
                Require(ReferenceEquals(equippedSlot.CurrentItem, a), "a 장착 칸 표시");
                Require(equipmentUiUpdates == 1, "장비 변경당 UI 갱신 1회");
                Require(ReferenceEquals(inventory.GetSlot(7).item, occupied ? b : null), "b 원래 보관 위치 반환");
                Require(ReferenceEquals(source.CurrentItem, occupied ? b : null), "보관 칸 표시 동기화");
                Require(ReferenceEquals(inventory.GetSlot(1).item, sameId), "같은 ID의 다른 무기 보존");
                Require(inventory.GetAllItems().Count + 1 == originalCount, "아이템 총수 보존");
                Require(b.rarity == Rarity.Epic && a.rarity == Rarity.Rare, "등급 보존");
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
