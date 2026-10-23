using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Nytherion.Core.Managers;
using Nytherion.Data.ScriptableObjects.Relics;
using Nytherion.Data.ScriptableObjects.Weapons;
using Nytherion.GamePlay.Characters.Enemy;
using Nytherion.GamePlay.Characters.Player;
using Nytherion.GamePlay.Combat;
using Nytherion.GamePlay.Combat.Weapons;
using Nytherion.Gameplay.Relics.Modules;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace Nytherion.Editor
{
    [InitializeOnLoad]
    public static class ChainStormPlayVerification
    {
        private const string Output = "output/chain-storm";
        private const string Pending = "Nytherion.ChainStorm.PlayVerification";
        private static double readyAt;
        private static readonly List<GameObject> temporary = new List<GameObject>();
        private static readonly List<string> results = new List<string>();
        private static RelicEffectController controller;
        private static RelicTranscendenceData data;
        private static bool wasDebugActive;
        private static TrainingDummy source;
        private static TrainingDummy neighbor;
        private static int stage;
        private static int hitFrame;
        private static WindBlastProjectile flyingWind;
        private static double flightDeadline;
        private static RelicSetBonusData windSet;
        private static RelicSetBonusData archerSet;
        private static int oldWindRequirement;
        private static int oldArcherRequirement;
        private static PlayerManager testedPlayer;
        private static float oldCritChance;
        private static WeaponBase oldWeapon;
        private static float attackDamage;

        static ChainStormPlayVerification() => EditorApplication.update += Update;

        private static void Update()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (File.Exists(Output + "/play.request") || File.Exists(Output + "/zero-damage.request") ||
                File.Exists(Output + "/archer-play.request"))
            {
                bool archerOnly = File.Exists(Output + "/archer-play.request");
                bool zeroDamage = File.Exists(Output + "/zero-damage.request");
                File.Delete(Output + (archerOnly ? "/archer-play.request" : zeroDamage ? "/zero-damage.request" : "/play.request"));
                SessionState.SetBool(Pending + ".ArcherOnly", archerOnly);
                SessionState.SetBool(Pending + ".ZeroDamage", zeroDamage);
                SessionState.SetBool(Pending, true);
                SessionState.SetBool(Pending + ".Exit", !EditorApplication.isPlaying);
                SessionState.SetBool(Pending + ".Background", Application.runInBackground);
                if (!EditorApplication.isPlaying) EditorApplication.isPlaying = true;
                readyAt = 0d;
                stage = 0;
                return;
            }
            if (!SessionState.GetBool(Pending, false) || !EditorApplication.isPlaying) return;
            Application.runInBackground = true;
            EditorApplication.QueuePlayerLoopUpdate();
            if (readyAt == 0d) readyAt = EditorApplication.timeSinceStartup + 4d;
            if (EditorApplication.timeSinceStartup < readyAt) return;
            try
            {
                if (stage == 2)
                {
                    if (flyingWind != null && flyingWind.gameObject.activeSelf)
                    {
                        RequireFlightTime();
                        return;
                    }
                    Require(source.GetComponent<StatusEffectManager>().HasEffect("Lightning") && neighbor.GetComponent<StatusEffectManager>().HasEffect("Lightning"), "실제 허수아비로 이동한 돌풍의 충돌·번개 확산");
                    Require(Object.FindObjectsOfType<ChainLightningVFX>().Any(effect => effect.name == "ChainStormLightningVFX" && effect.IsVisible), "자연 이동·충돌 후 번개부르미 방식 이펙트 생성");
                    VerifyGameCameraLightning();
                    stage = 3;
                    hitFrame = Time.frameCount;
                    readyAt = EditorApplication.timeSinceStartup + 0.01d;
                    return;
                }
                if (stage == 0)
                {
                    results.Clear();
                    VerifyHit();
                    if (SessionState.GetBool(Pending + ".ArcherOnly", false))
                    {
                        VerifyArcherCriticals();
                        Finish();
                        return;
                    }
                    hitFrame = Time.frameCount;
                    stage = 1;
                    readyAt = EditorApplication.timeSinceStartup + 0.05d;
                    return;
                }
                if (Time.frameCount < hitFrame + 2) return;
                bool sourceStatus = source.GetComponent<StatusEffectManager>().HasEffect("Lightning");
                bool neighborStatus = neighbor.GetComponent<StatusEffectManager>().HasEffect("Lightning");
                bool sourceIcon = HasLightningIcon(source);
                bool neighborIcon = HasLightningIcon(neighbor);
                results.Add($"플레이 실제 적중: 첫 대상 감전={sourceStatus}, 주변 감전={neighborStatus}, 첫 대상 아이콘={sourceIcon}, 주변 아이콘={neighborIcon}");
                Require(sourceStatus && neighborStatus && sourceIcon && neighborIcon, "첫 대상·주변 대상 감전 상태와 아이콘");
                if (stage == 1)
                {
                    VerifyWindHit();
                    stage = 2;
                    hitFrame = Time.frameCount;
                    readyAt = EditorApplication.timeSinceStartup + 0.05d;
                    return;
                }
                results.Add("PASS GameScene 대시·공격 이벤트·일반 적중·자동 발사 돌풍 적중·번개 생성·감전 아이콘");
                Finish();
            }
            catch (Exception error) { results.Add("FAIL " + error); Finish(); }
        }

        private static void VerifyWindHit()
        {
            // 일반 적중에서 남은 선을 돌풍 적중 결과로 오인하지 않습니다.
            foreach (ChainLightningVFX effect in Object.FindObjectsOfType<ChainLightningVFX>()
                .Where(item => item.name == "ChainStormLightningVFX")) Object.DestroyImmediate(effect.gameObject);
            source.GetComponent<StatusEffectManager>().ClearAllEffects();
            neighbor.GetComponent<StatusEffectManager>().ClearAllEffects();
            PlayerManager player = controller.GetComponent<PlayerManager>();
            HashSet<WindBlastProjectile> before = new HashSet<WindBlastProjectile>(Object.FindObjectsOfType<WindBlastProjectile>());
            player.EventManager.TriggerPlayerDashStarted();
            Action<Vector2, Vector3> attack = (Action<Vector2, Vector3>)typeof(PlayerCombat)
                .GetField("OnPlayerAttackStarted", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(player.PlayerCombat);
            attack?.Invoke(Vector2.right, source.transform.position);
            if (player.GetComponent<DashStormRuntime>().RemainingStormCharges == 3)
                player.EventManager.TriggerPlayerRangedAttack(Vector2.right, 1, attackDamage, player.transform, "");
            player.currentPlayerData.critChance = 1f;
            WindBlastProjectile wind = Object.FindObjectsOfType<WindBlastProjectile>().FirstOrDefault(item => !before.Contains(item));
            Require(wind != null, "대시 후 공격 이벤트로 돌풍 자동 발사");
            float windDamage = (float)typeof(WindBlastProjectile).GetField("damage", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(wind);
            if (SessionState.GetBool(Pending + ".ZeroDamage", false))
                Require(windDamage == 0f, "숲의 가시 데이터로 자동 발사된 돌풍 피해량 0");
            wind.transform.position = source.transform.position + Vector3.left * 1.5f;
            Physics2D.SyncTransforms();
            flyingWind = wind;
            flightDeadline = EditorApplication.timeSinceStartup + 3d;
        }

        private static void VerifyHit()
        {
            PlayerManager player = Object.FindObjectOfType<PlayerManager>();
            Require(player != null && player.EventManager != null && player.PlayerCombat != null, "GameScene 플레이어 초기화");
            controller = player.GetComponent<RelicEffectController>();
            Require(controller != null, "실제 유물 효과 컨트롤러");
            testedPlayer = player;
            oldCritChance = player.currentPlayerData.critChance;
            attackDamage = 100f;
            oldWeapon = player.PlayerCombat.currentWeapon;
            if (SessionState.GetBool(Pending + ".ZeroDamage", false))
            {
                WeaponData weaponData = AssetDatabase.LoadAssetAtPath<WeaponData>("Assets/Nytherion/Data/ScriptableObjects/Weapons/ForestThorn.asset");
                Require(weaponData != null && weaponData.damage == 0f, "실제 숲의 가시 기본 피해량 0 데이터");
                GameObject weaponObject = new GameObject("[숲의 가시 피해 0 검증]");
                weaponObject.SetActive(false);
                temporary.Add(weaponObject);
                ForestThornWeapon weapon = weaponObject.AddComponent<ForestThornWeapon>();
                weapon.weaponData = weaponData;
                player.PlayerCombat.currentWeapon = weapon;
                attackDamage = weaponData.damage * weapon.CurrentDamageMultiplier;
            }
            windSet = AssetDatabase.LoadAssetAtPath<RelicSetBonusData>("Assets/Nytherion/Data/ScriptableObjects/Relics/MobileWarriorSet.asset");
            archerSet = AssetDatabase.LoadAssetAtPath<RelicSetBonusData>("Assets/Nytherion/Data/ScriptableObjects/Relics/StormArcherSet.asset");
            oldWindRequirement = controller.GetDebugSetRequirement(windSet);
            oldArcherRequirement = controller.GetDebugSetRequirement(archerSet);
            bool archerOnly = SessionState.GetBool(Pending + ".ArcherOnly", false);
            controller.SetDebugSetBonus(windSet, archerOnly ? 0 : 6);
            controller.SetDebugSetBonus(archerSet, archerOnly ? 4 : 6);
            data = AssetDatabase.LoadAssetAtPath<RelicTranscendenceData>("Assets/Nytherion/Data/ScriptableObjects/Relics/LightningPursuitTranscendence.asset");
            wasDebugActive = controller.IsDebugTranscendenceActive(data);
            controller.SetDebugTranscendence(data, !archerOnly);
            DashStormRuntime runtime = player.GetComponent<DashStormRuntime>();
            if (!archerOnly) Require(runtime != null, "초월 효과 런타임 등록");
            Camera camera = Camera.main;
            Require(camera != null, "실제 게임 카메라");
            Vector3 center = camera.ViewportToWorldPoint(new Vector3(0.5f, 0.5f, -camera.transform.position.z));
            center.z = 0f;
            source = CreateDummy(player, center + Vector3.left);
            neighbor = CreateDummy(player, center + Vector3.right);
            Physics2D.SyncTransforms();
            if (archerOnly) return;
            player.EventManager.TriggerPlayerDashStarted();
            Require(runtime.RemainingStormCharges == 3, "실제 대시 이벤트 3회 충전");
            Action<Vector2, Vector3> attack = (Action<Vector2, Vector3>)typeof(PlayerCombat)
                .GetField("OnPlayerAttackStarted", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(player.PlayerCombat);
            attack?.Invoke(Vector2.right, source.transform.position);
            // 차징 무기는 공격 시작 대신 실제 원거리 발사 이벤트를 사용합니다.
            if (runtime.RemainingStormCharges == 3)
                player.EventManager.TriggerPlayerRangedAttack(Vector2.right, 1, attackDamage, player.transform, "");
            Require(runtime.RemainingStormCharges == 2, "실제 공격 이벤트 충전 1회 소모");
            player.currentPlayerData.critChance = 1f;
            results.Add("검증 구성: 폭풍사수 6세트·돌풍 6세트·연쇄 폭풍·대시 후 공격·치명타 100%");
            results.Add("일반 적중 입력 피해량=" + attackDamage);
            source.TakeDamage(attackDamage);
            ChainLightningVFX[] lines = Object.FindObjectsOfType<ChainLightningVFX>()
                .Where(effect => effect.IsVisible).ToArray();
            results.Add("실제 적중 직후 번개 선 수=" + lines.Length);
            Require(lines.Any(effect => effect.GetComponent<LineRenderer>().positionCount > 1 &&
                Vector3.Distance(effect.GetComponent<LineRenderer>().GetPosition(0), source.transform.position) < 0.1f), "실제 적중 이벤트에서 번개 선 생성");
            VerifyGameCameraLightning();
        }

        private static TrainingDummy CreateDummy(PlayerManager player, Vector3 position)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Debug/TrainingDummy.prefab");
            GameObject target = Object.Instantiate(prefab, position, Quaternion.identity);
            temporary.Add(target);
            target.transform.position = position;
            TrainingDummy dummy = target.GetComponent<TrainingDummy>();
            dummy.Construct(player.EventManager, null, player);
            target.GetComponent<StatusEffectManager>().ConfigureCombatContext(player);
            return dummy;
        }

        private static bool HasLightningIcon(TrainingDummy enemy)
        {
            Transform icon = enemy.transform.Find("StatusIcon_Lightning");
            return icon != null && icon.gameObject.activeSelf && icon.GetComponent<SpriteRenderer>().sprite != null;
        }

        private static void VerifyArcherCriticals()
        {
            results.Add("검증 구성: 연쇄 폭풍·돌풍 비활성, 대시 없이 폭풍사수 치명타만 검증");
            foreach (int requirement in new[] { 4, 6 })
            {
                controller.SetDebugSetBonus(archerSet, requirement);
                ClearArcherHit();
                testedPlayer.currentPlayerData.critChance = 0f;
                source.TakeDamage(100f);
                Require(!HasArcherLightning(), $"폭풍사수 {requirement}세트 비치명타는 번개 미생성");

                foreach (float damage in new[] { 100f, 0f })
                {
                    ClearArcherHit();
                    testedPlayer.currentPlayerData.critChance = 1f;
                    source.TakeDamage(damage);
                    Require(HasArcherLightning(), $"폭풍사수 {requirement}세트 피해 {damage} 치명타 번개 생성");
                    Require(neighbor.GetComponent<StatusEffectManager>().HasEffect("Lightning"), "치명타 주변 대상 감전 유지");
                    VerifyGameCameraLightning("StormArcherLightningVFX");
                }

                ClearArcherHit();
                source.TakeDamage(100f, true);
                Require(!HasArcherLightning(), $"폭풍사수 {requirement}세트 연쇄 피해로 번개 재발동 없음");
            }
        }

        private static bool HasArcherLightning() => Object.FindObjectsOfType<ChainLightningVFX>()
            .Any(effect => effect.name == "StormArcherLightningVFX" && effect.IsVisible);

        private static void ClearArcherHit()
        {
            foreach (ChainLightningVFX effect in Object.FindObjectsOfType<ChainLightningVFX>()
                .Where(item => item.name == "StormArcherLightningVFX")) Object.DestroyImmediate(effect.gameObject);
            source.GetComponent<StatusEffectManager>().ClearAllEffects();
            neighbor.GetComponent<StatusEffectManager>().ClearAllEffects();
        }

        private static void VerifyGameCameraLightning(string effectName = "ChainStormLightningVFX")
        {
            Camera main = Camera.main;
            ChainLightningVFX effect = Object.FindObjectsOfType<ChainLightningVFX>()
                .First(item => item.name == effectName && item.IsVisible);
            LineRenderer line = effect.GetComponent<LineRenderer>();
            Renderer[] renderers = effect.GetComponentsInChildren<Renderer>();
            bool[] enabledStates = renderers.Select(renderer => renderer.enabled).ToArray();
            GameObject cameraObject = new GameObject("[게임 카메라 번개 픽셀 검증]");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.CopyFrom(main);
            camera.transform.SetPositionAndRotation(main.transform.position, main.transform.rotation);
            camera.enabled = false;
            int rendererIndex = new SerializedObject(main.GetUniversalAdditionalCameraData()).FindProperty("m_RendererIndex").intValue;
            camera.GetUniversalAdditionalCameraData().SetRenderer(rendererIndex);
            RenderTexture target = RenderTexture.GetTemporary(512, 256, 24, RenderTextureFormat.ARGB32);
            Texture2D readback = new Texture2D(512, 256, TextureFormat.RGBA32, false);
            RenderTexture previous = RenderTexture.active;
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                readback.ReadPixels(new Rect(0, 0, 512, 256), 0, 0);
                readback.Apply();
                Color32[] withLightning = readback.GetPixels32();
                foreach (Renderer renderer in renderers) renderer.enabled = false;
                camera.Render();
                RenderTexture.active = target;
                readback.ReadPixels(new Rect(0, 0, 512, 256), 0, 0);
                readback.Apply();
                Color32[] withoutLightning = readback.GetPixels32();
                int changed = 0;
                for (int i = 0; i < withLightning.Length; i++)
                    if (Mathf.Abs(withLightning[i].r - withoutLightning[i].r) + Mathf.Abs(withLightning[i].g - withoutLightning[i].g) +
                        Mathf.Abs(withLightning[i].b - withoutLightning[i].b) > 20) changed++;
                results.Add($"실제 GameScene 카메라·배경 위 번개 표시 픽셀 차이={changed}, 이펙트 월드 배율={line.transform.lossyScale}");
                Require(changed > 5, "실제 게임 배경 위 번개 렌더 출력");
            }
            finally
            {
                for (int i = 0; i < renderers.Length; i++)
                    if (renderers[i] != null) renderers[i].enabled = enabledStates[i];
                camera.targetTexture = null;
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
                Object.DestroyImmediate(readback);
                Object.DestroyImmediate(cameraObject);
            }
        }

        private static void Finish()
        {
            SessionState.SetBool(Pending, false);
            if (controller != null && data != null) controller.SetDebugTranscendence(data, wasDebugActive);
            if (controller != null && windSet != null) controller.SetDebugSetBonus(windSet, oldWindRequirement);
            if (controller != null && archerSet != null) controller.SetDebugSetBonus(archerSet, oldArcherRequirement);
            if (testedPlayer != null && testedPlayer.currentPlayerData != null) testedPlayer.currentPlayerData.critChance = oldCritChance;
            if (testedPlayer != null && testedPlayer.PlayerCombat != null) testedPlayer.PlayerCombat.currentWeapon = oldWeapon;
            foreach (GameObject item in temporary) if (item != null) Object.DestroyImmediate(item);
            temporary.Clear();
            File.WriteAllLines(Output + (SessionState.GetBool(Pending + ".ArcherOnly", false) ? "/archer-play-verification.txt" :
                SessionState.GetBool(Pending + ".ZeroDamage", false) ? "/zero-damage-verification.txt" : "/play-verification.txt"), results);
            Application.runInBackground = SessionState.GetBool(Pending + ".Background", false);
            if (SessionState.GetBool(Pending + ".Exit", false)) EditorApplication.isPlaying = false;
        }

        private static void Require(bool success, string label)
        {
            if (!success) throw new InvalidOperationException(label);
            results.Add("PASS " + label);
        }

        private static void RequireFlightTime()
        {
            if (EditorApplication.timeSinceStartup > flightDeadline)
                throw new InvalidOperationException("돌풍이 실제 허수아비와 충돌하지 않았습니다.");
        }
    }
}
