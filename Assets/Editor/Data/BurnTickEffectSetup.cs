using System;
using System.IO;
using Nytherion.Data.ScriptableObjects;
using Nytherion.GamePlay.Combat;
using UnityEditor;
using UnityEngine;

namespace Nytherion.Editor
{
    [InitializeOnLoad]
    public static class BurnTickEffectSetup
    {
        private const string Output = "output/burn-tick";
        private const string SheetPath = "Assets/Nytherion/Art/Combat/BurnParticleSheet.png";
        private const string PrefabPath = "Assets/Prefabs/Gameplay/Combat/VFX/BurnTickVFX.prefab";
        private const string DataPath = "Assets/Nytherion/Data/StatusEffectDatabase.asset";

        static BurnTickEffectSetup() => EditorApplication.update += ProcessRequest;

        private static void ProcessRequest()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(Output + "/five-frame-setup.request")) return;
            File.Delete(Output + "/five-frame-setup.request");
            try { Apply(); }
            catch (Exception error)
            {
                File.WriteAllText(Output + "/verification.txt", "FAIL " + error);
                Debug.LogException(error);
            }
        }

        [MenuItem("Tools/Nytherion/상태이상/화상 피해 이펙트 연결")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("편집 모드에서 실행해 주세요.");
            Directory.CreateDirectory(Output);
            AssetDatabase.ImportAsset(SheetPath, ImportAssetOptions.ForceSynchronousImport);
            Texture2D sheet = AssetDatabase.LoadAssetAtPath<Texture2D>(SheetPath);
            StatusEffectDatabase data = AssetDatabase.LoadAssetAtPath<StatusEffectDatabase>(DataPath);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (sheet == null || sheet.width != 160 || sheet.height != 32 || data == null || prefab == null ||
                prefab.GetComponent<BurnTickVFX>() == null)
                throw new InvalidOperationException("160×32 화상 시트, 프리팹과 상태이상 데이터베이스를 확인해 주세요.");
            WeaponFireEffectAssets.SliceGrid(sheet, new Vector2Int(32, 32), 32f);
            Sprite[] frames = WeaponFireEffectAssets.LoadFrames(sheet);
            if (frames.Length != 5) throw new InvalidOperationException("화상 시트는 프레임 5장이어야 합니다.");
            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                BurnTickVFX effect = root.GetComponent<BurnTickVFX>();
                SerializedObject serialized = new SerializedObject(effect);
                SerializedProperty frameArray = serialized.FindProperty("frames");
                frameArray.arraySize = frames.Length;
                for (int i = 0; i < frames.Length; i++) frameArray.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];
                serialized.ApplyModifiedPropertiesWithoutUndo();
                foreach (SpriteRenderer oldFlame in root.GetComponentsInChildren<SpriteRenderer>(true)) oldFlame.enabled = false;
                effect.enabled = false;
                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                data.burnTickPrefab = saved.GetComponent<BurnTickVFX>();
                EditorUtility.SetDirty(data);
                AssetDatabase.SaveAssetIfDirty(data);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            Verify();
            Debug.Log("[BurnTickEffectSetup] 화상 파티클 시트와 프리팹 참조를 연결했습니다.", data);
        }

        public static void Verify()
        {
            StatusEffectDatabase data = AssetDatabase.LoadAssetAtPath<StatusEffectDatabase>(DataPath);
            Texture2D sheet = AssetDatabase.LoadAssetAtPath<Texture2D>(SheetPath);
            Sprite[] frames = WeaponFireEffectAssets.LoadFrames(sheet);
            if (data == null || data.burnTickPrefab == null || frames.Length != 5)
                throw new InvalidOperationException("화상 프리팹 참조 또는 프레임 검증 실패");
            SerializedProperty configured = new SerializedObject(data.burnTickPrefab).FindProperty("frames");
            if (configured == null || configured.arraySize != frames.Length)
                throw new InvalidOperationException("화상 프리팹 프레임 개수가 올바르지 않습니다.");
            for (int i = 0; i < frames.Length; i++)
                if (configured.GetArrayElementAtIndex(i).objectReferenceValue != frames[i])
                    throw new InvalidOperationException("화상 프레임 참조 또는 순서가 올바르지 않습니다.");
            File.WriteAllText(Output + "/verification.txt", "PASS 32×32 프레임 5장, 프리팹 및 데이터베이스 참조\n");
        }
    }
}
