using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Nytherion.Core.Data;
using Nytherion.Core.Interfaces;
using Nytherion.Core.Managers;
using Nytherion.Core.Systems;
using Nytherion.Data.ScriptableObjects.Items;
using Nytherion.Data.ScriptableObjects.Weapons;
using Nytherion.UI.Controllers;
using Nytherion.UI.Inventory;
using Nytherion.UI.Presenters;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VContainer;
using Object = UnityEngine.Object;

namespace Nytherion.Editor
{
    [InitializeOnLoad]
    public static class InventoryUpdateVerification
    {
        private const string Output = "output/inventory-updates";

        static InventoryUpdateVerification() => EditorApplication.update += Update;

        private static void Update()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode ||
                !File.Exists(Output + "/verify.request")) return;
            File.Delete(Output + "/verify.request");
            Verify();
        }

        [MenuItem("Tools/Nytherion/Inventory/Verify Update Ownership")]
        public static void Verify()
        {
            Directory.CreateDirectory(Output);
            var results = new List<string>();
            try
            {
                using (var test = new Fixture())
                {
                    test.Expect(() => test.Inventory.AddItem(test.Potion, 2), 0);
                    test.Expect(() => test.Inventory.AddItem(test.Potion, 3), 0);
                    Require(test.Inventory.GetSlot(0).count == 5, "스택 합계");
                    test.Expect(() => test.Inventory.RemoveItem(test.Potion.ID), 0);
                    test.Expect(() => test.Inventory.AddItemToSlot(test.Weapon, 1, 7), 7);
                    results.Add("PASS 추가·스택 변경·ID 제거·지정 슬롯 추가: 데이터 알림 1회, 해당 슬롯 표시 1회");

                    test.Expect(() => test.Inventory.SwapItems(0, 7), 0, 7);
                    Require(test.LastChange.changeType == InventoryChangeType.SlotsSwapped &&
                        test.LastChange.slotIndex == 0 && test.LastChange.otherSlotIndex == 7, "교환 슬롯 정보");
                    test.Expect(() => test.Inventory.RemoveItemFromSlot(0), 0);
                    test.ExpectNoChange(() => test.Inventory.RemoveItemFromSlot(0));
                    test.ExpectNoChange(() => test.Inventory.RemoveItemFromSlot(7, 99));
                    test.ExpectNoChange(() => test.Inventory.SwapItems(7, 7));
                    test.ExpectNoChange(() => test.Inventory.SwapItems(1, 2));
                    results.Add("PASS 슬롯 교환은 두 칸만 1회씩 갱신, 삭제는 한 칸만 갱신, 실패·같은 칸·빈칸 교환은 알림 없음");

                    test.Expect(() => test.Inventory.InventoryModel.AddItem(test.Potion, 1), 7);
                    test.Expect(() => test.Inventory.AddItemToSlot(test.Weapon, 1, 3), 3);
                    test.Expect(() => test.Inventory.StageForDismantle(3, test.Weapon), 3);
                    test.Expect(() => { test.Inventory.ResetDismantle(); return true; }, 3);
                    results.Add("PASS 모델 직접 변경 전달, 분해 예약·초기화도 각 한 번 갱신");

                    test.Expect(() => { test.Inventory.EnsureSlotCapacity(30); return true; });
                    test.WatchSlots();
                    Require(test.Slots.Length == 30, "확장 슬롯 생성");
                    test.Expect(() => test.Inventory.AddItemToSlot(test.Weapon, 1, 28), 28);
                    test.Inventory.RemoveItemFromSlot(28);
                    results.Add("PASS 24 → 30 슬롯 확장 후 새 슬롯의 부분 갱신, UI·Controller 반복 초기화 후 중복 구독 없음");

                    var before = test.Slots.Select(slot => (slot.CurrentItem, slot.CurrentCount)).ToArray();
                    test.ResetCounts();
                    Require(test.Inventory.SortItems(), "정렬 변경");
                    test.RequireCurrentSlots();
                    Require(test.Notifications == 1, "정렬 알림 한 번");
                    for (int i = 0; i < test.Slots.Length; i++)
                        Require(test.Writes[i] == (before[i].CurrentItem == test.Slots[i].CurrentItem &&
                            before[i].CurrentCount == test.Slots[i].CurrentCount ? 0 : 1), "정렬 시 변경된 슬롯만 표시 " + i);
                    test.ExpectNoChange(() => test.Inventory.SortItems());
                    test.ResetCounts();
                    test.Controller.RequestUIUpdate();
                    test.UI.Close();
                    test.UI.Open(false);
                    Require(test.Notifications == 0 && test.Writes.Sum() == 0, "재열기·수동 갱신의 동일 슬롯 재표시 생략");
                    results.Add("PASS 정렬 알림 1회와 변경 슬롯만 갱신, 재정렬·재열기·수동 갱신 시 동일 표시 보존");

                    test.VerifyLoad();
                    results.Add("PASS 로드 알림 1회, 새 모델 구독 유지, 교체된 이전 모델의 이벤트는 전달하지 않음");
                    test.Presenter.Cleanup();
                    test.ResetCounts();
                    Require(test.Inventory.AddItem(test.Potion), "정리 후 데이터 변경");
                    Require(test.Notifications == 1 && test.Writes.Sum() == 0, "Presenter 정리 시 구독 해제");
                    test.Presenter.Initialize();
                    test.WatchSlots();
                    test.RequireCurrentSlots();
                    test.Expect(() => test.Inventory.AddItem(test.Potion), 1);
                    results.Add("PASS Presenter 정리·재초기화 후 표시 복구, 변경당 구독 한 번 유지");
                }
            }
            catch (Exception error)
            {
                results.Add("FAIL " + error);
                Debug.LogException(error);
            }
            File.WriteAllLines(Output + "/verification.txt", results);
        }

        private sealed class Fixture : IDisposable
        {
            private readonly Scene scene = EditorSceneManager.NewPreviewScene();
            private readonly List<ItemData> assets = new List<ItemData>();
            private readonly IObjectResolver resolver;
            private readonly Transform parent;
            private readonly HashSet<InventorySlotUI> watched = new HashSet<InventorySlotUI>();
            public readonly InventoryDataManager Inventory;
            public readonly InventoryPresenter Presenter;
            public readonly InventoryUI UI;
            public readonly InventoryUIController Controller;
            public readonly ConsumableData Potion;
            public readonly WeaponData Weapon;
            public InventorySlotUI[] Slots;
            public readonly int[] Writes = new int[64];
            public int Notifications;
            public InventoryChangeData LastChange;

            public Fixture()
            {
                GameObject root = Create("인벤토리 갱신 검증", typeof(RectTransform), typeof(CanvasGroup));
                Inventory = root.AddComponent<InventoryDataManager>();
                Inventory.Initialize();
                var events = root.AddComponent<EventManager>();
                var refs = root.AddComponent<GameSceneUIRefs>();
                var pool = root.AddComponent<ObjectPoolManager>();
                pool.Initialize();
                Presenter = root.AddComponent<InventoryPresenter>();
                parent = Create("슬롯", typeof(RectTransform)).transform;
                parent.SetParent(root.transform, false);
                GameObject template = Create("UI_검증슬롯", typeof(RectTransform), typeof(InventorySlotUI));
                template.SetActive(false);
                SetField(refs, "inventorySlotParent", parent);
                SetField(refs, "inventorySlotPrefab", template);
                SetField(refs, "inventoryCanvasGroup", root.GetComponent<CanvasGroup>());
                SetField(refs, "inventoryCloseButton", Create("닫기", typeof(RectTransform), typeof(Button)).GetComponent<Button>());
                var builder = new ContainerBuilder();
                builder.RegisterInstance(Inventory);
                builder.RegisterInstance(events);
                builder.RegisterInstance(Presenter);
                resolver = builder.Build();
                Presenter.Construct(resolver, refs, pool);
                UI = root.AddComponent<InventoryUI>();
                UI.Construct(resolver, refs);
                Controller = root.AddComponent<InventoryUIController>();
                Controller.Construct(UI);
                Controller.Initialize();
                Controller.Initialize();
                UI.Initialize();
                Presenter.Initialize();
                WatchSlots();
                Inventory.OnDataChanged += change => { Notifications++; LastChange = change; };
                Potion = ScriptableObject.CreateInstance<ConsumableData>();
                Weapon = ScriptableObject.CreateInstance<WeaponData>();
                Potion.itemName_EN = "Z Potion";
                Weapon.itemName_EN = "A Weapon";
                Weapon.instanceId = Guid.NewGuid().ToString();
                SetField(Potion, "uniqueID", "update-potion");
                SetField(Weapon, "uniqueID", "update-weapon");
                assets.Add(Potion);
                assets.Add(Weapon);
            }

            private GameObject Create(string name, params Type[] components)
            {
                var instance = new GameObject(name, components);
                SceneManager.MoveGameObjectToScene(instance, scene);
                return instance;
            }

            public void WatchSlots()
            {
                Slots = parent.GetComponentsInChildren<InventorySlotUI>(true);
                foreach (InventorySlotUI slot in Slots)
                    if (watched.Add(slot)) slot.OnSlotUpdated += changed => Writes[((InventorySlotUI)changed).SlotIndex]++;
            }

            public void ResetCounts() { Notifications = 0; Array.Clear(Writes, 0, Writes.Length); }

            public void Expect(Func<bool> action, params int[] changedSlots)
            {
                ResetCounts();
                Require(action(), "변경 성공");
                Require(Notifications == 1, "데이터 알림 한 번");
                RequireCurrentSlots();
                for (int i = 0; i < Slots.Length; i++)
                    Require(Writes[i] == (changedSlots.Contains(i) ? 1 : 0), "표시 갱신 횟수 " + i + ": " + Writes[i]);
            }

            public void ExpectNoChange(Func<bool> action)
            {
                ResetCounts();
                Require(!action() && Notifications == 0 && Writes.Sum() == 0, "실패·변경 없음은 알림·표시 변경 없음");
            }

            public void RequireCurrentSlots()
            {
                for (int i = 0; i < Slots.Length; i++)
                {
                    var data = Inventory.GetSlot(i);
                    Require(Slots[i].SlotIndex == i && Slots[i].CurrentItem == data.item && Slots[i].CurrentCount == data.count,
                        "슬롯 번호와 표시 일치 " + i);
                }
            }

            public void VerifyLoad()
            {
                FieldInfo table = typeof(ItemDatabase).GetField("itemTable", BindingFlags.Static | BindingFlags.NonPublic);
                FieldInfo initialized = typeof(ItemDatabase).GetField("isInitialized", BindingFlags.Static | BindingFlags.NonPublic);
                object previousTable = table.GetValue(null), previousInitialized = initialized.GetValue(null);
                try
                {
                    table.SetValue(null, new Dictionary<string, ItemData> { [Potion.ID] = Potion, [Weapon.ID] = Weapon });
                    initialized.SetValue(null, true);
                    var data = new SaveData();
                    Inventory.PopulateSaveData(data);
                    InventoryModel previous = Inventory.InventoryModel;
                    ResetCounts();
                    Inventory.LoadFromSaveData(JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(data)));
                    foreach (var entry in Inventory.GetAllItems())
                        if (entry.item is EquipmentData) assets.Add(entry.item);
                    Require(Notifications == 1, "로드 알림 한 번");
                    RequireCurrentSlots();
                    ResetCounts();
                    previous.AddItem(Potion, 2);
                    Require(Notifications == 0 && Writes.Sum() == 0, "이전 모델 구독 해제");
                    Expect(() => Inventory.AddItem(Potion), 1);
                }
                finally { table.SetValue(null, previousTable); initialized.SetValue(null, previousInitialized); }
            }

            public void Dispose()
            {
                EditorSceneManager.ClosePreviewScene(scene);
                resolver?.Dispose();
                foreach (ItemData asset in assets) if (asset != null) Object.DestroyImmediate(asset);
            }
        }

        private static void SetField(object target, string name, object value)
        {
            Type type = target.GetType();
            FieldInfo field = null;
            while (type != null && field == null) { field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic); type = type.BaseType; }
            if (field == null) throw new InvalidOperationException(name + " 필드 없음");
            field.SetValue(target, value);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
