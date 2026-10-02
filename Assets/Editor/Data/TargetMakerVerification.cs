using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Nytherion.Core.Managers;
using Nytherion.Data.ScriptableObjects.Relics;
using Nytherion.Data.ScriptableObjects.Weapons;
using Nytherion.Data.ScriptableObjects.Skill;
using Nytherion.GamePlay.Characters.Enemy;
using Nytherion.GamePlay.Characters.Player;
using Nytherion.GamePlay.Combat;
using Nytherion.GamePlay.Skills;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object = UnityEngine.Object;

namespace Nytherion.Editor
{
    [InitializeOnLoad]
    public static class TargetMakerVerification
    {
        private const string Pending = "Nytherion.TargetMaker.Verify";
        private static readonly List<Object> temporary = new List<Object>();
        private static readonly List<string> results = new List<string>();
        private static readonly FieldInfo Health = typeof(EnemyBase).GetField("currentHealth", BindingFlags.Instance | BindingFlags.NonPublic);
        private static PlayerRelicManager relics;
        private static RelicData relic;
        private static double readyAt;
        private static bool exitAfter;
        private static bool previousBackground;
        private static float previousTimeScale;

        static TargetMakerVerification()
        {
            EditorApplication.update += Update;
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Pending, false))
                    readyAt = EditorApplication.timeSinceStartup + 3d;
            };
        }

        private static void Update()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (File.Exists(TargetMakerSetup.Output + "/verify.request"))
            {
                File.Delete(TargetMakerSetup.Output + "/verify.request");
                SessionState.SetBool(Pending, true);
                SessionState.SetBool(Pending + ".Exit", !EditorApplication.isPlaying);
                readyAt = EditorApplication.timeSinceStartup + 3d;
                if (!EditorApplication.isPlaying) EditorApplication.isPlaying = true;
            }
            if (!SessionState.GetBool(Pending, false) || !EditorApplication.isPlaying || EditorApplication.timeSinceStartup < readyAt) return;
            SessionState.SetBool(Pending, false);
            exitAfter = SessionState.GetBool(Pending + ".Exit", false);
            previousBackground = Application.runInBackground;
            previousTimeScale = Time.timeScale;
            Application.runInBackground = true;
            Time.timeScale = 1f;
            results.Clear();
            try
            {
                GameObject root = new GameObject("[TargetMaker 검증] 플레이어");
                temporary.Add(root);
                root.transform.position = new Vector3(10000f, 10000f);
                root.AddComponent<SpriteRenderer>();
                var manager = root.AddComponent<PlayerManager>();
                relics = root.AddComponent<PlayerRelicManager>();
                relics.enabled = false;
                manager.Initialize();
                relic = Object.Instantiate(AssetDatabase.LoadAssetAtPath<RelicData>(TargetMakerSetup.RelicPath));
                temporary.Add(relic);
                WeaponData data = Object.Instantiate(AssetDatabase.LoadAssetAtPath<WeaponData>(BlazeshadeWeaponSetup.DataPath));
                temporary.Add(data);
                var weapon = Object.Instantiate((BlazeshadeWeapon)data.weaponPrefab, root.transform);
                weapon.Initialize(data);
                weapon.StartCoroutine(Run(Verify(root.transform, weapon, data)));
            }
            catch (Exception error) { Finish(error); }
        }

        private static IEnumerator Run(IEnumerator checks)
        {
            while (true)
            {
                object next;
                try
                {
                    if (!checks.MoveNext()) { Finish(null); yield break; }
                    next = checks.Current;
                }
                catch (Exception error) { Finish(error); yield break; }
                yield return next;
            }
        }

        private static IEnumerator Verify(Transform owner, BlazeshadeWeapon weapon, WeaponData data)
        {
            EnemyBase nearby = Enemy(owner.position + Vector3.right * 0.1f, true);
            Vector3 center = owner.position + Vector3.right * 30f;
            EnemyBase[] targets = Enumerable.Range(0, 72).Select(i => Enemy(center, i == 0)).ToArray();
            EnemyBase entrant = Enemy(center + Vector3.right * (data.range + 2f), false);
            Require(!weapon.CanAttack(), "미장착 시 수동 공격 차단");
            Physics2D.SyncTransforms();
            yield return new WaitForSeconds(Mathf.Max(0.05f, data.cooldown) + 0.1f);
            Require(Value(nearby) < 10000f && targets.All(enemy => Value(enemy) == 10000f), "기본 오라는 플레이어 주변만 타격");
            results.Add("PASS 미장착: 새 이미지의 지속 오라와 플레이어 중심 피해");
            SetRelic(true);
            yield return null;
            float nearbyHealth = Value(nearby);
            Require(weapon.CanAttack(), "장착 시 수동 공격 활성화");
            AnimationClip summon = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Nytherion/Art/Combat/Weapons/Animations/Blazeshade/BlazeShadeSummonEffect.anim");
            AnimationClip attack = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Nytherion/Art/Combat/Weapons/Animations/Blazeshade/BlazeShadeAttackEffect.anim");
            weapon.Attack(Vector2.right, center);
            Require(!weapon.CanAttack(), "소환/공격 중 중복 입력 차단");
            Require(Visual("BlazeShadeSummonEffect", center) != null, "마우스 지정 위치에 소환 효과");
            yield return new WaitForSeconds(summon.length * 0.5f);
            Require(targets.All(enemy => Value(enemy) == 10000f), "소환 중 피해 없음");
            owner.position += Vector3.up * 10f;
            yield return new WaitForSeconds(summon.length * 0.5f + 0.1f);
            Require(Visual("BlazeShadeAttackEffect", center) != null, "이동 후에도 지정 위치에 공격 효과 고정");
            Require(targets.All(enemy => Mathf.Abs(Value(enemy) - (10000f - data.damage)) < 0.01f), "밀집 적 72명과 중복 콜라이더 각각 1회 피해");
            entrant.transform.position = center;
            Physics2D.SyncTransforms();
            yield return new WaitForSeconds(0.15f);
            Require(Mathf.Abs(Value(entrant) - (10000f - data.damage)) < 0.01f, "공격 도중 진입한 적 1회 타격");
            yield return new WaitForSeconds(attack.length + 0.1f);
            Require(targets.All(enemy => Mathf.Abs(Value(enemy) - (10000f - data.damage)) < 0.01f), "공격 중 추가 피해 없음");
            Require(Value(nearby) == nearbyHealth, "타겟 모드에서 플레이어 주변 오라 피해 중지");
            Require(Visual("BlazeShadeAttackEffect", center) == null && weapon.CanAttack(), "애니메이션 종료 시 제거 및 재공격 가능");
            results.Add("PASS 소환→공격 순서, 지정 위치 고정, 72명/중복 콜라이더/늦은 진입 1회 타격, 종료 후 제거");
            weapon.Attack(Vector2.right, center);
            SetRelic(false);
            yield return null;
            yield return null;
            Require(Visual("BlazeShadeSummonEffect", center) == null && !weapon.CanAttack(), "해제 중 소환 취소");
            SetRelic(true);
            relic.isDisabled = true;
            Rebuild();
            Require(!weapon.CanAttack(), "침묵 상태 지정 공격 비활성화");
            relic.isDisabled = false;
            Rebuild();
            weapon.Attack(Vector2.right, center);
            weapon.enabled = false;
            yield return null;
            Require(Visual("BlazeShadeSummonEffect", center) == null, "무기 비활성화 시 이펙트 정리");
            results.Add("PASS 장착 해제/침묵/무기 비활성화 시 지정 공격 중지 및 정리");

            var staffData = Object.Instantiate(AssetDatabase.LoadAssetAtPath<WeaponData>(GuardianStaffWeaponSetup.DataPath));
            temporary.Add(staffData);
            staffData.hitEffectPrefab = null;
            var staff = Object.Instantiate((GuardianStaffWeapon)staffData.weaponPrefab, owner);
            staff.Initialize(staffData);
            EnemyBase atOwner = Enemy(owner.position, false);
            staff.Attack(Vector2.right, center);
            var effect = Object.FindObjectsOfType<GuardianStaffAttackEffect>().Single(e => Vector3.Distance(e.transform.position, center) < 0.01f);
            owner.position += Vector3.up * 10f;
            yield return null;
            Require(Vector3.Distance(effect.transform.position, center) < 0.01f && Value(atOwner) == 10000f, "수호자의 지팡이 지정 공격/이펙트 위치 고정");
            Require(Mathf.Abs(Value(targets[0]) - (10000f - data.damage - staffData.damage)) < 0.01f, "수호자의 지팡이 지정 위치 피해");
            yield return new WaitForSeconds(Mathf.Max(staffData.cooldown, effect.Duration) + 0.1f);
            SetRelic(false);
            EnemyBase restored = Enemy(owner.position, false);
            Physics2D.SyncTransforms();
            staff.Attack(Vector2.right, center);
            Require(Value(restored) < 10000f, "해제 후 플레이어 중심 공격 복구");
            results.Add("PASS 수호자의 지팡이 지정 위치 공격 및 해제 후 플레이어 중심 복구");
            foreach (object step in VerifyChainIgnition(owner)) yield return step;
        }

        private static IEnumerable VerifyChainIgnition(Transform owner)
        {
            var data = Object.Instantiate(AssetDatabase.LoadAssetAtPath<ChainIgnitionSkillData>(ChainIgnitionSkillSetup.DataPath));
            temporary.Add(data);
            data.coolDown = 0f;
            data.skillLevel = 1;
            data.baseProjectileCount = 8;
            data.castCenterOffset = new Vector2(0.2f, 0.3f);
            data.firstRingRadius = 1f;
            data.range = 3f;
            data.useEllipticalHitRange = false;
            data.explosionRadius = 0.1f;
            var skill = Object.Instantiate(data.skillPrefab, owner).GetComponent<ChainIgnitionSkill>();
            skill.skillData = data;
            skill.caster = owner;
            Vector3 mouse = owner.position + Vector3.right * 20f;
            Vector3 targetCenter = mouse;
            var waves = (List<ChainIgnitionWave>)typeof(ChainIgnitionSkill).GetField("waves", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(skill);
            CastChain(skill, mouse);
            Require(Vector3.Distance(waves.Last().transform.position, owner.position + (Vector3)data.castCenterOffset) < 0.01f, "연쇄 점화 미장착 시 플레이어 중심과 보정 유지");
            yield return null;
            waves.Last().gameObject.SetActive(false);
            SetRelic(true);
            GameObject targetObject = new GameObject("[TargetMaker 검증] 연쇄 점화 피해 대상");
            temporary.Add(targetObject);
            targetObject.layer = LayerMask.NameToLayer("Enemy");
            targetObject.transform.position = targetCenter;
            targetObject.AddComponent<CircleCollider2D>().radius = 0.05f;
            targetObject.AddComponent<CircleCollider2D>().radius = 0.05f;
            var target = targetObject.AddComponent<ChainIgnitionVerificationTarget>();
            Vector3 originalCenter = targetCenter;
            var ringTargets = new ChainIgnitionVerificationTarget[3];
            for (int i = 0; i < ringTargets.Length; i++)
                ringTargets[i] = ChainTarget(targetCenter + Vector3.right * data.GetRingRadius(i));
            ChainIgnitionVerificationTarget atPlayer = ChainTarget(owner.position + (Vector3)data.castCenterOffset + Vector3.right * data.firstRingRadius);
            CastChain(skill, mouse);
            ChainIgnitionWave originalWave = waves[0];
            ChainIgnitionWave wave = waves.Last();
            Require(waves.Count == 2 && originalWave != wave &&
                Vector3.Distance(originalWave.transform.position, originalCenter) < 0.01f &&
                Vector3.Distance(wave.transform.position, targetCenter) < 0.01f, "확산 폭발과 추가 중심 폭발 모두 조준 지점 중심");
            FieldInfo start = typeof(ChainIgnitionWave).GetField("startTime", BindingFlags.Instance | BindingFlags.NonPublic);
            Require(Mathf.Approximately((float)start.GetValue(originalWave), (float)start.GetValue(wave)), "첫 파동과 추가 폭발 동시 시작");
            bool[] originalDirections = (bool[])typeof(ChainIgnitionWave).GetField("activeDirections", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(originalWave);
            Require(originalDirections.Count(active => active) == 8, "기존 8방향 폭발 유지");
            bool[] directions = (bool[])typeof(ChainIgnitionWave).GetField("activeDirections", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(wave);
            Require(directions[0] && directions.Count(active => active) == 1, "기본 투사체 8개여도 폭발 하나만 사용");
            Vector3[] positions = (Vector3[])typeof(ChainIgnitionWave).GetField("explosionGroundPositions", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(wave);
            Require(positions[0] == Vector3.zero, "원 둘레가 아닌 조준 지점에 피해 중심 배치");
            owner.position += Vector3.up * 10f;
            yield return new WaitForSeconds(data.DamageDelay + 0.1f);
            Require(Vector3.Distance(wave.transform.position, targetCenter) < 0.01f && target.Hits == 1, "이동 후 지정 위치 고정 및 중복 콜라이더 한 번 타격");
            Require(wave.GetComponentsInChildren<SpriteRenderer>().Count(renderer => renderer.enabled) == 1, "표시되는 폭발 하나");
            Require(Vector3.Distance(originalWave.transform.position, originalCenter) < 0.01f &&
                originalWave.GetComponentsInChildren<SpriteRenderer>().Count(renderer => renderer.enabled) == 8, "플레이어 이동 후에도 조준 지점에서 첫 파동 8방향 유지");
            Require(ringTargets[0].Hits == 1 && ringTargets[1].Hits == 0 && ringTargets[2].Hits == 0 && atPlayer.Hits == 0,
                "1차는 조준 지점의 가까운 원과 중심점만 타격, 플레이어 주위 피해 없음");
            yield return new WaitForSeconds(data.WaveInterval);
            Require(ringTargets[1].Hits == 1 && ringTargets[2].Hits == 0 && target.Hits == 1 && atPlayer.Hits == 0,
                "2차는 같은 조준 지점의 중간 원만 타격, 중심 추가 폭발 없음");
            yield return new WaitForSeconds(data.WaveInterval);
            Require(ringTargets[2].Hits == 1 && target.Hits == 1 && atPlayer.Hits == 0,
                "3차는 같은 조준 지점의 먼 원만 타격, 중심 추가 폭발 없음");
            yield return new WaitForSeconds(data.AnimationDuration + 0.1f);
            bool[] playedSounds = (bool[])typeof(ChainIgnitionWave).GetField("playedSoundWaves", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(wave);
            bool[] damagedWaves = (bool[])typeof(ChainIgnitionWave).GetField("damagedWaves", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(wave);
            Require(target.Hits == 1 && playedSounds.Count(played => played) == 1 && damagedWaves.Count(damaged => damaged) == 1,
                "조준 지점은 추가 폭발/피해/효과음 각 1회만 처리");
            bool[] originalDamage = (bool[])typeof(ChainIgnitionWave).GetField("damagedWaves", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(originalWave);
            bool[] originalSound = (bool[])typeof(ChainIgnitionWave).GetField("playedSoundWaves", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(originalWave);
            Require(originalDamage.All(damaged => damaged) && originalSound.All(played => played), "기존 1·2·3차 폭발/피해/효과음 유지");
            foreach (ChainIgnitionWave item in waves) item.gameObject.SetActive(false);
            SetRelic(false);
            CastChain(skill, mouse);
            Require(Vector3.Distance(waves[0].transform.position, owner.position + (Vector3)data.castCenterOffset) < 0.01f &&
                !wave.gameObject.activeSelf, "장착 해제 시 추가 폭발 없이 기존 파동만 시전");
            yield return null;
            waves[0].gameObject.SetActive(false);
            SetRelic(true);
            relic.isDisabled = true;
            Rebuild();
            CastChain(skill, mouse);
            Require(Vector3.Distance(waves[0].transform.position, owner.position + (Vector3)data.castCenterOffset) < 0.01f &&
                !wave.gameObject.activeSelf, "침묵 시 추가 폭발 없이 기존 파동만 시전");
            yield return null;
            foreach (ChainIgnitionWave item in waves) item.gameObject.SetActive(false);
            relic.isDisabled = false;
            Rebuild();
            CastChain(skill, mouse);
            Require(waves.Count == 2 && waves.All(item => item.gameObject.activeSelf) &&
                Vector3.Distance(wave.transform.position, mouse) < 0.01f, "재장착 후 기존 풀로 연쇄 파동과 추가 폭발 재사용");
            results.Add("PASS 조준 지점 중심 3단계/8방향 확산 및 단계별 피해, 1차에만 중심점 추가 폭발/피해 1회, 플레이어 주변 피해 없음, 해제/침묵 및 풀 재사용");
        }

        private static ChainIgnitionVerificationTarget ChainTarget(Vector3 position)
        {
            var root = new GameObject("[TargetMaker 검증] 연쇄 폭발 위치별 대상");
            temporary.Add(root);
            root.transform.position = position;
            root.layer = LayerMask.NameToLayer("Enemy");
            root.AddComponent<CircleCollider2D>().radius = 0.02f;
            return root.AddComponent<ChainIgnitionVerificationTarget>();
        }

        private static void CastChain(ChainIgnitionSkill skill, Vector3 mouse)
        {
            Require(Camera.main != null && Mouse.current != null, "카메라/마우스 준비");
            Vector2 previous = Mouse.current.position.ReadValue();
            InputUpdateType update = InputState.currentUpdateType;
            try
            {
                InputState.Change(Mouse.current.position, (Vector2)Camera.main.WorldToScreenPoint(mouse), update);
                Require(skill.TryUse(), "연쇄 점화 시전 성공");
            }
            finally { InputState.Change(Mouse.current.position, previous, update); }
        }

        private static void SetRelic(bool active)
        {
            relics.equippedRelics.Clear();
            if (active) relics.equippedRelics.Add(relic);
            Rebuild();
        }
        private static void Rebuild() => typeof(PlayerRelicManager).GetMethod("RebuildCombatModifiers", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(relics, null);
        private static float Value(EnemyBase enemy) => (float)Health.GetValue(enemy);
        private static GameObject Visual(string name, Vector3 center) => Object.FindObjectsOfType<SpriteRenderer>()
            .Select(renderer => renderer.gameObject).FirstOrDefault(root => root.name == name + "(Clone)" && Vector3.Distance(root.transform.position, center) < 0.01f);
        private static EnemyBase Enemy(Vector3 position, bool duplicate)
        {
            var root = new GameObject("[TargetMaker 검증] 적");
            temporary.Add(root);
            root.transform.position = position;
            root.layer = LayerMask.NameToLayer("Enemy");
            root.AddComponent<CircleCollider2D>().radius = 0.05f;
            if (duplicate) root.AddComponent<CircleCollider2D>().radius = 0.05f;
            var enemy = root.AddComponent<EnemyBase>();
            Health.SetValue(enemy, 10000f);
            return enemy;
        }
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        private static void Finish(Exception error)
        {
            if (error != null) { results.Add("FAIL " + error); Debug.LogException(error); }
            File.WriteAllLines(TargetMakerSetup.Output + "/verification.txt", new[] { "Unity " + Application.unityVersion + " / 현재 씬 Play Mode", error == null ? "PASS" : "FAIL" }.Concat(results));
            foreach (Object item in temporary) if (item != null) Object.Destroy(item);
            temporary.Clear();
            Time.timeScale = previousTimeScale;
            Application.runInBackground = previousBackground;
            if (exitAfter) EditorApplication.isPlaying = false;
        }
    }
}
