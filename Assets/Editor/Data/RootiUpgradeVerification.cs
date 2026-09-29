using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Nytherion.Core.Interfaces;
using Nytherion.Core.Managers;
using Nytherion.Data.ScriptableObjects.Relics;
using Nytherion.Data.ScriptableObjects.Skill;
using Nytherion.GamePlay.Characters.Player;
using Nytherion.GamePlay.Combat;
using Nytherion.GamePlay.Skills;
using Nytherion.Gameplay.Relics.Modules;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Nytherion.Editor
{
    /// <summary>실제 루티 에셋, 스킬 사용 경로와 Physics2D로 충전·강화·튕김을 검증합니다.</summary>
    [InitializeOnLoad]
    public static class RootiUpgradeVerification
    {
        private const string Pending = "RootiUpgradeVerification.Pending";
        private const string Output = RootiUpgradeRelicSetup.Output;
        private static readonly Vector3 Center = new Vector3(1000f, 1000f);
        private static readonly List<string> checks = new List<string>();
        private static readonly List<GameObject> objects = new List<GameObject>();
        private static IEnumerator routine;
        private static float nextTime;
        private static double readyAt;
        private static TurretSkillData data;
        private static PlayerManager player;
        private static TurretSkill skill;
        private static RootiUpgradeRuntime upgrades;
        private static Camera testCamera;

        static RootiUpgradeVerification()
        {
            EditorApplication.update += Update;
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Pending, false))
                    readyAt = EditorApplication.timeSinceStartup + 3d;
            };
        }

        // 배치 실행은 별도의 프로젝트 복사본에서만 사용합니다.
        public static void RunBatch()
        {
            // 테스트 복사본의 저장 경로를 분리하여 원래 게임 세이브를 보호합니다.
            PlayerSettings.productName += "_RootiVerification";
            RootiUpgradeRelicSetup.Setup();
            EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity");
            SessionState.SetBool(Pending, true);
            EditorApplication.isPlaying = true;
        }

        private static void Update()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            try
            {
                if (SessionState.GetBool(Pending, false) && EditorApplication.isPlaying && readyAt > 0d &&
                    EditorApplication.timeSinceStartup >= readyAt)
                {
                    SessionState.SetBool(Pending, false);
                    Time.timeScale = 1f;
                    routine = Verify();
                }
                if (routine == null || Time.time < nextTime) return;
                if (!routine.MoveNext()) { Finish(null); return; }
                nextTime = Time.time + (float)routine.Current;
            }
            catch (Exception error) { Finish(error); }
        }

        private static IEnumerator Verify()
        {
            string[] names = { "PurpleRadish", "Ginseng", "AzureBerry", "EchoFlower", "BounceBerry" };
            foreach (string name in names)
            {
                RelicData relic = Relic(name);
                Require(relic != null && relic.Image != null && relic.effectModules.Any(module => module.effects.Any(effect =>
                    effect is RootiUpgradeRelicEffect)), name + " 에셋·아이콘·SerializeReference 효과 역직렬화");
            }
            data = Object.Instantiate(AssetDatabase.LoadAssetAtPath<TurretSkillData>(
                "Assets/Nytherion/Data/ScriptableObjects/Skill/Turret_Skill.asset"));
            Require(data.turretPrefab.GetComponent<RootiTurretController>() != null &&
                data.projectilePrefab.GetComponent<CollisionObject>() != null && data.skillPrefab.GetComponent<TurretSkill>() != null,
                "실제 루티 포탑·씨앗·시전 프리팹 Inspector 참조");
            Require(data.projectilePrefab.GetComponent<ProjDistanceLimit>() == null &&
                data.projectilePrefab.GetComponent<ProjCameraBoundsLimit>() == null,
                "씨앗 프리팹의 이동 거리·카메라 화면 이탈 반환 컴포넌트 제거");
            // 화면 이탈·카메라 이동·확대가 씨앗을 반환하지 않는지 확인합니다.
            foreach (Camera camera in Object.FindObjectsOfType<Camera>())
                if (camera.CompareTag("MainCamera")) camera.enabled = false;
            testCamera = Track(new GameObject("루티 검증 게임 카메라")).AddComponent<Camera>();
            testCamera.tag = "MainCamera";
            testCamera.orthographic = true;
            testCamera.orthographicSize = 20f;
            testCamera.aspect = 1f;
            testCamera.transform.position = Center + Vector3.back * 10f;
            data.coolDown = 1.4f;
            data.maxTurretCount = 20;
            data.duration = 60f;
            data.attackInterval = 0.8f;
            player = Track(new GameObject("루티 강화 검증 플레이어")).AddComponent<PlayerManager>();
            player.transform.position = Center;
            PlayerRelicManager relicManager = player.gameObject.AddComponent<PlayerRelicManager>();
            RelicEffectController effects = player.gameObject.AddComponent<RelicEffectController>();
            yield return 0.05f;
            foreach (string name in names) relicManager.equippedRelics.Add(Object.Instantiate(Relic(name)));
            effects.ReevaluateAllConditions();
            upgrades = player.GetComponent<RootiUpgradeRuntime>();
            Require(upgrades != null && upgrades.AdditionalCharges == 1 && upgrades.AdditionalProjectiles == 2 &&
                upgrades.AdditionalSummons == 1 && Mathf.Approximately(upgrades.AttackSpeedMultiplier, 1.25f) && upgrades.MaxBounces == 3,
                "기존 RelicEffectController 장착 경로로 다섯 강화 적용");
            effects.ReevaluateAllConditions();
            Require(upgrades.AdditionalProjectiles == 2 && upgrades.AdditionalCharges == 1,
                "조건 재평가 중복 적용 방지");

            skill = Track(Object.Instantiate(data.skillPrefab)).GetComponent<TurretSkill>();
            skill.skillData = data;
            skill.caster = player.transform;
            SkillBase baseSkill = skill;
            Require(skill.CurrentCharges == 2 && skill.MaxCharges == 2, "인삼 최대 저장 1 → 2 및 초기 충전");
            Require(baseSkill.TryUse() && skill.CurrentCharges == 1 && Rootis().Length == 2,
                "SkillBase 사용 경로: 충전 1개로 복제초 루티 2마리 소환");
            float firstUse = Time.time;
            Require(!baseSkill.TryUse() && baseSkill.GetRemainingCooldown() > 0.45f,
                "충전이 남아 있어도 0.5초 전 연속 소환 차단·대기시간 표시");
            yield return 0.32f;
            Require(!baseSkill.TryUse() && skill.CurrentCharges == 1, "0.5초 미만 사용 시도에서 충전 보존");
            yield return 0.22f;
            Require(baseSkill.TryUse() && skill.CurrentCharges == 0 && Rootis().Length == 4,
                "0.5초 후 즉시 두 번째 소환·4마리 생성");
            Require(skill.RemainingRechargeTime < 1f, "연속 소환이 첫 충전의 회복 타이머를 초기화하지 않음");
            yield return Mathf.Max(0.01f, firstUse + data.coolDown + 0.08f - Time.time);
            Require(skill.CurrentCharges == 1, "첫 쿨타임 종료 시 하나만 회복");
            yield return data.coolDown + 0.08f;
            Require(skill.CurrentCharges == 2 && skill.RemainingRechargeTime == 0f, "두 번째 쿨타임 종료 시 최대 두 개 저장");

            RelicData ginseng = relicManager.equippedRelics.First(relic => relic.name.StartsWith("Ginseng"));
            ginseng.isDisabled = true;
            effects.ReevaluateAllConditions();
            Require(skill.MaxCharges == 1 && skill.CurrentCharges == 1, "인삼 침묵 시 최대 저장·남은 충전 1개로 제한");
            ginseng.isDisabled = false;
            effects.ReevaluateAllConditions();
            Require(skill.MaxCharges == 2 && skill.CurrentCharges == 1, "인삼 재활성화로 무료 충전이 생기지 않음");
            yield return data.coolDown + 0.08f;
            Require(skill.CurrentCharges == 2, "재활성화로 늘어난 저장 칸도 쿨타임으로 회복");

            RootiTurretController rooti = Rootis()[0];
            foreach (RootiTurretController other in Rootis())
            {
                other.enabled = false;
                other.transform.position = Center + Vector3.up * 30f;
            }
            rooti.transform.position = Center;
            rooti.enabled = true;
            rooti.Deploy(Center, Center);
            yield return 0.8f;
            RootiUpgradeTarget targetA = Target("적 A", Center + Vector3.right * 2f + Vector3.up * 0.46f);
            Physics2D.SyncTransforms();
            yield return 0.05f;
            // 실제 공격 애니메이션 이벤트가 씨앗을 쏘도록 기다립니다.
            float shotDeadline = Time.time + 1.5f;
            while (Seeds().Length == 0 && Time.time < shotDeadline) yield return 0.01f;
            Require(Seeds().Length == 3, "실제 루티 공격 애니메이션에서 벽청 베리 씨앗 1 → 3개 발사");
            GameObject[] seeds = Seeds();
            Require(seeds.All(seed => seed.GetComponent<BounceModifier>().enabled) &&
                seeds.Select(seed => seed.GetComponent<Rigidbody2D>().velocity.normalized).Distinct().Count() == 3,
                "추가 씨앗 좌우 발사·각 투사체에 통통 베리 적용");
            Animator animator = rooti.GetComponentInChildren<Animator>();
            Require(Mathf.Approximately(animator.speed, 1.25f), "자색 무가 공격 애니메이션과 공격 간격에 함께 반영");
            rooti.enabled = false;
            foreach (GameObject seed in seeds) seed.GetComponent<CollisionObject>().ReturnToPool();

            // 실제 물리 충돌에서 A를 맞힌 씨앗이 다른 적 B로 방향을 바꿉니다.
            RootiUpgradeTarget targetB = Target("적 B", Center + Vector3.right * 2f + Vector3.up * 2f);
            targetA.transform.position = Center + Vector3.right;
            Physics2D.SyncTransforms();
            GameObject bouncingSeed = SpawnSeed(rooti, Center, Vector2.right);
            yield return 0.24f;
            Require(targetA.Hits == 1 && bouncingSeed.activeSelf &&
                bouncingSeed.GetComponent<Rigidbody2D>().velocity.y > 0.1f,
                "Physics2D 적 충돌 피해 후 범위 내 다른 적 방향으로 튕김");
            yield return 0.55f;
            Require(targetB.Hits == 1 && bouncingSeed.activeSelf,
                "튕긴 씨앗이 실제로 두 번째 적에 피해·대상이 없으면 무작위 방향으로 생존");
            targetA.gameObject.SetActive(false);
            targetB.gameObject.SetActive(false);
            bouncingSeed.GetComponent<CollisionObject>().ReturnToPool();

            targetA.transform.position = Center + Vector3.right;
            targetB.transform.position = Center + Vector3.right * 2f;
            targetA.ResetHits();
            targetB.ResetHits();
            targetA.gameObject.SetActive(true);
            targetB.gameObject.SetActive(true);
            RootiUpgradeTarget targetC = Target("적 C", Center + Vector3.right * 3f);
            RootiUpgradeTarget targetD = Target("적 D", Center + Vector3.right * 4f);
            Physics2D.SyncTransforms();
            GameObject limitedSeed = SpawnSeed(rooti, Center, Vector2.right);
            yield return 0.9f;
            Require(targetA.Hits == 1 && targetB.Hits == 1 && targetC.Hits == 1 && targetD.Hits == 1 && !limitedSeed.activeSelf,
                "실제 네 적 연속 명중·3회 튕김 후 종료·같은 적 중복 피해 방지");
            foreach (RootiUpgradeTarget target in new[] { targetA, targetB, targetC, targetD }) target.gameObject.SetActive(false);

            GameObject wall = Track(new GameObject("루티 검증 벽"));
            wall.tag = "Wall";
            wall.transform.position = Center + Vector3.right;
            wall.AddComponent<BoxCollider2D>().size = new Vector2(0.2f, 2f);
            Physics2D.SyncTransforms();
            GameObject wallSeed = SpawnSeed(rooti, Center, Vector2.right);
            yield return 0.24f;
            Require(!wallSeed.activeSelf, "통통 베리 씨앗은 벽 충돌 시 튕기지 않고 종료");
            wall.SetActive(false);

            GameObject obstacle = Track(Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Nytherion/Core/test/Obstacle_test.prefab")));
            obstacle.transform.position = Center + Vector3.right;
            Physics2D.SyncTransforms();
            GameObject obstacleSeed = SpawnSeed(rooti, Center, Vector2.right);
            yield return 0.3f;
            Require(!obstacleSeed.activeSelf, "실제 Obstacle 레이어 구조물 프리팹 충돌 시 종료");
            obstacle.SetActive(false);

            GameObject parentWall = Track(new GameObject("벽 태그 부모"));
            parentWall.tag = "Wall";
            parentWall.transform.position = Center + Vector3.right;
            GameObject wallBody = new GameObject("태그 없는 벽 충돌체");
            wallBody.transform.SetParent(parentWall.transform, false);
            wallBody.AddComponent<BoxCollider2D>().size = new Vector2(0.2f, 2f);
            Physics2D.SyncTransforms();
            GameObject childWallSeed = SpawnSeed(rooti, Center, Vector2.right);
            yield return 0.24f;
            Require(!childWallSeed.activeSelf, "부모에 벽 태그가 있는 자식 충돌체도 벽으로 처리");
            parentWall.SetActive(false);

            GameObject sensor = Track(new GameObject("구조물 감지용 트리거"));
            sensor.layer = LayerMask.NameToLayer("Obstacle");
            sensor.transform.position = Center;
            sensor.AddComponent<CircleCollider2D>().isTrigger = true;
            GameObject sensorSeed = SpawnSeed(rooti, Center, Vector2.zero);
            Physics2D.SyncTransforms();
            yield return 0.08f;
            Require(sensorSeed.activeSelf, "구조물 레이어의 감지용 트리거는 씨앗을 막지 않음");
            sensorSeed.GetComponent<CollisionObject>().ReturnToPool();
            sensor.SetActive(false);

            GameObject floor = Track(new GameObject("루티 검증 바닥"));
            floor.tag = "Ground";
            floor.layer = LayerMask.NameToLayer("Floor");
            floor.transform.position = Center;
            floor.AddComponent<BoxCollider2D>();
            GameObject floorSeed = SpawnSeed(rooti, Center, Vector2.zero);
            Physics2D.SyncTransforms();
            yield return 0.08f;
            Require(floorSeed.activeSelf, "바닥 충돌체와 겹쳐도 씨앗 유지");
            floorSeed.GetComponent<CollisionObject>().ReturnToPool();
            floor.SetActive(false);

            // 루티가 살아 있는 동안 유물을 해제해도 다음 씨앗과 공격속도에서 바로 원복됩니다.
            relicManager.equippedRelics.Clear();
            effects.ReevaluateAllConditions();
            Require(upgrades.AdditionalProjectiles == 0 && upgrades.MaxBounces == 0 && upgrades.AttackSpeedMultiplier == 1f,
                "유물 해제 시 모든 강화 원복");
            GameObject plainSeed = SpawnSeed(rooti, Center, Vector2.right);
            Require(!plainSeed.GetComponent<BounceModifier>().enabled, "풀 재사용·유물 해제 후 이전 튕김 상태가 남지 않음");
            targetA.gameObject.SetActive(true);
            targetA.ResetHits();
            Physics2D.SyncTransforms();
            yield return 0.24f;
            Require(targetA.Hits == 1 && !plainSeed.activeSelf, "유물 없는 루티 씨앗은 적 1회 피해 후 풀 반환");

            // 기존 무기 튕김 옵션은 적이 없을 때 계속 날아가는 기능이 자동으로 켜지지 않습니다.
            GameObject defaultSeed = ObjectPoolManager.Instance.SpawnFromPool(data.projectilePrefab, Center, Quaternion.identity);
            BounceModifier defaultBounce = defaultSeed.GetComponent<BounceModifier>();
            defaultBounce.enabled = true;
            targetA.transform.position = Center;
            Physics2D.SyncTransforms();
            Require(!defaultBounce.OnHit(targetA.GetComponent<Collider2D>()), "기존 BounceModifier 기본 동작 보존");
            defaultSeed.GetComponent<CollisionObject>().ReturnToPool();
            targetA.gameObject.SetActive(false);

            GameObject longRangeSeed = SpawnSeed(rooti, Center, Vector2.right);
            yield return data.range / data.projectileSpeed + 0.3f;
            Require(longRangeSeed.activeSelf && Vector3.Distance(longRangeSeed.transform.position, Center) > data.range,
                "적 탐색 사거리보다 멀리 날아가도 씨앗 유지");
            longRangeSeed.GetComponent<CollisionObject>().ReturnToPool();

            GameObject marginSeed = SpawnSeed(rooti, Center, Vector2.zero);
            marginSeed.transform.position = testCamera.ViewportToWorldPoint(new Vector3(1.05f, 0.5f, 10f));
            yield return 0.05f;
            Require(marginSeed.activeSelf, "화면 가장자리 바깥 5% 위치에서 씨앗 유지");
            marginSeed.transform.position = testCamera.ViewportToWorldPoint(new Vector3(1.11f, 0.5f, 10f));
            yield return 0.05f;
            Require(marginSeed.activeSelf, "카메라 화면 바깥 10%를 초과해도 씨앗 유지");
            marginSeed.GetComponent<CollisionObject>().ReturnToPool();
            foreach (Vector2 viewport in new[] { new Vector2(-2f, 0.5f), new Vector2(0.5f, -2f), new Vector2(0.5f, 3f) })
            {
                GameObject edgeSeed = SpawnSeed(rooti, Center, Vector2.zero);
                edgeSeed.transform.position = testCamera.ViewportToWorldPoint(new Vector3(viewport.x, viewport.y, 10f));
                yield return 0.05f;
                Require(edgeSeed.activeSelf, "왼쪽·아래·위 화면에서 멀리 벗어나도 씨앗 유지 " + viewport);
                edgeSeed.GetComponent<CollisionObject>().ReturnToPool();
            }
            GameObject reusedSeed = SpawnSeed(rooti, Center, Vector2.zero);
            yield return 0.05f;
            Require(reusedSeed.activeSelf, "풀 재사용 후에도 씨앗 유지");
            testCamera.transform.position += Vector3.right * 50f;
            yield return 0.05f;
            Require(reusedSeed.activeSelf, "카메라 이동으로 화면에서 벗어나도 씨앗 유지");
            reusedSeed.GetComponent<CollisionObject>().ReturnToPool();
            testCamera.transform.position = Center + Vector3.back * 10f;
            GameObject zoomSeed = SpawnSeed(rooti, Center + Vector3.right * 10f, Vector2.zero);
            testCamera.orthographicSize = 2f;
            yield return 0.05f;
            Require(zoomSeed.activeSelf, "카메라 확대에 따른 화면 범위 축소 후에도 씨앗 유지");
            zoomSeed.GetComponent<CollisionObject>().ReturnToPool();
            testCamera.orthographicSize = 20f;
            GameObject legacySeed = ObjectPoolManager.Instance.SpawnFromPool(data.projectilePrefab,
                Center + Vector3.right * 100f, Quaternion.identity);
            legacySeed.AddComponent<ProjCameraBoundsLimit>().Initialize(testCamera, 0.1f);
            legacySeed.AddComponent<ProjDistanceLimit>();
            typeof(RootiTurretController).GetMethod("ConfigureProjectile", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(rooti, new object[] { legacySeed });
            yield return 0.05f;
            Require(legacySeed.activeSelf && !legacySeed.GetComponent<ProjCameraBoundsLimit>().enabled &&
                !legacySeed.GetComponent<ProjDistanceLimit>().enabled,
                "이전에 생성된 풀의 거리·화면 이탈 반환 컴포넌트도 비활성화");
            legacySeed.GetComponent<CollisionObject>().ReturnToPool();
            Require(AssetDatabase.LoadAssetAtPath<TurretSkillData>("Assets/Nytherion/Data/ScriptableObjects/Skill/Turret_Skill.asset").coolDown == 5f,
                "런타임 강화가 공유 스킬 에셋 원본 수치를 수정하지 않음");
        }

        private static RelicData Relic(string name) => AssetDatabase.LoadAssetAtPath<RelicData>(RootiUpgradeRelicSetup.RelicRoot + name + ".asset");
        private static RootiTurretController[] Rootis() => Object.FindObjectsOfType<RootiTurretController>()
            .Where(rooti => Vector3.Distance(rooti.transform.position, Center) < 100f).ToArray();
        private static GameObject[] Seeds() => ObjectPoolManager.Instance.GetComponentsInChildren<CollisionObject>()
            .Where(seed => seed.poolTag == "RootiSeedProjectile" && seed.gameObject.activeInHierarchy).Select(seed => seed.gameObject).ToArray();
        private static GameObject Track(GameObject value) { objects.Add(value); return value; }
        private static RootiUpgradeTarget Target(string name, Vector3 position)
        {
            GameObject value = Track(new GameObject(name));
            value.tag = "Enemy";
            value.transform.position = position;
            value.AddComponent<CircleCollider2D>().radius = 0.15f;
            return value.AddComponent<RootiUpgradeTarget>();
        }
        private static GameObject SpawnSeed(RootiTurretController rooti, Vector3 position, Vector2 direction)
        {
            GameObject seed = ObjectPoolManager.Instance.SpawnFromPool(data.projectilePrefab, position, Quaternion.identity);
            seed.GetComponent<Rigidbody2D>().velocity = direction * data.projectileSpeed;
            typeof(RootiTurretController).GetMethod("ConfigureProjectile", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(rooti, new object[] { seed });
            return seed;
        }
        private static void Require(bool value, string label)
        {
            if (!value) throw new Exception(label);
            checks.Add("PASS " + label);
            Debug.Log("[RootiUpgradeVerification] PASS " + label);
        }
        private static void Finish(Exception error)
        {
            routine = null;
            if (error != null) checks.Add("FAIL " + error);
            Directory.CreateDirectory(Output);
            File.WriteAllLines(Output + "/verification.txt", checks.Concat(new[] { error == null ? "RESULT: PASS" : "RESULT: FAIL" }));
            foreach (GameObject value in objects) if (value != null) Object.Destroy(value);
            foreach (RootiTurretController rooti in Rootis()) Object.Destroy(rooti.gameObject);
            EditorApplication.Exit(error == null ? 0 : 1);
        }
    }

    public sealed class RootiUpgradeTarget : MonoBehaviour, IDamageable
    {
        public int Hits { get; private set; }
        public void ResetHits() => Hits = 0;
        public void TakeDamage(float damageAmount, bool isChain = false) => Hits++;
    }
}
