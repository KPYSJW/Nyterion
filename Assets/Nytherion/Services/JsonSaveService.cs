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
                return null;
            }

            try
            {
                string json = File.ReadAllText(path);
                SaveData data = JsonUtility.FromJson<SaveData>(json);
                return data;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[JsonSaveService] 데이터 로드 실패: {e.Message}");
                return null;
            }
        }
    }
}