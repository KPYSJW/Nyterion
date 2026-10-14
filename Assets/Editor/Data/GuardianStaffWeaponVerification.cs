using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Nytherion.Data.ScriptableObjects.Items;
using Nytherion.Data.ScriptableObjects.Weapons;
using Nytherion.GamePlay.Characters.Enemy;
using Nytherion.GamePlay.Characters.Player;
using Nytherion.GamePlay.Combat;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Nytherion.Editor
{
    [InitializeOnLoad]
    public static class GuardianStaffWeaponVerification
    {
        private const string Pending = "Nytherion.GuardianStaff.Verify";
        private static readonly FieldInfo Health = typeof(EnemyBase).GetField("currentHealth", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly List<string> results = new List<string>();
        private static readonly List<GameObject> temporary = new List<GameObject>();
        private static readonly List<EnemyBase> targets = new List<EnemyBase>();
        private static GuardianStaffWeapon weapon;
        private static WeaponData settings;
        private static Transform owner;
        private static PlayerController controller;
        private static EnemyBase outside;
        private static EnemyBase extendedRangeTarget;
        private static int extendedRangeHits;
        private static float nextCheck;
        private static float sortingCheckAt;
        private static bool sortingCheckPending;
        private static int summonPrefabSortingOrder;
        private static float previousTimeScale;
        private static bool previousBackground;
        private static bool exitAfter;
        private static bool running;
        private static int attacks;
        private static double readyAt;

        static GuardianStaffWeaponVerification()
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
            string request = GuardianStaffWeaponSetup.Output + "/verify.request";
            if (File.Exists(request))
            {
                File.Delete(request);
                SessionState.SetBool(Pending, true);
                SessionState.SetBool(Pending + ".Exit", !EditorApplication.isPlaying);
                readyAt = EditorApplication.timeSinceStartup + 3d;
                if (!EditorApplication.isPlaying) EditorApplication.isPlaying = true;
            }
            if (SessionState.GetBool(Pending, false) && EditorApplication.isPlaying && EditorApplication.timeSinceStartup >= readyAt)
            {
                SessionState.SetBool(Pending, false);
                try { Begin(); }
                catch (Exception error) { Finish(error); }
            }
            if (!running) return;
            try
            {
                if (sortingCheckPending && Time.time >= sortingCheckAt)
                {
                    VerifySorting(ActiveEffects().Single());
                    sortingCheckPending = false;
                    results.Add("PASS 재생 중 플레이어 정렬 순서 변경 후에도 공격 이펙트가 플레이어·무기보다 앞에 표시");
                }
                if (Time.time < nextCheck) return;
                foreach (EnemyBase target in targets) Equal(10000f - attacks * 15f, (float)Health.GetValue(target), "공격당 한 번 타격");
                Equal(10000f, (float)Health.GetValue(outside), "원 밖의 적 제외");
                Equal(10000f - extendedRangeHits * 15f, (float)Health.GetValue(extendedRangeTarget), "이펙트별 배율에 따른 확장 범위 타격");
                Require(!ActiveEffects().Any(), "애니메이션 종료 후 이펙트 반환");
                Require(!ActiveHitEffects().Any(), "타격 이펙트 애니메이션 종료 후 풀 반환");
                results.Add($"PASS 공격 {attacks}: 중복 콜라이더 포함 72명 각 1회 타격, 범위 밖 제외, 이펙트 종료/반환");
                if (attacks == 5) { Finish(null); return; }
                Cast();
            }
            catch (Exception error) { Finish(error); }
        }

        private static void Begin()
        {
            results.Clear();
            targets.Clear();
            attacks = 0;
            extendedRangeHits = 0;
            exitAfter = SessionState.GetBool(Pending + ".Exit", false);
            previousTimeScale = Time.timeScale;
            previousBackground = Application.runInBackground;
            Application.runInBackground = true;
            Time.timeScale = 1f;
            WeaponData data = AssetDatabase.LoadAssetAtPath<WeaponData>(GuardianStaffWeaponSetup.DataPath);
            Require(data != null && data.weaponPrefab != null, "무기 에셋/프리팹 연결");
            var weaponSerialized = new SerializedObject(data.weaponPrefab);
            Require(weaponSerialized.FindProperty("attackEffects").arraySize == 4, "공격 이펙트 4개 연결");
            for (int number = 1; number <= 4; number++)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(GuardianStaffWeaponSetup.EffectPrefix + number + ".prefab");
                Require(prefab != null && weaponSerialized.FindProperty("attackEffects").GetArrayElementAtIndex(number - 1).objectReferenceValue ==
                    prefab.GetComponent<GuardianStaffAttackEffect>(), "순서별 공격 프리팹 연결");
                var animator = prefab.GetComponent<Animator>();
                Require(animator != null && animator.runtimeAnimatorController != null && animator.cullingMode == AnimatorCullingMode.AlwaysAnimate,
                    "공격 Animator 연결 및 화면 밖에서도 종료");
                AnimationClip clip = animator.runtimeAnimatorController.animationClips.Single();
                var keys = AnimationUtility.GetObjectReferenceCurve(clip,
                    EditorCurveBinding.PPtrCurve(string.Empty, typeof(SpriteRenderer), "m_Sprite"));
                int count = number == 1 ? 4 : number == 4 ? 6 : 14;
                Require(!clip.isLooping && keys.Length == count + 1 && keys[count].value == null, "공격 1회 재생 후 이미지 제거");
                Require(keys.Take(count).All(key => AssetDatabase.GetAssetPath(key.value).EndsWith($"Guardian'sStaffAttackEffect{number}.png")),
                    "공격 프레임 원본 연결");
                GameObject attackSample = Object.Instantiate(prefab);
                temporary.Add(attackSample);
                Animator sampleAnimation = attackSample.GetComponent<Animator>();
                SpriteRenderer sampleSprite = attackSample.GetComponent<SpriteRenderer>();
                sampleAnimation.Rebind();
                sampleAnimation.Play(0, 0, 0f);
                sampleAnimation.Update(0f);
                Require(sampleSprite.sprite == keys[0].value, "공격 첫 프레임 재생");
                sampleAnimation.Update((count - 0.75f) / clip.frameRate);
                Require(sampleSprite.sprite == keys[count - 1].value, "공격 마지막 프레임 재생");
                sampleAnimation.Update(1f / clip.frameRate);
                Require(sampleSprite.sprite == null, "공격 재생 종료 후 잔상 없음");
                attackSample.SetActive(false);
                attackSample.SetActive(true);
                attackSample.GetComponent<GuardianStaffAttackEffect>().Play(null, 1f, null, null, null);
                Require(sampleSprite.sprite == keys[0].value, "비활성화 후 재사용 시 공격 첫 프레임 복구");
                attackSample.SetActive(false);
                results.Add($"PASS 공격 이펙트 {number}: Animator {count}프레임, 마지막 이미지 제거, 재사용 시 첫 프레임 복구");
            }
            Require(data.hitEffectPrefab == AssetDatabase.LoadAssetAtPath<GameObject>(GuardianStaffWeaponSetup.HitEffectPath), "타격 이펙트 프리팹 연결");
            var hitAnimator = data.hitEffectPrefab.GetComponent<Animator>();
            Require(hitAnimator != null && hitAnimator.runtimeAnimatorController != null, "타격 이펙트 애니메이터 연결");
            AnimationClip hitClip = hitAnimator.runtimeAnimatorController.animationClips.Single();
            var hitKeys = AnimationUtility.GetObjectReferenceCurve(hitClip,
                EditorCurveBinding.PPtrCurve(string.Empty, typeof(SpriteRenderer), "m_Sprite"));
            Require(hitKeys.Take(4).All(key => AssetDatabase.GetAssetPath(key.value).EndsWith("Guardian'sStaffHitEffect.png")), "지정한 타격 이펙트 4프레임 연결");
            Require(hitKeys.Length == 5 && hitKeys[4].value == null, "마지막 프레임 뒤 스프라이트 즉시 제거");
            Equal(4f / hitClip.frameRate, hitKeys[4].time, "4프레임 재생 완료 시점에 제거");
            GameObject sample = new GameObject("[GuardianStaffVerification] 종료 프레임 검증");
            temporary.Add(sample);
            SpriteRenderer sampleRenderer = sample.AddComponent<SpriteRenderer>();
            Animator sampleAnimator = sample.AddComponent<Animator>();
            sampleAnimator.runtimeAnimatorController = hitAnimator.runtimeAnimatorController;
            sampleAnimator.Rebind();
            sampleAnimator.Play("GuardianStaffHit", 0, (3.25f / hitClip.frameRate) / hitClip.length);
            sampleAnimator.Update(0f);
            Require(sampleRenderer.sprite == hitKeys[3].value, $"마지막 프레임 정상 표시: 실제={sampleRenderer.sprite?.name}");
            sampleAnimator.Play("GuardianStaffHit", 0, (4.1f / hitClip.frameRate) / hitClip.length);
            sampleAnimator.Update(0f);
            Require(sampleRenderer.sprite == null, "풀 반환 전에도 종료 시점 이후 마지막 이미지 없음");
            sampleAnimator.Play("GuardianStaffHit", 0, 0f);
            sampleAnimator.Update(0f);
            Require(sampleRenderer.sprite == hitKeys[0].value, "재사용 시 첫 프레임 복구");
            sample.SetActive(false);
            results.Add("PASS 마지막 프레임 한 프레임 표시 후 약 0.286초에 제거, 풀 반환 대기 중 잔상 없음");
            Require(!hitClip.isLooping && data.hitEffectPrefab.GetComponent<AutoReturnToPool>() != null, "타격 애니메이션 1회 재생/풀 반환 설정");
            Require(data.icon == AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Nytherion/Art/Combat/Weapons/Icons/Guardian'sStaff_Icon.png"), "아이콘 연결");
            Require(data.weaponSprite == AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Nytherion/Art/Combat/Weapons/Sprites/Guardian'sStaff.png"), "무기 스프라이트 연결");
            var database = AssetDatabase.LoadAssetAtPath<ItemDatabaseSO>("Assets/Nytherion/Data/ScriptableObjects/Items/ItemDatabaseSO.asset");
            Require(database.allItems.Count(item => item == data) == 1, "데이터베이스 단일 등록");
            results.Add("PASS 이미지/아이콘 및 아이템 등록 확인, 현재 무기 비주얼 설정으로 검증");

            settings = Object.Instantiate(data);
            summonPrefabSortingOrder = AssetDatabase.FindAssets("t:Prefab", new[]
                { "Assets/Prefabs/Gameplay/Characters/Companions", "Assets/Prefabs/Gameplay/Skills" })
                .Select(guid => AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid)))
                .SelectMany(prefab => prefab.GetComponentsInChildren<SpriteRenderer>(true))
                .Select(renderer => renderer.sortingOrder).DefaultIfEmpty(0).Max();
            GameObject root = new GameObject("[GuardianStaffVerification] 플레이어 중심");
            temporary.Add(root);
            owner = root.transform;
            owner.position = new Vector3(10000f, 10000f, 0f);
            root.AddComponent<SpriteRenderer>().sortingOrder = 12;
            controller = root.AddComponent<PlayerController>();
            controller.enabled = false;
            weapon = Object.Instantiate((GuardianStaffWeapon)data.weaponPrefab, owner);
            // 무기 위치를 중심에서 멀리 두어 손이나 조준점 기준으로 피해가 발생하는 오류를 검출합니다.
            weapon.transform.localPosition = new Vector3(8f, 0.3f, 0f);
            weapon.Initialize(settings);
            weapon.damageMultiplier = 1.5f;
            for (int i = 0; i < 72; i++) targets.Add(Target(owner.position + Vector3.right * settings.range * 0.5f, i == 0));
            outside = Target(owner.position + Vector3.right * (settings.range * 2f + 1f), false);
            extendedRangeTarget = Target(owner.position + Vector3.up * settings.range * 1.25f, false);
            Physics2D.SyncTransforms();
            running = true;
            Cast();
        }

        private static void Cast()
        {
            attacks++;
            if (attacks == 4)
            {
                settings.range *= 1.5f;
                owner.GetComponent<SpriteRenderer>().sortingOrder = 200;
                typeof(PlayerController).GetField("<IsFacingRight>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(controller, false);
            }
            var effects = new SerializedObject(weapon).FindProperty("attackEffects");
            var selectedEffect = (GuardianStaffAttackEffect)effects.GetArrayElementAtIndex((attacks - 1) % 4).objectReferenceValue;
            float expectedRadius = settings.range * selectedEffect.RadiusMultiplier;
            bool hitsExtendedTarget = selectedEffect.RadiusMultiplier > 1.25f;
            int expectedHitCount = targets.Count + (hitsExtendedTarget ? 1 : 0);
            extendedRangeTarget.transform.position = owner.position + Vector3.up * settings.range * 1.25f;
            outside.transform.position = owner.position + Vector3.right * (Mathf.Max(settings.range, expectedRadius) + 1f);
            Physics2D.SyncTransforms();
            weapon.Attack(Vector2.left, owner.position + Vector3.up * 20f);
            if (hitsExtendedTarget) extendedRangeHits++;
            Equal(10000f - extendedRangeHits * 15f, (float)Health.GetValue(extendedRangeTarget), "기본 범위 밖 적은 확장 공격에서만 피격");
            Require(ActiveHitEffects().Count() == expectedHitCount, "중복 콜라이더를 가진 적도 타격 이펙트 1개만 생성");
            Vector3 expectedHitPosition = targets[0].GetComponent<Collider2D>().bounds.center;
            Require(ActiveHitEffects().Count(effect => Vector3.Distance(effect.transform.position, expectedHitPosition) < 0.01f) == targets.Count, "타격 이펙트 적 피격 위치에서 생성");
            GuardianStaffAttackEffect effect = ActiveEffects().Single();
            VerifySorting(effect);
            owner.GetComponent<SpriteRenderer>().sortingOrder += 3;
            sortingCheckAt = Time.time + 0.1f;
            sortingCheckPending = true;
            int expected = (attacks - 1) % 4 + 1;
            Require(effect.name.Replace("(Clone)", "").Trim() == "GuardianStaffAttackEffect" + expected, "이펙트 1→2→3→4→1 순환");
            Equal(0f, Vector3.Distance(effect.transform.position, owner.position), "플레이어 중심 생성");
            Equal(expectedRadius, effect.NativeRadius * effect.transform.lossyScale.x, "이펙트 크기와 원형 타격 반지름 일치");
            float before = (float)Health.GetValue(targets[0]);
            weapon.Attack(Vector2.right);
            Equal(before, (float)Health.GetValue(targets[0]), "쿨다운 중 중복 입력 차단");
            Require(ActiveEffects().Count() == 1, "쿨다운 중 이펙트 추가 생성 차단");
            Require(ActiveHitEffects().Count() == expectedHitCount, "쿨다운 중 타격 이펙트 추가 생성 차단");
            results.Add("PASS 적 72명 각각 피격 위치에 타격 이펙트 1개 생성, 중복 콜라이더/쿨다운 중 추가 생성 없음");
            results.Add($"PASS 이펙트 {expected}, 플레이어 중심, 배율 {selectedEffect.RadiusMultiplier}, 표시/판정 반지름 {expectedRadius}, 확장 범위 타격={hitsExtendedTarget}, 쿨다운 차단");
            nextCheck = Time.time + Mathf.Max(settings.cooldown, effect.Duration) + 0.15f;
            File.WriteAllLines(GuardianStaffWeaponSetup.Output + "/verification-progress.txt", results);
        }

        private static IEnumerable<GuardianStaffAttackEffect> ActiveEffects() => Object.FindObjectsOfType<GuardianStaffAttackEffect>()
            .Where(effect => effect.gameObject.activeInHierarchy && Vector3.Distance(effect.transform.position, owner.position) < 2f);

        private static void VerifySorting(GuardianStaffAttackEffect effect)
        {
            SpriteRenderer effectRenderer = effect.GetComponent<SpriteRenderer>();
            SpriteRenderer playerRenderer = owner.GetComponent<SpriteRenderer>();
            SpriteRenderer weaponRenderer = weapon.GetComponent<SpriteRenderer>();
            Require(effectRenderer.sortingLayerID == playerRenderer.sortingLayerID, "플레이어와 같은 Sorting Layer");
            Require(effectRenderer.sortingOrder > playerRenderer.sortingOrder && effectRenderer.sortingOrder > weaponRenderer.sortingOrder,
                "공격 이펙트 Order in Layer가 플레이어·무기보다 큼");
            Require(effectRenderer.sortingOrder > summonPrefabSortingOrder && effectRenderer.sortingOrder > playerRenderer.sortingOrder + 2,
                "현재 소환수·소환물 프리팹 및 플레이어 상대 순서보다 앞에 표시");
        }

        private static IEnumerable<AutoReturnToPool> ActiveHitEffects() => Object.FindObjectsOfType<AutoReturnToPool>()
            .Where(effect => effect.gameObject.activeInHierarchy && effect.name.Replace("(Clone)", "").Trim() == "GuardianStaffHitEffect" &&
                Vector3.Distance(effect.transform.position, owner.position) < settings.range * 1.25f + 0.1f);

        private static EnemyBase Target(Vector3 position, bool duplicate)
        {
            GameObject root = new GameObject("[GuardianStaffVerification] 적");
            temporary.Add(root);
            root.transform.position = position;
            root.layer = LayerMask.NameToLayer("Enemy");
            root.AddComponent<CircleCollider2D>().radius = 0.05f;
            if (duplicate)
            {
                var child = new GameObject("중복 콜라이더");
                child.transform.SetParent(root.transform, false);
                child.layer = root.layer;
                child.AddComponent<CircleCollider2D>().radius = 0.05f;
            }
            var enemy = root.AddComponent<EnemyBase>();
            Health.SetValue(enemy, 10000f);
            return enemy;
        }

        private static void Equal(float expected, float actual, string message) => Require(Mathf.Abs(expected - actual) < 0.01f, $"{message}: 예상 {expected}, 실제 {actual}");
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }

        private static void Finish(Exception error)
        {
            if (error != null) { results.Add("FAIL " + error); Debug.LogException(error); }
            if (weapon != null && attacks == 5 && error == null)
            {
                Equal(-8f, weapon.transform.localPosition.x, "왼쪽 방향 위치 반전");
                Require(weapon.transform.localScale.x < 0f, "왼쪽 방향 스프라이트 반전");
                Equal(-settings.spriteRotationOffset, Mathf.DeltaAngle(0f, weapon.transform.localEulerAngles.z), "왼쪽 방향 각도 반전");
                results.Add("PASS 좌우 방향 전환과 범위 확장 시 표시/피해 일치");
            }
            File.WriteAllLines(GuardianStaffWeaponSetup.Output + "/verification.txt",
                new[] { "Unity " + Application.unityVersion + " / 현재 씬 Play Mode", error == null ? "PASS" : "FAIL" }.Concat(results));
            foreach (GameObject root in temporary) if (root != null) Object.Destroy(root);
            temporary.Clear();
            if (settings != null) Object.Destroy(settings);
            Time.timeScale = previousTimeScale;
            Application.runInBackground = previousBackground;
            running = false;
            if (exitAfter) EditorApplication.isPlaying = false;
        }
    }
}
