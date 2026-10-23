using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Nytherion.Core.Data;
using Nytherion.Core.Enums;
using Nytherion.Core.Managers;
using Nytherion.Data.ScriptableObjects.Weapons;
using Nytherion.GamePlay.Combat;
using Nytherion.Services;
using Nytherion.UI.Inventory;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using VContainer;
using Object = UnityEngine.Object;

namespace Nytherion.Editor
{
    public static class PlayerEquipmentRestoreVerification
    {
        private const string Key = "Nytherion.EquipmentRestore.";
        private static JsonSaveService Service => new JsonSaveService(Path.Combine(Application.persistentDataPath, "nytherion_savedata.json"));

        public static void Seed()
        {
            Require(Application.persistentDataPath.Contains("_ScopeVerification_"), "격리 세이브 경로");
            var weapon = AssetDatabase.LoadAssetAtPath<WeaponData>("Assets/Nytherion/Data/ScriptableObjects/Weapons/ShamanStaff.asset");
            var entry = new EquippedItemEntry
            {
                slotType = EquipmentSlotType.Weapon, itemId = weapon.ID,
                instanceId = Guid.NewGuid().ToString(), rarity = Rarity.Rare
            };
            var data = new SaveData();
            data.equippedItemsData.Add(entry);
            Require(Service.Save(data), "최초 장착 무기 세이브 생성");
            Remember(entry);
        }

        public static void VerifyLoaded()
        {
            var scope = Object.FindObjectOfType<GameSceneLifetimeScope>();
            var player = scope.Container.Resolve<PlayerManager>();
            var equipment = scope.Container.Resolve<EquipmentDataManager>();
            var equipped = equipment.GetEquipment(EquipmentSlotType.Weapon) as WeaponData;
            var slot = scope.Container.Resolve<GameSceneUIRefs>().EquipmentPanel.GetComponentsInChildren<EquipmentSlotUI>(true)
                .Single(item => item.SlotType == EquipmentSlotType.Weapon);
            Require(slot.CurrentItem == equipped, "재장착 없이 장착 UI와 로드 데이터 일치");
            string expectedId = SessionState.GetString(Key + "ItemId", "");
            if (expectedId.Length == 0)
            {
                Require(equipped == null && player.PlayerCombat.currentWeapon == null, "해제 상태 저장 후 재시작하면 캐릭터도 무기 없음");
                return;
            }
            Require(equipped != null && equipped.ID == expectedId &&
                equipped.instanceId == SessionState.GetString(Key + "InstanceId", "") && equipped.rarity == Rarity.Rare,
                "저장된 무기 ID·인스턴스 ID·등급 복원");
            Require(player.PlayerCombat.currentWeapon != null && player.PlayerCombat.currentWeapon.weaponData == equipped,
                "재장착 없이 저장된 무기가 캐릭터에 자동 생성");
            FireLoadedWeapon(player);

            // 같은 의존성을 다시 주입해도 무기를 중복 생성하지 않아야 한다.
            var originalWeapon = player.PlayerCombat.currentWeapon;
            for (int i = 0; i < 2; i++)
                player.Construct(equipment, InputManager.Instance, player.EventManager,
                    scope.Container.Resolve<RelicManager>(), scope.Container.Resolve<SkillDataManager>(), scope.Container);
            Require(player.PlayerCombat.currentWeapon == originalWeapon, "반복 주입 시 기존 무기 유지");
            int Subscriptions() => ((Delegate)typeof(EquipmentDataManager).GetField("OnEquipmentChanged",
                    BindingFlags.Instance | BindingFlags.NonPublic).GetValue(equipment)).GetInvocationList()
                .Count(handler => ReferenceEquals(handler.Target, player));
            Require(Subscriptions() == 1, "반복 주입 후 장비 변경 구독 1회");
            player.enabled = false;
            Require(Subscriptions() == 0, "비활성화 시 장비 변경 구독 해제");
            equipment.SetEquipment(EquipmentSlotType.Weapon, null, false);
            player.enabled = true;
            Require(Subscriptions() == 1, "재활성화 시 장비 변경 구독 1회 복구");
            Require(player.PlayerCombat.currentWeapon == null, "비활성 중 변경된 장비를 재활성화 시 동기화");
            equipment.SetEquipment(EquipmentSlotType.Weapon, equipped, false);
            Require(player.PlayerCombat.currentWeapon != null && player.PlayerCombat.currentWeapon.weaponData == equipped,
                "재활성화 후 장비 변경 알림 유지");
        }

        private static void FireLoadedWeapon(PlayerManager player)
        {
            Require(!UIPanelBase.IsAnyPanelOpen, "복원된 무기의 공격 검증 전 UI 닫힘");
            var ranged = player.PlayerCombat.currentWeapon as RangedWeapon;
            Require(ranged != null, "저장 무기의 원거리 타입");
            Transform originalPoint = ranged.firePoint;
            var point = new GameObject("저장 무기 검증 총구");
            point.transform.position = new Vector3(20000f, 20000f);
            Mouse previous = Mouse.current;
            Mouse mouse = null;
            try
            {
                ranged.firePoint = point.transform;
                int Count() => player.ObjectPool.GetComponentsInChildren<CollisionObject>()
                    .Count(projectile => projectile.transform.position.x > 19000f);
                int before = Count();
                Vector2 position = PlayerAttackVerification.FindClearPointerPosition();
                mouse = InputSystem.AddDevice<Mouse>();
                InputSystem.QueueStateEvent(mouse, new MouseState { position = position });
                InputSystem.Update();
                InputSystem.QueueStateEvent(mouse, new MouseState { position = position }.WithButton(MouseButton.Left));
                InputSystem.Update();
                InputSystem.QueueStateEvent(mouse, new MouseState { position = position });
                InputSystem.Update();
                Require(Count() > before, "재장착 없이 복원된 무기의 마우스 클릭 → 실제 투사체 생성");
            }
            finally
            {
                if (mouse != null)
                {
                    InputSystem.QueueStateEvent(mouse, new MouseState());
                    InputSystem.Update();
                    InputSystem.RemoveDevice(mouse);
                    previous?.MakeCurrent();
                }
                ranged.firePoint = originalPoint;
                Object.Destroy(point);
            }
        }

        public static void PrepareNextSave(int nextRun)
        {
            var scope = Object.FindObjectOfType<GameSceneLifetimeScope>();
            var equipment = scope.Container.Resolve<EquipmentDataManager>();
            if (nextRun == 1)
            {
                var weapon = Object.Instantiate(AssetDatabase.LoadAssetAtPath<WeaponData>("Assets/Nytherion/Data/ScriptableObjects/Weapons/Afterglow.asset"));
                weapon.ApplyRarityStats(Rarity.Rare);
                equipment.SetEquipment(EquipmentSlotType.Weapon, weapon, false);
            }
            else equipment.SetEquipment(EquipmentSlotType.Weapon, null, false);
            var save = scope.Container.Resolve<SaveLoadManager>();
            save.ForceSaveGame();
            var result = Service.LoadWithResult();
            Require(result.IsSuccess, "정상 저장 파일 읽기");
            var entry = result.Data.equippedItemsData.SingleOrDefault(item => item.slotType == EquipmentSlotType.Weapon);
            var current = equipment.GetEquipment(EquipmentSlotType.Weapon);
            Require(current == null ? entry == null : entry != null && entry.itemId == current.ID && entry.instanceId == current.instanceId,
                "현재 착용·해제 상태가 실제 JSON 파일에 저장됨");
            Remember(entry);
        }

        private static void Remember(EquippedItemEntry entry)
        {
            SessionState.SetString(Key + "ItemId", entry?.itemId ?? "");
            SessionState.SetString(Key + "InstanceId", entry?.instanceId ?? "");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
