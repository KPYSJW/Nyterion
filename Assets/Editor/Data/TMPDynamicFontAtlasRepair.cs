using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace Nytherion.Editor
{
    /// <summary>플레이 종료 후 사라진 동적 아틀라스와 해당 글리프 캐시를 함께 복구한다.</summary>
    [InitializeOnLoad]
    public static class TMPDynamicFontAtlasRepair
    {
        private const string Output = "output/relic-effect-tooltip";
        private static bool pending = true;

        static TMPDynamicFontAtlasRepair()
        {
            EditorApplication.update += Update;
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredEditMode) pending = true;
                if (state == PlayModeStateChange.ExitingEditMode) RepairLoadedFonts();
            };
        }

        private static void Update()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode) return;

            string request = Output + "/atlas-repair.request";
            bool requested = File.Exists(request);
            if (!pending && !requested) return;
            pending = false;
            if (requested) File.Delete(request);
            List<string> repaired = RepairLoadedFonts();
            if (requested)
            {
                Directory.CreateDirectory(Output);
                File.WriteAllLines(Output + "/atlas-repair.txt", repaired);
            }
        }

        [MenuItem("Tools/Nytherion/폰트/누락된 TMP 동적 아틀라스 복구")]
        public static void RepairFromMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[TMP 폰트] 플레이를 종료한 뒤 아틀라스를 복구해 주세요.");
                return;
            }
            RepairLoadedFonts();
        }

        public static List<string> RepairLoadedFonts()
        {
            var results = new List<string>();
            foreach (TMP_FontAsset font in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
            {
                if (font == null || font.atlasPopulationMode != AtlasPopulationMode.Dynamic ||
                    !EditorUtility.IsPersistent(font) || !HasMissingAtlas(font)) continue;

                string path = AssetDatabase.GetAssetPath(font);
                if (font.sourceFontFile == null || font.atlasTextures == null ||
                    font.atlasTextures.Length == 0 || font.atlasTextures[0] == null)
                {
                    results.Add("FAIL: " + path);
                    Debug.LogError("[TMP 폰트] 원본 글꼴과 기본 아틀라스 확인이 필요합니다: " + path);
                    continue;
                }

                // 글꼴·스타일·주 에셋 GUID는 유지하고, 사용할 수 없는 아틀라스의 글리프도 다시 굽는다.
                uint[] characters = font.characterTable.Select(character => character.unicode).Distinct().ToArray();
                font.ClearFontAssetData();
                font.TryAddCharacters(characters, out _);
                EditorUtility.SetDirty(font);
                AssetDatabase.SaveAssetIfDirty(font);
                TMPro_EventManager.ON_FONT_PROPERTY_CHANGED(true, font);
                if (HasMissingAtlas(font))
                {
                    results.Add("FAIL: " + path);
                    continue;
                }
                results.Add("PASS: " + path);
                Debug.Log("[TMP 폰트] 누락된 동적 아틀라스 참조 복구: " + path);
            }
            return results;
        }

        private static bool HasMissingAtlas(TMP_FontAsset font)
        {
            Texture2D[] textures = font.atlasTextures;
            var serialized = new SerializedObject(font);
            int activeIndex = serialized.FindProperty("m_AtlasTextureIndex").intValue;
            if (textures == null || activeIndex < 0 || activeIndex >= textures.Length ||
                textures[activeIndex] == null) return true;

            // 배열 끝의 미사용 슬롯은 정상이다. 실제 글리프가 사용하는 텍스처만 검사한다.
            foreach (var glyph in font.glyphTable)
            {
                if (glyph.atlasIndex >= textures.Length || textures[glyph.atlasIndex] == null) return true;
            }
            return false;
        }
    }
}
