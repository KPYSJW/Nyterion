using System;
using System.IO;
using System.Linq;
using Nytherion.Data.ScriptableObjects.Skill;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace Nytherion.Editor
{
    /// <summary>포탈 애니메이션을 유지하며 UI 아이콘의 투명 여백만 줄입니다.</summary>
    [InitializeOnLoad]
    public static class PortalGateIconSetup
    {
        private const string Output = "output/portal-gate";
        private const string IconName = "PortalGateIcon";

        static PortalGateIconSetup() => EditorApplication.update += Update;

        private static void Update()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || !File.Exists(Output + "/icon.request")) return;
            File.Delete(Output + "/icon.request");
            try { Apply(); }
            catch (Exception error)
            {
                File.WriteAllText(Output + "/icon-result.txt", "FAIL " + error);
                Debug.LogException(error);
            }
        }

        [MenuItem("Tools/Nytherion/Portal Gate/아이콘 여백 보정")]
        public static void Apply()
        {
            Directory.CreateDirectory(Output);
            string path = PortalGateSkillSetup.SheetPath;
            Sprite[] frames = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>()
                .Where(sprite => sprite.name.StartsWith("PortalGate_")).ToArray();
            var originalIds = frames.ToDictionary(sprite => sprite.name, sprite =>
            {
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(sprite, out string guid, out long id);
                return id;
            });
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            var factories = new SpriteDataProviderFactories();
            factories.Init();
            var provider = factories.GetSpriteEditorDataProviderFromObject(importer);
            provider.InitSpriteEditorDataProvider();
            var rects = provider.GetSpriteRects().ToList();
            SpriteRect icon = rects.FirstOrDefault(rect => rect.name == IconName);
            if (icon == null)
            {
                icon = new SpriteRect { name = IconName, spriteID = GUID.Generate() };
                rects.Add(icon);
            }
            // 첫 프레임의 실제 그림(18x20)에 여유를 남겨 22x22 영역을 사용합니다.
            icon.rect = new Rect(5f, 5f, 22f, 22f);
            icon.alignment = SpriteAlignment.Center;
            icon.pivot = Vector2.one * 0.5f;
            provider.SetSpriteRects(rects.ToArray());
            provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(
                rects.Select(rect => new SpriteNameFileIdPair(rect.name, rect.spriteID)));
            provider.Apply();
            importer.SaveAndReimport();
            Sprite[] sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToArray();
            foreach (var frame in originalIds)
            {
                Sprite current = sprites.Single(sprite => sprite.name == frame.Key);
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(current, out string guid, out long id);
                if (id != frame.Value) throw new InvalidOperationException("기존 애니메이션 프레임 참조가 변경됐습니다: " + frame.Key);
            }
            SkillData data = AssetDatabase.LoadAssetAtPath<SkillData>(PortalGateSkillSetup.DataPath);
            data.icon = sprites.Single(sprite => sprite.name == IconName);
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssetIfDirty(data);
            File.WriteAllText(Output + "/icon-result.txt",
                "PASS UI 아이콘 32x32 → 22x22, 표시 크기 약 1.45배, 기존 애니메이션 프레임 " + originalIds.Count + "개 ID 유지, 스킬 아이콘 참조 저장");
        }
    }
}
