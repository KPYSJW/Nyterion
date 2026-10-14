using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json.Linq;
using Nytherion.Core.Data;
using Nytherion.Core.Enums;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Nytherion.Services
{
    public enum SaveLoadStatus { Success, Missing, RecoveredBackup, Corrupt, ReadError }

    public sealed class SaveLoadResult
    {
        public SaveLoadStatus Status { get; }
        public SaveData Data { get; }
        public string Error { get; }
        public bool IsSuccess => Status == SaveLoadStatus.Success || Status == SaveLoadStatus.RecoveredBackup;

        public SaveLoadResult(SaveLoadStatus status, SaveData data = null, string error = null)
        {
            Status = status;
            Data = data;
            Error = error;
        }
    }

    public class JsonSaveService
    {
        private const string SaveFileName = "nytherion_savedata.json";
        private readonly string savePath;
        public string LastError { get; private set; }

        public JsonSaveService() : this(Path.Combine(Application.persistentDataPath, SaveFileName)) { }

        // 검증에서는 실제 사용자 세이브와 분리한 파일 경로를 사용합니다.
        public JsonSaveService(string savePath)
        {
            this.savePath = Path.GetFullPath(savePath);
        }

        public bool Save(SaveData data)
        {
            LastError = null;
            string temporaryPath = savePath + ".tmp";
            try
            {
                if (!ValidateData(data, out string error)) throw new InvalidDataException(error);
                string json = JsonUtility.ToJson(data, true);
                SaveLoadResult serialized = Parse(json);
                if (!serialized.IsSuccess) throw new InvalidDataException(serialized.Error);

                Directory.CreateDirectory(Path.GetDirectoryName(savePath));
                SaveLoadResult existing = ReadFile(savePath);
                if (existing.Status == SaveLoadStatus.ReadError) throw new IOException(existing.Error);
                if (existing.Status != SaveLoadStatus.Success)
                {
                    SaveLoadResult backup = ReadFile(savePath + ".bak");
                    if (backup.Status == SaveLoadStatus.ReadError ||
                        (existing.Status == SaveLoadStatus.Corrupt && !backup.IsSuccess) ||
                        (existing.Status == SaveLoadStatus.Missing && backup.Status == SaveLoadStatus.Corrupt))
                        throw new InvalidDataException("복구되지 않은 저장 파일을 덮어쓸 수 없습니다. " + (backup.Error ?? existing.Error));
                }

                using (FileStream stream = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    byte[] bytes = new UTF8Encoding(false).GetBytes(json);
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }
                SaveLoadResult written = ReadFile(temporaryPath);
                if (!written.IsSuccess) throw new InvalidDataException("임시 저장 검증 실패: " + written.Error);

                if (existing.Status == SaveLoadStatus.Success)
                {
                    // 교체 실패 시 원본을 삭제하지 않습니다. 백업에는 직전 정상 파일만 기록합니다.
                    File.Replace(temporaryPath, savePath, savePath + ".bak");
                }
                else
                {
                    if (existing.Status == SaveLoadStatus.Corrupt)
                    {
                        string corruptPath = savePath + ".corrupt-" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff") + "-" + Guid.NewGuid().ToString("N");
                        File.Move(savePath, corruptPath);
                    }
                    // 백업 복구 후 첫 저장에서는 정상 백업을 그대로 보존합니다.
                    File.Move(temporaryPath, savePath);
                }
                return true;
            }
            catch (Exception exception)
            {
                LastError = exception.Message;
                Debug.LogError($"[JsonSaveService] 데이터 저장 실패: {LastError}");
                return false;
            }
            finally
            {
                try { if (File.Exists(temporaryPath)) File.Delete(temporaryPath); }
                catch (Exception exception) { Debug.LogWarning($"[JsonSaveService] 임시 파일 정리 실패: {exception.Message}"); }
            }
        }

        public SaveLoadResult LoadWithResult()
        {
            SaveLoadResult primary = ReadFile(savePath);
            if (primary.IsSuccess || primary.Status == SaveLoadStatus.ReadError) return primary;
            SaveLoadResult backup = ReadFile(savePath + ".bak");
            if (backup.IsSuccess) return new SaveLoadResult(SaveLoadStatus.RecoveredBackup, backup.Data, primary.Error);
            if (backup.Status == SaveLoadStatus.ReadError) return backup;
            if (primary.Status == SaveLoadStatus.Missing && backup.Status == SaveLoadStatus.Missing) return primary;
            return new SaveLoadResult(SaveLoadStatus.Corrupt, error:
                "정상 세이브와 백업을 찾지 못했습니다. " + primary.Error + " " + backup.Error);
        }

        // 기존 호출과의 호환용입니다. 게임 초기화는 LoadWithResult로 실패 원인을 구분합니다.
        public SaveData Load() => LoadWithResult().Data;

        private static SaveLoadResult ReadFile(string path)
        {
            try { return Parse(File.ReadAllText(path, Encoding.UTF8)); }
            catch (FileNotFoundException) { return new SaveLoadResult(SaveLoadStatus.Missing); }
            catch (DirectoryNotFoundException) { return new SaveLoadResult(SaveLoadStatus.Missing); }
            catch (Exception exception) { return new SaveLoadResult(SaveLoadStatus.ReadError, error: exception.Message); }
        }

        private static SaveLoadResult Parse(string json)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(json)) throw new InvalidDataException("저장 파일이 비어 있습니다.");
                JObject root = JObject.Parse(json, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                // JsonUtility가 잘못된 형식을 기본값으로 읽어 손상을 숨기지 않도록 먼저 검사합니다.
                ValidateJsonFields(root, typeof(SaveData));
                if (!(root["currencyTypes"] is JArray) || !(root["currencyAmounts"] is JArray))
                    throw new InvalidDataException("필수 통화 저장 항목이 없습니다.");
                SaveData data = JsonUtility.FromJson<SaveData>(json);
                NormalizeOptionalData(data);
                if (!ValidateData(data, out string error)) throw new InvalidDataException(error);
                return new SaveLoadResult(SaveLoadStatus.Success, data);
            }
            catch (Exception exception) { return new SaveLoadResult(SaveLoadStatus.Corrupt, error: exception.Message); }
        }

        private static void ValidateJsonFields(JToken token, Type type)
        {
            if (token.Type == JTokenType.Null)
            {
                if (type.IsValueType) throw new InvalidDataException("값 형식 저장 항목이 null입니다.");
                return;
            }
            if (type.IsEnum || type == typeof(int))
            {
                if (token.Type != JTokenType.Integer || !int.TryParse(token.ToString(), out _))
                    throw new InvalidDataException("정수 저장 항목의 형식이 올바르지 않습니다.");
            }
            else if (type == typeof(float))
            {
                if (token.Type != JTokenType.Float && token.Type != JTokenType.Integer)
                    throw new InvalidDataException("좌표 저장 항목의 형식이 올바르지 않습니다.");
                float value = token.Value<float>();
                if (float.IsNaN(value) || float.IsInfinity(value)) throw new InvalidDataException("좌표가 유한하지 않습니다.");
            }
            else if (type == typeof(bool) || type == typeof(string))
            {
                if (token.Type != (type == typeof(bool) ? JTokenType.Boolean : JTokenType.String))
                    throw new InvalidDataException("저장 항목의 형식이 올바르지 않습니다.");
            }
            else if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
            {
                if (!(token is JArray array)) throw new InvalidDataException("목록 저장 항목의 형식이 올바르지 않습니다.");
                foreach (JToken entry in array) ValidateJsonFields(entry, type.GetGenericArguments()[0]);
            }
            else
            {
                if (!(token is JObject obj)) throw new InvalidDataException("객체 저장 항목의 형식이 올바르지 않습니다.");
                foreach (System.Reflection.FieldInfo field in type.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
                    if (obj.TryGetValue(field.Name, out JToken value)) ValidateJsonFields(value, field.FieldType);
            }
        }

        private static void NormalizeOptionalData(SaveData data)
        {
            if (data == null) return;
            // 예전 세이브에서 없던 선택 항목은 빈 상태로 복원합니다. ID와 직렬화 필드는 유지합니다.
            data.inventoryData ??= new List<ItemEntry>();
            data.quickSlotData ??= new List<QuickSlotEntry>();
            data.equippedItemsData ??= new List<EquippedItemEntry>();
            data.shopStockData ??= new List<ShopStockState>();
            data.ownedSkills ??= new List<SkillEntry>();
            data.equippedSkillIds ??= new List<string>();
            data.relicData ??= new RelicGridState();
            data.relicData.placedBlocks ??= new List<RelicGridState.SavedRelicBlock>();
            data.progressionState ??= new ProgressionState();
            data.progressionState.unlockedSkills ??= new List<string>();
            data.progressionState.completedMilestones ??= new List<string>();
            data.progressionState.activeProgresses ??= new List<MilestoneProgressEntry>();
            data.progressionState.unlockedProjectiles ??= new List<string>();
            data.dungeonMapData ??= new DungeonMapSaveData();
        }

        private static bool ValidateData(SaveData data, out string error)
        {
            error = null;
            if (data == null || data.currencyTypes == null || data.currencyAmounts == null ||
                data.currencyTypes.Count != data.currencyAmounts.Count)
                error = "통화 저장 항목이 없거나 목록 길이가 다릅니다.";
            else
            {
                HashSet<CurrencyType> currencies = new HashSet<CurrencyType>();
                for (int i = 0; i < data.currencyTypes.Count; i++)
                    if (!Enum.IsDefined(typeof(CurrencyType), data.currencyTypes[i]) || !currencies.Add(data.currencyTypes[i]) || data.currencyAmounts[i] < 0)
                        error = "통화 종류가 잘못됐거나 중복 또는 음수 수량이 있습니다.";
                HashSet<int> inventorySlots = new HashSet<int>();
                foreach (ItemEntry entry in data.inventoryData ?? new List<ItemEntry>())
                    if (entry == null || entry.slotIndex < 0 || !inventorySlots.Add(entry.slotIndex) ||
                        string.IsNullOrWhiteSpace(entry.itemId) || entry.count <= 0 || !Enum.IsDefined(typeof(Rarity), entry.rarity))
                        error = "인벤토리 저장 항목이 올바르지 않습니다.";
                HashSet<int> quickSlots = new HashSet<int>();
                foreach (QuickSlotEntry entry in data.quickSlotData ?? new List<QuickSlotEntry>())
                    if (entry == null || entry.slotIndex < 0 || !quickSlots.Add(entry.slotIndex) ||
                        string.IsNullOrWhiteSpace(entry.itemId) || entry.count <= 0 || !Enum.IsDefined(typeof(Rarity), entry.rarity))
                        error = "퀵슬롯 저장 항목이 올바르지 않습니다.";
                HashSet<EquipmentSlotType> equipmentSlots = new HashSet<EquipmentSlotType>();
                foreach (EquippedItemEntry entry in data.equippedItemsData ?? new List<EquippedItemEntry>())
                    if (entry == null || !Enum.IsDefined(typeof(EquipmentSlotType), entry.slotType) || !equipmentSlots.Add(entry.slotType) ||
                        string.IsNullOrWhiteSpace(entry.itemId) || !Enum.IsDefined(typeof(Rarity), entry.rarity))
                        error = "장비 저장 항목이 올바르지 않습니다.";
                foreach (SkillEntry entry in data.ownedSkills ?? new List<SkillEntry>())
                    if (entry == null || entry.level < 1 || entry.exp < 0) error = "스킬 저장 항목이 올바르지 않습니다.";
                foreach (ShopStockState entry in data.shopStockData ?? new List<ShopStockState>())
                    if (entry == null || entry.price < 0 || (!entry.isUnlimited && entry.remainingStock < 0))
                        error = "상점 저장 항목이 올바르지 않습니다.";
                foreach (RelicGridState.SavedRelicBlock entry in data.relicData?.placedBlocks ?? new List<RelicGridState.SavedRelicBlock>())
                    if (entry == null || string.IsNullOrWhiteSpace(entry.relicId) ||
                        !((entry.gridRow == -1 && entry.gridCol == -1) || (entry.gridRow >= 0 && entry.gridCol >= 0)))
                        error = "유물 저장 항목이 올바르지 않습니다.";
                if (data.shopRerollCount < 0) error = "상점 리롤 횟수가 음수입니다.";
            }
            return error == null;
        }

        public void DeleteSaveData()
        {
            // 백업이 남으면 다음 로드에서 복구되므로 명시적 세이브 삭제 시 함께 삭제합니다.
            foreach (string path in new[] { savePath, savePath + ".bak", savePath + ".tmp" })
                if (File.Exists(path)) File.Delete(path);
            Debug.Log("[JsonSaveService] 세이브 데이터와 백업을 삭제했습니다.");
        }

#if UNITY_EDITOR
        [MenuItem("Tools/Delete Save Data")]
        public static void ClearSaveDataMenuItem() => new JsonSaveService().DeleteSaveData();
#endif
    }
}
