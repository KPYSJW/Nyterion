using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Nytherion.Core.Interfaces;
using Nytherion.Data.ScriptableObjects.Gacha;
using Nytherion.Data.ScriptableObjects.Skill;
using Nytherion.GamePlay.Characters.Enemy;
using Nytherion.GamePlay.Skills;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Nytherion.Editor
{
    /// <summary>실제 프리팹과 플레이 모드의 Physics2D로 추적, 충돌, 폭발 및 재사용을 검증합니다.</summary>
    [InitializeOnLoad]
    public static class PyroTankSkillVerification
    {
        private const string Output = "output/pyro-tank";
        private const string Pending = "PyroTankVerification.Pending";
        private static readonly Vector3 Center = new Vector3(1000f, 1000f, 0f);
        private static readonly List<GameObject> objects = new List<GameObject>();
        private static readonly List<string> checks = new List<string>();
        private static PyroTankSkillData data;
        private static PyroTankController tank;
        private static PyroTankSkill skill;
        private static IEnumerator routine;
        private static double readyAt;
        private static float nextAt, previousTimeScale;
        private static bool exitAfter;

        static PyroTankSkillVerification()
        {
            EditorApplication.update += Update;
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Pending, false))
                    readyAt = EditorApplication.timeSinceStartup + 2d;
                if (state == PlayModeStateChange.ExitingPlayMode && routine != null) Finish(new Exception("플레이 모드 검증이 중단되었습니다."));
            };
        }

        [MenuItem("Tools/Nytherion/Pyro Tank/Verify In Play Mode")]
        public static void Start()
        {
            if (routine != null || SessionState.GetBool(Pending, false)) return;
            if (!EditorApplication.isPlaying && AssetDatabase.LoadAssetAtPath<PyroTankSkillData>(PyroTankSkillSetup.DataPath) == null)
                PyroTankSkillSetup.CreateAssets();
            if (!EditorApplication.isPlaying) PyroTankSkillSetup.UpdateTankCollider();
            SessionState.SetBool(Pending + ".ExitAfter", !EditorApplication.isPlaying);
            SessionState.SetBool(Pending, true);
            if (EditorApplication.isPlaying) readyAt = EditorApplication.timeSinceStartup + 0.1d;
            else EditorApplication.isPlaying = true;
        }

        // 별도 프로젝트 복사본에서 실행해 사용자 씬과 저장 파일을 건드리지 않습니다.
        public static void RunBatch()
        {
            PyroTankSkillSetup.UpdateJumpAssets();
            PyroTankSkillSetup.UpdateTankCollider();
            EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity");
            SessionState.SetBool(Pending + ".Batch", true);
            Start();
        }

        private static void Update()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            try
            {
                if (File.Exists(Output + "/verify.request") && !EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    File.Delete(Output + "/verify.request");
                    Start();
                }
                if (SessionState.GetBool(Pending, false) && EditorApplication.isPlaying && readyAt > 0d &&
                    EditorApplication.timeSinceStartup >= readyAt)
                {
                    SessionState.SetBool(Pending, false);
                    exitAfter = SessionState.GetBool(Pending + ".ExitAfter", false);
                    previousTimeScale = Time.timeScale;
                    Time.timeScale = 1f;
                    checks.Clear();
                    data = Object.Instantiate(AssetDatabase.LoadAssetAtPath<PyroTankSkillData>(PyroTankSkillSetup.DataPath));
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
            Require(data != null && data.tankPrefab != null && data.skillPrefab != null && data.icon != null &&
                    data.explosionAnimation != null && data.jumpAnimation != null, "스킬 데이터·전차·시전 프리팹·아이콘·점프·폭발 클립 참조");
            Require(data.tankPrefab.GetComponent<CircleCollider2D>().offset == Vector2.zero,
                "전차 Sprite 피벗과 충돌체 중심 일치");
            Require(Frames("PyroTank_Idle") == 4 && Frames("PyroTank_Walk") == 2 && Frames("PyroTankExplosionEffect") == 8,
                "첨부 원본 Idle 4 / Walk 2 / 폭발 8프레임 슬라이스");
            Require(Frames("PyroTank_Jump") == 4 && data.tankPrefab.transform.Find("JumpVisual") != null,
                "Jump 4프레임 클립·전용 그림·Animator 연결");
            Require(Mathf.Abs(data.jumpAnimation.length - 4f / data.jumpAnimation.frameRate) < 0.001f,
                "Jump 클립은 같은 길이의 4프레임이며 착지 프레임 중복 없음");
            SkillDatabaseSO database = AssetDatabase.LoadAssetAtPath<SkillDatabaseSO>(
                "Assets/Nytherion/Data/ScriptableObjects/Skill/SkillDatabaseSO.asset");
            PyroTankSkillData source = AssetDatabase.LoadAssetAtPath<PyroTankSkillData>(PyroTankSkillSetup.DataPath);
            Require(database.GetSkillById("skill_pyro_tank") == source, "스킬 데이터베이스 등록과 기존 저장 ID 경로 연결");
            GachaPoolSO pool = AssetDatabase.LoadAssetAtPath<GachaPoolSO>(
                "Assets/Nytherion/Data/ScriptableObjects/Gacha/GachaPool/Skill/SkillGachaPool.asset");
            Require(pool.items.Any(entry => entry.item == source), "스킬 가챠 획득 경로 등록");

            ResetCase();
            tank = Track(Object.Instantiate(data.tankPrefab.gameObject)).GetComponent<PyroTankController>();
            Require(!tank.gameObject.activeSelf, "비활성 전차 프리팹의 최초 시전 조건");
            Vector3 prefabScale = data.tankPrefab.transform.localScale;
            tank.Begin(data, Center, Vector2.right, 2f);
            Require(tank.transform.localScale == prefabScale * 2f,
                "Awake 이전 최초 시전에서도 원본 크기와 효과 크기 배율 적용");
            tank.gameObject.SetActive(false);
            tank.Begin(data, Center, Vector2.right, 0.5f);
            Require(tank.transform.localScale == prefabScale * 0.5f,
                "전차 재사용 시 최초 원본 크기 유지·이전 배율 누적 없음");
            ResetCase();
            data.lifetime = 10f;
            data.moveSpeed = 3f;
            GameObject caster = Track(new GameObject("PyroTankVerificationCaster"));
            caster.transform.position = Center;
            GameObject firePoint = Track(new GameObject("FirePointAwayFromCaster"));
            firePoint.transform.position = Center + Vector3.right * 20f;
            skill = Track(Object.Instantiate(data.skillPrefab)).GetComponent<PyroTankSkill>();
            skill.skillData = data;
            skill.caster = caster.transform;
            skill.firePoint = firePoint.transform;
            data.coolDown = 0f;
            PyroTankVerificationTarget a = Target("표적 A", Center + Vector3.right * 4f);
            Target("먼 적", Center + Vector3.up * 6f);
            Physics2D.SyncTransforms();
            Vector2 castAim = (Vector2)typeof(PyroTankSkill).GetMethod("GetAimDirection", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(skill, new object[] { Center });
            Require(skill.TryUse(), "기존 SkillBase 사용 경로로 시전");
            tank = GetSkillTank(0);
            Vector3 jumpOrigin = tank.transform.position;
            Require(Vector3.Distance(jumpOrigin, Center + (Vector3)castAim * data.spawnForwardOffset) < 0.001f,
                "플레이어의 현재 조준 방향 앞쪽에서 생성·발사점 위치 제외");
            Require(Jumping() && GetTarget() == null && tank.transform.Find("JumpVisual").gameObject.activeSelf,
                "사용 즉시 점프 애니메이션 시작·착지 전 추적 보류");
            Capture("jump-start.png");
            yield return data.jumpDuration * 0.375f;
            Require(Jumping() && Vector2.Dot((Vector2)(tank.transform.position - jumpOrigin), castAim) > data.jumpDistance * 0.2f &&
                    tank.transform.Find("JumpVisual").localPosition.y > 0f && GetTarget() == null,
                "조준 방향으로 전진하며 최고점에서 점프 그림 표시·물리 중심과 그림 분리");
            Capture("jump-apex.png");
            yield return data.jumpDuration * 0.625f + 0.12f;
            Require(!Jumping() && !tank.transform.Find("JumpVisual").gameObject.activeSelf &&
                    tank.transform.Find("JumpVisual").localPosition == Vector3.zero && tank.GetComponent<SpriteRenderer>().enabled,
                "착지 시 점프 그림 종료·원래 전차 그림 복구");
            Require(GetTarget() == a.transform && tank.GetComponent<Rigidbody2D>().velocity.x > 0f &&
                    tank.GetComponent<Animator>().GetCurrentAnimatorStateInfo(0).IsName("Walk"), "범위 내 가장 가까운 적 선정·돌진·Walk 재생");
            Capture("walk.png");
            caster.transform.position += Vector3.left * 10f;
            PyroTankVerificationTarget b = Target("새로 가까워진 적 B", tank.transform.position + Vector3.up * 2f);
            Physics2D.SyncTransforms();
            yield return 0.1f;
            Require(GetTarget() == a.transform && tank.transform.position.x > Center.x,
                "더 가까운 적이 나타나도 기존 표적 유지·시전자 이동과 독립");
            Object.Destroy(a.gameObject);
            yield return 0.08f;
            Require(GetTarget() == b.transform && tank.GetComponent<Rigidbody2D>().velocity.y > 0f,
                "돌진 중 표적 파괴 시 현재 위치에서 가장 가까운 적으로 재탐색");
            b.gameObject.SetActive(false);
            foreach (PyroTankVerificationTarget other in objects.Where(obj => obj != null).Select(obj => obj.GetComponent<PyroTankVerificationTarget>()).Where(value => value != null))
                other.gameObject.SetActive(false);
            yield return 0.08f;
            Vector3 stopped = tank.transform.position;
            Require(GetTarget() == null && tank.GetComponent<Rigidbody2D>().velocity == Vector2.zero,
                "표적 비활성화·범위 내 적 부재 시 즉시 정지");
            yield return 0.12f;
            Require(Vector3.Distance(tank.transform.position, stopped) < 0.01f &&
                    tank.GetComponent<Animator>().GetCurrentAnimatorStateInfo(0).IsName("Idle"), "적이 없는 동안 제자리 유지·Idle 재생");
            Capture("idle.png");
            PyroTankVerificationTarget c = Target("다시 들어온 적", stopped + Vector3.left * 3f);
            Physics2D.SyncTransforms();
            yield return 0.08f;
            Require(GetTarget() == c.transform && tank.GetComponent<Rigidbody2D>().velocity.x < 0f &&
                    tank.GetComponent<SpriteRenderer>().flipX == !data.invertFacing, "정지 중 새 적 탐색·돌진 재개·좌측 방향 표시");
            c.transform.position = stopped + Vector3.left * 20f;
            Physics2D.SyncTransforms();
            yield return 0.08f;
            Require(GetTarget() == null && tank.GetComponent<Rigidbody2D>().velocity == Vector2.zero, "표적이 탐색 범위를 벗어나면 정지");
            Require(skill.TryUse() && GetSkillTanks().Count == 2 && GetSkillTank(0).gameObject.activeSelf,
                "중첩 시전은 별도 전차를 사용하고 기존 전차 유지");
            skill.enabled = false;
            Require(GetSkillTanks().All(value => !value.gameObject.activeSelf), "스킬 비활성화 시 모든 전차와 연출 정리");

            ResetCase();
            SpawnTank(new Vector2(-1f, 1f).normalized);
            Vector2 diagonalDirection = new Vector2(-1f, 1f).normalized;
            Require(Jumping() && tank.transform.Find("JumpVisual").GetComponent<SpriteRenderer>().flipX == !data.invertFacing,
                "왼쪽 조준 시 점프 그림도 이미지의 앞부분이 진행 방향을 향함");
            yield return data.jumpDuration + 0.12f;
            Require(!Jumping() && Vector2.Distance(tank.GetComponent<Rigidbody2D>().position,
                    (Vector2)Center + diagonalDirection * data.jumpDistance) < 0.03f && GetTarget() == null,
                "대각선 조준 방향으로 지정 거리만큼 점프·적이 없으면 착지 지점에서 Idle");
            Capture("jump-landed.png");
            tank.Begin(data, Center, Vector2.right);
            yield return data.jumpDuration * 0.4f;
            tank.gameObject.SetActive(false);
            Require(!Jumping() && tank.transform.Find("JumpVisual").localPosition == Vector3.zero,
                "점프 중 비활성화 시 높이·이동·애니메이션 상태 초기화");
            tank.Begin(data, Center, Vector2.right);
            yield return data.jumpDuration + 0.1f;
            Require(!Jumping() && Vector2.Distance(tank.GetComponent<Rigidbody2D>().position,
                    (Vector2)Center + Vector2.right * data.jumpDistance) < 0.03f,
                "재사용한 전차도 처음부터 점프하며 이전 착지 위치와 높이를 재사용하지 않음");

            float originalJumpDuration = data.jumpDuration;
            foreach (float duration in new[] { originalJumpDuration, originalJumpDuration * 2f })
            {
                IEnumerator phases = VerifyJumpPhases(duration, duration == originalJumpDuration);
                while (phases.MoveNext()) yield return (float)phases.Current;
            }
            data.jumpDuration = originalJumpDuration;

            ResetCase();
            GameObject jumpWall = Track(new GameObject("점프 경로의 벽"));
            jumpWall.layer = LayerMask.NameToLayer("Wall");
            jumpWall.tag = "Wall";
            jumpWall.transform.position = Center + Vector3.right * 0.85f;
            jumpWall.AddComponent<BoxCollider2D>().size = new Vector2(0.03f, 4f);
            SpawnTank(Vector2.right);
            yield return data.jumpDuration + 0.05f;
            Require(Exploded() && tank.transform.position.x < jumpWall.transform.position.x &&
                    !Jumping() && !tank.transform.Find("JumpVisual").gameObject.activeSelf,
                "점프 이동 중에도 벽 관통 방지·충돌 폭발·점프 그림 정리");

            ResetCase();
            data.lifetime = 0.1f;
            SpawnTank(Vector2.right);
            yield return 0.18f;
            Require(Exploded() && !Jumping() && !tank.transform.Find("JumpVisual").gameObject.activeSelf,
                "점프 중 수명이 만료돼도 한 번 폭발하고 점프 연출 정리");

            ResetCase();
            data.lifetime = 10f;
            data.moveSpeed = 6f;
            PyroTankVerificationTarget assigned = Target("지정 표적", Center + Vector3.right * 5f);
            SpawnTank();
            yield return 0.08f;
            Require(GetTarget() == assigned.transform, "다른 적 충돌 검증의 최초 표적 확정");
            PyroTankVerificationTarget blocker = Target("경로를 가로막은 적", tank.transform.position + Vector3.right * 0.7f);
            blocker.gameObject.AddComponent<BoxCollider2D>().size = Vector2.one * 0.2f;
            PyroTankVerificationTarget nearby = Target("폭발 범위 안 적", blocker.transform.position + Vector3.up * 0.8f);
            PyroTankVerificationTarget outside = Target("폭발 범위 밖 적", blocker.transform.position + Vector3.up * 3f);
            PyroTankVerificationTarget friendly = Target("아군", blocker.transform.position + Vector3.down * 0.6f, false);
            Physics2D.SyncTransforms();
            yield return 0.13f;
            Require(Exploded() && blocker.Hits == 1 && nearby.Hits == 1 && outside.Hits == 0 && friendly.Hits == 0 && assigned.Hits == 0,
                $"지정 표적 외 다른 적과 충돌 즉시 폭발·적에게만 범위 피해·복수 Collider 중복 피해 방지 " +
                $"(폭발={Exploded()}, 전차={tank.GetComponent<Rigidbody2D>().position}, 충돌 적={blocker.transform.position}, " +
                $"피해 횟수={blocker.Hits}/{nearby.Hits}/{outside.Hits}/{friendly.Hits}/{assigned.Hits})");
            Require(Mathf.Abs(blocker.Damage - data.damage) < 0.001f && !tank.GetComponent<CircleCollider2D>().enabled &&
                    tank.GetComponent<Rigidbody2D>().velocity == Vector2.zero, "폭발 피해량·즉시 이동 및 충돌 종료");
            Require(tank.transform.Find("ExplosionVisual").gameObject.activeSelf && !tank.GetComponent<SpriteRenderer>().enabled,
                "전차 숨김·첨부 폭발 애니메이션 재생");
            Capture("explosion.png");
            yield return data.ExplosionDuration + 0.12f;
            Require(!tank.gameObject.activeSelf && blocker.Hits == 1, "폭발 한 번 처리·애니메이션 종료 후 풀 반환");
            blocker.gameObject.SetActive(false);
            nearby.gameObject.SetActive(false);
            outside.gameObject.SetActive(false);
            assigned.gameObject.SetActive(false);
            tank.Begin(data, Center);
            yield return 0.08f;
            Require(!Exploded() && tank.GetComponent<CircleCollider2D>().enabled && tank.GetComponent<SpriteRenderer>().enabled && GetTarget() == null,
                "풀 재사용 시 표적·폭발 상태·Collider·Renderer 초기화");

            ResetCase();
            data.moveSpeed = 80f;
            Target("벽 뒤 적", Center + Vector3.right * 4f);
            GameObject wall = Track(new GameObject("얇은 벽"));
            wall.layer = LayerMask.NameToLayer("Wall");
            wall.tag = "Wall";
            wall.transform.position = Center + Vector3.right;
            wall.AddComponent<BoxCollider2D>().size = new Vector2(0.03f, 4f);
            SpawnTank();
            yield return 0.08f;
            Require(Exploded() && tank.transform.position.x < wall.transform.position.x,
                "한 물리 프레임보다 얇은 벽을 고속 돌진해도 관통 없이 폭발");

            ResetCase();
            data.moveSpeed = 80f;
            PyroTankVerificationTarget thinEnemy = Target("작은 적", Center + Vector3.right);
            thinEnemy.GetComponent<CircleCollider2D>().radius = 0.03f;
            SpawnTank();
            yield return 0.08f;
            Require(Exploded() && thinEnemy.Hits == 1 && tank.transform.position.x < thinEnemy.transform.position.x,
                "고속 돌진 중 작은 적 관통 없이 충돌 폭발");

            ResetCase();
            data.moveSpeed = 6f;
            data.lifetime = 0.35f;
            GameObject sensor = Track(new GameObject("적 감지용 센서"));
            sensor.layer = LayerMask.NameToLayer("Enemy");
            sensor.tag = "Enemy";
            sensor.transform.position = Center;
            sensor.AddComponent<CircleCollider2D>().isTrigger = true;
            PyroTankVerificationTarget stationaryNearby = Target("시간 만료 범위 피해 대상", Center + Vector3.up);
            data.range = 0.5f;
            Target("탐색 범위 밖 적", Center + Vector3.right * 2f);
            SpawnTank();
            yield return 0.15f;
            Require(!Exploded() && GetTarget() == null && tank.GetComponent<Rigidbody2D>().velocity == Vector2.zero,
                "범위 밖 적·감지 트리거 제외·수명 전 Idle 유지");
            yield return 0.3f;
            Require(Exploded() && stationaryNearby.Hits == 1 && Vector3.Distance(tank.transform.position, Center) < 0.01f,
                "정지 중에도 소환 시점부터 수명 만료 시 자동 폭발·범위 피해");

            ResetCase();
            data.range = 8f;
            data.lifetime = 10f;
            data.moveSpeed = 0.1f;
            GameObject deadObject = Track(new GameObject("사망 연출 중 적"));
            deadObject.tag = "Enemy";
            deadObject.layer = LayerMask.NameToLayer("Enemy");
            deadObject.transform.position = Center + Vector3.right;
            deadObject.AddComponent<CircleCollider2D>().radius = 0.1f;
            EnemyBase deadEnemy = deadObject.AddComponent<EnemyBase>();
            typeof(EnemyBase).GetField("<isDead>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(deadEnemy, true);
            PyroTankVerificationTarget alive = Target("살아 있는 적", Center + Vector3.up * 3f);
            SpawnTank();
            yield return 0.08f;
            Require(GetTarget() == alive.transform, "사망·비활성 적을 제외하고 살아 있는 적 탐색");
            ResetCase();
        }

        private static int Frames(string name)
        {
            return AssetDatabase.LoadAllAssetsAtPath(PyroTankSkillSetup.ArtRoot + "/Sprites/" + name + ".png").OfType<Sprite>().Count();
        }

        private static IEnumerator VerifyJumpPhases(float duration, bool capture)
        {
            ResetCase();
            data.jumpDuration = duration;
            SpawnTank(Vector2.right);
            yield return duration * 0.125f;
            Transform visual = tank.transform.Find("JumpVisual");
            SpriteRenderer renderer = visual.GetComponent<SpriteRenderer>();
            float worldHeight = visual.localPosition.y * Mathf.Abs(tank.transform.lossyScale.y);
            Require(Jumping() && renderer.sprite.name == "PyroTank_Jump_00" && worldHeight > 0f && worldHeight < data.jumpHeight,
                $"점프 {duration:0.##}초: 1프레임 상승 중 높이·Sprite 일치");
            if (capture) Capture("jump-frame-1.png");
            yield return duration * 0.25f;
            worldHeight = visual.localPosition.y * Mathf.Abs(tank.transform.lossyScale.y);
            Require(Jumping() && renderer.sprite.name == "PyroTank_Jump_01" && Mathf.Abs(worldHeight - data.jumpHeight) < 0.001f,
                $"점프 {duration:0.##}초: 2프레임 최고 높이 유지·Sprite 일치");
            if (capture) Capture("jump-frame-2.png");
            yield return duration * 0.25f;
            worldHeight = visual.localPosition.y * Mathf.Abs(tank.transform.lossyScale.y);
            Require(Jumping() && renderer.sprite.name == "PyroTank_Jump_02" && worldHeight > 0f && worldHeight < data.jumpHeight,
                $"점프 {duration:0.##}초: 3프레임 하강 중 높이·Sprite 일치");
            if (capture) Capture("jump-frame-3.png");
            yield return duration * 0.25f;
            Vector2 landing = (Vector2)Center + Vector2.right * data.jumpDistance;
            Require(Jumping() && renderer.sprite.name == "PyroTank_Jump_03" && visual.localPosition == Vector3.zero &&
                    Vector2.Distance(tank.GetComponent<Rigidbody2D>().position, landing) < 0.01f &&
                    tank.GetComponent<Rigidbody2D>().velocity == Vector2.zero && GetTarget() == null,
                $"점프 {duration:0.##}초: 4프레임 지면 착지·전진 종료·착지 동작 중 추적 보류");
            if (capture) Capture("jump-frame-4.png");
            yield return duration * 0.25f;
            Require(!Jumping() && !visual.gameObject.activeSelf && tank.GetComponent<SpriteRenderer>().enabled,
                $"점프 {duration:0.##}초: 착지 프레임 표시 후 Idle로 복귀");
        }

        private static void SpawnTank(Vector2 launchDirection = default)
        {
            Physics2D.SyncTransforms();
            tank = Track(Object.Instantiate(data.tankPrefab.gameObject)).GetComponent<PyroTankController>();
            tank.Begin(data, Center, launchDirection);
            Physics2D.SyncTransforms();
        }

        private static PyroTankVerificationTarget Target(string name, Vector3 position, bool enemy = true)
        {
            GameObject root = Track(new GameObject(name));
            root.transform.position = position;
            if (enemy) { root.tag = "Enemy"; root.layer = LayerMask.NameToLayer("Enemy"); }
            root.AddComponent<CircleCollider2D>().radius = 0.12f;
            return root.AddComponent<PyroTankVerificationTarget>();
        }

        private static Transform GetTarget() => (Transform)Field("target").GetValue(tank);
        private static bool Exploded() => (bool)Field("hasExploded").GetValue(tank);
        private static bool Jumping() => (bool)Field("isJumping").GetValue(tank);
        private static FieldInfo Field(string name) => typeof(PyroTankController).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        private static List<PyroTankController> GetSkillTanks() => (List<PyroTankController>)typeof(PyroTankSkill)
            .GetField("tanks", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(skill);
        private static PyroTankController GetSkillTank(int index) => GetSkillTanks()[index];
        private static GameObject Track(GameObject obj) { objects.Add(obj); return obj; }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
            checks.Add("PASS " + message);
        }

        private static void ResetCase()
        {
            foreach (GameObject obj in objects) if (obj != null) Object.DestroyImmediate(obj);
            objects.Clear();
            tank = null;
            skill = null;
        }

        private static void Capture(string filename)
        {
            GameObject cameraObject = new GameObject("PyroTankVerificationCamera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 1.5f;
            camera.transform.position = tank.transform.position + Vector3.up * 0.6f + Vector3.back * 10f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.025f, 0.035f, 0.065f);
            RenderTexture texture = new RenderTexture(512, 512, 24);
            Texture2D image = new Texture2D(512, 512, TextureFormat.RGB24, false);
            RenderTexture previous = RenderTexture.active;
            try
            {
                camera.targetTexture = texture;
                camera.Render();
                RenderTexture.active = texture;
                image.ReadPixels(new Rect(0, 0, 512, 512), 0, 0);
                image.Apply();
                Directory.CreateDirectory(Output);
                File.WriteAllBytes(Output + "/" + filename, image.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                camera.targetTexture = null;
                Object.DestroyImmediate(image);
                Object.DestroyImmediate(texture);
                Object.DestroyImmediate(cameraObject);
            }
        }

        private static void Finish(Exception error)
        {
            Directory.CreateDirectory(Output);
            File.WriteAllText(Output + "/verification.txt", "Unity " + Application.unityVersion + " / " + SceneManager.GetActiveScene().name +
                " / PlayMode=" + EditorApplication.isPlaying + "\n" + string.Join("\n", checks) + "\n" +
                (error == null ? "RESULT: PASS" : "RESULT: FAIL\n" + error));
            if (error != null) Debug.LogException(error);
            else Debug.Log("[PyroTankSkillVerification] 모든 검증을 통과했습니다.");
            routine = null;
            ResetCase();
            if (data != null) Object.DestroyImmediate(data);
            data = null;
            Time.timeScale = previousTimeScale;
            SessionState.SetBool(Pending, false);
            readyAt = 0d;
            if (exitAfter) EditorApplication.isPlaying = false;
            if (SessionState.GetBool(Pending + ".Batch", false))
            {
                SessionState.SetBool(Pending + ".Batch", false);
                int exitCode = error == null ? 0 : 1;
                EditorApplication.delayCall += () => EditorApplication.Exit(exitCode);
            }
        }
    }

    public sealed class PyroTankVerificationTarget : MonoBehaviour, IDamageable
    {
        public int Hits { get; private set; }
        public float Damage { get; private set; }
        public void TakeDamage(float damageAmount, bool isChain = false) { Hits++; Damage += damageAmount; }
    }
}
