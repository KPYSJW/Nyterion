using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Nytherion.Core.Interfaces;
using Nytherion.Data.ScriptableObjects.Skill;
using Nytherion.GamePlay.Skills;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Nytherion.Editor
{
    /// <summary>실제 스킬 프리팹과 Physics2D를 사용하여 파동, 중복 피해, 이동 및 재사용을 검증합니다.</summary>
    [InitializeOnLoad]
    public static class ChainIgnitionSkillVerification
    {
        private const string Output = "output/chain-ignition";
        private const string Pending = "ChainIgnitionVerification.Pending";
        private static readonly List<GameObject> objects = new List<GameObject>();
        private static readonly List<string> checks = new List<string>();
        private static readonly Vector3 Center = new Vector3(1000f, 1000f, 0f);
        private static ChainIgnitionSkill skill;
        private static ChainIgnitionSkillData data;
        private static GameObject caster;
        private static ChainIgnitionWave firstWave;
        private static ChainIgnitionVerificationTarget[] ringTargets;
        private static ChainIgnitionVerificationTarget overlappingTarget, largeTarget, friendlyTarget, outsideTarget;
        private static ChainIgnitionVerificationTarget offsetTarget, unshiftedTarget;
        private static readonly List<ChainIgnitionVerificationTarget> crowdedTargets = new List<ChainIgnitionVerificationTarget>();
        private static float startTime, previousTimeScale;
        private static int stage;
        private static double readyAt;
        private static bool exitAfter;

        static ChainIgnitionSkillVerification()
        {
            EditorApplication.update += Update;
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Pending, false))
                    readyAt = EditorApplication.timeSinceStartup + 2d;
                if (state == PlayModeStateChange.ExitingPlayMode && skill != null) Cleanup();
            };
        }

        [MenuItem("Tools/Nytherion/Chain Ignition/Verify In Play Mode")]
        public static void Start()
        {
            if (SessionState.GetBool(Pending, false) || skill != null) return;
            if (!EditorApplication.isPlaying)
            {
                ChainIgnitionSkillData source = AssetDatabase.LoadAssetAtPath<ChainIgnitionSkillData>(ChainIgnitionSkillSetup.DataPath);
                if (source == null || source.wavePrefab == null || source.skillPrefab == null || !source.HasValidAnimation)
                    ChainIgnitionSkillSetup.CreateAssets();
            }
            exitAfter = !EditorApplication.isPlaying;
            SessionState.SetBool(Pending + ".ExitAfter", exitAfter);
            SessionState.SetBool(Pending, true);
            if (EditorApplication.isPlaying) readyAt = EditorApplication.timeSinceStartup + 0.1d;
            else EditorApplication.isPlaying = true;
        }

        private static void Update()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            // 컴파일로 취소된 플레이 진입 요청은 다음 검증을 막지 않도록 정리합니다.
            if (!EditorApplication.isPlayingOrWillChangePlaymode && skill == null && readyAt == 0d)
                SessionState.SetBool(Pending, false);
            if (File.Exists(Output + "/verify.request") && !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                File.Delete(Output + "/verify.request");
                Start();
            }
            try
            {
                if (SessionState.GetBool(Pending, false) && EditorApplication.isPlaying && readyAt > 0d &&
                    EditorApplication.timeSinceStartup >= readyAt)
                {
                    SessionState.SetBool(Pending, false);
                    exitAfter = SessionState.GetBool(Pending + ".ExitAfter", false);
                    Begin();
                }
                if (skill == null || EditorApplication.isPaused) return;
                float elapsed = Time.time - startTime;
                float sampleDelay = data.AnimationDuration * 0.4f;
                float finishTime = (ChainIgnitionSkillData.WaveCount - 1) * data.WaveInterval + data.AnimationDuration + 0.1f;
                if (stage == 0 && elapsed >= sampleDelay)
                {
                    firstWave = Object.FindObjectsOfType<ChainIgnitionWave>().Single(wave => Vector3.Distance(wave.transform.position, Center) < 0.01f);
                    SpriteRenderer[] renderers = firstWave.GetComponentsInChildren<SpriteRenderer>();
                    Require(renderers.Count(renderer => renderer.enabled) == 8, "첫 원의 8방향 폭발");
                    Require(renderers.Take(8).All(renderer => Mathf.Abs(Vector3.Distance(renderer.transform.position, Center) - 1.2f) < 0.01f), "첫 원 반경 1.2");
                    Require(ringTargets[0].Hits == 1 && ringTargets[1].Hits == 0 && ringTargets[2].Hits == 0,
                        "1차 폭발 중에는 2·3차 피해 없음");
                    Capture("wave-1.png");
                    caster.transform.position = Center + Vector3.right * 12f;
                    Physics2D.SyncTransforms();
                    stage = 1;
                }
                if (stage == 1 && elapsed >= data.WaveInterval + sampleDelay)
                {
                    SpriteRenderer[] renderers = firstWave.GetComponentsInChildren<SpriteRenderer>();
                    Require(Vector3.Distance(firstWave.transform.position, Center) < 0.01f, "시전자 이동 후에도 시전 중심 고정");
                    Require(renderers.Skip(8).Take(8).All(renderer => renderer.enabled && Mathf.Abs(Vector3.Distance(renderer.transform.position, Center) - 2.4f) < 0.01f), "두 번째 원 반경 2.4, 8방향");
                    Require(renderers.Count(renderer => renderer.enabled) == 8 && renderers.Take(8).All(renderer => !renderer.enabled),
                        "1차 애니메이션 종료 후 2차만 재생");
                    Require(ringTargets[1].Hits == 1 && ringTargets[2].Hits == 0, "2차 폭발 중에는 3차 피해 없음");
                    Capture("wave-2.png");
                    stage = 2;
                }
                if (stage == 2 && elapsed >= 2f * data.WaveInterval + sampleDelay)
                {
                    SpriteRenderer[] renderers = firstWave.GetComponentsInChildren<SpriteRenderer>();
                    Require(renderers.Skip(16).All(renderer => renderer.enabled && Mathf.Abs(Vector3.Distance(renderer.transform.position, Center) - 3.6f) < 0.01f), "마지막 원 반경 3.6, 8방향");
                    Require(renderers.Count(renderer => renderer.enabled) == 8 && renderers.Take(16).All(renderer => !renderer.enabled),
                        "2차 애니메이션 종료 후 3차만 재생");
                    Capture("wave-3.png");
                    stage = 3;
                }
                if (stage == 3 && elapsed >= finishTime)
                {
                    Require(ringTargets.All(target => target.Hits == 1 && Mathf.Approximately(target.Damage, 10f)), "각 원의 실제 Physics2D 피해");
                    Require(overlappingTarget.Hits == 1, "인접한 폭발과 다중 콜라이더 중복 피해 방지");
                    Require(largeTarget.Hits == 3 && Mathf.Approximately(largeTarget.Damage, 30f), "큰 적은 파동마다 한 번, 총 세 번 피격");
                    Require(friendlyTarget.Hits == 0 && outsideTarget.Hits == 0, "플레이어 레이어와 범위 밖 대상 제외");
                    Require(crowdedTargets.All(target => target.Hits == 1), "32개를 넘는 밀집 대상도 누락 없이 피격");
                    Require(!firstWave.gameObject.activeSelf && firstWave.GetComponentsInChildren<SpriteRenderer>().All(renderer => !renderer.enabled), "애니메이션 종료와 로컬 풀 반환");
                    data.coolDown = 0f;
                    data.castCenterOffset = new Vector2(2f, 6f);
                    Vector3 shiftedCenter = caster.transform.position + (Vector3)data.castCenterOffset;
                    offsetTarget = Target("보정된 첫 원", shiftedCenter + Vector3.right * data.GetRingRadius(0), 0.05f);
                    unshiftedTarget = Target("보정 전 첫 원", caster.transform.position + Vector3.right * data.GetRingRadius(0), 0.05f);
                    Physics2D.SyncTransforms();
                    Require(skill.TryUse(), "쿨다운 종료 후 재시전");
                    Require(firstWave.gameObject.activeSelf && Vector3.Distance(firstWave.transform.position, shiftedCenter) < 0.01f, "기존 폭발 오브젝트 재사용 및 X·Y 중심 보정 적용");
                    caster.transform.position += Vector3.left * 12f;
                    startTime = Time.time;
                    stage = 4;
                    return;
                }
                if (stage == 4 && elapsed >= finishTime)
                {
                    Require(!firstWave.gameObject.activeSelf, "반복 사용 후 타이머 초기화 및 종료");
                    Require(offsetTarget.Hits == 1 && Mathf.Approximately(offsetTarget.Damage, 10f) && unshiftedTarget.Hits == 0,
                        "중심 보정으로 연출과 피해 판정이 함께 이동하고 시전자 이동 후에도 고정");
                    skill.gameObject.SetActive(false);
                    Require(!firstWave.gameObject.activeSelf, "스킬 비활성화 시 폭발 정리");
                    Finish(null);
                }
            }
            catch (Exception exception) { Finish(exception); }
        }

        private static void Begin()
        {
            checks.Clear();
            Directory.CreateDirectory(Output);
            previousTimeScale = Time.timeScale;
            Time.timeScale = 1f;
            ChainIgnitionSkillData source = AssetDatabase.LoadAssetAtPath<ChainIgnitionSkillData>(ChainIgnitionSkillSetup.DataPath);
            Texture2D sheet = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Nytherion/Art/Skills/Sprites/ChainIgnition.png");
            Require(source != null && sheet != null && source.HasValidAnimation &&
                source.explosionFrames.Length == sheet.width / sheet.height && source.icon != null,
                "스킬 데이터, 원본 시트의 전체 프레임 및 아이콘 연결");
            Require(source.explosionFrames.Select((frame, index) =>
                frame.rect == new Rect(index * sheet.height, 0, sheet.height, sheet.height)).All(valid => valid),
                "수정한 프레임 크기와 재생 순서 반영");
            Require(source.skillPrefab.GetComponent<ChainIgnitionSkill>() != null && source.wavePrefab != null, "스킬 및 폭발 프리팹 연결");
            SkillDatabaseSO database = AssetDatabase.LoadAssetAtPath<SkillDatabaseSO>("Assets/Nytherion/Data/ScriptableObjects/Skill/SkillDatabaseSO.asset");
            Require(database.allSkills.Count(entry => entry == source) == 1 && database.GetSkillById(source.skillID) == source, "데이터베이스 등록과 저장 ID 조회");
            data = Object.Instantiate(source);
            Require(new SerializedObject(data).FindProperty("castCenterOffset") != null, "Inspector 중심 보정 필드 직렬화");
            data.castCenterOffset = Vector2.zero;
            caster = Track(new GameObject("[ChainIgnitionVerification] 시전자"));
            caster.transform.position = Center;
            skill = Object.Instantiate(source.skillPrefab, caster.transform).GetComponent<ChainIgnitionSkill>();
            skill.skillData = data;
            skill.caster = caster.transform;
            GameObject weaponPoint = Track(new GameObject("[ChainIgnitionVerification] 발사점"));
            weaponPoint.transform.position = Center + Vector3.right * 30f;
            skill.firePoint = weaponPoint.transform;
            ringTargets = Enumerable.Range(0, 3).Select(index => Target("원 " + index, Center + Vector3.right * data.GetRingRadius(index), 0.05f)).ToArray();
            overlappingTarget = Target("중복 판정", Center + new Vector3(1.024f, 0.424f), 0.05f);
            new GameObject("추가 콜라이더").transform.SetParent(overlappingTarget.transform, false);
            overlappingTarget.transform.GetChild(0).gameObject.layer = LayerMask.NameToLayer("Enemy");
            overlappingTarget.transform.GetChild(0).gameObject.AddComponent<CircleCollider2D>().radius = 0.05f;
            largeTarget = Target("큰 적", Center, 3f);
            friendlyTarget = Target("플레이어 레이어", Center + Vector3.right * 1.2f, 0.05f, 0);
            outsideTarget = Target("범위 밖", Center + Vector3.right * 7f, 0.05f);
            for (int i = 0; i < 40; i++) crowdedTargets.Add(Target("밀집 " + i, Center + Vector3.right * 3.6f, 0.03f));
            Physics2D.SyncTransforms();
            Require(skill.TryUse(), "실제 SkillBase.TryUse 시전 경로");
            Require(!skill.TryUse(), "시전 직후 쿨다운 중 재시전 차단");
            startTime = Time.time;
            stage = 0;
        }

        private static GameObject Track(GameObject obj) { objects.Add(obj); return obj; }

        private static ChainIgnitionVerificationTarget Target(string name, Vector3 position, float radius, int layer = -1)
        {
            GameObject obj = Track(new GameObject("[ChainIgnitionVerification] " + name));
            obj.transform.position = position;
            obj.layer = layer >= 0 ? layer : LayerMask.NameToLayer("Enemy");
            obj.AddComponent<CircleCollider2D>().radius = radius;
            return obj.AddComponent<ChainIgnitionVerificationTarget>();
        }

        private static void Require(bool condition, string name)
        {
            if (!condition) throw new InvalidOperationException(name);
            checks.Add("PASS " + name);
        }

        private static void Capture(string filename)
        {
            GameObject cameraObject = new GameObject("[ChainIgnitionVerification] 렌더 카메라");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.enabled = false;
            camera.orthographic = true;
            camera.orthographicSize = 5.3f;
            camera.transform.position = Center + Vector3.back * 10f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.025f, 0.035f, 0.065f);
            RenderTexture texture = new RenderTexture(640, 640, 24);
            Texture2D image = new Texture2D(640, 640, TextureFormat.RGB24, false);
            RenderTexture previous = RenderTexture.active;
            try
            {
                camera.targetTexture = texture;
                camera.Render();
                RenderTexture.active = texture;
                image.ReadPixels(new Rect(0, 0, 640, 640), 0, 0);
                image.Apply();
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
            else Debug.Log("[ChainIgnitionSkillVerification] 모든 검증을 통과했습니다.");
            Cleanup();
            SessionState.SetBool(Pending, false);
            if (exitAfter) EditorApplication.isPlaying = false;
        }

        private static void Cleanup()
        {
            foreach (GameObject obj in objects) if (obj != null) Object.DestroyImmediate(obj);
            objects.Clear();
            if (data != null) Object.DestroyImmediate(data);
            skill = null;
            data = null;
            crowdedTargets.Clear();
            Time.timeScale = previousTimeScale;
        }
    }

    public class ChainIgnitionVerificationTarget : MonoBehaviour, IDamageable
    {
        public int Hits { get; private set; }
        public float Damage { get; private set; }
        public void TakeDamage(float damageAmount, bool isChain = false) { Hits++; Damage += damageAmount; }
    }
}
