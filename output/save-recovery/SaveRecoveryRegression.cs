using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Newtonsoft.Json.Linq;
using Nytherion.Core.Data;
using Nytherion.Core.Enums;
using Nytherion.Core.Managers;
using Nytherion.Core.Systems;
using Nytherion.Services;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using VContainer;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class SaveRecoveryRegression
{
    private const string Pending = "SaveRecoveryRegression.Pending";
    private static readonly List<string> Results = new List<string>();
    private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static IEnumerator routine;
    private static double started;
    private static int failures;
    private static string reportDirectory;
    private static string fixtures;

    static SaveRecoveryRegression() { EditorApplication.update += Update; }

    public static void RunBatch()
    {
        if (!Application.isBatchMode || !Application.dataPath.Replace('\\', '/').EndsWith("tmp/save-recovery-regression/Assets"))
            throw new InvalidOperationException("격리 저장 검증 프로젝트에서만 실행하세요.");
        PlayerSettings.productName = "Nytherion_SaveRecoveryRegression";
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
                started = EditorApplication.timeSinceStartup;
                reportDirectory = Path.GetFullPath("../../output/save-recovery");
                fixtures = Path.Combine(reportDirectory, "fixtures-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(fixtures);
                routine = Run();
            }
            if (routine == null) return;
            if (EditorApplication.timeSinceStartup - started > 90) throw new TimeoutException("회귀 검사 시간 초과");
            if (!routine.MoveNext()) Finish();
        }
        catch (Exception exception)
        {
            failures++;
            Results.Add("FAIL: 미처리 예외 " + exception);
            Finish();
        }
    }

    private static void Finish()
    {
        routine = null;
        Time.timeScale = 1f;
        Results.Add("총 " + (Results.Count) + "개 검사, 실패 " + failures + "개");
        Results.Add("Unity " + Application.unityVersion + "; 실제 JsonUtility/파일 I/O/MonoBehaviour/코루틴/VContainer 사용. 데이터 매니저와 UI/스코프는 격리 협력 객체.");
        File.WriteAllLines(Path.Combine(reportDirectory, "verification.txt"), Results);
        Debug.Log(string.Join("\n", Results));
        EditorApplication.Exit(failures == 0 ? 0 : 1);
    }

    private static void Check(string name, Action action)
    {
        try { action(); Results.Add("PASS: " + name); }
        catch (Exception exception) { failures++; Results.Add("FAIL: " + name + " — " + exception); }
    }
    private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, Private).GetValue(target);
    private static void Set(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
    private static void Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Private).Invoke(target, args);
    private static string NewPath() => Path.Combine(fixtures, Guid.NewGuid().ToString("N") + ".json");
    private static SaveData Data(int gold)
    {
        SaveData data = new SaveData();
        data.currencyTypes.Add(CurrencyType.Gold); data.currencyAmounts.Add(gold);
        data.inventoryData.Add(new ItemEntry { slotIndex = 3, itemId = "legacy-item", count = 2, instanceId = "legacy-instance", rarity = Rarity.Rare });
        data.ownedSkills.Add(new SkillEntry { skillId = "", level = 1, exp = 0 });
        data.relicData.placedBlocks.Add(new RelicGridState.SavedRelicBlock { relicId = "legacy-relic", gridRow = -1, gridCol = -1 });
        data.dungeonMapData.lastSafeX = 12.5f;
        return data;
    }

    private static IEnumerator Run()
    {
        Check("파일 없음은 Missing이며 새 파일을 만들지 않음", () => {
            string path = NewPath(); Require(new JsonSaveService(path).LoadWithResult().Status == SaveLoadStatus.Missing && !File.Exists(path), "없음 처리 실패");
        });
        Check("저장/로드 왕복 및 기존 데이터 전체 보존", () => {
            string path = NewPath(); JsonSaveService service = new JsonSaveService(path); SaveData source = Data(120);
            Require(service.Save(source), service.LastError);
            Require(JsonUtility.ToJson(source) == JsonUtility.ToJson(service.Load()), "왕복 데이터 불일치");
            Require(!File.Exists(path + ".tmp"), "임시 파일 잔존");
        });
        Check("두 번째 저장은 직전 정상 파일을 백업", () => {
            string path = NewPath(); JsonSaveService service = new JsonSaveService(path);
            Require(service.Save(Data(10)) && service.Save(Data(20)), service.LastError);
            Require(service.Load().currencyAmounts[0] == 20 && new JsonSaveService(path + ".bak").Load().currencyAmounts[0] == 10, "백업 불일치");
        });
        Check("손상 원본 복구 후 저장해도 정상 백업과 손상 증거 보존", () => {
            string path = NewPath(); JsonSaveService service = new JsonSaveService(path);
            Require(service.Save(Data(10)) && service.Save(Data(20)), service.LastError);
            string backup = File.ReadAllText(path + ".bak"); File.WriteAllText(path, "{broken");
            Require(service.LoadWithResult().Status == SaveLoadStatus.RecoveredBackup && service.Load().currencyAmounts[0] == 10, "백업 복구 실패");
            Require(service.Save(Data(30)), service.LastError);
            Require(File.ReadAllText(path + ".bak") == backup, "정상 백업 덮어씀");
            string[] archives = Directory.GetFiles(fixtures, Path.GetFileName(path) + ".corrupt-*");
            Require(archives.Length == 1 && File.ReadAllText(archives[0]) == "{broken", "손상 원본 보존 실패");
            Require(service.Load().currencyAmounts[0] == 30, "복구 후 저장 실패");
        });
        Check("원본 없음/백업 있음은 백업 복구", () => {
            string path = NewPath(); JsonSaveService service = new JsonSaveService(path);
            Require(service.Save(Data(5)) && service.Save(Data(6)), service.LastError); File.Delete(path);
            Require(service.LoadWithResult().Status == SaveLoadStatus.RecoveredBackup && service.Load().currencyAmounts[0] == 5, "백업 무시");
        });
        Check("원본과 백업 손상 시 직접 저장도 거부하고 둘 다 보존", () => {
            string path = NewPath(); File.WriteAllText(path, "bad-main"); File.WriteAllText(path + ".bak", "bad-backup");
            JsonSaveService service = new JsonSaveService(path);
            Require(service.LoadWithResult().Status == SaveLoadStatus.Corrupt && !service.Save(Data(4)), "손상 파일 저장 허용");
            Require(File.ReadAllText(path) == "bad-main" && File.ReadAllText(path + ".bak") == "bad-backup", "손상 원본 훼손");
        });
        Check("명시적 세이브 삭제는 백업도 제거하여 부활 방지", () => {
            string path = NewPath(); JsonSaveService service = new JsonSaveService(path);
            Require(service.Save(Data(1)) && service.Save(Data(2)), service.LastError); service.DeleteSaveData();
            Require(service.LoadWithResult().Status == SaveLoadStatus.Missing, "삭제 후 백업 복구됨");
        });
        foreach (string json in new[] { "", "   ", "{", "null", "[]", "{}", "{\"currencyTypes\":[0],\"currencyAmounts\":[]}", "{\"currencyTypes\":[0],\"currencyAmounts\":[-1]}", "{\"currencyTypes\":[0,0],\"currencyAmounts\":[1,2]}", "{\"currencyTypes\":[0],\"currencyAmounts\":[\"9\"]}", "{\"currencyTypes\":[0],\"currencyTypes\":[1],\"currencyAmounts\":[9]}" })
        {
            string input = json;
            Check("손상 데이터 거부: " + (string.IsNullOrWhiteSpace(input) ? "빈 파일" : input), () => {
                string path = NewPath(); File.WriteAllText(path, input); JsonSaveService service = new JsonSaveService(path);
                Require(service.LoadWithResult().Status == SaveLoadStatus.Corrupt, "손상을 정상 취급");
            });
        }
        Check("구버전 선택 필드 없음과 스킬 빈 슬롯 호환", () => {
            string path = NewPath(); File.WriteAllText(path, "{\"currencyTypes\":[0],\"currencyAmounts\":[9]}");
            JsonSaveService service = new JsonSaveService(path); SaveLoadResult result = service.LoadWithResult();
            Require(result.IsSuccess && result.Data.inventoryData != null && result.Data.relicData.placedBlocks != null && result.Data.progressionState.activeProgresses != null, "구버전 호환 실패");
            Require(service.Save(result.Data), service.LastError);
        });
        Check("잘못된 슬롯/중복 슬롯/null 항목 거부", () => {
            foreach (int mode in new[] { 0, 1, 2 }) {
                SaveData invalid = Data(1);
                if (mode == 0) invalid.inventoryData[0].slotIndex = -1;
                if (mode == 1) invalid.inventoryData.Add(invalid.inventoryData[0]);
                if (mode == 2) invalid.inventoryData.Add(null);
                Require(!new JsonSaveService(NewPath()).Save(invalid), "무효 항목 저장 허용 " + mode);
            }
        });
        Check("읽기 잠금은 ReadError이며 백업으로 숨기지 않음", () => {
            string path = NewPath(); JsonSaveService service = new JsonSaveService(path); Require(service.Save(Data(1)) && service.Save(Data(2)), service.LastError);
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                Require(service.LoadWithResult().Status == SaveLoadStatus.ReadError && !service.Save(Data(3)), "읽기 오류 숨김");
            Require(service.Load().currencyAmounts[0] == 2, "잠금 중 원본 훼손");
        });
        Check("임시 파일 쓰기 실패는 원본 보존", () => {
            string path = NewPath(); JsonSaveService service = new JsonSaveService(path); Require(service.Save(Data(1)), service.LastError);
            string original = File.ReadAllText(path); Directory.CreateDirectory(path + ".tmp");
            Require(!service.Save(Data(2)) && File.ReadAllText(path) == original, "쓰기 실패 시 원본 훼손");
            Directory.Delete(path + ".tmp");
        });
        Check("읽기 전용 원본의 파일 교체 실패는 원본과 정상 백업 보존", () => {
            string path = NewPath(); JsonSaveService service = new JsonSaveService(path); Require(service.Save(Data(1)) && service.Save(Data(2)), service.LastError);
            string original = File.ReadAllText(path), backup = File.ReadAllText(path + ".bak");
            File.SetAttributes(path, FileAttributes.ReadOnly);
            try { Require(!service.Save(Data(3)) && File.ReadAllText(path) == original && File.ReadAllText(path + ".bak") == backup, "교체 실패 시 원본 훼손"); }
            finally { File.SetAttributes(path, FileAttributes.Normal); }
        });

        string managerPath = NewPath(); JsonSaveService managerService = new JsonSaveService(managerPath);
        GameObject owner = new GameObject("저장 회귀 검증"); CurrencyDataManager currency = owner.AddComponent<CurrencyDataManager>();
        ContainerBuilder builder = new ContainerBuilder(); builder.RegisterInstance(currency);
        IObjectResolver container = builder.Build(); DataLifetimeScope scope = owner.AddComponent<DataLifetimeScope>();
        scope.Container = container; DataLifetimeScope.Instance = scope;
        SaveLoadManager manager = owner.AddComponent<SaveLoadManager>(); manager.enabled = false; Set(manager, "saveService", managerService);
        Time.timeScale = 0f; manager.Initialize();
        float until = Time.realtimeSinceStartup + 2.4f;
        while (Time.realtimeSinceStartup < until) yield return null;
        Check("최초 초기화: 일시정지 중에도 로드와 차단 해제 완료", () => {
            Require(Field<bool>(manager, "hasLoadedData") && Field<bool>(manager, "isInitializationComplete") && !Field<bool>(manager, "preventAutoSave") && !manager.IsSaveBlocked, "최초 초기화 완료 실패");
        });
        Time.timeScale = 1f;
        Check("첫 강제 저장 및 맵/씬 밖 퀵슬롯 보존", () => {
            manager.CurrentSaveData.dungeonMapData.lastSafeX = 33f;
            manager.CurrentSaveData.quickSlotData.Add(new QuickSlotEntry { slotIndex = 1, itemId = "potion", count = 2 });
            currency.Gold = 90; manager.ForceSaveGame();
            SaveData saved = managerService.Load();
            Require(saved != null && saved.currencyAmounts[0] == 90 && saved.dungeonMapData.lastSafeX == 33f && saved.quickSlotData.Count == 1, "수집하지 않는 필드 유실");
        });
        Check("이전 코드의 강제 로드 후 영구 저장 차단 재현", () => {
            LegacySaveLoadManager legacy = owner.AddComponent<LegacySaveLoadManager>(); legacy.enabled = false;
            Set(legacy, "saveService", managerService); Set(legacy, "hasLoadedData", true);
            legacy.ForceLoadGame(); Require(Field<bool>(legacy, "preventAutoSave"), "기존 문제 재현 실패");
            currency.Gold = 999; legacy.ForceSaveGame(); Require(managerService.Load().currencyAmounts[0] == 90, "기존 차단 재현 실패");
            Object.DestroyImmediate(legacy);
        });
        Check("수정 코드: 강제 로드 후 차단 해제 및 다시 저장", () => {
            manager.ForceLoadGame(); Require(currency.Gold == 90 && !Field<bool>(manager, "preventAutoSave") && !manager.IsSaveBlocked, "강제 로드 해제 실패");
            currency.Gold = 91; manager.ForceSaveGame(); Require(managerService.Load().currencyAmounts[0] == 91, "강제 로드 후 저장 실패");
        });
        Check("초기화 차단/로드 중에는 강제 저장도 원본 보호", () => {
            string original = File.ReadAllText(managerPath); currency.Gold = 999;
            Set(manager, "preventAutoSave", true); manager.ForceLoadGame();
            Require(Field<bool>(manager, "preventAutoSave"), "외부 차단 상태 유실"); currency.Gold = 999; manager.ForceSaveGame();
            Set(manager, "preventAutoSave", false); Set(manager, "isLoadingData", true); manager.ForceSaveGame();
            Require(File.ReadAllText(managerPath) == original, "로드 중 강제 저장 허용"); Set(manager, "isLoadingData", false);
        });
        Check("종료/일시중지/포커스 상실 저장은 1초 쿨다운 우회", () => {
            Set(manager, "lastSaveTime", Time.unscaledTime);
            currency.Gold = 92; Call(manager, "OnApplicationQuit"); Require(managerService.Load().currencyAmounts[0] == 92, "종료 저장 누락");
            currency.Gold = 93; Call(manager, "OnApplicationPause", true); Require(managerService.Load().currencyAmounts[0] == 93, "일시중지 저장 누락");
            currency.Gold = 94; Call(manager, "OnApplicationFocus", false); Require(managerService.Load().currencyAmounts[0] == 94, "포커스 저장 누락");
        });
        Check("서비스 쓰기 실패 시 성공 시각과 원본 불변, 이후 재시도 가능", () => {
            string original = File.ReadAllText(managerPath); Set(manager, "lastSaveTime", -10f); Directory.CreateDirectory(managerPath + ".tmp");
            currency.Gold = 95; manager.ForceSaveGame();
            Require(Field<float>(manager, "lastSaveTime") == -10f && File.ReadAllText(managerPath) == original && !string.IsNullOrEmpty(manager.LastError), "쓰기 실패 성공 취급");
            Directory.Delete(managerPath + ".tmp"); manager.ForceSaveGame(); Require(managerService.Load().currencyAmounts[0] == 95 && manager.LastError == null, "쓰기 재시도 실패");
        });
        Check("수집 예외 시 부분 데이터 저장과 메모리 스냅샷 훼손 방지", () => {
            string original = File.ReadAllText(managerPath); int previous = manager.CurrentSaveData.currencyAmounts[0];
            currency.ThrowOnSave = true; currency.Gold = 96; Set(manager, "lastSaveTime", -10f); manager.ForceSaveGame();
            Require(Field<float>(manager, "lastSaveTime") == -10f && File.ReadAllText(managerPath) == original && manager.CurrentSaveData.currencyAmounts[0] == previous, "부분 저장 또는 원본 상태 훼손");
            currency.ThrowOnSave = false;
        });
        Check("로드 적용 예외는 저장 차단, 정상 재로드 후 복구", () => {
            string original = File.ReadAllText(managerPath); currency.ThrowOnLoad = true; manager.ForceLoadGame();
            Require(manager.IsSaveBlocked && !Field<bool>(manager, "isLoadingData") && !Field<bool>(manager, "preventAutoSave"), "예외 정리 실패");
            currency.Gold = 999; manager.ForceSaveGame(); Require(File.ReadAllText(managerPath) == original, "부분 로드 상태 저장");
            currency.ThrowOnLoad = false; manager.ForceLoadGame(); Require(!manager.IsSaveBlocked && currency.Gold == 95, "정상 재로드 실패");
        });
        Check("양쪽 파일 손상은 새 게임 초기화/덮어쓰기 거부, 복구 후 저장 가능", () => {
            string healthy = File.ReadAllText(managerPath); int gold = currency.Gold;
            File.WriteAllText(managerPath, "broken-main"); File.WriteAllText(managerPath + ".bak", "broken-backup"); manager.ForceLoadGame();
            Require(manager.IsSaveBlocked && currency.Gold == gold && manager.LastLoadStatus == SaveLoadStatus.Corrupt, "손상 상태 초기화됨");
            currency.Gold = 999; manager.ForceSaveGame(); Require(File.ReadAllText(managerPath) == "broken-main" && File.ReadAllText(managerPath + ".bak") == "broken-backup", "손상 파일 덮어씀");
            File.WriteAllText(managerPath, healthy); manager.ForceLoadGame(); currency.Gold = 97; manager.ForceSaveGame();
            Require(!manager.IsSaveBlocked && managerService.Load().currencyAmounts[0] == 97, "수리 후 저장 실패");
        });
        Check("매니저 백업 복구 후 저장 가능", () => {
            currency.Gold = 98; manager.ForceSaveGame(); File.WriteAllText(managerPath, "{broken"); manager.ForceLoadGame();
            Require(manager.LastLoadStatus == SaveLoadStatus.RecoveredBackup && currency.Gold == 97 && !manager.IsSaveBlocked, "매니저 복구 실패");
            currency.Gold = 99; manager.ForceSaveGame(); Require(managerService.Load().currencyAmounts[0] == 99, "백업 복구 후 저장 차단");
        });
        Check("매니저 읽기 오류 후 저장 보호와 정상 재로드", () => {
            using (FileStream locked = new FileStream(managerPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) {
                manager.ForceLoadGame(); Require(manager.LastLoadStatus == SaveLoadStatus.ReadError && manager.IsSaveBlocked && !Field<bool>(manager, "preventAutoSave"), "읽기 오류 처리 실패");
            }
            manager.ForceLoadGame(); Require(!manager.IsSaveBlocked && currency.Gold == 99, "잠금 해제 후 복구 실패");
        });
        Object.DestroyImmediate(owner); DataLifetimeScope.Instance = null; container.Dispose();
        yield return null;
    }
}
