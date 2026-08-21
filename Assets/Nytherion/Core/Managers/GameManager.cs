using UnityEngine;
using VContainer;
using VContainer.Unity;
using Nytherion.Core.Enums;
using Nytherion.Core.Data;
using System.IO;

namespace Nytherion.Core.Managers
{
    public class GameManager : BaseManager
    {
        private CurrencyDataManager currencyDataManager;
        private SaveLoadManager saveLoadManager;

        private static bool verboseLogging = false;

        [Inject]
        public void Construct(CurrencyDataManager currencyDataManager, SaveLoadManager saveLoadManager)
        {
            this.currencyDataManager = currencyDataManager;
            this.saveLoadManager = saveLoadManager;
        }

        protected override void OnInitializeInternal()
        {
            base.OnInitializeInternal();
            Nytherion.GamePlay.Characters.Player.PlayerHealth.OnPlayerDied += HandlePlayerDeath;
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            Nytherion.GamePlay.Characters.Player.PlayerHealth.OnPlayerDied -= HandlePlayerDeath;
        }

        private void HandlePlayerDeath()
        {
            if (currencyDataManager != null)
            {
                currencyDataManager.ResetBasicCurrencies();
            }
            if (saveLoadManager != null)
            {
                saveLoadManager.SaveGame();
            }
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F1))
            {
                if (currencyDataManager == null)
                {
                    Debug.LogError("CurrencyDataManager가 null입니다. 골드를 추가할 수 없습니다.");
                    return;
                }
                currencyDataManager.AddCurrency(CurrencyType.Gold, 1000);
            }
            if (Input.GetKeyDown(KeyCode.F2))
            {
                if (currencyDataManager == null)
                {
                    Debug.LogError("CurrencyDataManager가 null입니다. 토큰을 추가할 수 없습니다.");
                    return;
                }
                currencyDataManager.AddCurrency(CurrencyType.Token, 10);
            }
            if (Input.GetKeyDown(KeyCode.F3))
            {
                if (saveLoadManager != null)
                {
                    saveLoadManager.SaveGame();
                }
                else
                {
                    Debug.LogError("[GameManager] SaveLoadManager가 null입니다.");
                }
            }
            if (Input.GetKeyDown(KeyCode.F4))
            {
                if (saveLoadManager != null)
                {
                    saveLoadManager.LoadGameIfNeeded();
                }
                else
                {
                    Debug.LogError("[GameManager] SaveLoadManager가 null입니다.");
                }
            }

            if (Input.GetKeyDown(KeyCode.F5))
            {
                // 저장 파일 경로 확인 및 내용 출력
                CheckSaveFile();
            }

            if (Input.GetKeyDown(KeyCode.F6))
            {
                // 저장 파일 삭제 (새 게임 테스트용)
                DeleteSaveFile();
            }

            if (Input.GetKeyDown(KeyCode.F7))
            {
                // 현재 게임 상태 출력
                PrintGameState();
            }

            if (Input.GetKeyDown(KeyCode.F8))
            {
                // 강제 저장 후 바로 로드 테스트
                TestSaveLoad();
            }

            if (Input.GetKeyDown(KeyCode.F9))
            {
                // 데이터 변경 후 자동저장 테스트 (30초 기다리지 않고)
                TestAutoSave();
            }

            if (Input.GetKeyDown(KeyCode.F10))
            {
                // 상세 로깅 토글
                ToggleVerboseLogging();
            }
        }

        public override void PopulateSaveData(SaveData saveData)
        {
            // GameManager는 저장할 데이터가 없음 (디버그 기능만 제공)
        }

        public override void LoadFromSaveData(SaveData saveData)
        {
            // GameManager는 로드할 데이터가 없음 (디버그 기능만 제공)
        }

        private void CheckSaveFile()
        {
            string path = Path.Combine(Application.persistentDataPath, "nytherion_savedata.json");
            Debug.Log($"[GameManager] 저장 파일 경로: {path}");

            if (File.Exists(path))
            {
                try
                {
                    string content = File.ReadAllText(path);
                    long fileSize = new FileInfo(path).Length;
                    Debug.Log($"[GameManager] 파일 크기: {fileSize} bytes");
                    Debug.Log($"[GameManager] 파일 내용:\n{content}");
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[GameManager] 파일 읽기 실패: {e.Message}");
                }
            }
            else
            {
                Debug.Log("[GameManager] 저장 파일이 존재하지 않습니다.");
            }
        }

        private void DeleteSaveFile()
        {
            string path = Path.Combine(Application.persistentDataPath, "nytherion_savedata.json");

            if (File.Exists(path))
            {
                try
                {
                    File.Delete(path);
                    Debug.Log("[GameManager] 저장 파일이 삭제되었습니다. 다음 로드시 새 게임으로 시작됩니다.");
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[GameManager] 파일 삭제 실패: {e.Message}");
                }
            }
            else
            {
                Debug.Log("[GameManager] 삭제할 저장 파일이 없습니다.");
            }
        }

        private void PrintGameState()
        {
            if (currencyDataManager != null)
            {
                int gold = currencyDataManager.GetGold();
                int tokens = currencyDataManager.GetTokens();
                Debug.Log($"[GameManager] 현재 게임 상태:");
                Debug.Log($"  - Gold: {gold}");
                Debug.Log($"  - Tokens: {tokens}");
            }
            else
            {
                Debug.LogError("[GameManager] CurrencyDataManager가 null입니다.");
            }
        }

        private void TestSaveLoad()
        {
            if (saveLoadManager == null || currencyDataManager == null)
            {
                Debug.LogError("[GameManager] 매니저가 null입니다.");
                return;
            }

            // 현재 상태 저장
            int beforeGold = currencyDataManager.GetGold();
            int beforeTokens = currencyDataManager.GetTokens();

            Debug.Log($"[GameManager] 저장 전: Gold={beforeGold}, Tokens={beforeTokens}");

            // 강제 저장
            saveLoadManager.SaveGame();

            // 데이터 변경
            currencyDataManager.AddCurrency(CurrencyType.Gold, 999);
            currencyDataManager.AddCurrency(CurrencyType.Token, 99);

            int afterChangeGold = currencyDataManager.GetGold();
            int afterChangeTokens = currencyDataManager.GetTokens();
            Debug.Log($"[GameManager] 변경 후: Gold={afterChangeGold}, Tokens={afterChangeTokens}");

            // 다시 로드 (저장된 상태로 복원되어야 함)
            Debug.Log("[GameManager] 강제 로드 실행...");
            saveLoadManager.ForceLoadGame();

            int afterLoadGold = currencyDataManager.GetGold();
            int afterLoadTokens = currencyDataManager.GetTokens();
            Debug.Log($"[GameManager] 로드 후: Gold={afterLoadGold}, Tokens={afterLoadTokens}");

            // 검증
            if (afterLoadGold == beforeGold && afterLoadTokens == beforeTokens)
            {
                Debug.Log("✅ [GameManager] 저장/로드 테스트 성공!");
            }
            else
            {
                Debug.LogError("❌ [GameManager] 저장/로드 테스트 실패!");
            }
        }

        private void TestAutoSave()
        {
            if (currencyDataManager == null)
            {
                Debug.LogError("[GameManager] CurrencyDataManager가 null입니다.");
                return;
            }

            Debug.Log("[GameManager] 자동 저장 테스트 시작 - 골드 추가");
            currencyDataManager.AddCurrency(CurrencyType.Gold, 100);

            // 자동 저장이 트리거되었는지 확인하기 위해 잠시 후 파일 확인
            StartCoroutine(CheckAutoSaveResult());
        }

        private System.Collections.IEnumerator CheckAutoSaveResult()
        {
            yield return new UnityEngine.WaitForSeconds(0.5f); // 자동 저장 완료 대기

            string path = Path.Combine(Application.persistentDataPath, "nytherion_savedata.json");
            if (File.Exists(path))
            {
                var lastWriteTime = File.GetLastWriteTime(path);
                var timeDiff = System.DateTime.Now - lastWriteTime;

                if (timeDiff.TotalSeconds < 2)
                {
                    Debug.Log("✅ [GameManager] 자동 저장이 정상적으로 실행되었습니다.");
                }
                else
                {
                    Debug.LogWarning($"⚠️ [GameManager] 자동 저장이 {timeDiff.TotalSeconds:F1}초 전에 실행되었습니다.");
                }
            }
            else
            {
                Debug.LogError("❌ [GameManager] 저장 파일이 생성되지 않았습니다.");
            }
        }

        private void ToggleVerboseLogging()
        {
            verboseLogging = !verboseLogging;
            Debug.Log($"🔊 [GameManager] 상세 로깅: {(verboseLogging ? "ON" : "OFF")}");

            if (verboseLogging)
            {
                Debug.Log("📝 다른 매니저들의 상세 로그가 활성화됩니다.");
            }
            else
            {
                Debug.Log("🔇 불필요한 로그들이 숨겨집니다.");
            }
        }

        public static bool IsVerboseLogging()
        {
            return verboseLogging;
        }
    }
}

