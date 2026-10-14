using System;
using System.IO;
using System.Linq;
using Nytherion.Data.ScriptableObjects.Weapons;
using Nytherion.GamePlay.Combat;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Nytherion.Editor
{
    /// <summary>ManaBall의 현재 연출 설정을 기준으로 심연 연출을 연결합니다.</summary>
    [InitializeOnLoad]
    public static class ShamanStaffMuzzleSetup
    {
        public const string Output = "output/shaman-staff-muzzle";
        private const string WeaponPath = "Assets/Nytherion/Data/ScriptableObjects/Weapons/ShamanStaff.asset";
        private const string SourceSheetPath = "Assets/Nytherion/Art/Combat/ManaBallMuzzleFlash.png";
        private const string TargetSheetPath = "Assets/Nytherion/Art/Combat/AbyssalFlameMuzzleFlash.png";
        private const string TargetPrefabPath = "Assets/Prefabs/Gameplay/Combat/VFX/AbyssalFlameMuzzleFlash.prefab";
        private const string TargetAnimationFolder = "Assets/Nytherion/Art/Combat/VFX/Animations/Weapons/AbyssalFlameMuzzleFlash";

        static ShamanStaffMuzzleSetup() => EditorApplication.update += Update;

        private static void Update()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode) return;
            string request = Output + "/setup.request";
            if (!File.Exists(request)) return;
            File.Delete(request);
            try { Apply(); }
            catch (Exception error)
            {
                File.WriteAllText(Output + "/setup.txt", "FAIL " + error);
                Debug.LogException(error);
            }
        }

        [MenuItem("Tools/Nytherion/발사 연출/ShamanStaff 심연 연출을 ManaBall에 맞추기")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("편집 모드에서 실행해 주세요.");
            Directory.CreateDirectory(Output);
            WeaponData data = AssetDatabase.LoadAssetAtPath<WeaponData>(WeaponPath);
            if (data == null || !(data.weaponPrefab is ShamanStaff) || data.fireEffectPrefab == null)
                throw new InvalidOperationException("ShamanStaff의 기본 ManaBall 발사 연출을 먼저 연결해 주세요.");
            GameObject source = data.fireEffectPrefab;
            SpriteMuzzleFlashEffect sourceEffect = source.GetComponent<SpriteMuzzleFlashEffect>();
            if (sourceEffect == null) throw new InvalidOperationException("기본 발사 연출에 SpriteMuzzleFlashEffect가 필요합니다.");
            var sourceSettings = new SerializedObject(sourceEffect);
            var sourceClip = sourceSettings.FindProperty("animationClip").objectReferenceValue as AnimationClip;
            Vector3 offset = sourceSettings.FindProperty("muzzleOffset").vector3Value;
            Sprite[] sourceFrames = WeaponFireEffectAssets.LoadFrames(AssetDatabase.LoadAssetAtPath<Texture2D>(SourceSheetPath));
            var binding = EditorCurveBinding.PPtrCurve(string.Empty, typeof(SpriteRenderer), "m_Sprite");
            var sourceKeys = sourceClip != null ? AnimationUtility.GetObjectReferenceCurve(sourceClip, binding) : null;
            if (sourceFrames.Length == 0 || sourceKeys == null || sourceKeys.Length == 0 ||
                sourceKeys.Any(key => Array.IndexOf(sourceFrames, key.value as Sprite) < 0))
                throw new InvalidOperationException("ManaBall의 프레임과 발사 애니메이션을 확인해 주세요.");

            MatchSpriteLayout();
            Sprite[] frames = WeaponFireEffectAssets.LoadFrames(AssetDatabase.LoadAssetAtPath<Texture2D>(TargetSheetPath));
            WeaponData temporary = Object.Instantiate(data);
            GameObject target;
            try
            {
                target = WeaponFireEffectAssets.CreateAndAssign(temporary, frames, 0, frames.Length - 1,
                    sourceClip.frameRate, source.transform.localScale.x, offset, TargetPrefabPath, TargetAnimationFolder);
            }
            finally { Object.DestroyImmediate(temporary); }

            var targetSettings = new SerializedObject(target.GetComponent<SpriteMuzzleFlashEffect>());
            var targetClip = (AnimationClip)targetSettings.FindProperty("animationClip").objectReferenceValue;
            string clipName = targetClip.name;
            EditorUtility.CopySerialized(sourceClip, targetClip);
            targetClip.name = clipName;
            // 종료 이벤트와 키 시각까지 복사하고, 이미지 참조만 교체합니다.
            var targetKeys = sourceKeys.Select(key => new ObjectReferenceKeyframe {
                time = key.time, value = frames[Array.IndexOf(sourceFrames, key.value as Sprite)] }).ToArray();
            AnimationUtility.SetObjectReferenceCurve(targetClip, binding, targetKeys);
            EditorUtility.SetDirty(targetClip);
            AssetDatabase.SaveAssetIfDirty(targetClip);

            GameObject root = PrefabUtility.LoadPrefabContents(TargetPrefabPath);
            try
            {
                root.transform.localPosition = source.transform.localPosition;
                root.transform.localRotation = source.transform.localRotation;
                root.transform.localScale = source.transform.localScale;
                var serialized = new SerializedObject(root.GetComponent<SpriteMuzzleFlashEffect>());
                serialized.FindProperty("sortingOrderOffset").intValue = sourceSettings.FindProperty("sortingOrderOffset").intValue;
                serialized.FindProperty("minimumSortingOrder").intValue = sourceSettings.FindProperty("minimumSortingOrder").intValue;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                root.GetComponent<SpriteRenderer>().sprite = (Sprite)targetKeys[0].value;
                PrefabUtility.SaveAsPrefabAsset(root, TargetPrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }

            string weaponPrefabPath = AssetDatabase.GetAssetPath(data.weaponPrefab);
            root = PrefabUtility.LoadPrefabContents(weaponPrefabPath);
            try
            {
                var serialized = new SerializedObject(root.GetComponent<ShamanStaff>());
                serialized.FindProperty("abyssalFlameFireEffectPrefab").objectReferenceValue = target;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, weaponPrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }

            var linked = new SerializedObject(target.GetComponent<SpriteMuzzleFlashEffect>());
            if (target.transform.localScale != source.transform.localScale ||
                linked.FindProperty("muzzleOffset").vector3Value != offset ||
                targetClip.frameRate != sourceClip.frameRate || !Mathf.Approximately(targetClip.length, sourceClip.length) ||
                frames.Where((frame, index) => frame.rect != sourceFrames[index].rect ||
                    frame.pivot != sourceFrames[index].pivot || frame.pixelsPerUnit != sourceFrames[index].pixelsPerUnit).Any())
                throw new InvalidOperationException("ManaBall과 심연 연출의 설정 동기화에 실패했습니다.");
            File.WriteAllLines(Output + "/dependencies.txt", AssetDatabase.GetDependencies(new[] {
                WeaponPath, "Assets/Nytherion/Data/ScriptableObjects/Relics/Elemental/EyeOfAbyss.asset" }, true));
            File.WriteAllText(Output + "/setup.txt", "PASS ManaBall 기준으로 심연 연출 동기화: " +
                frames.Length + "프레임 / " + sourceClip.frameRate + "fps / " + sourceClip.length +
                "초 / 크기 " + source.transform.localScale + " / 오프셋 " + offset + " / 프레임 영역·피벗·PPU 일치");
            Debug.Log("[ShamanStaffMuzzleSetup] 심연 연출을 ManaBall 설정에 맞췄습니다.");
        }

        private static void MatchSpriteLayout()
        {
            Texture2D source = AssetDatabase.LoadAssetAtPath<Texture2D>(SourceSheetPath);
            Texture2D target = AssetDatabase.LoadAssetAtPath<Texture2D>(TargetSheetPath);
            if (source == null || target == null || source.width != target.width || source.height != target.height)
                throw new InvalidOperationException("심연 시트의 이미지 크기를 ManaBall과 동일하게 맞춰 주세요.");
            var sourceImporter = (TextureImporter)AssetImporter.GetAtPath(SourceSheetPath);
            var targetImporter = (TextureImporter)AssetImporter.GetAtPath(TargetSheetPath);
            targetImporter.textureType = TextureImporterType.Sprite;
            targetImporter.spriteImportMode = SpriteImportMode.Multiple;
            targetImporter.spritePixelsPerUnit = sourceImporter.spritePixelsPerUnit;
            targetImporter.filterMode = sourceImporter.filterMode;
            targetImporter.mipmapEnabled = sourceImporter.mipmapEnabled;
            targetImporter.alphaIsTransparency = sourceImporter.alphaIsTransparency;
            targetImporter.textureCompression = sourceImporter.textureCompression;
            var factory = new SpriteDataProviderFactories();
            factory.Init();
            var sourceProvider = factory.GetSpriteEditorDataProviderFromObject(sourceImporter);
            var targetProvider = factory.GetSpriteEditorDataProviderFromObject(targetImporter);
            sourceProvider.InitSpriteEditorDataProvider();
            targetProvider.InitSpriteEditorDataProvider();
            SpriteRect[] sourceRects = sourceProvider.GetSpriteRects().OrderByDescending(rect => rect.rect.y).ThenBy(rect => rect.rect.x).ToArray();
            SpriteRect[] previous = targetProvider.GetSpriteRects().OrderByDescending(rect => rect.rect.y).ThenBy(rect => rect.rect.x).ToArray();
            SpriteRect[] rects = sourceRects.Select((rect, index) => new SpriteRect {
                name = index < previous.Length ? previous[index].name : "AbyssalFlameMuzzleFlash_" + index,
                spriteID = index < previous.Length ? previous[index].spriteID : GUID.Generate(),
                rect = rect.rect, alignment = rect.alignment, pivot = rect.pivot, border = rect.border }).ToArray();
            targetProvider.SetSpriteRects(rects);
            targetProvider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(
                rects.Select(rect => new SpriteNameFileIdPair(rect.name, rect.spriteID)));
            targetProvider.Apply();
            targetImporter.SaveAndReimport();
        }
    }
}