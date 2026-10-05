using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Nytherion.GamePlay.Characters.Player;
using Nytherion.GamePlay.Combat;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Nytherion.Editor
{
    /// <summary>실제 플레이어 애니메이션 컨트롤러로 전투 중 정지 자세와 재생 복구를 확인합니다.</summary>
    [InitializeOnLoad]
    public static class PlayerCombatIdleVerification
    {
        private const string Output = "output/combat-idle";
        private const string Pending = "Nytherion.PlayerCombatIdle.Verify";
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static double readyAt;

        static PlayerCombatIdleVerification() => EditorApplication.update += Update;

        [MenuItem("Tools/Nytherion/Player/Verify Combat Idle In Play Mode")]
        public static void Start()
        {
            SessionState.SetBool(Pending + ".Exit", !EditorApplication.isPlaying);
            SessionState.SetBool(Pending, true);
            readyAt = EditorApplication.timeSinceStartup + 3d;
            if (!EditorApplication.isPlaying) EditorApplication.isPlaying = true;
        }

        private static void Update()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (File.Exists(Output + "/verify.request"))
            {
                File.Delete(Output + "/verify.request");
                Start();
            }
            if (!SessionState.GetBool(Pending, false) || !EditorApplication.isPlaying) return;
            if (readyAt == 0d) readyAt = EditorApplication.timeSinceStartup + 3d;
            if (EditorApplication.timeSinceStartup < readyAt) return;

            SessionState.SetBool(Pending, false);
            Directory.CreateDirectory(Output);
            try
            {
                File.WriteAllText(Output + "/verification.txt", Verify());
            }
            catch (Exception error)
            {
                File.WriteAllText(Output + "/verification.txt", "FAIL " + error);
                Debug.LogException(error);
            }
            finally
            {
                if (SessionState.GetBool(Pending + ".Exit", false)) EditorApplication.isPlaying = false;
                readyAt = 0d;
            }
        }

        private static string Verify()
        {
            var results = new List<string>
            {
                "Unity " + Application.unityVersion + " / " + SceneManager.GetActiveScene().name + " / PlayMode",
                "시간 경계는 마지막 전투 시각을 조정하고 실제 Animator.Update로 프레임을 확인합니다."
            };
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/Gameplay/Characters/Player/Player.prefab");
            Require(prefab != null && prefab.GetComponent<PlayerController>() != null,
                "플레이어 프리팹의 PlayerController 참조");
            Require(Mathf.Approximately(new SerializedObject(prefab.GetComponent<PlayerController>())
                .FindProperty("idleAnimationResumeDelay").floatValue, 3f), "프리팹 기본 대기 시간 3초");
            VerifyController(prefab.GetComponent<Animator>().runtimeAnimatorController,
                new[] { "Idle" }, "기본 플레이어", results);
            VerifyController(AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                "Assets/Nytherion/Art/Characters/Player/Animations/DirectionalTest2/PlayerDirectionalTest2.controller"),
                new[] { "Idle_Up", "Idle_Right", "Idle_Down", "Idle_Left" }, "방향별 플레이어", results);
            return string.Join("\n", results);
        }

        private static void VerifyController(RuntimeAnimatorController controller, string[] idleNames,
            string label, List<string> results)
        {
            Require(controller != null, label + " 컨트롤러 참조");
            GameObject root = new GameObject("[검증] 전투 대기 자세");
            root.transform.position = new Vector3(20000f, 20000f);
            try
            {
                SpriteRenderer renderer = root.AddComponent<SpriteRenderer>();
                Animator animator = root.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.Rebind();
                animator.Update(0f);
                PlayerController player = root.AddComponent<PlayerController>();
                player.enabled = false;
                Field(player, "animator", animator);
                PlayerCombat combat = root.AddComponent<PlayerCombat>();
                combat.enabled = false;
                Field(player, "playerCombat", combat);
                PlayerHealth health = root.AddComponent<PlayerHealth>();
                health.InitializeHealth(100f);

                foreach (string idle in idleNames)
                {
                    Field(player, "lastCombatActivityTime", float.NegativeInfinity);
                    player.PlayAnimation(idle);
                    animator.Update(0f);
                    animator.Update(0.5f);
                    Require(animator.speed > 0f && animator.GetCurrentAnimatorStateInfo(0).normalizedTime > 0f,
                        "비전투 idle 재생: " + label + "/" + idle + " speed=" + animator.speed +
                        " time=" + animator.GetCurrentAnimatorStateInfo(0).normalizedTime);

                    player.NotifyCombatActivity();
                    Invoke(player, "LateUpdate");
                    Sprite pose = renderer.sprite;
                    Require(pose != null && animator.speed == 0f, "전투 중 정지 이미지");
                    animator.Update(1f);
                    Require(renderer.sprite == pose && Mathf.Approximately(
                        animator.GetCurrentAnimatorStateInfo(0).normalizedTime, 0f), "첫 프레임 고정");

                    Field(player, "lastCombatActivityTime", Time.time - 2.9f);
                    Invoke(player, "LateUpdate");
                    Require(animator.speed == 0f, "3초 전 idle 억제");
                    Field(player, "lastCombatActivityTime", Time.time - 3.1f);
                    Invoke(player, "LateUpdate");
                    animator.Update(0.5f);
                    Require(animator.speed > 0f && animator.GetCurrentAnimatorStateInfo(0).normalizedTime > 0f,
                        "3초 후 idle 재개");
                }
                results.Add("PASS " + label + " " + idleNames.Length + "개 idle: 정지 이미지·시간 경계·재생 복구");

                player.NotifyCombatActivity();
                player.PlayAnimation(idleNames[0]);
                player.PlayAnimation("Walk");
                animator.Update(0.25f);
                Require(animator.speed == 1f && animator.GetCurrentAnimatorStateInfo(0).IsName("Walk"),
                    "전투 중 이동 애니메이션");
                if (animator.HasState(0, Animator.StringToHash("Dash")))
                {
                    player.PlayAnimation(idleNames[0]);
                    player.PlayAnimation("Dash");
                    animator.Update(0.05f);
                    Require(animator.speed == 1f && animator.GetCurrentAnimatorStateInfo(0).IsName("Dash"),
                        "전투 중 대시 애니메이션");
                }
                results.Add("PASS " + label +
                    (animator.HasState(0, Animator.StringToHash("Dash")) ? " 이동·대시" : " 이동") +
                    " 재생 속도 복구");

                WeaponBase weapon = root.AddComponent<LaserWeapon>();
                weapon.enabled = false;
                combat.currentWeapon = weapon;
                Field(combat, "isAttackHeld", true);
                Field(player, "lastCombatActivityTime", Time.time - 10f);
                Field(player, "idleAnimationResumeDelay", 0f);
                player.PlayAnimation(idleNames[0]);
                Invoke(player, "LateUpdate");
                Require(combat.IsAttackActive && animator.speed == 0f,
                    "공격 유지 중에는 대기 시간이 0이어도 idle 억제");
                Field(combat, "isAttackHeld", false);
                Invoke(player, "LateUpdate");
                Require(animator.speed == 1f, "대기 시간 0에서 공격 종료 즉시 복구");
                Field(player, "idleAnimationResumeDelay", 3f);
                health.TakeDamage(1f);
                Invoke(player, "LateUpdate");
                Require(animator.speed == 0f, "피격 시 전투 대기 갱신");
                Invoke(player, "OnDisable");
                Require(animator.speed == 1f, "비활성화 시 재생 속도 복구");
                results.Add("PASS " + label + " 공격 유지·0초 설정·피격·비활성화 복구");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void Field(object target, string name, object value) =>
            target.GetType().GetField(name, Private).SetValue(target, value);

        private static void Invoke(object target, string name) =>
            target.GetType().GetMethod(name, Private).Invoke(target, null);

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
