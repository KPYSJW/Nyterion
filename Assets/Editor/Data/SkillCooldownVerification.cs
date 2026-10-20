using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Nytherion.Core.Managers;
using Nytherion.Data.ScriptableObjects.Skill;
using Nytherion.GamePlay.Characters.Player;
using Nytherion.GamePlay.Skills;
using Nytherion.UI.Skill;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Nytherion.Editor
{
    /// <summary>현재 씬을 저장하지 않고, 분리된 세이브 경로에서 실제 장착 이벤트와 HUD를 검증한다.</summary>
    [InitializeOnLoad]
    public static class SkillCooldownVerification
    {
        private const string Key = "Nytherion.SkillCooldownVerification";
        private const string Output = SkillCooldownHUDSetup.Output;
        private static readonly List<Object> temporary = new List<Object>();
        private static IEnumerator routine;
        private static double nextAt;
        private static double deadline;
        private static SkillDataManager dataManager;
        private static SkillData[] originalEquipped;
        private static SkillData[] originalStorage;
        private static SkillCooldownHUD hud;
        private static PlayerSkillManager player;
        private static SkillUIController controller;

        static SkillCooldownVerification()
        {
            EditorApplication.update += Update;
            EditorApplication.playModeStateChanged += state =>
            {
                if (!SessionState.GetBool(Key, false)) return;
                if (state == PlayModeStateChange.EnteredPlayMode)
                {
                    Application.runInBackground = true;
                    nextAt = EditorApplication.timeSinceStartup + 6d;
                    deadline = EditorApplication.timeSinceStartup + 120d;
                }
                if (state == PlayModeStateChange.EnteredEditMode)
                {
                    PlayerSettings.productName = SessionState.GetString(Key + ".Product", PlayerSettings.productName);
                    ConfigureCaptureResolution(false);
                    SessionState.SetBool(Key, false);
                    File.AppendAllText(Output + "/verification.txt", "PASS 원래 제품명·세이브 경로 복원, 씬 미저장 변경 유지\n");
                }
            };
        }

        private static void Update()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (!EditorApplication.isPlayingOrWillChangePlaymode && File.Exists(Output + "/restore-view.request"))
            {
                File.Delete(Output + "/restore-view.request");
                ConfigureCaptureResolution(false);
                Pass("Game View 원래 해상도 복원 및 임시 캡처 해상도 제거");
                return;
            }
            if (File.Exists(Output + "/verify.request") && !SessionState.GetBool(Key, false))
            {
                File.Delete(Output + "/verify.request");
                if (EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    File.WriteAllText(Output + "/verification.txt", "NOT_RUN 편집 모드에서 실행해 주세요.");
                    return;
                }
                SessionState.SetString(Key + ".Product", PlayerSettings.productName);
                ConfigureCaptureResolution(true);
                SessionState.SetBool(Key, true);
                PlayerSettings.productName += "_SkillCooldownVerification_" + Guid.NewGuid().ToString("N").Substring(0, 8);
                File.WriteAllText(Output + "/verification.txt", "RUNNING 격리 세이브: " + Application.persistentDataPath + "\n");
                EditorApplication.isPlaying = true;
                return;
            }
            if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || nextAt == 0d ||
                EditorApplication.timeSinceStartup < nextAt) return;
            try
            {
                Require(EditorApplication.timeSinceStartup < deadline, "검증 시간 초과");
                if (routine == null) routine = Verify();
                if (!routine.MoveNext()) { Finish(null); return; }
                nextAt = EditorApplication.timeSinceStartup + (float)routine.Current;
            }
            catch (Exception error) { Finish(error); }
        }

        private static IEnumerator Verify()
        {
            Require(Application.persistentDataPath.Contains("_SkillCooldownVerification_"), "사용자 저장 파일과 격리");
            controller = Object.FindObjectOfType<SkillUIController>();
            player = Object.FindObjectOfType<PlayerSkillManager>();
            dataManager = Object.FindObjectOfType<SkillDataManager>();
            hud = Object.FindObjectOfType<SkillCooldownHUD>();
            Require(controller != null && controller.IsInitialized && player != null && dataManager != null && hud != null,
                "실제 씬 DI 초기화·HUD 프리팹 생성");
            Require(Object.FindObjectsOfType<SkillCooldownHUD>().Length == 1, "HUD 단일 인스턴스");
            originalEquipped = (SkillData[])dataManager.equippedSkills.Clone();
            originalStorage = (SkillData[])dataManager.storageSkills.Clone();

            var template = Track(new GameObject("쿨타임 검증 프리팹"));
            template.AddComponent<TestSkill>();
            SkillData a = Track(ScriptableObject.CreateInstance<SkillData>());
            a.skillID = "verify-cooldown-a";
            a.skillPrefab = template;
            a.coolDown = 1.2f;
            a.icon = AssetDatabase.LoadAssetAtPath<SkillData>("Assets/Nytherion/Data/ScriptableObjects/Skill/PortalGate_Skill.asset").icon;
            SkillData b = Track(Object.Instantiate(a));
            b.skillID = "verify-cooldown-b";
            b.icon = AssetDatabase.LoadAssetAtPath<SkillData>("Assets/Nytherion/Data/ScriptableObjects/Skill/ProjectileGate_Skill.asset").icon;
            var turret = Track(Object.Instantiate(AssetDatabase.LoadAssetAtPath<TurretSkillData>(
                "Assets/Nytherion/Data/ScriptableObjects/Skill/Turret_Skill.asset")));
            turret.skillID = "verify-turret";
            turret.coolDown = 1.5f;
            turret.maxCharges = 2;
            turret.duration = 0.1f;

            Apply(a, b, turret);
            SkillBase first = player.equippedSkills[0];
            Require(first.TryUse() && player.equippedSkills[1].TryUse(), "기본 스킬 사용");
            hud.Refresh();
            Require(Slots()[0].cooldownOverlay.enabled && Slots()[0].cooldownText.enabled &&
                Slots()[0].cooldownOverlay.fillAmount > 0.9f, "쿨타임 덮개·남은 초 표시");
            controller.Open(false);
            controller.Close();
            controller.Open(false);
            controller.Close();
            controller.SyncUIFromData();
            Apply(a, b, turret);
            Require(ReferenceEquals(first, player.equippedSkills[0]) && !first.TryUse(), "창 열기·표시 갱신·같은 데이터 이벤트로 초기화되지 않음");
            Pass("스킬창 반복 열기와 데이터 갱신 후 동일 인스턴스·쿨타임 유지");

            float before = first.GetRemainingCooldown();
            Apply(b, a, turret);
            Require(!player.equippedSkills[1].TryUse() && player.equippedSkills[1].GetRemainingCooldown() > before - 0.1f,
                "슬롯 맞교환 후 각 스킬 쿨타임 유지");
            Apply(b, null, turret);
            yield return 0.25f;
            Apply(b, a, turret);
            float after = player.equippedSkills[1].GetRemainingCooldown();
            Require(after > 0f && after < before - 0.1f && !player.equippedSkills[1].TryUse(), "보관 중 시간 경과·재장착 재사용 차단");
            for (int i = 0; i < 4; i++) { Apply(b, null, turret); Apply(b, a, turret); }
            Require(!player.equippedSkills[1].TryUse(), "반복 해제·장착으로 쿨타임 우회 불가");
            SkillData alias = Track(Object.Instantiate(a));
            Apply(b, alias, turret);
            Require(!player.equippedSkills[1].TryUse(), "같은 스킬 ID의 다른 데이터 인스턴스로 교체해도 쿨타임 유지");
            Pass("슬롯 맞교환·해제·재장착·반복 교체 방지 및 보관 중 쿨타임 경과");

            Apply(a, a, turret);
            Require(player.equippedSkills[1] == null, "동일 ID 중복 슬롯으로 쿨타임 우회 불가");
            Apply(a, b, turret);
            hud.Refresh();
            float paused = player.equippedSkills[0].GetRemainingCooldown();
            Time.timeScale = 0f;
            yield return 0.2f;
            Require(Mathf.Abs(paused - player.equippedSkills[0].GetRemainingCooldown()) < 0.05f, "일시정지 중 쿨타임 정지");
            Time.timeScale = 1f;
            Pass("동일 ID 중복 장착 차단·일시정지 시간 일치");
            yield return 1.25f;
            hud.Refresh();
            Require(player.equippedSkills[0].CanUse() && !Slots()[0].cooldownOverlay.enabled && !Slots()[0].cooldownText.enabled,
                "실제 시간 경과 후 사용 가능·덮개/숫자 숨김");
            // 완료 연출은 전환 프레임에 확인한다.
            player.equippedSkills[0].RestoreCooldownState(new SkillBase.CooldownState { LastUseTime = Time.time });
            hud.Refresh();
            player.equippedSkills[0].RestoreCooldownState(new SkillBase.CooldownState { LastUseTime = Time.time - a.coolDown - 0.05f });
            hud.Refresh();
            Require(Slots()[0].readyFlash.enabled, "쿨타임 완료 테두리 반짝임");
            yield return 0.25f;
            hud.Refresh();
            Require(!Slots()[0].readyFlash.enabled, "완료 반짝임 종료");
            Pass("쿨타임 종료·재사용 허용·숫자/덮개 숨김·완료 반짝임");

            TurretSkill turretSkill = (TurretSkill)player.equippedSkills[2];
            Require(turretSkill.CurrentCharges == 2 && turretSkill.TryUse() && turretSkill.CurrentCharges == 1, "포탑 실제 사용·충전 차감");
            Apply(a, b, null);
            Apply(a, b, turret);
            turretSkill = (TurretSkill)player.equippedSkills[2];
            Require(turretSkill.CurrentCharges == 1 && !turretSkill.TryUse(), "충전이 남아 있어도 재장착으로 최소 소환 간격 우회 불가");
            yield return 0.55f;
            hud.Refresh();
            Require(turretSkill.CanUse() && !Slots()[2].cooldownOverlay.enabled && Slots()[2].chargeText.text == "1" &&
                Slots()[2].rechargeTrack.activeSelf && Slots()[2].rechargeFill.fillAmount > 0f,
                "충전이 남으면 사용 가능, 별도 충전 수·회복 게이지");
            Require(turretSkill.TryUse() && turretSkill.CurrentCharges == 0, "포탑 충전 소진");
            float recharge = turretSkill.RemainingRechargeTime;
            hud.Refresh();
            Require(Slots()[2].cooldownOverlay.enabled && Slots()[2].chargeText.text == "0", "소진 시 덮개와 충전 수 0 표시");
            Apply(a, b, null);
            yield return 0.2f;
            Apply(a, b, turret);
            turretSkill = (TurretSkill)player.equippedSkills[2];
            Require(turretSkill.CurrentCharges == 0 && turretSkill.RemainingRechargeTime < recharge && !turretSkill.TryUse(),
                "포탑 재장착 후 충전 0·원래 충전 시점·재사용 대기 유지");
            for (int i = 0; i < 4; i++) { Apply(a, b, null); Apply(a, b, turret); }
            Require(((TurretSkill)player.equippedSkills[2]).CurrentCharges == 0, "포탑 반복 교체로 충전 복구 불가");
            Pass("포탑 충전 수·회복 게이지·소진 표시·반복 재장착 후 충전과 소환 간격 유지");
            yield return 1.4f;
            hud.Refresh();
            turretSkill = (TurretSkill)player.equippedSkills[2];
            Require(turretSkill.CurrentCharges == 1 && turretSkill.CanUse(), "보관·재장착 후 원래 시각에 충전 1개 복구");
            Apply(a, b, null);
            yield return 1.6f;
            Apply(a, b, turret);
            Require(((TurretSkill)player.equippedSkills[2]).CurrentCharges == 2, "보관 중 여러 충전 회복");
            Pass("포탑 보관 중 순차 충전 회복·최대 충전 상한 유지");

            a.coolDown = 8f;
            b.coolDown = 12f;
            turret.coolDown = 10f;
            player.equippedSkills[0].RestoreCooldownState(new SkillBase.CooldownState { LastUseTime = Time.time - 3f });
            player.equippedSkills[1].RestoreCooldownState(new SkillBase.CooldownState { LastUseTime = Time.time - 2f });
            Require(((TurretSkill)player.equippedSkills[2]).TryUse(), "시각 검증 포탑 충전 사용");
            hud.Refresh();
            yield return 0.6f;
            ScreenCapture.CaptureScreenshot(Output + "/hud.png");
            yield return 0.5f;
            Pass("실제 GameScene HUD 화면 저장: hud.png");
            Apply(null, null, null);
            hud.Refresh();
            foreach (SkillCooldownHUD.Slot slot in Slots())
                Require(!slot.cooldownOverlay.enabled && !slot.cooldownText.enabled && !slot.chargeText.enabled &&
                    !slot.rechargeTrack.activeSelf && !slot.readyFlash.enabled, "빈 슬롯의 모든 효과 정리");
            Pass("해제 후 빈 슬롯의 쿨타임·충전·완료 효과 정리");
        }

        private static void Apply(SkillData a, SkillData b, SkillData c) =>
            dataManager.UpdateSkills(new[] { a, b, c }, Array.Empty<SkillData>());
        private static SkillCooldownHUD.Slot[] Slots() => (SkillCooldownHUD.Slot[])typeof(SkillCooldownHUD)
            .GetField("slots", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(hud);
        private static T Track<T>(T value) where T : Object { temporary.Add(value); return value; }
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        private static void Pass(string message) => File.AppendAllText(Output + "/verification.txt", "PASS " + message + "\n");

        private static void ConfigureCaptureResolution(bool enable)
        {
            // 작은 도킹 영역에서도 실제 1920×1080 렌더 결과를 확인하고 원래 설정을 복원한다.
            Assembly assembly = typeof(EditorWindow).Assembly;
            Type viewType = assembly.GetType("UnityEditor.GameView");
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            Object[] views = Resources.FindObjectsOfTypeAll(viewType);
            if (views.Length == 0) throw new InvalidOperationException("캡처할 Game View가 없습니다.");
            var view = (EditorWindow)views[0];
            PropertyInfo selected = viewType.GetProperty("selectedSizeIndex", flags);
            Type sizesType = assembly.GetType("UnityEditor.GameViewSizes");
            object sizes = typeof(ScriptableSingleton<>).MakeGenericType(sizesType).GetProperty("instance").GetValue(null);
            Type groupType = assembly.GetType("UnityEditor.GameViewSizeGroupType");
            object group = sizesType.GetMethod("GetGroup", flags).Invoke(sizes, new[] { Enum.Parse(groupType, "Standalone") });
            Type groupClass = group.GetType();
            if (enable)
            {
                SessionState.SetInt(Key + ".Resolution", (int)selected.GetValue(view));
                Type sizeType = assembly.GetType("UnityEditor.GameViewSize");
                Type sizeKind = assembly.GetType("UnityEditor.GameViewSizeType");
                object size = Activator.CreateInstance(sizeType, flags, null,
                    new[] { Enum.Parse(sizeKind, "FixedResolution"), (object)1920, 1080, "SkillCooldownVerification" }, null);
                groupClass.GetMethod("AddCustomSize", flags).Invoke(group, new[] { size });
                int index = (int)groupClass.GetMethod("GetTotalCount", flags).Invoke(group, null) - 1;
                SessionState.SetInt(Key + ".CaptureResolution", index);
                selected.SetValue(view, index);
            }
            else
            {
                selected.SetValue(view, SessionState.GetInt(Key + ".Resolution", 0));
                int index = SessionState.GetInt(Key + ".CaptureResolution", -1);
                int builtin = (int)groupClass.GetMethod("GetBuiltinCount", flags).Invoke(group, null);
                if (index >= builtin)
                {
                    object size = groupClass.GetMethod("GetGameViewSize", flags).Invoke(group, new object[] { index });
                    if ((string)size.GetType().GetProperty("baseText", flags).GetValue(size) == "SkillCooldownVerification")
                        groupClass.GetMethod("RemoveCustomSize", flags).Invoke(group, new object[] { index });
                }
            }
        }

        private static void Finish(Exception error)
        {
            routine = null;
            nextAt = 0d;
            Time.timeScale = 1f;
            if (error != null) File.AppendAllText(Output + "/verification.txt", "FAIL " + error + "\n");
            if (controller != null) controller.Close();
            if (dataManager != null && originalEquipped != null) dataManager.UpdateSkills(originalEquipped, originalStorage);
            foreach (Object item in temporary) if (item != null) Object.Destroy(item);
            temporary.Clear();
            originalEquipped = null;
            EditorApplication.isPlaying = false;
        }
    }
}
