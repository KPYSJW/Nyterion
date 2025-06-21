using System;
using UnityEngine;
using Nytherion.UI.Inventory;
using Nytherion.Core.Interfaces;
using System.Text;
using System.Security.Cryptography;

namespace Nytherion.Services
{
    public class PlayerPrefsInventorySaveService : IInventorySaveService
    {
        private const string SAVE_KEY_PREFIX = "Inventory_";
        private const string VERSION_KEY = "_Version";
        private const int CURRENT_VERSION = 1;
        private readonly string saveKey;

        private const string ENCRYPTION_KEY = "YourSecureEncryptionKey123";

        [Serializable]
        private class SaveData
        {
            public string data;
            public int version;
        }

        public PlayerPrefsInventorySaveService() : this("DefaultSlot") { }

        public PlayerPrefsInventorySaveService(string slotName)
        {
            if (string.IsNullOrEmpty(slotName))
                throw new ArgumentException("슬롯 이름이 유효하지 않습니다.", nameof(slotName));

            saveKey = $"{SAVE_KEY_PREFIX}{slotName}";
        }

        public void SaveInventory(InventoryState state)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));

            try
            {
                string json = JsonUtility.ToJson(state);
                string encryptedData = Encrypt(json);
                SaveData saveData = new SaveData
                {
                    data = encryptedData,
                    version = CURRENT_VERSION
                };
                
                string saveJson = JsonUtility.ToJson(saveData);
                
                PlayerPrefs.SetString(saveKey, saveJson);
                PlayerPrefs.SetInt($"{saveKey}{VERSION_KEY}", CURRENT_VERSION);
                
                try
                {
                    PlayerPrefs.Save();
                }
                catch (Exception saveEx)
                {
                    Debug.LogError($"[SaveService] 플레이어 프리퍼런스 저장 실패: {saveEx.Message}");
                }
                
                #if UNITY_EDITOR
                #endif
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveService] 인벤토리 저장 중 오류: {e.Message}");
                throw;
            }
        }

        public InventoryState LoadInventory()
        {
            try
            {
                if (!PlayerPrefs.HasKey(saveKey))
                {
                    Debug.Log($"[SaveService] 저장된 인벤토리 데이터가 없습니다: {saveKey}");
                    return null;
                }

                string saveJson = PlayerPrefs.GetString(saveKey);
                if (string.IsNullOrEmpty(saveJson))
                {
                    Debug.LogWarning($"[SaveService] 빈 인벤토리 데이터: {saveKey}");
                    return null;
                }

                int savedVersion = PlayerPrefs.GetInt($"{saveKey}{VERSION_KEY}", 0);
                if (savedVersion != CURRENT_VERSION)
                {
                    Debug.LogWarning($"[SaveService] 버전 불일치: {savedVersion} (현재 버전: {CURRENT_VERSION})");
                }

                SaveData saveData = JsonUtility.FromJson<SaveData>(saveJson);
                if (saveData == null)
                {
                    Debug.LogError($"[SaveService] 저장 데이터 파싱 실패: {saveKey}");
                    return null;
                }

                string decryptedData = Decrypt(saveData.data);
                
                InventoryState state = JsonUtility.FromJson<InventoryState>(decryptedData);
                
                if (state == null)
                {
                    Debug.LogError("[Inventory] Failed to deserialize inventory data");
                    return null;
                }

                return state;
            }
            catch (Exception e)
            {
                Debug.LogError($"[Inventory] Error loading inventory: {e.Message}");
                return null;
            }
        }


        public void DeleteSaveData()
        {
            try
            {
                if (PlayerPrefs.HasKey(saveKey))
                {
                    PlayerPrefs.DeleteKey(saveKey);
                    PlayerPrefs.DeleteKey($"{saveKey}{VERSION_KEY}");
                    PlayerPrefs.Save();
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveService] 인벤토리 데이터 삭제 중 오류: {e.Message}");
            }
        }

        #region 암호화/복호화 (선택 사항)
        
        private string Encrypt(string data)
        {
            try
            {
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