using System;
using UnityEngine;
using Nytherion.UI.Inventory;
using Nytherion.Core.Interfaces;
using System.Text;
using System.Security.Cryptography;

namespace Nytherion.Services
{
    /// <summary>
    /// PlayerPrefs를 사용한 인벤토리 저장 서비스 구현체
    /// </summary>
    public class PlayerPrefsInventorySaveService : IInventorySaveService
    {
        private const string SAVE_KEY_PREFIX = "Inventory_";
        private const string VERSION_KEY = "_Version";
        private const int CURRENT_VERSION = 1;
        private readonly string saveKey;

        // 암호화 키 (실제 프로젝트에서는 더 안전한 방법으로 관리해야 함)
        private const string ENCRYPTION_KEY = "YourSecureEncryptionKey123";

        [Serializable]
        private class SaveData
        {
            public string data;
            public int version;
        }

        /// <summary>
        /// 기본 생성자 (기본 저장 슬롯 사용)
        /// </summary>
        public PlayerPrefsInventorySaveService() : this("DefaultSlot") { }

        /// <summary>
        /// 지정된 슬롯 이름으로 저장 서비스 생성
        /// </summary>
        /// <param name="slotName">저장 슬롯 이름</param>
        public PlayerPrefsInventorySaveService(string slotName)
        {
            if (string.IsNullOrEmpty(slotName))
                throw new ArgumentException("슬롯 이름이 유효하지 않습니다.", nameof(slotName));

            saveKey = $"{SAVE_KEY_PREFIX}{slotName}";
        }

        /// <summary>
        /// 인벤토리 상태를 저장합니다.
        /// </summary>
        public void SaveInventory(InventoryState state)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));

            try
            {
                // 인벤토리 상태를 JSON으로 직렬화
                string json = JsonUtility.ToJson(state);
                
                // 암호화 (선택 사항)
                string encryptedData = Encrypt(json);
                
                // 버전 정보와 함께 저장
                SaveData saveData = new SaveData
                {
                    data = encryptedData,
                    version = CURRENT_VERSION
                };
                
                string saveJson = JsonUtility.ToJson(saveData);
                
                // 저장
                PlayerPrefs.SetString(saveKey, saveJson);
                PlayerPrefs.SetInt($"{saveKey}{VERSION_KEY}", CURRENT_VERSION);
                
                // PlayerPrefs.Save()는 void를 반환하므로 try-catch로 오류 처리
                try
                {
                    PlayerPrefs.Save();
                }
                catch (Exception saveEx)
                {
                    Debug.LogError($"[SaveService] 플레이어 프리퍼런스 저장 실패: {saveEx.Message}");
                }
                
                #if UNITY_EDITOR
                Debug.Log($"[SaveService] 인벤토리 저장 완료: {saveKey}");
                #endif
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveService] 인벤토리 저장 중 오류: {e.Message}");
                throw;
            }
        }

        /// <summary>
        /// 저장된 인벤토리 상태를 로드합니다.
        /// </summary>
        public InventoryState LoadInventory()
        {
            try
            {
                if (!PlayerPrefs.HasKey(saveKey))
                {
                    Debug.Log($"[SaveService] 저장된 인벤토리 데이터가 없습니다: {saveKey}");
                    return null;
                }

                // 저장된 데이터 로드
                string saveJson = PlayerPrefs.GetString(saveKey);
                if (string.IsNullOrEmpty(saveJson))
                {
                    Debug.LogWarning($"[SaveService] 빈 인벤토리 데이터: {saveKey}");
                    return null;
                }

                // 버전 확인
                int savedVersion = PlayerPrefs.GetInt($"{saveKey}{VERSION_KEY}", 0);
                if (savedVersion != CURRENT_VERSION)
                {
                    Debug.LogWarning($"[SaveService] 버전 불일치: {savedVersion} (현재 버전: {CURRENT_VERSION})");
                    // 여기서 버전 마이그레이션 로직을 추가할 수 있습니다.
                }

                // JSON 파싱
                SaveData saveData = JsonUtility.FromJson<SaveData>(saveJson);
                if (saveData == null)
                {
                    Debug.LogError($"[SaveService] 저장 데이터 파싱 실패: {saveKey}");
                    return null;
                }

                // 복호화
                string decryptedData = Decrypt(saveData.data);
                
                // 인벤토리 상태로 역직렬화
                InventoryState state = JsonUtility.FromJson<InventoryState>(decryptedData);
                
                if (state == null)
                {
                    Debug.LogError("[Inventory] Failed to deserialize inventory data");
                    return null;
                }

                Debug.Log($"[Inventory] Loaded {state.Items?.Count ?? 0} items from save");
                return state;
            }
            catch (Exception e)
            {
                Debug.LogError($"[Inventory] Error loading inventory: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// 저장된 인벤토리 데이터를 삭제합니다.
        /// </summary>
        public void DeleteSaveData()
        {
            try
            {
                if (PlayerPrefs.HasKey(saveKey))
                {
                    PlayerPrefs.DeleteKey(saveKey);
                    PlayerPrefs.DeleteKey($"{saveKey}{VERSION_KEY}");
                    PlayerPrefs.Save();
                    Debug.Log($"[SaveService] 인벤토리 데이터 삭제됨: {saveKey}");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveService] 인벤토리 데이터 삭제 중 오류: {e.Message}");
            }
        }

        #region 암호화/복호화 (선택 사항)
        
        // 참고: 이는 단순한 예시이며, 프로덕션 환경에서는 더 강력한 암호화 방식을 사용해야 합니다.
        
        private string Encrypt(string data)
        {
            try
            {
                // 실제 프로젝트에서는 AES와 같은 강력한 암호화 사용을 권장합니다.
                // 여기서는 단순히 Base64 인코딩만 수행합니다.
                byte[] dataBytes = Encoding.UTF8.GetBytes(data);
                return Convert.ToBase64String(dataBytes);
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveService] 암호화 실패: {e.Message}");
                return data; // 암호화 실패 시 원본 데이터 반환
            }
        }

        public string Decrypt(string encryptedData)
        {
            try
            {
                // Base64 디코딩
                byte[] dataBytes = Convert.FromBase64String(encryptedData);
                return Encoding.UTF8.GetString(dataBytes);
            }
            catch
            {
                Debug.LogError("[SaveService] 복호화 실패: 잘못된 데이터 형식");
                return "{}"; // 빈 JSON 객체 반환
            }
        }
        
        #endregion
    }
}