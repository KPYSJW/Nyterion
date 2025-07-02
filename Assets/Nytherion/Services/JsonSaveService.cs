using UnityEngine;
using Nytherion.Core.Data;
using System.IO;

namespace Nytherion.Services
{
    public class JsonSaveService
    {
        private static readonly string saveFileName = "nytherion_savedata.json";

        public void Save(SaveData data)
        {
            string json = JsonUtility.ToJson(data, true);
            string path = Path.Combine(Application.persistentDataPath, saveFileName);

            try
            {
                File.WriteAllText(path, json);
                #if UNITY_EDITOR
                Debug.Log($"<color=cyan>[JsonSaveService] 데이터 저장 성공: {path}</color>");
                #endif
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[JsonSaveService] 데이터 저장 실패: {e.Message}");
            }
        }

        public SaveData Load()
        {
            string path = Path.Combine(Application.persistentDataPath, saveFileName);

            if (!File.Exists(path))
            {
                #if UNITY_EDITOR
                Debug.LogWarning("[JsonSaveService] 저장된 파일이 없습니다. 새 데이터를 생성합니다.");
                #endif
                return new SaveData(); 
            }

            try
            {
                string json = File.ReadAllText(path);
                SaveData data = JsonUtility.FromJson<SaveData>(json);
                #if UNITY_EDITOR
                Debug.Log($"<color=lime>[JsonSaveService] 데이터 로드 성공: {path}</color>");
                #endif
                return data;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[JsonSaveService] 데이터 로드 실패: {e.Message}");
                return new SaveData(); 
            }
        }
    }
}