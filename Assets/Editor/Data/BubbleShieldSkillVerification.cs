using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Nytherion.Core.Managers;
using Nytherion.Data.ScriptableObjects.Skill;
using Nytherion.GamePlay.Characters.Player;
using Nytherion.GamePlay.Skills;
using Nytherion.UI.Components;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Nytherion.Editor
{
    /// <summary>별도 프로젝트의 플레이 모드에서 피해 흡수·연출·HUD를 검증한다.</summary>
    [InitializeOnLoad]
    public static class BubbleShieldSkillVerification
    {
        private const string Key = "Nytherion.BubbleShieldVerification";
        private const string Output = BubbleShieldSkillSetup.Output;
        private static readonly List<string> checks = new List<string>();
        private static IEnumerator routine;
        private static double deadline;
        private static float nextAt;

        static BubbleShieldSkillVerification()
        {
            EditorApplication.update += Update;
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Key, false))
                {
                    routine = SessionState.GetBool(Key + ".Hit", false) ? VerifyHit() :
                        SessionState.GetBool(Key + ".GameScene", false) ? VerifyGameScene() : Verify();
                    deadline = EditorApplication.timeSinceStartup + 60d;
                    nextAt = 0f;
                }
            };
        }

        public static void RunBatch()
        {
            if (!Application.isBatchMode || !Application.dataPath.Replace('\\', '/').Contains("/tmp/"))
                throw new InvalidOperationException("별도 tmp 검증 프로젝트의 배치 모드에서만 실행할 수 있습니다.");
            PlayerSettings.productName = "Nytherion_BubbleShield_Verification";
            BubbleShieldSkillSetup.CreateAssets();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SessionState.SetBool(Key, true);
            EditorApplication.isPlaying = true;
        }

        public static void RunGameSceneBatch()
        {
            if (!Application.isBatchMode || !Application.dataPath.Replace('\\', '/').Contains("/tmp/"))
                throw new InvalidOperationException("별도 tmp 검증 프로젝트에서만 실행할 수 있습니다.");
            PlayerSettings.productName = "Nytherion_BubbleShield_GameScene_Verification";
            EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity");
            SessionState.SetBool(Key + ".GameScene", true);
            SessionState.SetBool(Key, true);
            EditorApplication.isPlaying = true;
        }

        public static void RunHitBatch()
        {
            SessionState.SetBool(Key + ".Hit", true);
            RunBatch();
        }

        private static IEnumerator VerifyHit()
        {
            BubbleShieldSkillData data = Object.Instantiate(AssetDatabase.LoadAssetAtPath<BubbleShieldSkillData>(BubbleShieldSkillSetup.DataPath));
            data.coolDown = 0f;
            data.duration = 5f;
            Require(data.hitEffectPrefab != null, "실제 스킬 데이터의 피격 프리팹 연결");
            AnimationClip clip = new SerializedObject(data.hitEffectPrefab).FindProperty("animationClip").objectReferenceValue as AnimationClip;
            ObjectReferenceKeyframe[] frames = AnimationUtility.GetObjectReferenceCurve(clip,
                EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"));
            Require(frames.Length == 4 && frames.Take(3).All(frame => ((Sprite)frame.value).rect.size == new Vector2(16, 16)) &&
                frames[3].value == null && !AnimationUtility.GetAnimationClipSettings(clip).loopTime,
                "첨부 시트 16×16 3프레임 및 일회 재생 후 표시 종료");
            GameObject owner = new GameObject("Shield Hit Verification Player");
            owner.tag = "Player";
            SpriteRenderer body = owner.AddComponent<SpriteRenderer>();
            body.sprite = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Gameplay/Characters/Player/Player.prefab")
                .GetComponent<SpriteRenderer>().sprite;
            PlayerHealth health = owner.AddComponent<PlayerHealth>();
            health.InitializeHealth(100f);
            CircleCollider2D collider = owner.AddComponent<CircleCollider2D>();
            collider.radius = 0.18f;
            Rigidbody2D rigidbody = owner.AddComponent<Rigidbody2D>();
            rigidbody.bodyType = RigidbodyType2D.Kinematic;
            rigidbody.gravityScale = 0f;
            BubbleShieldSkill skill = Object.Instantiate(data.skillPrefab, owner.transform).GetComponent<BubbleShieldSkill>();
            skill.skillData = data;
            skill.caster = owner.transform;
            Require(skill.TryUse(), "피격 검증용 실드 시전");
            yield return 0.65f;
            Vector2 center = body.bounds.center;
            health.SetInvulnerable(true);
            health.TakeDamage(10f, center + Vector2.right);
            Require(skill.GetComponentsInChildren<BubbleShieldHitEffect>(true).Length == 0, "무적 중 피격 연출 미발생");
            health.SetInvulnerable(false);
            health.TakeDamage(10f, center + Vector2.right * 0.1f);
            yield return 0.03f;
            BubbleShieldHitEffect[] impacts = skill.GetComponentsInChildren<BubbleShieldHitEffect>();
            SpriteRenderer shield = skill.GetComponentsInChildren<SpriteRenderer>().First(renderer => renderer.gameObject.name == "BubbleShield Visual");
            float radius = shield.sprite.bounds.size.x * Mathf.Abs(shield.transform.lossyScale.x) * data.hitSurfaceRadius;
            Require(impacts.Length == 1 && health.CurrentShield == 90f && health.CurrentHealth == 100f &&
                Vector2.Distance(impacts[0].transform.position, center + Vector2.right * radius) < 0.01f,
                "피해 흡수 1회당 우측 실드 표면에 정확히 1회 재생");
            Require(impacts[0].GetComponent<SpriteRenderer>().sortingOrder > shield.sortingOrder,
                "피격 연출이 실드보다 앞에 표시");
            health.TakeDamage(10f, center + Vector2.left * 0.1f);
            yield return 0.03f;
            Require(skill.GetComponentsInChildren<BubbleShieldHitEffect>().Length == 2 &&
                skill.GetComponentsInChildren<BubbleShieldHitEffect>().Any(effect =>
                    Vector2.Distance(effect.transform.position, center + Vector2.left * radius) < 0.01f),
                "서로 다른 피격 위치의 연출이 독립 재생");
            owner.transform.position += Vector3.right;
            yield return 0.03f;
            Require(Vector2.Distance(impacts[0].transform.position, (Vector2)body.bounds.center + Vector2.right * radius) < 0.01f,
                "이동 중에도 맞은 방향의 실드 표면 추적");
            Canvas preview = new GameObject("Hit Preview Canvas", typeof(Canvas)).GetComponent<Canvas>();
            preview.renderMode = RenderMode.ScreenSpaceCamera;
            Capture(body.bounds.center, preview, "shield-hit.png", 1.3f);
            yield return 0.3f;
            Require(skill.GetComponentsInChildren<BubbleShieldHitEffect>().Length == 0,
                "3프레임 완료 후 반복 없이 피격 연출 종료");
            health.TakeDamage(1f, body.bounds.center + Vector3.up);
            yield return 0.03f;
            Require(skill.GetComponentsInChildren<BubbleShieldHitEffect>(true).Length == 2 &&
                skill.GetComponentsInChildren<BubbleShieldHitEffect>().Length == 1,
                "종료된 피격 객체 재사용");
            yield return 0.3f;
            float beforeProjectile = health.CurrentShield;
            GameObject projectile = new GameObject("Shield Hit Enemy Projectile");
            projectile.transform.position = owner.transform.position + Vector3.right * 0.15f;
            projectile.AddComponent<CircleCollider2D>().isTrigger = true;
            projectile.AddComponent<EnemyProjectiles>().Initialize(5f);
            Physics2D.SyncTransforms();
            yield return 0.05f;
            Require(projectile == null && health.CurrentShield == beforeProjectile - 5f &&
                skill.GetComponentsInChildren<BubbleShieldHitEffect>().Length == 1,
                "실제 적 투사체 충돌로 피격 1회 및 피해 차감");
            health.TakeDamage(health.CurrentShield, body.bounds.center + Vector3.down);
            Require(skill.Phase == BubbleShieldSkill.ShieldPhase.Crash &&
                skill.GetComponentsInChildren<BubbleShieldHitEffect>().Length == 2,
                "실드를 파괴하는 마지막 공격에도 피격 연출 발생");
            yield return 0.4f;
            Require(skill.GetComponentsInChildren<BubbleShieldHitEffect>().Length == 0,
                "실드 파괴 완료 시 잔여 피격 연출 정리");
            health.TakeDamage(1f, owner.transform.position);
            Require(skill.GetComponentsInChildren<BubbleShieldHitEffect>().Length == 0, "실드가 없으면 피격 연출 미발생");
            data.duration = 0.15f;
            skill.TryUse();
            yield return 0.85f;
            Require(health.CurrentShield == 0f && skill.GetComponentsInChildren<BubbleShieldHitEffect>().Length == 0,
                "지속시간 만료에는 피격 연출 미발생");
            yield return 0.4f;
            skill.TryUse();
            health.TakeDamage(1f, body.bounds.center + Vector3.right);
            skill.gameObject.SetActive(false);
            Require(skill.GetComponentsInChildren<BubbleShieldHitEffect>().Length == 0, "장착 해제 시 피격 객체 정리");
        }

        private static IEnumerator VerifyGameScene()
        {
            yield return 6f;
            PlayerSkillManager player = Object.FindObjectOfType<PlayerSkillManager>();
            HPBarUI hud = Object.FindObjectOfType<HPBarUI>();
            Require(player != null && hud != null && Object.FindObjectOfType<GameSceneLifetimeScope>() != null,
                "GameScene 실제 플레이어·HP UI·LifetimeScope 초기화");
            PlayerHealth health = player.GetComponent<PlayerHealth>();
            BubbleShieldSkillData data = Object.Instantiate(AssetDatabase.LoadAssetAtPath<BubbleShieldSkillData>(BubbleShieldSkillSetup.DataPath));
            data.duration = 0.3f;
            player.SetEquippedSkills(new SkillData[] { data, null, null });
            BubbleShieldSkill skill = player.equippedSkills[0] as BubbleShieldSkill;
            Require(skill != null && skill.caster == player.transform && skill.TryUse(),
                "GameScene 실제 장착 경로를 통한 버블 실드 시전");
            float previous = health.CurrentHealth;
            health.TakeDamage(40f, player.transform.position + Vector3.right);
            Image overlay = Get<Image>(hud, "shieldFillImage");
            TextMeshProUGUI label = Get<TextMeshProUGUI>(hud, "hpText");
            Image hp = Get<Image>(hud, "hpFillImage");
            Require(health.CurrentHealth == previous && health.CurrentShield == 60f &&
                overlay != null && overlay.gameObject.activeInHierarchy && overlay.sprite == data.shieldBarSprite &&
                overlay.sprite != null && overlay.color == Color.white &&
                overlay.transform.parent == hp.transform.parent && overlay.transform.GetSiblingIndex() > hp.transform.GetSiblingIndex() &&
                Mathf.Approximately(overlay.fillAmount, 0.6f) && label.text == "60",
                "GameScene 첨부 실드 바를 체력바 위에 표시하고 실드 수치만 표시");
            Require(skill.GetComponentsInChildren<BubbleShieldHitEffect>().Length == 1,
                "GameScene 실제 실드 피해 흡수 시 피격 연출 1회 발생");
            yield return 0.7f;
            Require(skill.Phase == BubbleShieldSkill.ShieldPhase.Sustained,
                "GameScene 생성 연출 이후 유지 상태 전환");
            yield return 0.35f;
            Require(health.CurrentShield == 0f && !overlay.gameObject.activeSelf && label.text == $"{health.CurrentHealth}" &&
                skill.Phase == BubbleShieldSkill.ShieldPhase.Crash,
                "GameScene 지속시간 만료 시 HUD 제거와 파괴 연출");
            yield return 0.4f;
            Require(skill.Phase == BubbleShieldSkill.ShieldPhase.Inactive,
                "GameScene 파괴 연출 완료 및 객체 정리");
            data.coolDown = 0f;
            Require(skill.TryUse(), "GameScene 초과 피해 검증용 실드 재시전");
            float beforeOverflow = health.CurrentHealth;
            health.TakeDamage(40f);
            Require(health.CurrentShield == 60f && health.CurrentHealth == beforeOverflow && label.text == "60",
                "GameScene 실드 흡수 중 체력 보존 및 실드 60 표시");
            health.TakeDamage(80f);
            Require(health.CurrentShield == 0f && health.CurrentHealth == beforeOverflow - 20f &&
                !overlay.gameObject.activeSelf && label.text == $"{health.CurrentHealth}",
                "GameScene 실드 60에 피해 80이면 체력 20만 차감하고 체력 수치 복귀");
        }

        private static void Update()
        {
            if (routine == null || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            try
            {
                if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("실드 검증 시간 초과");
                if (Time.time < nextAt) return;
                if (!routine.MoveNext()) { Finish(null); return; }
                nextAt = Time.time + (float)routine.Current;
            }
            catch (Exception error) { Finish(error); }
        }

        private static IEnumerator Verify()
        {
            BubbleShieldSkillData source = AssetDatabase.LoadAssetAtPath<BubbleShieldSkillData>(BubbleShieldSkillSetup.DataPath);
            Require(source != null && source.skillPrefab.GetComponent<BubbleShieldSkill>() != null &&
                source.summonFrames.Length == 7 && source.crashFrames.Length == 4 && source.shieldSprite != null,
                "첨부 이미지 생성 7/유지 1/파괴 4프레임 및 실제 스킬 프리팹 연결");
            Require(source.summonFrames.Concat(source.crashFrames).All(sprite => sprite.rect.size == new Vector2(32, 32)),
                "32×32 프레임 분할 및 정방형 피벗");
            Require(source.GetShield(1) == 100f && source.GetShield(5) == 200f && source.GetShield(10) == 325f,
                "스킬 레벨별 실드량 100/200/325");
            Require(source.shieldBarSprite == AssetDatabase.LoadAssetAtPath<Sprite>(BubbleShieldSkillSetup.ShieldBarPath) &&
                source.shieldBarSprite != null && source.shieldBarSprite.rect.size == new Vector2(288, 48),
                "첨부한 288×48 실드 바 이미지 연결");
            Require(AssetDatabase.LoadAssetAtPath<SkillDatabaseSO>(
                "Assets/Nytherion/Data/ScriptableObjects/Skill/SkillDatabaseSO.asset").GetSkillById(source.skillID) == source,
                "저장 ID 복원용 DB 등록");
            BubbleShieldSkillData data = Object.Instantiate(source);
            data.duration = 2f;
            data.coolDown = 0f;

            GameObject owner = new GameObject("Shield Verification Player");
            SpriteRenderer playerSprite = owner.AddComponent<SpriteRenderer>();
            playerSprite.sprite = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/Gameplay/Characters/Player/Player.prefab").GetComponent<SpriteRenderer>().sprite;
            PlayerHealth health = owner.AddComponent<PlayerHealth>();
            health.InitializeHealth(100f);
            PlayerManager manager = owner.AddComponent<PlayerManager>();
            SkillDataManager levels = new GameObject("Shield Levels").AddComponent<SkillDataManager>();
            manager.Construct(null, null, null, null, levels);
            levels.skillStates[data.skillID] = new SkillState { level = 1 };
            BubbleShieldSkill skill = Object.Instantiate(data.skillPrefab, owner.transform).GetComponent<BubbleShieldSkill>();
            skill.skillData = data;
            skill.caster = owner.transform;

            Canvas canvas = new GameObject("Shield HUD", typeof(RectTransform), typeof(Canvas)).GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            Image fill = new GameObject("HP Fill", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            fill.transform.SetParent(canvas.transform, false);
            fill.type = Image.Type.Filled;
            fill.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.color = new Color(0.75f, 0.1f, 0.18f);
            fill.rectTransform.sizeDelta = new Vector2(300, 24);
            fill.rectTransform.anchoredPosition = new Vector2(0, 140);
            TextMeshProUGUI label = new GameObject("HP Text", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            label.transform.SetParent(canvas.transform, false);
            label.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Nytherion/Art/UI/Fonts/Galmuri/Galmuri11 SDF.asset");
            label.fontSize = 24;
            label.alignment = TextAlignmentOptions.Center;
            label.rectTransform.sizeDelta = new Vector2(300, 40);
            label.rectTransform.anchoredPosition = new Vector2(0, 175);
            HPBarUI hud = canvas.gameObject.AddComponent<HPBarUI>();
            Set(hud, "hpFillImage", fill);
            Set(hud, "hpText", label);
            hud.Construct(health);

            Require(skill.TryUse() && skill.Phase == BubbleShieldSkill.ShieldPhase.Summon && health.CurrentShield == 100f,
                "생성 첫 프레임부터 실드 즉시 활성화");
            Require(!skill.TryUse(), "활성 실드 중 중복 시전 차단");
            health.SetInvulnerable(true);
            health.TakeDamage(10f);
            Require(health.CurrentShield == 100f && health.CurrentHealth == 100f, "무적 중 체력 및 실드 미소모");
            health.SetInvulnerable(false);
            health.TakeDamage(40f);
            Image shieldFill = Get<Image>(hud, "shieldFillImage");
            Require(health.CurrentHealth == 100f && health.CurrentShield == 60f &&
                Mathf.Approximately(shieldFill.fillAmount, 0.6f) && shieldFill.sprite == source.shieldBarSprite && label.text == "60",
                "생성 중 피해 흡수 및 첨부 실드 바·실드 수치만 표시");
            yield return 0.7f;
            Require(skill.Phase == BubbleShieldSkill.ShieldPhase.Sustained &&
                skill.GetComponentInChildren<SpriteRenderer>().sprite == data.shieldSprite,
                "생성 애니메이션 종료 후 BubbleShield 유지");
            owner.transform.position = new Vector3(1f, 0.5f, 0f);
            owner.transform.localScale = new Vector3(-1.2f, 0.8f, 1f);
            yield return 0.05f;
            SpriteRenderer visual = skill.GetComponentInChildren<SpriteRenderer>();
            Require(Vector3.Distance(visual.bounds.center, playerSprite.bounds.center) < 0.01f &&
                Mathf.Abs(visual.bounds.size.x - visual.bounds.size.y) < 0.01f,
                "이동·반전·비균등 확대 중 플레이어 중심 추적 및 구형 유지");
            Capture(owner.transform.position, canvas, "shield-active.png");
            health.TakeDamage(60f);
            Require(health.CurrentHealth == 100f && health.CurrentShield == 0f &&
                skill.Phase == BubbleShieldSkill.ShieldPhase.Crash && visual.sprite == data.crashFrames[0] &&
                !shieldFill.gameObject.activeSelf && label.text == "100",
                "정확한 실드 소진 시 체력 보존·파괴 연출·체력 수치 복귀");
            yield return 0.4f;
            Require(skill.Phase == BubbleShieldSkill.ShieldPhase.Inactive && !visual.enabled,
                "파괴 4프레임 완료 후 표시 종료");
            levels.skillStates[data.skillID].level = 5;
            Require(skill.TryUse() && health.CurrentShield == 200f, "실제 PlayerManager의 스킬 레벨 5 적용");
            health.TakeDamage(225f);
            Require(health.CurrentHealth == 75f && health.CurrentShield == 0f && fill.fillAmount == 0.75f &&
                !shieldFill.gameObject.activeSelf && label.text == "75",
                "실드 200에 피해 225이면 체력 25만 차감하고 체력 75 표시");
            Capture(owner.transform.position, canvas, "shield-bar-broken.png");
            yield return 0.4f;
            data.duration = 0.15f;
            Require(skill.TryUse(), "파괴 완료 후 재사용");
            yield return 0.7f;
            Require(health.CurrentShield == 200f, "지속시간은 생성 완료 후부터 계산");
            yield return 0.18f;
            Require(health.CurrentShield == 0f && skill.Phase == BubbleShieldSkill.ShieldPhase.Crash && label.text == "75",
                "지속시간 만료 시 실드 제거·체력 수치 복귀 및 파괴 연출");
            yield return 0.4f;
            Require(skill.TryUse(), "지속시간 만료 후 재시전");
            skill.gameObject.SetActive(false);
            Require(health.CurrentShield == 0f && !visual.enabled, "장착 해제 시 잔여 실드·연출 정리");
            skill.gameObject.SetActive(true);
            skill.TryUse();
            health.InitializeHealth(100f);
            Require(health.CurrentShield == 0f && health.MaxShield == 0f, "체력 재초기화 시 이전 실드 제거");
            skill.gameObject.SetActive(false);
            skill.gameObject.SetActive(true);
            data.coolDown = 20f;
            skill.RestoreCooldownState(new SkillBase.CooldownState());
            Require(skill.TryUse() && skill.GetRemainingCooldown() > 19f, "기존 스킬 쿨타임 시스템 연결");
            SkillBase.CooldownState cooldown = skill.CaptureCooldownState();
            skill.gameObject.SetActive(false);
            BubbleShieldSkill restored = Object.Instantiate(data.skillPrefab).GetComponent<BubbleShieldSkill>();
            restored.skillData = data;
            restored.caster = owner.transform;
            restored.RestoreCooldownState(cooldown);
            Require(!restored.TryUse() && restored.GetRemainingCooldown() > 19f,
                "장착 해제·재장착 시 쿨타임 보존");
            health.ApplyShield(20f, restored);
            owner.SetActive(false);
            Require(health.CurrentShield == 0f, "시전자 비활성화 시 실드 제거");
            Object.Destroy(data);
        }

        private static void Capture(Vector3 center, Canvas canvas, string name, float orthographicSize = 3f)
        {
            Camera camera = new GameObject("Shield Preview Camera").AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = orthographicSize;
            camera.transform.position = center + Vector3.back * 10f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.055f, 0.08f, 0.12f);
            canvas.worldCamera = camera;
            canvas.planeDistance = 5f;
            RenderTexture target = new RenderTexture(800, 450, 24);
            RenderTexture previous = RenderTexture.active;
            Texture2D image = new Texture2D(800, 450, TextureFormat.RGB24, false);
            try
            {
                Canvas.ForceUpdateCanvases();
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 800, 450), 0, 0);
                image.Apply();
                File.WriteAllBytes(Output + "/" + name, image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previous;
                target.Release();
                Object.Destroy(target);
                Object.Destroy(image);
                Object.Destroy(camera.gameObject);
            }
        }

        private static void Set(object target, string field, object value) => target.GetType()
            .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        private static T Get<T>(object target, string field) => (T)target.GetType()
            .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
        private static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); checks.Add("PASS " + message); }
        private static void Finish(Exception error)
        {
            routine = null;
            SessionState.SetBool(Key, false);
            bool gameScene = SessionState.GetBool(Key + ".GameScene", false);
            bool hit = SessionState.GetBool(Key + ".Hit", false);
            SessionState.SetBool(Key + ".Hit", false);
            SessionState.SetBool(Key + ".GameScene", false);
            if (error != null) checks.Add("FAIL " + error);
            Directory.CreateDirectory(Output);
            File.WriteAllLines(Output + (hit ? "/hit-verification.txt" :
                gameScene ? "/game-scene-verification.txt" : "/verification.txt"), checks);
            EditorApplication.isPlaying = false;
            EditorApplication.Exit(error == null ? 0 : 1);
        }
    }
}
