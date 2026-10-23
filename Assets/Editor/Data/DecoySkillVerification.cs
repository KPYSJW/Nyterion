using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Nytherion.Data.ScriptableObjects.Gacha;
using Nytherion.Data.ScriptableObjects.Skill;
using Nytherion.GamePlay.Characters.Enemy;
using Nytherion.GamePlay.Skills;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object = UnityEngine.Object;

namespace Nytherion.Editor
{
    /// <summary>별도 Unity 프로젝트의 빈 씬에서 실제 프리팹·물리·마우스 입력을 검증합니다.</summary>
    [InitializeOnLoad]
    public static class DecoySkillVerification
    {
        private const string Pending = "Nytherion.Decoy.Verify";
        private static readonly List<GameObject> objects = new List<GameObject>();
        private static readonly List<string> checks = new List<string>();
        private static IEnumerator routine;
        private static float nextAt;
        private static double deadline;
        private static DecoySkillData data;

        static DecoySkillVerification()
        {
            EditorApplication.update += Update;
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Pending, false))
                { routine = Verify(); deadline = EditorApplication.timeSinceStartup + 45d; nextAt = 0f; }
            };
        }

        // 사용자 씬과 세이브 파일에 접근하지 않는 별도 복사본에서만 호출합니다.
        public static void RunBatch()
        {
            DecoySkillSetup.CreateAssets();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SessionState.SetBool(Pending, true);
            EditorApplication.isPlaying = true;
        }

        private static void Update()
        {
            if (routine == null || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            try
            {
                if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("더미 검증 시간 초과");
                if (Time.time < nextAt) return;
                if (!routine.MoveNext()) { Finish(null); return; }
                nextAt = Time.time + (float)routine.Current;
            }
            catch (Exception error) { Finish(error); }
        }

        private static IEnumerator Verify()
        {
            data = Object.Instantiate(AssetDatabase.LoadAssetAtPath<DecoySkillData>(DecoySkillSetup.DataPath));
            Require(data != null && data.dummyPrefab != null && data.skillPrefab != null && data.dummySprite != null,
                "스킬 데이터·시전 프리팹·더미 프리팹·본체 스프라이트 연결");
            Require(data.summonFrames.Length == 5 && data.summonFrames.All(frame => frame.rect.size == new Vector2(32f, 64f)),
                "첨부 시트의 32×64 소환 애니메이션 5프레임");
            Require(data.coolDown == 45f && data.tauntRadius == 2.5f && data.spawnDistance == 1f,
                "쿨타임 45초·유인 반경 2.5·배치 거리 1");
            Require(data.GetDurability(1) == 100f && data.GetDurability(5) == 200f &&
                data.GetLifetime(1) == 8f && data.GetLifetime(5) == 12f, "레벨별 내구도·지속시간 증가");
            SkillData source = AssetDatabase.LoadAssetAtPath<SkillData>(DecoySkillSetup.DataPath);
            Require(AssetDatabase.LoadAssetAtPath<SkillDatabaseSO>("Assets/Nytherion/Data/ScriptableObjects/Skill/SkillDatabaseSO.asset")
                .GetSkillById("skill_decoy") == source, "저장 ID 복원용 DB 등록");
            Require(AssetDatabase.LoadAssetAtPath<GachaPoolSO>("Assets/Nytherion/Data/ScriptableObjects/Gacha/GachaPool/Skill/SkillGachaPool.asset")
                .items.Any(entry => entry.item == source) && AssetDatabase.LoadAssetAtPath<Object>(DecoySkillSetup.RelicPath) != null,
                "스킬 가챠 및 각인 획득 경로");

            GameObject owner = Track(new GameObject("DecoyVerificationPlayer"));
            owner.tag = "Player";
            owner.transform.position = new Vector3(100f, 100f, 0f);
            Camera camera = Track(new GameObject("DecoyVerificationCamera")).AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.orthographic = true;
            camera.orthographicSize = 4f;
            camera.transform.position = owner.transform.position + Vector3.back * 10f;
            Mouse mouse = Mouse.current ?? InputSystem.AddDevice<Mouse>();
            Vector2 direction = new Vector2(0.8f, 0.6f);
            Vector2 mousePosition = camera.WorldToScreenPoint(owner.transform.position + (Vector3)(direction * 3f));
            InputSystem.QueueStateEvent(mouse, new MouseState { position = mousePosition });
            InputSystem.Update();

            DecoySkill skill = Track(Object.Instantiate(data.skillPrefab)).GetComponent<DecoySkill>();
            skill.skillData = data;
            skill.caster = owner.transform;
            Require(skill.TryUse(), "실제 마우스 입력 방향으로 스킬 시전");
            DecoyDummy dummy = skill.ActiveDummy;
            Require(Vector2.Distance(dummy.transform.position, (Vector2)owner.transform.position + direction) < 0.01f,
                "시전 시점 마우스 방향의 정확히 1 거리 위치 고정");
            Require(!dummy.IsAlive && !dummy.GetComponent<Collider2D>().enabled && !skill.TryUse(),
                "연출 중 피격·유인 비활성 및 연속 시전 쿨타임 차단");
            EnemyAIController near = Enemy("Near", dummy.transform.position + Vector3.right * 2f);
            EnemyAIController far = Enemy("Far", dummy.transform.position + Vector3.left * 3.1f);
            Require(near.CurrentTarget == owner.transform && far.CurrentTarget == owner.transform, "소환 연출 중 플레이어 타깃 유지");
            yield return 0.65f;
            Require(dummy.IsAlive && dummy.CurrentDurability == 100f && dummy.RemainingLifetime > 7.5f,
                "연출 완료 후 본체 생성 및 그 시점부터 지속시간 시작");
            Require(near.CurrentTarget == dummy.transform && far.CurrentTarget == owner.transform,
                "반경 내부 적만 유인하고 3.1 거리 적은 플레이어 유지");
            EnemyAIController airborne = Enemy("Airborne", dummy.transform.position + Vector3.down * 2f);
            airborne.GetComponent<Collider2D>().isTrigger = true;
            Physics2D.SyncTransforms();
            yield return 0.15f;
            Require(airborne.CurrentTarget == dummy.transform, "도약 중 트리거 몸체를 가진 적도 반경 내 유인");
            far.transform.position = dummy.transform.position + Vector3.up * 2.4f;
            Physics2D.SyncTransforms();
            yield return 0.15f;
            Require(far.CurrentTarget == dummy.transform, "나중에 반경에 진입한 적도 유인");
            near.transform.position = dummy.transform.position + Vector3.right * 2.6f;
            Require(near.CurrentTarget == owner.transform, "반경 이탈 즉시 플레이어 타깃 복원");

            GameObject meleeObject = Track(new GameObject("DecoyVerificationMelee"));
            meleeObject.transform.position = dummy.transform.position;
            meleeObject.AddComponent<CircleCollider2D>().isTrigger = true;
            meleeObject.AddComponent<MeleeAttackCollider>().Initialize(30f);
            Physics2D.SyncTransforms();
            yield return 0.15f;
            Require(dummy.CurrentDurability == 70f, "실제 근접 트리거 충돌로 내구도 차감");
            meleeObject.SetActive(false);
            GameObject projectile = Track(new GameObject("DecoyVerificationProjectile"));
            projectile.transform.position = dummy.transform.position;
            projectile.AddComponent<CircleCollider2D>().isTrigger = true;
            projectile.AddComponent<EnemyProjectiles>().Initialize(20f);
            Physics2D.SyncTransforms();
            yield return 0.15f;
            Require(dummy.CurrentDurability == 50f && projectile == null, "적 투사체 피해 및 피격 후 투사체 제거");
            Capture(camera, "decoy-preview.png");
            dummy.TakeDamage(50f);
            Require(!dummy.gameObject.activeSelf && far.CurrentTarget == owner.transform,
                "내구도 소진 시 더미 제거 및 적 타깃 복원");

            data.baseLifetime = 0.3f;
            data.lifetimePerLevel = 0f;
            dummy.Begin(data, owner.transform, owner.transform.position + Vector3.right, 5);
            yield return 0.6f;
            Require(dummy.IsAlive && dummy.CurrentDurability == 200f, "재사용한 더미의 레벨 5 내구도 초기화");
            yield return 0.35f;
            Require(!dummy.gameObject.activeSelf, "지속시간 만료 시 더미 제거");
            dummy.Begin(data, owner.transform, owner.transform.position + Vector3.right, 1);
            skill.gameObject.SetActive(false);
            Require(!dummy.gameObject.activeSelf, "스킬 해제 시 소환 중 객체 정리");

            skill.gameObject.SetActive(true);
            DecoySkill blocked = Track(Object.Instantiate(data.skillPrefab)).GetComponent<DecoySkill>();
            blocked.skillData = data;
            blocked.caster = owner.transform;
            GameObject wall = Track(new GameObject("DecoyVerificationWall"));
            wall.layer = LayerMask.NameToLayer("Wall");
            wall.transform.position = owner.transform.position + (Vector3)direction;
            wall.AddComponent<BoxCollider2D>().size = Vector2.one * 0.6f;
            Physics2D.SyncTransforms();
            InputSystem.QueueStateEvent(mouse, new MouseState { position = mousePosition });
            InputSystem.Update();
            Require(!blocked.TryUse() && blocked.GetRemainingCooldown() == 0f,
                "벽 안 배치 실패 시 객체·쿨타임 미소비");
            wall.SetActive(false);
            Physics2D.SyncTransforms();
            Require(blocked.TryUse(), "벽 제거 후 같은 스킬 즉시 재시전 가능");
            owner.SetActive(false);
            yield return 0.05f;
            Require(!blocked.ActiveDummy.gameObject.activeSelf, "시전자 비활성화 시 소환 중 객체 정리");
        }

        private static EnemyAIController Enemy(string name, Vector3 position)
        {
            GameObject root = Track(new GameObject("DecoyVerification" + name));
            root.SetActive(false);
            root.layer = LayerMask.NameToLayer("Enemy");
            root.transform.position = position;
            root.AddComponent<CircleCollider2D>().radius = 0.15f;
            Rigidbody2D body = root.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            root.AddComponent<NavMeshAgent>().enabled = false;
            EnemyAIController enemy = root.AddComponent<EnemyAIController>();
            enemy.Root = root.transform;
            enemy.detectRange = 100f;
            root.SetActive(true);
            enemy.SetMovementAllowed(false);
            return enemy;
        }

        private static void Capture(Camera camera, string name)
        {
            RenderTexture target = new RenderTexture(800, 450, 24);
            RenderTexture previous = RenderTexture.active;
            Texture2D image = new Texture2D(800, 450, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 800, 450), 0, 0);
                image.Apply();
                Directory.CreateDirectory(DecoySkillSetup.Output);
                File.WriteAllBytes(DecoySkillSetup.Output + "/" + name, image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previous;
                target.Release();
                Object.Destroy(target);
                Object.Destroy(image);
            }
        }
        private static GameObject Track(GameObject obj) { objects.Add(obj); return obj; }
        private static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); checks.Add("PASS " + message); }
        private static void Finish(Exception error)
        {
            routine = null;
            SessionState.SetBool(Pending, false);
            foreach (GameObject obj in objects) if (obj != null) { obj.SetActive(false); Object.Destroy(obj); }
            objects.Clear();
            if (data != null) Object.Destroy(data);
            if (error != null) checks.Add("FAIL " + error);
            Directory.CreateDirectory(DecoySkillSetup.Output);
            File.WriteAllLines(DecoySkillSetup.Output + "/verification.txt", checks);
            EditorApplication.isPlaying = false;
            EditorApplication.Exit(error == null ? 0 : 1);
        }
    }
}
