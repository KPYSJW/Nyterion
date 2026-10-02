using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Nytherion.Core.Managers;
using Nytherion.Data.ScriptableObjects.Player;
using Nytherion.Data.ScriptableObjects.Relics;
using Nytherion.Data.ScriptableObjects.Weapons;
using Nytherion.GamePlay.Characters.Player;
using Nytherion.GamePlay.Combat;
using Nytherion.GamePlay.Combat.Weapons;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Nytherion.Editor
{
    /// <summary>실제 이빌아이 프리팹으로 유물 조건과 쿨다운 중 누른 입력을 검증합니다.</summary>
    [InitializeOnLoad]
    public static class ChargeRelicVerification
    {
        private const string Output = "output/charge-relic";
        private const string Pending = "Nytherion.ChargeRelic.Verify";
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static double readyAt;

        static ChargeRelicVerification() => EditorApplication.update += Update;

        [MenuItem("Tools/Nytherion/Charge Relic/Verify Evil Eye In Play Mode")]
        public static void Start()
        {
            AssetDatabase.ImportAsset("Assets/Nytherion/Data/ScriptableObjects/Weapons/EvilEye.asset",
                ImportAssetOptions.ForceSynchronousImport);
            SessionState.SetBool(Pending + ".Exit", !EditorApplication.isPlaying);
            SessionState.SetBool(Pending, true);
            readyAt = EditorApplication.timeSinceStartup + 5d;
            if (!EditorApplication.isPlaying) EditorApplication.isPlaying = true;
        }

        private static void Update()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (File.Exists("charge-relic.verify.request"))
            {
                File.Delete("charge-relic.verify.request");
                Start();
            }
            if (!SessionState.GetBool(Pending, false) || !EditorApplication.isPlaying) return;
            if (readyAt == 0d) readyAt = EditorApplication.timeSinceStartup + 5d;
            if (EditorApplication.timeSinceStartup < readyAt) return;
            SessionState.SetBool(Pending, false);
            Directory.CreateDirectory(Output);
            try { File.WriteAllText(Output + "/verification.txt", Verify()); }
            catch (Exception error)
            {
                File.WriteAllText(Output + "/verification.txt", "FAIL: " + error);
                Debug.LogException(error);
            }
            finally
            {
                if (SessionState.GetBool(Pending + ".Exit", false)) EditorApplication.isPlaying = false;
                readyAt = 0d;
            }
        }

        private static string Verify()
        {
            var results = new List<string>();
            var source = AssetDatabase.LoadAssetAtPath<WeaponData>("Assets/Nytherion/Data/ScriptableObjects/Weapons/EvilEye.asset");
            Require(source != null && source.weaponPrefab is GenericChargeableWeapon &&
                source.requiredRelicId == "ChargeRelic" && source.chargeEffectPrefab != null,
                "이빌아이 무기/유물 조건/차징 이펙트 Inspector 참조");
            results.Add("PASS 이빌아이 프리팹 및 차징의 구/이펙트 참조");

            GameObject root = new GameObject("[검증] 이빌아이 차징");
            root.transform.position = new Vector3(10000f, 10000f);
            var player = root.AddComponent<PlayerManager>();
            player.enabled = false;
            var relics = root.AddComponent<PlayerRelicManager>();
            relics.enabled = false;
            typeof(PlayerManager).GetField("<playerRelicManager>k__BackingField", Private).SetValue(player, relics);
            var stats = ScriptableObject.CreateInstance<PlayerData>();
            player.currentPlayerData = stats;
            var combat = root.AddComponent<PlayerCombat>();
            combat.enabled = false;
            combat.Construct(InputManager.Instance);
            typeof(PlayerCombat).GetField("weaponPoint", Private).SetValue(combat, root.transform);

            FieldInfo poolInstance = typeof(ObjectPoolManager).GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
            ObjectPoolManager previousPool = ObjectPoolManager.Instance;
            poolInstance.SetValue(null, null);
            var pool = new GameObject("[검증] 이빌아이 전용 풀").AddComponent<ObjectPoolManager>();
            pool.Initialize();
            RelicData chargeRelic = Object.Instantiate(AssetDatabase.LoadAssetAtPath<RelicData>(
                "Assets/Nytherion/Data/ScriptableObjects/Relics/CombatUtility/ChargeRelic.asset"));
            try
            {
                combat.EquipWeapon(source.weaponPrefab, source);
                var weapon = (GenericChargeableWeapon)combat.currentWeapon;
                weapon.enabled = false;
                Require(weapon.weaponData == source && Mathf.Approximately(weapon.maxChargeTime, source.maxChargeTime),
                    "장착 후 데이터 초기화");
                Call(combat, "HandleAttackDown");
                Require(!weapon.IsCharging && ActiveProjectiles(pool).Count == 1, "유물 미장착 즉시 일반 발사");
                Call(combat, "HandleAttackUp");
                ReturnProjectiles(pool);
                results.Add("PASS 유물 미장착: 즉시 일반 발사");

                relics.AddRelic(chargeRelic);
                Require(relics.IsRelicActive("ChargeRelic"), "유물 장착 스냅샷");
                // 첫 발사 직후 쿨다운 도중 누르고, 쿨다운이 끝난 프레임을 재현합니다.
                Call(combat, "HandleAttackDown");
                Require(!Pressing(weapon), "쿨다운 도중 차징 대기");
                typeof(WeaponBase).GetField("lastAttackTime", Private).SetValue(weapon, Time.time - source.cooldown - 1f);
                Call(combat, "Update");
                Require(Pressing(weapon), "쿨다운 종료 후 유지 입력으로 차징 시작");
                typeof(ChargeableRangedWeapon).GetField("pressTime", Private).SetValue(weapon, weapon.chargeThresholdTime);
                typeof(ChargeableRangedWeapon).GetMethod("Update", Private).Invoke(weapon, null);
                Require(weapon.IsCharging && !weapon.CanAttack(), "차징 진행 중 추가 공격 차단");
                typeof(ChargeableRangedWeapon).GetField("currentChargeTime", Private).SetValue(weapon, weapon.maxChargeTime * 0.6f);
                Call(combat, "Update");
                Require(weapon.IsCharging && weapon.ChargePercent >= 0.59f, "누른 입력 재시도로 차징량 초기화 방지");
                Require(typeof(ChargeableRangedWeapon).GetField("activeChargeEffectInstance", Private).GetValue(weapon) != null,
                    "실제 차징 이펙트 생성");
                Require(ActiveProjectiles(pool).Count == 0, "차징 중 발사 보류");
                results.Add("PASS 쿨다운 중 누름 → 쿨다운 종료 후 차징, 누름 유지 중 진행도/이펙트 유지");

                typeof(ChargeableRangedWeapon).GetField("currentChargeTime", Private).SetValue(weapon, weapon.maxChargeTime);
                Call(combat, "HandleAttackUp");
                Require(!weapon.IsCharging && !Pressing(weapon) && ActiveProjectiles(pool).Count == 1,
                    "버튼 해제 시 차징탄 한 발 발사");
                Require(Mathf.Approximately(ActiveProjectiles(pool)[0].damage, source.damage), "최대 차징 피해량");
                Require(typeof(ChargeableRangedWeapon).GetField("activeChargeEffectInstance", Private).GetValue(weapon) == null,
                    "해제 시 차징 이펙트 반환");
                ReturnProjectiles(pool);
                results.Add("PASS 최대 차징 후 버튼 해제: 투사체 1발, 피해량 " + source.damage + ", 이펙트 반환");

                Call(combat, "HandleAttackDown");
                Call(combat, "HandleAttackUp");
                typeof(WeaponBase).GetField("lastAttackTime", Private).SetValue(weapon, Time.time - source.cooldown - 1f);
                Call(combat, "Update");
                Require(!Pressing(weapon) && ActiveProjectiles(pool).Count == 0, "쿨다운 중 누르고 뗀 입력은 발사하지 않음");
                results.Add("PASS 쿨다운 중 누르고 떼기: 나중에 차징/발사하지 않음");

                relics.RemoveRelic(0);
                Call(combat, "HandleAttackDown");
                Require(!Pressing(weapon) && ActiveProjectiles(pool).Count == 1, "유물 해제 후 일반 발사 복귀");
                Call(combat, "HandleAttackUp");
                results.Add("PASS 유물 해제: 일반 발사 복귀");
                return string.Join("\n", results);
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(pool.gameObject);
                poolInstance.SetValue(null, previousPool);
                Object.DestroyImmediate(stats);
                Object.DestroyImmediate(chargeRelic);
            }
        }

        private static void Call(PlayerCombat combat, string method) =>
            typeof(PlayerCombat).GetMethod(method, Private).Invoke(combat, null);
        private static bool Pressing(ChargeableRangedWeapon weapon) =>
            (bool)typeof(ChargeableRangedWeapon).GetField("isPressing", Private).GetValue(weapon);
        private static List<CollisionObject> ActiveProjectiles(ObjectPoolManager pool) =>
            pool.GetComponentsInChildren<CollisionObject>(true).Where(p => p.gameObject.activeSelf).ToList();
        private static void ReturnProjectiles(ObjectPoolManager pool)
        {
            foreach (CollisionObject projectile in ActiveProjectiles(pool))
                pool.ReturnToPool("EvilEyeProj", projectile.gameObject);
        }
        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
