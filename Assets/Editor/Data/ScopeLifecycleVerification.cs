using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Nytherion.Core.Managers;
using Nytherion.Core.Systems;
using Nytherion.UI.Controllers;
using Nytherion.UI.Inventory;
using Nytherion.UI.Presenters;
using Nytherion.UI.RelicBoard;
using Nytherion.UI.Progression;
using Nytherion.UI.Skill;
using Nytherion.UI.Title;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer;
using VContainer.Internal;
using VContainer.Unity;
using Object = UnityEngine.Object;

namespace Nytherion.Editor
{
    /// <summary> 사용자 세이브와 분리한 경로에서 실제 씬 진입·재진입과 스코프 수명을 검증한다. </summary>
    [InitializeOnLoad]
    public static class ScopeLifecycleVerification
    {
        private const string Key = "Nytherion.ScopeLifecycleVerification.";
        private const string Output = "output/scope-lifecycle";
        private static readonly Type[] ManagerTypes =
        {
            typeof(SaveLoadManager), typeof(CurrencyDataManager), typeof(InventoryDataManager),
            typeof(RelicManager), typeof(EquipmentDataManager), typeof(ShopManager),
            typeof(StageManager), typeof(SkillDataManager), typeof(ProgressionManager)
        };

        [Serializable] private sealed class SceneBackup { public SceneSetup[] scenes; }

        static ScopeLifecycleVerification()
        {
            EditorApplication.update += Update;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            Application.logMessageReceived += CaptureError;
        }

        private static string Result => Output + "/" + SessionState.GetString(Key + "Route", "Title") + ".txt";

        private static void Update()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (!EditorApplication.isPlayingOrWillChangePlaymode && File.Exists(Output + "/reset.request"))
            {
                File.Delete(Output + "/reset.request");
                if (SessionState.GetBool(Key + "Pending", false)) RestoreEditor();
                return;
            }
            if (!SessionState.GetBool(Key + "Pending", false))
            {
                foreach (string route in new[] { "Title", "GameScene", "Boot", "Attack", "WeaponRestore" })
                {
                    string request = Output + "/" + route + ".request";
                    if (!File.Exists(request)) continue;
                    File.Delete(request);
                    Begin(route);
                    return;
                }
                return;
            }
            if (SessionState.GetBool(Key + "RestartPending", false) && !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                if (EditorApplication.timeSinceStartup < SessionState.GetFloat(Key + "WaitUntil", 0)) return;
                SessionState.SetBool(Key + "RestartPending", false);
                SessionState.SetBool(Key + "Stopping", false);
                SessionState.SetFloat(Key + "Deadline", (float)EditorApplication.timeSinceStartup + 600f);
                Next(0, EditorApplication.timeSinceStartup + 5);
                EditorApplication.isPlaying = true;
                return;
            }
            if (!EditorApplication.isPlaying || SessionState.GetBool(Key + "Stopping", false)) return;
            try
            {
                double now = EditorApplication.timeSinceStartup;
                Require(now < SessionState.GetFloat(Key + "Deadline", 0), "씬 진입·재진입 시간 초과");
                Require(!SessionState.GetBool(Key + "Error", false), "플레이 오류 발생: errors.txt 확인");
                Require(Application.persistentDataPath == SessionState.GetString(Key + "TestSavePath", ""), "검증 세이브 경로 변경");
                if (now < SessionState.GetFloat(Key + "WaitUntil", 0)) return;
                var transition = RootLifetimeScope.Instance?.Container?.Resolve<SceneTransitionManager>();
                if (transition != null && (bool)typeof(SceneTransitionManager).GetField("isTransitioning",
                    BindingFlags.Instance | BindingFlags.NonPublic).GetValue(transition)) return;

                int phase = SessionState.GetInt(Key + "Phase", 0);
                string scene = SceneManager.GetActiveScene().name;
                if (phase == 0 && scene == "Title")
                {
                    if (!DataReady()) return;
                    RecordManagerIds();
                    Require(Object.FindObjectOfType<TitleLifetimeScope>().Parent == RootLifetimeScope.Instance, "Title의 Root 부모");
                    Pass("Title 진입, Root → Data 부모와 9개 매니저 초기화·로드 완료");
                    Object.FindObjectOfType<TitleMenuManager>().OnStartGame();
                    Next(1, now + 5);
                }
                else if ((phase == 0 || phase == 1 || phase == 4) && scene == "GameScene")
                {
                    if (!DataReady()) return;
                    if (phase == 0) RecordManagerIds();
                    ValidateGameScope();
                    if (SessionState.GetString(Key + "Route", "") == "WeaponRestore")
                    {
                        int run = SessionState.GetInt(Key + "RestartRun", 0);
                        PlayerEquipmentRestoreVerification.VerifyLoaded();
                        Pass(run == 2 ? "해제 상태 저장·재시작 후 장착 UI·캐릭터 모두 무기 없음" :
                            "재시작 " + run + ": 저장 무기·등급·인스턴스와 장착 UI·캐릭터 일치, 재장착 없이 공격 및 재활성화 검증");
                        if (run < 2)
                        {
                            PlayerEquipmentRestoreVerification.PrepareNextSave(run + 1);
                            Pass(run == 0 ? "다른 무기 착용 후 실제 JSON 저장" : "무기 해제 후 실제 JSON 저장");
                            SessionState.SetInt(Key + "RestartRun", run + 1);
                            SessionState.SetBool(Key + "RestartPending", true);
                            SessionState.SetBool(Key + "Stopping", true);
                            EditorApplication.isPlaying = false;
                        }
                        else Finish(null);
                        return;
                    }
                    var relicUI = Object.FindObjectOfType<RelicUIController>();
                    Require(relicUI != null, "유물 UI 생성");
                    relicUI.Open(false);
                    Next(phase == 4 ? 5 : 2, now + 2);
                }
                else if ((phase == 2 || phase == 5) && scene == "GameScene")
                {
                    ValidateRelicGrid();
                    Object.FindObjectOfType<RelicUIController>().Close();
                    Pass("GameScene 부모 상속·동일 매니저·초기화 목록·UI·유물 코루틴 검증");
                    string route = SessionState.GetString(Key + "Route", "");
                    if (route == "Attack" || route == "Title")
                    {
                        Require(PlayerAttackVerification.VerifyEquippedWeapon(), "장착 무기의 공격 입력·실제 투사체 검증");
                        Pass("실제 장비 변경 → 마우스 공격 입력 → 투사체 생성, UI 차단 해제 및 입력 재연결 검증 완료");
                    }
                    if (phase == 2 && SessionState.GetString(Key + "Route", "") == "Title")
                    {
                        RootLifetimeScope.Instance.Container.Resolve<SceneTransitionManager>().LoadScene("Title");
                        Next(3, now + 4);
                    }
                    else if (phase == 2 && SessionState.GetString(Key + "Route", "") == "Attack")
                    {
                        Finish(null);
                    }
                    else if (phase == 2 && SessionState.GetString(Key + "Route", "") == "GameScene")
                    {
                        Directory.CreateDirectory(RelicPresentationSetup.Output);
                        File.WriteAllText(RelicPresentationSetup.Output + "/verification.txt", "RUNNING\n");
                        File.WriteAllText(RelicPresentationSetup.Output + "/verify.pending", "run");
                        File.Move(RelicPresentationSetup.Output + "/verify.pending", RelicPresentationSetup.Output + "/verify.request");
                        Next(6, now + 5);
                    }
                    else Finish(null);
                }
                else if (phase == 3 && scene == "Title")
                {
                    Require(DataReady(), "씬을 나간 뒤 영속 데이터 유지");
                    ValidateManagerIds(DataLifetimeScope.Instance.Container);
                    Require(Object.FindObjectsOfType<InventoryPresenter>(true).Length == 0, "이전 씬 Presenter 제거");
                    Require(Object.FindObjectsOfType<GameSceneLifetimeScope>(true).Length == 0, "이전 게임 스코프 제거");
                    Pass("GameScene → Title에서 영속 매니저 보존, 씬 스코프와 Presenter 정리");
                    Object.FindObjectOfType<TitleMenuManager>().OnStartGame();
                    Next(4, now + 5);
                }
                else if (phase == 6)
                {
                    string result = File.ReadAllText(RelicPresentationSetup.Output + "/verification.txt");
                    if (result.StartsWith("RUNNING")) return;
                    Require(!result.Contains("FAIL") && result.Contains("PASS"), "유물 효과·투사체·저장·드래그·슬롯 표시 회귀 검증");
                    Pass("격리 유물 효과·표시 통합 회귀 검증 완료");
                    Finish(null);
                }
            }
            catch (Exception error) { Finish(error); }
        }

        [MenuItem("Tools/Nytherion/Scopes/Verify Title and Reentry")]
        public static void VerifyTitle() => Begin("Title");

        [MenuItem("Tools/Nytherion/Scopes/Verify Equipped Weapon Attacks")]
        public static void VerifyAttacks() => Begin("Attack");

        [MenuItem("Tools/Nytherion/Scopes/Verify Saved Weapon Restart")]
        public static void VerifySavedWeapon() => Begin("WeaponRestore");

        private static void Begin(string route)
        {
            if (SessionState.GetBool(Key + "Pending", false)) return;
            Directory.CreateDirectory(Output);
            SessionState.SetString(Key + "Route", route);
            if (EditorApplication.isPlayingOrWillChangePlaymode || Enumerable.Range(0, SceneManager.sceneCount)
                .Any(i => SceneManager.GetSceneAt(i).isDirty))
            {
                File.WriteAllText(Result, "NOT_RUN 플레이 모드를 종료하고 미저장 씬 변경을 처리한 뒤 다시 실행하세요.");
                return;
            }

            SessionState.SetString(Key + "Scenes", JsonUtility.ToJson(new SceneBackup { scenes = EditorSceneManager.GetSceneManagerSetup() }));
            SessionState.SetString(Key + "Product", PlayerSettings.productName);
            SessionState.SetString(Key + "OriginalSavePath", Application.persistentDataPath);
            SessionState.SetBool(Key + "Pending", true);
            SessionState.SetBool(Key + "Stopping", false);
            SessionState.SetBool(Key + "Error", false);
            SessionState.SetInt(Key + "RestartRun", 0);
            SessionState.SetBool(Key + "RestartPending", false);
            File.WriteAllText(Result, "RUNNING " + route + "\n");
            File.WriteAllText(Output + "/" + route + "-errors.txt", "");
            try
            {
                PlayerSettings.productName += "_ScopeVerification_" + Guid.NewGuid().ToString("N").Substring(0, 8);
                Require(Application.persistentDataPath != SessionState.GetString(Key + "OriginalSavePath", ""), "세이브 경로 격리 실패");
                SessionState.SetString(Key + "TestSavePath", Application.persistentDataPath);
                File.AppendAllText(Result, "격리 세이브: " + Application.persistentDataPath + "\n");
                if (route == "WeaponRestore") PlayerEquipmentRestoreVerification.Seed();
                EditorSceneManager.OpenScene("Assets/Scenes/" + (route == "Attack" ? "GameScene" : route == "WeaponRestore" ? "Title" : route) + ".unity", OpenSceneMode.Single);
                SessionState.SetFloat(Key + "Deadline", (float)EditorApplication.timeSinceStartup + 600f);
                Next(0, EditorApplication.timeSinceStartup + 5);
                EditorApplication.isPlaying = true;
            }
            catch (Exception error)
            {
                File.AppendAllText(Result, "FAIL " + error + "\n");
                RestoreEditor();
            }
        }

        private static bool DataReady()
        {
            RootLifetimeScope root = RootLifetimeScope.Instance;
            DataLifetimeScope data = DataLifetimeScope.Instance;
            return root != null && root.Container != null && data != null && data.Parent == root && data.IsDataManagersReady();
        }

        private static void RecordManagerIds()
        {
            foreach (Type type in ManagerTypes)
                SessionState.SetInt(Key + type.Name, ((BaseManager)DataLifetimeScope.Instance.Container.Resolve(type)).GetInstanceID());
        }

        private static void ValidateManagerIds(IObjectResolver resolver)
        {
            foreach (Type type in ManagerTypes)
            {
                var manager = (BaseManager)resolver.Resolve(type);
                Require(manager.GetInstanceID() == SessionState.GetInt(Key + type.Name, 0) && manager.IsInitialized, type.Name + " 동일 인스턴스·초기화");
                Require(Object.FindObjectsOfType(type, true).Length == 1, type.Name + " 중복 인스턴스 없음");
            }
        }

        private static void ValidateGameScope()
        {
            var scope = Object.FindObjectOfType<GameSceneLifetimeScope>();
            Require(scope != null && scope.Parent == DataLifetimeScope.Instance && scope.Container != null, "GameScene의 Data 부모");
            Require(Object.FindObjectsOfType<RootLifetimeScope>().Length == 1 && Object.FindObjectsOfType<DataLifetimeScope>().Length == 1,
                "Root/Data 각 하나");
            ValidateManagerIds(scope.Container);
            var initializables = scope.Container.Resolve<ContainerLocal<IReadOnlyList<IInitializable>>>().Value;
            Require(!initializables.Any(item => ManagerTypes.Contains(item.GetType())), "자식 스코프에서 데이터 초기화 진입점 제외");
            Require(Object.FindObjectsOfType<GameSceneUIManager>(true).Length == 0 && !scope.Container.TryResolve<GameSceneUIManager>(out _),
                "프리팹 에셋 UI 매니저 등록 제거");
            Require(Object.FindObjectsOfType<InventoryManager>(true).Length == 0, "레거시 인벤토리 없음");
            Require(scope.Container.Resolve<InventoryUI>().IsInitialized && scope.Container.Resolve<InventoryPresenter>().IsInitialized &&
                scope.Container.Resolve<InventoryUIController>().IsUIReady(), "인벤토리 초기화 완료");
            Require(Object.FindObjectsOfType<InventoryPresenter>(true).Length == 1, "Presenter 단일 인스턴스");
            var milestones = Object.FindObjectOfType<MilestoneUIController>();
            var skills = Object.FindObjectOfType<SkillUIController>();
            Require(milestones != null && milestones.IsInitialized && skills != null && skills.IsInitialized,
                "마일스톤·스킬 UI 자동 생성·초기화");
            Require(Object.FindObjectOfType<PlayerManager>() != null, "플레이어 생성");
        }

        private static void ValidateRelicGrid()
        {
            var grid = Object.FindObjectOfType<RelicGridUI>();
            Array cells = (Array)typeof(RelicGridUI).GetField("slotCells", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(grid);
            var manager = DataLifetimeScope.Instance.Container.Resolve<RelicManager>();
            Require(cells != null && cells.Length == manager.GridRows * manager.GridColumns, "유물 보드 초기화 코루틴 실행");
        }

        private static void CaptureError(string message, string trace, LogType type)
        {
            if (!SessionState.GetBool(Key + "Pending", false) || (type != LogType.Error && type != LogType.Exception && type != LogType.Assert)) return;
            SessionState.SetBool(Key + "Error", true);
            File.AppendAllText(Output + "/" + SessionState.GetString(Key + "Route", "Title") + "-errors.txt", message + "\n" + trace + "\n");
        }

        private static void Next(int phase, double waitUntil)
        {
            SessionState.SetInt(Key + "Phase", phase);
            SessionState.SetFloat(Key + "WaitUntil", (float)waitUntil);
        }

        private static void Pass(string message) => File.AppendAllText(Result, "PASS " + message + "\n");

        private static void Finish(Exception error)
        {
            if (error != null) File.AppendAllText(Result, "FAIL " + error + "\n");
            else Pass("시나리오 완료");
            SessionState.SetBool(Key + "Stopping", true);
            EditorApplication.isPlaying = false;
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Key + "Pending", false))
            {
                // 에디터가 뒤에 있어도 씬 전환과 UI 코루틴을 계속 실행한다.
                Application.runInBackground = true;
            }
            if (state == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(Key + "Pending", false))
            {
                if (SessionState.GetBool(Key + "Error", false))
                    File.AppendAllText(Result, "FAIL 종료까지 수집한 플레이 오류: " + SessionState.GetString(Key + "Route", "Title") + "-errors.txt 확인\n");
                if (!SessionState.GetBool(Key + "Stopping", false))
                    File.AppendAllText(Result, "FAIL 검증 도중 플레이 모드가 중단되었습니다.\n");
                if (SessionState.GetBool(Key + "RestartPending", false) && !SessionState.GetBool(Key + "Error", false))
                {
                    Pass("플레이 종료 완료, Root/Data/플레이어를 새로 생성하여 같은 저장 파일로 재시작");
                    // 편집 상태 전환이 끝난 뒤 Update에서 다음 플레이를 시작한다.
                    Next(0, EditorApplication.timeSinceStartup + 1);
                    return;
                }
                RestoreEditor();
            }
        }

        private static void RestoreEditor()
        {
            try
            {
                PlayerSettings.productName = SessionState.GetString(Key + "Product", PlayerSettings.productName);
                var backup = JsonUtility.FromJson<SceneBackup>(SessionState.GetString(Key + "Scenes", ""));
                if (backup?.scenes != null) EditorSceneManager.RestoreSceneManagerSetup(backup.scenes);
                Require(Application.persistentDataPath == SessionState.GetString(Key + "OriginalSavePath", ""), "원래 세이브 경로 복원");
                Pass("제품명·저장 경로·열린 씬 복원");
            }
            catch (Exception error) { File.AppendAllText(Result, "FAIL 복원: " + error + "\n"); }
            finally
            {
                SessionState.SetBool(Key + "Pending", false);
                SessionState.SetBool(Key + "Stopping", false);
                SessionState.SetBool(Key + "RestartPending", false);
            }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
