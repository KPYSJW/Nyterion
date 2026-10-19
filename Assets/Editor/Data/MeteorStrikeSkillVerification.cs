using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Nytherion.Core.Managers;
using Nytherion.Data.ScriptableObjects.Enemy;
using Nytherion.Data.ScriptableObjects.Relics;
using Nytherion.Data.ScriptableObjects.Skill;
using Nytherion.GamePlay.Characters.Enemy;
using Nytherion.GamePlay.Skills;
using Nytherion.Gameplay.Relics.Modules;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Nytherion.Editor
{
    /// <summary>실제 Physics2D, 저장 레벨 조회, 운석 애니메이션 이벤트를 플레이 모드에서 검증합니다.</summary>
    [InitializeOnLoad]
    public static class MeteorStrikeSkillVerification
    {
        private const string Pending = "Nytherion.MeteorStrike.Verify";
        private const string Output = MeteorStrikeSkillSetup.Output;
        private static readonly Vector3 Origin = new Vector3(1000f, 1000f);
        private static readonly List<Object> objects = new List<Object>();
        private static readonly List<string> checks = new List<string>();
        private static IEnumerator routine;
        private static float nextAt;
        private static double readyAt;
        private static Camera previousCamera;

        static MeteorStrikeSkillVerification()
        {
            EditorApplication.update += Update;
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Pending, false))
                    readyAt = EditorApplication.timeSinceStartup + 4d;
            };
        }

        [MenuItem("Tools/Nytherion/Meteor Strike/Verify In Play Mode")]
        public static void Start()
        {
            if (routine != null || SessionState.GetBool(Pending, false)) return;
            Directory.CreateDirectory(Output);
            checks.Clear();
            SessionState.SetBool(Pending, true);
            SessionState.SetBool(Pending + ".ExitPlay", !EditorApplication.isPlaying);
            if (EditorApplication.isPlaying) readyAt = EditorApplication.timeSinceStartup + 0.2d;
            else EditorApplication.isPlaying = true;
        }

        // 복사본에서만 배치 실행하여 사용자의 씬과 저장 파일을 보존합니다.
        public static void RunBatch()
        {
            MeteorStrikeSkillSetup.CreateAssets();
            SessionState.SetBool(Pending + ".Batch", true);
            EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity");
            Start();
        }

        private static void Update()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            try
            {
                if (routine == null && SessionState.GetBool(Pending, false) && EditorApplication.isPlaying &&
                    readyAt > 0d && EditorApplication.timeSinceStartup >= readyAt)
                {
                    SessionState.SetBool(Pending, false);
                    routine = Verify();
                    nextAt = Time.time;
                }
                if (routine == null || EditorApplication.isPaused || Time.time < nextAt) return;
                if (!routine.MoveNext()) { Finish(null); return; }
                nextAt = Time.time + (float)routine.Current;
            }
            catch (Exception error) { Finish(error); }
        }

        private static IEnumerator Verify()
        {
            MeteorStrikeSkillData source = AssetDatabase.LoadAssetAtPath<MeteorStrikeSkillData>(MeteorStrikeSkillSetup.DataPath);
            Require(source != null && source.skillPrefab != null && source.impactPrefab != null && source.icon != null,
                "스킬 데이터·실행 프리팹·운석 프리팹·아이콘 연결");
            Require(AssetDatabase.AssetPathToGUID(MeteorStrikeSkillSetup.DataPath) == "f490c741e6c38da4c8efef123f11daef" &&
                source.skillID == "skill_meteor_strike", "기존 GUID·저장 ID 보존");
            Require(AssetDatabase.LoadAssetAtPath<SkillDatabaseSO>("Assets/Nytherion/Data/ScriptableObjects/Skill/SkillDatabaseSO.asset")
                .GetSkillById(source.skillID) == source, "스킬 DB의 기존 참조 유지");
            RelicData relic = AssetDatabase.LoadAssetAtPath<RelicData>("Assets/Nytherion/Data/ScriptableObjects/Relics/SkillRelics/Meteorite.asset");
            Require(relic.effectModules.SelectMany(module => module.effects).OfType<GrantSkillEffect>().Any(effect => effect.skillData == source),
                "기존 운석 유물의 스킬 지급 참조 유지");
            Require(source.GetDamage(1) == 120f && source.GetDamage(2) == 140f && source.GetDamage(10) == 300f,
                "기본 피해 120·레벨당 +20·10레벨 300");

            previousCamera = Camera.main;
            if (previousCamera != null) previousCamera.tag = "Untagged";
            Camera camera = Track(new GameObject("MeteorVerificationCamera")).AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.orthographic = true;
            camera.orthographicSize = 10f;
            camera.aspect = 1.6f;
            camera.transform.position = Origin + Vector3.back * 10f;

            MeteorStrikeSkillData data = Track(Object.Instantiate(source));
            data.coolDown = 0f;
            GameObject caster = Track(new GameObject("MeteorVerificationCaster"));
            caster.transform.position = Origin;
            SkillDataManager states = caster.AddComponent<SkillDataManager>();
            states.skillStates[data.skillID] = new SkillState { level = 3 };
            PlayerManager player = caster.AddComponent<PlayerManager>();
            player.Construct(null, null, null, null, states);
            MeteorStrikeSkill skill = Object.Instantiate(source.skillPrefab, caster.transform).GetComponent<MeteorStrikeSkill>();
            skill.skillData = data;
            skill.caster = caster.transform;
            Require(!skill.TryUse() && skill.GetRemainingCooldown() == 0f, "빈 화면에서는 시전·쿨다운을 소비하지 않음");

            EnemyData enemyData = Track(ScriptableObject.CreateInstance<EnemyData>());
            enemyData.maxHealth = 10000f;
            EnemyBase first = CreateEnemy(enemyData, new Vector2(-3f, 0f));
            EnemyBase second = CreateEnemy(enemyData, new Vector2(0f, 0f));
            EnemyBase third = CreateEnemy(enemyData, new Vector2(3f, 0f));
            EnemyBase outside = CreateEnemy(enemyData, new Vector2(0f, 12f));
            GameObject extraCollider = new GameObject("추가 피격 콜라이더");
            extraCollider.transform.SetParent(first.transform, false);
            extraCollider.layer = first.gameObject.layer;
            extraCollider.AddComponent<BoxCollider2D>().isTrigger = true;
            Physics2D.SyncTransforms();
            float castAt = Time.time;
            Require(skill.TryUse(), "화면 안의 적 세 명에게 시전 성공");
            Require(Impacts(skill).Count == 1, "첫 운석만 즉시 생성");
            yield return data.strikeInterval * 0.5f;
            Require(Impacts(skill).Count == 1, "설정한 시간차 전에는 다음 운석이 생성되지 않음");
            first.transform.position += Vector3.right * 2f;
            second.transform.position += Vector3.up * 2f;
            yield return data.strikeInterval * 3f + 1.2f;
            Require(Impacts(skill).Count == 3, "자식 콜라이더 중복 없이 적마다 운석 하나 배정");
            Require(new[] { first, second, third }.All(enemy => Mathf.Approximately(Health(enemy), 9840f)),
                "이동한 적 포함 각 1회 타격·저장 레벨 3의 피해 160 적용");
            Require(Health(outside) == 10000f, "카메라 바깥 적은 공격 제외");
            Require(Impacts(skill).All(impact => !impact.gameObject.activeSelf), "실제 애니메이션 이벤트로 운석 반환");
            yield return 0.02f;
            Require(skill.TryUse(), "재시전 성공");
            yield return 1.6f;
            Require(Impacts(skill).Count == 3 && Health(first) == 9680f, "재시전 시 로컬 운석 풀 재사용·피해 상태 초기화");

            first.gameObject.SetActive(false);
            second.gameObject.SetActive(false);
            third.gameObject.SetActive(false);
            List<EnemyBase> crowd = new List<EnemyBase>();
            for (int i = 0; i < 70; i++) crowd.Add(CreateEnemy(enemyData, new Vector2((i % 10) - 4.5f, (i / 10) - 3f)));
            data.strikeInterval = 0.02f;
            Physics2D.SyncTransforms();
            yield return 0.02f;
            Require(skill.TryUse(), "화면 안 70명 시전");
            yield return 4f;
            Require(crowd.All(enemy => Health(enemy) == 9840f), "고정 버퍼 상한 없이 70명 전원 각 1회 타격");
            Require(Health(outside) == 10000f, "다수 조준에서도 화면 바깥 적 제외");
            Require(skill.TryUse(), "도중 해제 검증 시전");
            skill.gameObject.SetActive(false);
            int impactCount = Impacts(skill).Count;
            yield return 0.2f;
            Require(Impacts(skill).Count == impactCount && Impacts(skill).All(impact => !impact.gameObject.activeSelf),
                "스킬 해제 시 대기 중 순차 시전과 진행 중 운석 정리");
            Require(Time.time > castAt, "GameScene 플레이 모드의 실제 시간·물리·애니메이션 경로 완료");
        }

        private static EnemyBase CreateEnemy(EnemyData data, Vector2 offset)
        {
            GameObject root = Track(new GameObject("MeteorVerificationEnemy"));
            root.layer = LayerMask.NameToLayer("Enemy");
            root.tag = "Enemy";
            root.transform.position = Origin + (Vector3)offset;
            root.AddComponent<BoxCollider2D>().size = Vector2.one * 0.5f;
            EnemyBase enemy = root.AddComponent<EnemyBase>();
            enemy.Initialize(data);
            return enemy;
        }

        private static float Health(EnemyBase enemy) => (float)typeof(EnemyBase)
            .GetField("currentHealth", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(enemy);
        private static List<MeteorStrikeImpact> Impacts(MeteorStrikeSkill skill) => (List<MeteorStrikeImpact>)typeof(MeteorStrikeSkill)
            .GetField("impacts", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(skill);
        private static T Track<T>(T obj) where T : Object { objects.Add(obj); return obj; }
        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
            checks.Add("PASS " + message);
        }

        private static void Finish(Exception error)
        {
            routine = null;
            SessionState.SetBool(Pending, false);
            foreach (Object obj in objects)
                if (obj != null) Object.Destroy(obj);
            objects.Clear();
            if (previousCamera != null) previousCamera.tag = "MainCamera";
            previousCamera = null;
            if (error != null) checks.Add("FAIL " + error);
            Directory.CreateDirectory(Output);
            File.WriteAllLines(Output + "/verification.txt", checks);
            if (SessionState.GetBool(Pending + ".ExitPlay", false)) EditorApplication.isPlaying = false;
            if (SessionState.GetBool(Pending + ".Batch", false)) EditorApplication.Exit(error == null ? 0 : 1);
        }
    }
}
