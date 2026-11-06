using System;
using System.IO;
using System.Linq;
using Nytherion.Data.ScriptableObjects.Gacha;
using Nytherion.Data.ScriptableObjects.Relics;
using Nytherion.Data.ScriptableObjects.Skill;
using UnityEditor;
using UnityEngine;

namespace Nytherion.Editor
{
    /// <summary>사용 중단된 루티 데이터 정리 및 현재 루티 아이콘 보정 도구입니다.</summary>
    [InitializeOnLoad]
    public static class RootiDataCleanup
    {
        private const string Output = "output/rooti-cleanup";
        private const string RelicRoot = "Assets/Nytherion/Data/ScriptableObjects/Relics/SkillRelics/";
        private const string ActiveGuid = "21915b07d256e544e9113b34e4a76341";
        private const string RetiredGuid = "3cf25cce484487d4eaab681476a246ce";
        private const string IconPath = "Assets/Nytherion/Art/Skills/Sprites/Rooti.png";

        static RootiDataCleanup() => EditorApplication.update += Update;

        private static void Update()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || !File.Exists(Output + "/apply.request")) return;
            File.Delete(Output + "/apply.request");
            try { Apply(); }
            catch (Exception error)
            {
                File.WriteAllText(Output + "/result.txt", "FAIL " + error);
                Debug.LogException(error);
            }
        }

        [MenuItem("Tools/Nytherion/Rooti/아이콘 및 구형 데이터 정리")]
        public static void Apply()
        {
            Directory.CreateDirectory(Output);
            string activePath = AssetDatabase.GUIDToAssetPath(ActiveGuid);
            RelicData active = AssetDatabase.LoadAssetAtPath<RelicData>(activePath);
            if (active == null || active.isDisabled) throw new InvalidOperationException("현재 루티 유물을 찾을 수 없습니다.");
            string retiredPath = AssetDatabase.GUIDToAssetPath(RetiredGuid);
            RelicData retired = AssetDatabase.LoadAssetAtPath<RelicData>(retiredPath);
            if (retired != null)
            {
                if (!retired.isDisabled) throw new InvalidOperationException("삭제 대상 루티가 사용 중단 상태가 아닙니다.");
                // 기존 구형 에셋은 작업 출력에 사본을 보존한 후 Unity API로 삭제합니다.
                File.Copy(retiredPath, Output + "/RetiredRooti.asset.backup", true);
                File.Copy(retiredPath + ".meta", Output + "/RetiredRooti.meta.backup", true);
                foreach (string guid in AssetDatabase.FindAssets("t:RelicDatabaseSO"))
                {
                    RelicDatabaseSO database = AssetDatabase.LoadAssetAtPath<RelicDatabaseSO>(AssetDatabase.GUIDToAssetPath(guid));
                    if (database.allRelics != null && database.allRelics.RemoveAll(item => item == retired) > 0)
                    { EditorUtility.SetDirty(database); AssetDatabase.SaveAssetIfDirty(database); }
                }
                foreach (string guid in AssetDatabase.FindAssets("t:GachaPoolSO"))
                {
                    GachaPoolSO pool = AssetDatabase.LoadAssetAtPath<GachaPoolSO>(AssetDatabase.GUIDToAssetPath(guid));
                    if (pool.items != null && pool.items.RemoveAll(rate => rate != null && rate.item == retired) > 0)
                    { EditorUtility.SetDirty(pool); AssetDatabase.SaveAssetIfDirty(pool); }
                }
                // DB・가챠 이외에 참조가 남아 있으면 삭제 전에 중단합니다.
                string[] dependents = AssetDatabase.GetAllAssetPaths().Where(path => path.StartsWith("Assets/") && path != retiredPath &&
                    AssetDatabase.GetDependencies(path, false).Contains(retiredPath)).ToArray();
                if (dependents.Length > 0) throw new InvalidOperationException("구형 루티 참조가 남아 있습니다: " + string.Join(", ", dependents));
                if (!AssetDatabase.DeleteAsset(retiredPath)) throw new InvalidOperationException("구형 루티 삭제에 실패했습니다.");
            }
            if (activePath != RelicRoot + "Rooti.asset")
            {
                string error = AssetDatabase.MoveAsset(activePath, RelicRoot + "Rooti.asset");
                if (!string.IsNullOrEmpty(error)) throw new InvalidOperationException(error);
            }
            Sprite icon = AssetDatabase.LoadAssetAtPath<Sprite>(IconPath);
            Sprite relicIcon = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Nytherion/Art/Relics/Sprites/SkillRelicRooti.png");
            if (icon == null || relicIcon == null) throw new InvalidOperationException("루티의 스킬 또는 유물 아이콘이 없습니다.");
            SkillData skill = AssetDatabase.LoadAssetAtPath<SkillData>("Assets/Nytherion/Data/ScriptableObjects/Skill/Turret_Skill.asset");
            skill.icon = icon;
            skill.skillName = "루티";
            skill.description = "적을 자동으로 공격하는 루티를 소환합니다.";
            active.name = "Rooti";
            active.koreanName = "루티 마법 책";
            active.englishName = "Rooti Magic Book";
            active.Image = relicIcon;
            active.description_KR = "적을 자동으로 공격하는 루티 소환 스킬을 얻는다.";
            active.description_EN = "Grants a skill that summons Rooti to automatically attack enemies.";
            ManaRelicSetup.Localize(active);
            EditorUtility.SetDirty(skill);
            EditorUtility.SetDirty(active);
            AssetDatabase.SaveAssetIfDirty(skill);
            AssetDatabase.SaveAssetIfDirty(active);
            const string obsolete = "Assets/Nytherion/Art/Skills/Sprites/RootiIcon.png";
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(obsolete) != null &&
                !AssetDatabase.GetAllAssetPaths().Any(path => path.StartsWith("Assets/") && path != obsolete &&
                    AssetDatabase.GetDependencies(path, false).Contains(obsolete)))
                AssetDatabase.DeleteAsset(obsolete);
            if (AssetDatabase.AssetPathToGUID(RelicRoot + "Rooti.asset") != ActiveGuid || active.relicName != "Relic of Turret")
                throw new InvalidOperationException("현재 루티의 GUID 또는 저장 ID가 변경됐습니다.");
            File.WriteAllText(Output + "/result.txt",
                "PASS 루티 스킬 아이콘과 전용 유물 아이콘 연결, GUID/저장 ID 유지");
        }
    }
}
