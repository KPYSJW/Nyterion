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
            if (data == null)
            {
                Debug.LogError("[JsonSaveService] SaveData가 null입니다. 저장을 중단합니다.");
                return;
            }

            string json = JsonUtility.ToJson(data, true);
            string path = Path.Combine(Application.persistentDataPath, saveFileName);

            Debug.Log($"[JsonSaveService] 저장 시도: {path}");
            Debug.Log($"[JsonSaveService] 저장할 데이터 크기: {json.Length} 문자");

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, json);

                // 저장 검증
                if (File.Exists(path))
                {
                    long fileSize = new FileInfo(path).Length;
                    Debug.Log($"[JsonSaveService] 저장 성공! 파일 크기: {fileSize} bytes");
                }
                else
                {
                    Debug.LogError("[JsonSaveService] 저장 후 파일이 존재하지 않습니다!");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[JsonSaveService] 데이터 저장 실패: {e.Message}");
                Debug.LogError($"[JsonSaveService] 저장 경로: {path}");
                Debug.LogError($"[JsonSaveService] 스택 트레이스: {e.StackTrace}");
            }
        }

        public SaveData Load()
        {
            string path = Path.Combine(Application.persistentDataPath, saveFileName);

            if (!File.Exists(path))
            {
                Debug.Log($"[JsonSaveService] 세이브 파일이 존재하지 않음: {path}");
                return null;
            }

            try
            {
                long fileSize = new FileInfo(path).Length;

                if (fileSize == 0)
                {
                    Debug.LogWarning("[JsonSaveService] 세이브 파일이 비어있습니다!");
                    return null;
                }

                string json = File.ReadAllText(path);

                if (string.IsNullOrEmpty(json))
                {
                    Debug.LogWarning("[JsonSaveService] 읽은 JSON 데이터가 비어있습니다!");
                    return null;
                }

                SaveData data = JsonUtility.FromJson<SaveData>(json);

                if (data == null)
                {
                    Debug.LogError("[JsonSaveService] JSON 파싱 결과가 null입니다!");
                    return null;
                }

                return data;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[JsonSaveService] 데이터 로드 실패: {e.Message}");
                Debug.LogError($"[JsonSaveService] 로드 경로: {path}");
                Debug.LogError($"[JsonSaveService] 스택 트레이스: {e.StackTrace}");
                return null;
            }
        }
    }
}