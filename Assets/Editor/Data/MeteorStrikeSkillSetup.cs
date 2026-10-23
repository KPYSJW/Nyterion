using System;
using System.IO;
using Nytherion.Data.ScriptableObjects.Relics;
using Nytherion.Data.ScriptableObjects.Skill;
using Nytherion.GamePlay.Skills;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Nytherion.Editor
{
    /// <summary>기존 스킬의 GUID와 기본 능력치를 보존하면서 전용 데이터 및 프리팹을 연결합니다.</summary>
    [InitializeOnLoad]
    public static class MeteorStrikeSkillSetup
    {
        public const string DataPath = "Assets/Nytherion/Data/ScriptableObjects/Skill/MeteorStrike_Skill.asset";
        public const string SkillPath = "Assets/Prefabs/Gameplay/Skills/MeteorStrikeSkill.prefab";
        public const string ImpactPath = "Assets/Prefabs/Gameplay/Skills/MeteorStrikeImpact.prefab";
        public const string Output = "output/meteor-strike";
        public const string Description = "시전 순간 카메라 안의 모든 적을 조준해 운석을 하나씩 시간차로 떨어뜨립니다. 레벨이 오를수록 피해량이 증가합니다.";
        public const string EnglishDescription = "Targets every enemy in view when cast, dropping a meteor on each in quick succession. Damage increases with skill level.";

        static MeteorStrikeSkillSetup() => EditorApplication.delayCall += SetupIfMissing;

        private static void SetupIfMissing()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            { EditorApplication.delayCall += SetupIfMissing; return; }
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            SkillData data = AssetDatabase.LoadAssetAtPath<SkillData>(DataPath);
            if (data != null && (!(data is MeteorStrikeSkillData meteor) ||
                meteor.skillPrefab == null || meteor.impactPrefab == null)) CreateAssets();
        }

        [MenuItem("Tools/Nytherion/Meteor Strike/Connect Assets")]
        public static void CreateAssets()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            Directory.CreateDirectory(Output);
            try
            {
                SkillData original = Require<SkillData>(DataPath);
                if (!(original is MeteorStrikeSkillData))
                {
                    // 같은 에셋의 스크립트만 교체해 스킬 ID, GUID, 저장 상태와 유물 참조를 유지합니다.
                    MeteorStrikeSkillData sample = ScriptableObject.CreateInstance<MeteorStrikeSkillData>();
                    SerializedObject serialized = new SerializedObject(original);
                    serialized.FindProperty("m_Script").objectReferenceValue = MonoScript.FromScriptableObject(sample);
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    Object.DestroyImmediate(sample);
                    // 스크립트 교체 후 Unity가 관리 객체를 다시 만들므로 새 참조로 저장합니다.
                    Save(Require<SkillData>(DataPath));
                    AssetDatabase.ImportAsset(DataPath, ImportAssetOptions.ForceUpdate);
                }
                MeteorStrikeSkillData data = Require<MeteorStrikeSkillData>(DataPath);
                if (data.targetLayers.value == 0) data.targetLayers = LayerMask.GetMask("Enemy");
                data.description = Description;
                if (data.icon == null)
                    data.icon = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Nytherion/Art/Combat/Weapons/Sprites/Staff/Meteor_Icon.png");
                GameObject impact = AssetDatabase.LoadAssetAtPath<GameObject>(ImpactPath);
                if (impact == null) impact = CreatePrefab(data, false);
                data.impactPrefab = impact.GetComponent<MeteorStrikeImpact>();
                GameObject skill = AssetDatabase.LoadAssetAtPath<GameObject>(SkillPath);
                if (skill == null) skill = CreatePrefab(data, true);
                data.skillPrefab = skill;
                Save(data);

                RelicData relic = Require<RelicData>("Assets/Nytherion/Data/ScriptableObjects/Relics/SkillRelics/Meteorite.asset");
                RelicDescriptionSetup.GetSkillDescriptions(data, out relic.description_KR, out relic.description_EN);
                Save(relic);
                File.WriteAllText(Output + "/setup.txt", "PASS 기존 스킬 GUID·ID 보존, 전용 데이터·실행 프리팹·낙하 애니메이션·아이콘·유물 설명 연결\n");
                Debug.Log("[MeteorStrikeSkillSetup] 화면 전체 순차 조준 스킬 연결 완료.", data);
            }
            catch (Exception error)
            {
                File.WriteAllText(Output + "/setup.txt", "FAIL " + error);
                Debug.LogException(error);
                throw;
            }
        }

        private static GameObject CreatePrefab(MeteorStrikeSkillData data, bool isSkill)
        {
            Scene preview = EditorSceneManager.NewPreviewScene();
            try
            {
                GameObject root = new GameObject(isSkill ? "MeteorStrikeSkill" : "MeteorStrikeImpact");
                SceneManager.MoveGameObjectToScene(root, preview);
                if (isSkill) root.AddComponent<MeteorStrikeSkill>().skillData = data;
                else
                {
                    // 무기용 충돌 로직을 복사하지 않고 기존 운석의 그림과 애니메이션만 재사용합니다.
                    GameObject template = Require<GameObject>("Assets/Prefabs/Gameplay/Combat/Proj/MeteorProj.prefab");
                    EditorUtility.CopySerialized(template.GetComponent<SpriteRenderer>(), root.AddComponent<SpriteRenderer>());
                    EditorUtility.CopySerialized(template.GetComponent<Animator>(), root.AddComponent<Animator>());
                    root.AddComponent<MeteorStrikeImpact>();
                    root.SetActive(false);
                }
                return PrefabUtility.SaveAsPrefabAsset(root, isSkill ? SkillPath : ImpactPath);
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
        }

        private static T Require<T>(string path) where T : Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null) throw new MissingReferenceException("필수 에셋이 없습니다: " + path);
            return asset;
        }

        private static void Save(Object asset)
        {
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssetIfDirty(asset);
        }
    }
}
