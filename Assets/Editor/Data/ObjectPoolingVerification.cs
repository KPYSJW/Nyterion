using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Nytherion.Core.Enums;
using Nytherion.Core.Managers;
using Nytherion.Data.ScriptableObjects.Relics;
using Nytherion.Data.ScriptableObjects.Skill;
using Nytherion.GamePlay.Characters.Companions;
using Nytherion.GamePlay.Combat.Weapons;
using Nytherion.GamePlay.Relics;
using Nytherion.GamePlay.Skills;
using Nytherion.UI.RelicBoard;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Nytherion.Editor
{
    /// <summary>실제 프리팹을 반복 재사용해 UI, 이펙트, 로봇과 포탑의 반환 및 상태 초기화를 검증합니다.</summary>
    [InitializeOnLoad]
    public static class ObjectPoolingVerification
    {
        private const string Pending = "Nytherion.ObjectPoolingVerification.Pending";
        private const string Output = "output/object-pooling/verification.txt";
        private static readonly Vector3 Center = new Vector3(1000f, 1000f);
        private static readonly List<string> checks = new List<string>();
        private static readonly List<string> errors = new List<string>();
        private static IEnumerator routine;
        private static float nextAt;
        private static ObjectPoolManager pool;

        static ObjectPoolingVerification()
        {
            EditorApplication.update += Update;
        }

        // 사용자 씬과 세이브를 보호하기 위해 별도의 프로젝트 복사본에서만 배치 실행합니다.
        public static void RunBatch()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("별도 배치 검증 프로젝트에서 실행하세요.");
            PlayerSettings.productName += "_ObjectPoolingVerification";
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SessionState.SetBool(Pending, true);
            EditorApplication.isPlaying = true;
        }

        private static void Update()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || !EditorApplication.isPlaying) return;
            try
            {
                if (SessionState.GetBool(Pending, false))
                {
                    SessionState.SetBool(Pending, false);
                    Application.logMessageReceived += CaptureError;
                    routine = Verify();
                }
                if (routine == null || Time.time < nextAt) return;
                if (!routine.MoveNext()) { Finish(null); return; }
                nextAt = Time.time + (routine.Current is float delay ? delay : 0f);
            }
            catch (Exception error) { Finish(error); }
        }

        private static IEnumerator Verify()
        {
            pool = new GameObject("검증용 오브젝트 풀").AddComponent<ObjectPoolManager>();
            pool.pools = new List<ObjectPoolManager.Pool>();
            pool.Initialize();
            VerifyPreview();

            GameObject droppedPrefab = Prefab("Assets/Prefabs/UI/Gacha/DroppedRelic.prefab");
            DroppedRelic dropped = Object.Instantiate(droppedPrefab, Center, Quaternion.identity).GetComponent<DroppedRelic>();
            GameObject effectPrefab = Field<GameObject>(dropped, "dropRelicEffectPrefab");
            Require(effectPrefab != null && effectPrefab.GetComponent<DropRelicVFXAnimationEvent>() != null,
                "유물 착지 이펙트 Inspector 참조 및 애니메이션 이벤트 연결");
            dropped.SetPool(pool, droppedPrefab.name);
            RelicData relic = ScriptableObject.CreateInstance<RelicData>();
            Set(dropped, "relicData", relic);
            for (int i = 0; i < 7; i++)
            {
                relic.rarity = i % 2 == 0 ? Rarity.Legendary : Rarity.Common;
                Invoke(dropped, "PlayDropRelicEffect");
                DropRelicVFXAnimationEvent effect = pool.GetComponentsInChildren<DropRelicVFXAnimationEvent>()
                    .Single(value => value.gameObject.activeInHierarchy);
                Color expected = relic.rarity == Rarity.Legendary
                    ? new Color(1f, 0.34f, 0.03f) : new Color(0.86f, 0.86f, 0.86f);
                Require(effect.GetComponent<SpriteRenderer>().color == expected, "착지 이펙트 등급 색상 초기화 " + i);
                yield return 0.4f;
                Require(effect != null && !effect.gameObject.activeSelf &&
                    pool.poolDictionary[effectPrefab.name].Count == 3, "착지 애니메이션 종료 후 파괴 없이 반환 " + i);
                effect.AnimationFinished();
                Require(pool.poolDictionary[effectPrefab.name].Count == 3, "착지 이펙트 중복 반환 방지 " + i);
            }
            Object.Destroy(dropped.gameObject);
            Object.Destroy(relic);

            GameObject meteorPrefab = Prefab("Assets/Prefabs/Gameplay/Combat/Proj/MeteorProj.prefab");
            HashSet<int> indicatorIds = new HashSet<int>();
            for (int i = 0; i < 8; i++)
            {
                MeteorProj meteor = pool.SpawnFromPool(meteorPrefab, Center + Vector3.up * 5f, Quaternion.identity, 1)
                    .GetComponent<MeteorProj>();
                meteor.Initialize(Center + Vector3.right * i, pool);
                GameObject indicator = Field<GameObject>(meteor, "activeIndicator");
                Require(indicator != null && indicator.activeSelf &&
                    indicator.transform.position == Center + Vector3.right * i, "메테오 표시 위치 초기화 " + i);
                indicatorIds.Add(indicator.GetInstanceID());
                Require(Mathf.Approximately(indicator.transform.localScale.x, meteor.explosionRadius * 2f),
                    "메테오 표시 크기 초기화 " + i);
                // 새 낙하를 초기화할 때 이전 표시가 중복으로 남지 않아야 합니다.
                meteor.Initialize(Center, pool);
                meteor.DisableProjectile();
                meteor.DisableProjectile();
                Require(pool.poolDictionary[meteor.indicatorPrefab.name].Count == 3 &&
                    pool.poolDictionary[meteorPrefab.name].Count == 1, "메테오 및 표시 반환·중복 반환 방지 " + i);
            }
            Require(indicatorIds.Count <= 3, "메테오 표시 반복 사용 시 인스턴스 수 유지");

            BoomMaker boom = Object.Instantiate(Prefab(
                "Assets/Prefabs/Gameplay/Characters/Companions/RoboPilot/BoomMaker.prefab"), Center, Quaternion.identity)
                .GetComponent<BoomMaker>();
            GameObject robotPrefab = Field<GameObject>(boom, "explosiveRobotPrefab");
            HashSet<int> robotIds = new HashSet<int>();
            for (int i = 0; i < 8; i++)
            {
                Set(boom, "isSummonPending", true);
                boom.DeployExplosiveRobot();
                List<ExplosiveRobot> active = Field<List<ExplosiveRobot>>(boom, "activeRobots");
                Require(active.Count == 1, "폭발 로봇 반환 후 소환 가능 " + i);
                ExplosiveRobot robot = active[0];
                robotIds.Add(robot.GetInstanceID());
                Require(robot.GetComponent<SpriteRenderer>().enabled && robot.GetComponent<CircleCollider2D>().enabled &&
                    robot.GetComponent<Rigidbody2D>().velocity == Vector2.zero, "로봇 렌더러·콜라이더·속도 복구 " + i);
                Invoke(robot, "Explode");
                robot.ReturnToPool();
                Require(robot != null && !robot.gameObject.activeSelf && active.Count == 0 &&
                    pool.poolDictionary[robotPrefab.name].Count == 3, "로봇 폭발·활성 목록 해제·중복 반환 방지 " + i);
                yield return 0.1f;
            }
            Require(robotIds.Count <= 3, "폭발 로봇 반복 사용 시 인스턴스 수 유지");
            Set(boom, "isSummonPending", true);
            boom.DeployExplosiveRobot();
            ExplosiveRobot remainingRobot = Field<List<ExplosiveRobot>>(boom, "activeRobots")[0];
            boom.gameObject.SetActive(false);
            Require(!remainingRobot.gameObject.activeSelf &&
                Field<List<ExplosiveRobot>>(boom, "activeRobots").Count == 0, "소환수 장착 해제 시 남은 로봇 회수");

            TurretSkillData data = Object.Instantiate(AssetDatabase.LoadAssetAtPath<TurretSkillData>(
                "Assets/Nytherion/Data/ScriptableObjects/Skill/Turret_Skill.asset"));
            Require(data != null && data.turretPrefab.GetComponent<RootiTurretController>() != null,
                "실제 루티 스킬·포탑 Inspector 참조");
            data.maxTurretCount = 1;
            data.duration = 30f;
            TurretSkill skill = new GameObject("검증용 포탑 스킬").AddComponent<TurretSkill>();
            skill.transform.position = Center;
            skill.caster = skill.transform;
            skill.skillData = data;
            HashSet<int> turretIds = new HashSet<int>();
            for (int i = 0; i < 8; i++)
            {
                Invoke(skill, "Activate");
                RootiTurretController turret = pool.GetComponentsInChildren<RootiTurretController>()
                    .Single(value => value.gameObject.activeInHierarchy);
                turretIds.Add(turret.GetInstanceID());
                yield return 0.05f;
                // 비행 중 반환해도 다음 배치에서 자식 위치와 애니메이션이 초기화되어야 합니다.
                Animator animator = turret.GetComponentInChildren<Animator>();
                animator.speed = 3f;
                Set(turret, "pendingTarget", skill.transform);
                Set(turret, "isSeedLaunchPending", true);
                Invoke(skill, "Activate");
                Require(!turret.gameObject.activeSelf, "최대 포탑 수 초과 시 가장 오래된 포탑 반환 " + i);
                RootiTurretController next = pool.GetComponentsInChildren<RootiTurretController>()
                    .Single(value => value.gameObject.activeInHierarchy);
                Require(Mathf.Approximately(next.GetComponentInChildren<Animator>().speed, 1f) &&
                    Field<Transform>(next, "pendingTarget") == null && !Field<bool>(next, "isSeedLaunchPending"),
                    "재사용 루티의 배치 애니메이션·공격 상태 초기화 " + i);
                next.ReturnToPool();
                next.ReturnToPool();
                Require(Field<List<TurretController>>(null, "activeTurrets", typeof(TurretController)).Count == 0 &&
                    pool.poolDictionary[data.turretPrefab.name].Count == 3, "루티 활성 목록 해제·중복 반환 방지 " + i);
            }
            Require(turretIds.Count <= 3, "루티 반복 사용 시 인스턴스 수 유지");
            GameObject plainPrefab = Prefab("Assets/Prefabs/Gameplay/Skills/Turret.prefab");
            TurretController plain = pool.SpawnFromPool(plainPrefab, Center, Quaternion.identity, 1).GetComponent<TurretController>();
            data.duration = 0.02f;
            plain.SetPool(pool, plainPrefab.name);
            plain.Initialize(data);
            yield return 0.15f;
            Require(plain != null && !plain.gameObject.activeSelf && pool.poolDictionary[plainPrefab.name].Count == 1,
                "기본 포탑 수명 종료 시 파괴 없이 반환");
            yield return 0.5f;
        }

        private static void VerifyPreview()
        {
            GameObject canvasObject = new GameObject("검증용 유물 보드", typeof(RectTransform), typeof(Canvas));
            RelicGridUI grid = canvasObject.AddComponent<RelicGridUI>();
            grid.rootCanvas = canvasObject.GetComponent<Canvas>();
            grid.previewContainer = new GameObject("Preview", typeof(RectTransform)).GetComponent<RectTransform>();
            grid.previewContainer.SetParent(canvasObject.transform, false);
            RelicSlotCell[,] cells = new RelicSlotCell[3, 3];
            for (int y = 0; y < 3; y++)
                for (int x = 0; x < 3; x++)
                {
                    GameObject cellObject = new GameObject("Cell", typeof(RectTransform), typeof(Image), typeof(RelicSlotCell));
                    cellObject.transform.SetParent(canvasObject.transform, false);
                    cellObject.GetComponent<RectTransform>().anchoredPosition = new Vector2(x * 100f, y * 100f);
                    cells[y, x] = cellObject.GetComponent<RelicSlotCell>();
                    Set(cells[y, x], "backgroundImage", cellObject.GetComponent<Image>());
                }
            Set(grid, "rows", 3);
            Set(grid, "columns", 3);
            Set(grid, "slotCells", cells);
            Set(grid, "levelUpGizmoPrefab", Prefab("Assets/Prefabs/UI/Relic/LevelUpGizmo.prefab"));
            Set(grid, "levelDownGizmoPrefab", Prefab("Assets/Prefabs/UI/Relic/LevelDownGizmo.prefab"));
            RelicData data = ScriptableObject.CreateInstance<RelicData>();
            data.influenceZones = new List<InfluenceZone>
            {
                new InfluenceZone { offset = Vector2Int.right, type = InfluenceType.LevelUp, levelAmount = 2 },
                new InfluenceZone { offset = Vector2Int.left, type = InfluenceType.LevelDown, levelAmount = 3 },
                new InfluenceZone { offset = Vector2Int.up, type = InfluenceType.Silence },
                new InfluenceZone { offset = Vector2Int.down, type = InfluenceType.SynergyLink }
            };
            RelicBlock block = new RelicBlock(data);
            grid.ShowPlacementPreview(block, Vector2Int.one);
            for (int i = 0; i < 4; i++) { block.Rotate(); grid.ShowPlacementPreview(block, Vector2Int.one); }
            int childCount = grid.previewContainer.GetComponentsInChildren<Transform>(true).Length;
            for (int i = 0; i < 40; i++)
            {
                block.Rotate();
                grid.ShowPlacementPreview(block, i % 2 == 0 ? Vector2Int.zero : Vector2Int.one);
            }
            grid.ShowPlacementPreview(block, Vector2Int.one);
            Require(grid.previewContainer.childCount == 4 &&
                grid.previewContainer.GetComponentsInChildren<Transform>(true).Length == childCount,
                "유물 미리보기 이동·회전 반복 시 기즈모와 숫자 라벨 재사용");
            Require(grid.previewContainer.GetComponentsInChildren<TextMeshProUGUI>()
                .Select(label => label.text).OrderBy(value => value, StringComparer.Ordinal).SequenceEqual(new[] { "+2", "-3" }),
                "유물 미리보기 영향 수치·침묵·시너지 전환 시 라벨 초기화");
            Color downColor = Field<GameObject>(grid, "levelDownGizmoPrefab").GetComponent<Graphic>().color;
            Color expectedDown = new Color(downColor.r, downColor.g, downColor.b, 0.5f);
            Require(grid.previewContainer.GetComponentsInChildren<Graphic>()
                .Any(graphic => graphic.color == expectedDown), "침묵 대체 기즈모의 색상이 레벨 다운에 남지 않음");
            grid.ClearPreview();
            Require(grid.previewContainer.Cast<Transform>().All(child => !child.gameObject.activeSelf),
                "유물 미리보기 종료 시 삭제 없이 비활성화");
            grid.ShowPlacementPreview(block, Vector2Int.one);
            grid.enabled = false;
            Require(grid.previewContainer.Cast<Transform>().All(child => !child.gameObject.activeSelf),
                "유물 보드 비활성화 시 미리보기 회수");
            Object.Destroy(canvasObject);
            Object.Destroy(data);
        }

        private static GameObject Prefab(string path)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) throw new InvalidOperationException("프리팹이 없습니다: " + path);
            return prefab;
        }

        private static T Field<T>(object target, string name, Type type = null)
        {
            return (T)(type ?? target.GetType()).GetField(name,
                BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic).GetValue(target);
        }

        private static void Set(object target, string name, object value)
        {
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        }

        private static void Invoke(object target, string name)
        {
            target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
        }

        private static void Require(bool condition, string label)
        {
            if (!condition) throw new InvalidOperationException(label);
            checks.Add("PASS " + label);
        }

        private static void CaptureError(string condition, string trace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                errors.Add(condition + "\n" + trace);
        }

        private static void Finish(Exception error)
        {
            routine = null;
            Application.logMessageReceived -= CaptureError;
            if (error != null) errors.Add(error.ToString());
            Directory.CreateDirectory(Path.GetDirectoryName(Output));
            File.WriteAllText(Output, string.Join("\n", checks) + "\n" +
                (errors.Count == 0 ? "PASS 모든 재사용 검증 통과" : "FAIL\n" + string.Join("\n", errors)));
            EditorApplication.Exit(errors.Count == 0 ? 0 : 1);
        }
    }
}
