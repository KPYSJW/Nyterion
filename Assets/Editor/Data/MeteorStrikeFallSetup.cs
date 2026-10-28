using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace Nytherion.Editor
{
    /// <summary>메테오 스킬 전용 낙하·착탄 시트와 애니메이션을 연결합니다.</summary>
    [InitializeOnLoad]
    public static class MeteorStrikeFallSetup
    {
        private const string Sheet = "Assets/Nytherion/Art/Combat/VFX/Sprites/MeteorStrikeFall.png";
        private const string ClipPath = "Assets/Nytherion/Art/Combat/VFX/Animations/MeteorStrikeFall.anim";
        private const string ExplosionSheet = "Assets/Nytherion/Art/Combat/VFX/Sprites/MeteorStrikeExplosion.png";
        private const string ExplosionClipPath = "Assets/Nytherion/Art/Combat/VFX/Animations/MeteorStrikeExplosion.anim";
        private const string ControllerPath = "Assets/Nytherion/Art/Combat/VFX/Animations/MeteorStrikeSkill.overrideController";
        private const string Request = MeteorStrikeSkillSetup.Output + "/fall-setup.request";

        static MeteorStrikeFallSetup() => EditorApplication.update += Update;

        private static void Update()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(Request)) return;
            File.Delete(Request);
            Connect();
        }

        [MenuItem("Tools/Nytherion/Meteor Strike/Connect Fall And Explosion Sprite Sheets")]
        public static void Connect()
        {
            Directory.CreateDirectory(MeteorStrikeSkillSetup.Output);
            try
            {
                AssetDatabase.ImportAsset(Sheet, ImportAssetOptions.ForceSynchronousImport);
                TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(Sheet);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Multiple;
                importer.spritePixelsPerUnit = 32f;
                importer.filterMode = FilterMode.Point;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                // 원본 마지막 프레임은 임포트에서 제외하고 2~5번째 프레임만 반복합니다.
                Slice(importer, "MeteorStrikeFall", 5, new Vector2(0.5f, 0.5f));
                Sprite[] frames = AssetDatabase.LoadAllAssetsAtPath(Sheet).OfType<Sprite>().OrderBy(frame => frame.name).ToArray();
                if (frames.Length != 5) throw new InvalidOperationException("낙하 시트의 마지막 프레임 제외에 실패했습니다.");
                Sprite[] loopFrames = frames.Skip(1).ToArray();

                AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);
                if (clip == null)
                {
                    clip = new AnimationClip { name = "MeteorStrikeFall" };
                    AssetDatabase.CreateAsset(clip, ClipPath);
                }
                clip.frameRate = 12f;
                AnimationUtility.SetObjectReferenceCurve(clip,
                    EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"),
                    loopFrames.Select((frame, index) => new ObjectReferenceKeyframe { time = index / 12f, value = frame }).ToArray());
                AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
                settings.startTime = 0f;
                settings.stopTime = loopFrames.Length / clip.frameRate;
                settings.loopTime = true;
                AnimationUtility.SetAnimationClipSettings(clip, settings);
                Save(clip);
                AnimationClip explosion = CreateExplosion();

                RuntimeAnimatorController original = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                    "Assets/Nytherion/Art/Combat/VFX/Animations/Meteor.controller");
                AnimatorOverrideController controller = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(ControllerPath);
                if (controller == null)
                {
                    controller = new AnimatorOverrideController(original);
                    AssetDatabase.CreateAsset(controller, ControllerPath);
                }
                controller.runtimeAnimatorController = original;
                controller["Fall"] = clip;
                controller["Explode"] = explosion;
                Save(controller);

                GameObject root = PrefabUtility.LoadPrefabContents(MeteorStrikeSkillSetup.ImpactPath);
                try
                {
                    root.GetComponent<Animator>().runtimeAnimatorController = controller;
                    root.GetComponent<SpriteRenderer>().sprite = loopFrames[0];
                    PrefabUtility.SaveAsPrefabAsset(root, MeteorStrikeSkillSetup.ImpactPath);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }

                ObjectReferenceKeyframe[] keys = AnimationUtility.GetObjectReferenceCurve(clip,
                    EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"));
                if (keys.Length != 4 || !keys.Select(key => key.value).SequenceEqual(loopFrames.Cast<UnityEngine.Object>()) ||
                    !AnimationUtility.GetAnimationClipSettings(clip).loopTime ||
                    !Mathf.Approximately(clip.length, 4f / 12f))
                    throw new InvalidOperationException("낙하 애니메이션의 2~5프레임 반복 검증에 실패했습니다.");
                if (controller["Fall"] != clip || controller["Explode"] != explosion ||
                    !AnimationUtility.GetAnimationEvents(explosion).Any(evt => evt.functionName == "ApplyDamage" && Mathf.Approximately(evt.time, 0.1f)) ||
                    !AnimationUtility.GetAnimationEvents(explosion).Any(evt => evt.functionName == "DisableProjectile"))
                    throw new InvalidOperationException("낙하·폭발 교체 또는 피해·반환 이벤트 검증에 실패했습니다.");
                File.WriteAllText(MeteorStrikeSkillSetup.Output + "/fall-setup.txt",
                    "PASS Unity 컴파일·낙하 2~5프레임 반복·마지막 프레임 임포트 제외·루프 길이 4/12초·착탄 9프레임 단발·12fps·스킬 전용 프리팹 연결·착탄 0.1초 피해·폭발 종료 후 반환\n");
            }
            catch (Exception error)
            {
                File.WriteAllText(MeteorStrikeSkillSetup.Output + "/fall-setup.txt", "FAIL " + error);
                Debug.LogException(error);
            }
        }

        private static AnimationClip CreateExplosion()
        {
            AssetDatabase.ImportAsset(ExplosionSheet, ImportAssetOptions.ForceSynchronousImport);
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(ExplosionSheet);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = 32f;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            Slice(importer, "MeteorStrikeExplosion", 9, new Vector2(0.5f, 0f));
            Sprite[] frames = AssetDatabase.LoadAllAssetsAtPath(ExplosionSheet).OfType<Sprite>().OrderBy(frame => frame.name).ToArray();
            if (frames.Length != 9) throw new InvalidOperationException("폭발 시트의 9프레임 분할에 실패했습니다.");
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ExplosionClipPath);
            if (clip == null)
            {
                clip = new AnimationClip { name = "MeteorStrikeExplosion" };
                AssetDatabase.CreateAsset(clip, ExplosionClipPath);
            }
            clip.frameRate = 12f;
            AnimationUtility.SetObjectReferenceCurve(clip,
                EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"),
                frames.Select((frame, index) => new ObjectReferenceKeyframe { time = index / 12f, value = frame }).ToArray());
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = false;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            AnimationUtility.SetAnimationEvents(clip, new[]
            {
                new AnimationEvent { time = 0.1f, functionName = "ApplyDamage" },
                new AnimationEvent { time = 9f / 12f - 0.001f, functionName = "DisableProjectile" }
            });
            Save(clip);
            return clip;
        }

        private static void Slice(TextureImporter importer, string prefix, int count, Vector2 pivot)
        {
            SpriteDataProviderFactories factory = new SpriteDataProviderFactories();
            factory.Init();
            ISpriteEditorDataProvider provider = factory.GetSpriteEditorDataProviderFromObject(importer);
            provider.InitSpriteEditorDataProvider();
            SpriteRect[] previous = provider.GetSpriteRects();
            SpriteRect[] rects = Enumerable.Range(0, count).Select(index =>
            {
                string name = prefix + "_" + index;
                SpriteRect old = previous.FirstOrDefault(rect => rect.name == name);
                return new SpriteRect
                {
                    name = name, rect = new Rect(index * 64, 0, 64, 64),
                    alignment = SpriteAlignment.Custom, pivot = pivot,
                    spriteID = old != null ? old.spriteID : GUID.Generate()
                };
            }).ToArray();
            provider.SetSpriteRects(rects);
            provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(
                rects.Select(rect => new SpriteNameFileIdPair(rect.name, rect.spriteID)));
            provider.Apply();
            importer.SaveAndReimport();
        }

        private static void Save(UnityEngine.Object asset)
        {
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssetIfDirty(asset);
        }
    }
}
