using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace Nytherion.Editor
{
    /// <summary>Galmuri 원본으로 누락된 동적 TMP 폰트 에셋을 생성한다.</summary>
    [InitializeOnLoad]
    public static class GalmuriFontSetup
    {
        private const string Folder = "Assets/Nytherion/Art/UI/Fonts/Galmuri";
        private const string Output = "output/galmuri-font";
        private static readonly string[] FontNames =
        {
            "Galmuri7", "Galmuri9", "Galmuri11", "Galmuri11-Bold", "Galmuri11-Condensed",
            "Galmuri14", "GalmuriMono7", "GalmuriMono9", "GalmuriMono11"
        };

        static GalmuriFontSetup() => EditorApplication.update += Update;

        private static void Update()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
                !File.Exists(Output + "/setup.request")) return;
            File.Delete(Output + "/setup.request");
            try { Apply(); }
            catch (Exception error)
            {
                File.WriteAllText(Output + "/setup.txt", "FAIL " + error);
                Debug.LogException(error);
            }
        }

        [MenuItem("Tools/Nytherion/폰트/Galmuri TMP 에셋 준비")]
        public static void Apply()
        {
            Directory.CreateDirectory(Output);
            var report = new List<string>();
            foreach (string fontName in FontNames)
            {
                string sourcePath = Folder + "/" + fontName + ".ttf";
                string assetPath = Folder + "/" + fontName + " SDF.asset";
                AssetDatabase.ImportAsset(sourcePath, ImportAssetOptions.ForceSynchronousImport);
                Font source = AssetDatabase.LoadAssetAtPath<Font>(sourcePath);
                if (source == null) throw new InvalidOperationException("폰트 원본 누락: " + sourcePath);

                TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
                if (font == null)
                {
                    // 한글 전체를 미리 굽지 않고 사용한 글자만 여러 아틀라스에 추가한다.
                    font = TMP_FontAsset.CreateFontAsset(source, 90, 9, GlyphRenderMode.SDFAA,
                        1024, 1024, AtlasPopulationMode.Dynamic, true);
                    if (font == null) throw new InvalidOperationException("TMP 폰트 생성 실패: " + fontName);
                    font.name = fontName + " SDF";
                    font.atlasTexture.name = fontName + " Atlas";
                    font.material.name = fontName + " SDF Material";
                    AssetDatabase.CreateAsset(font, assetPath);
                    AssetDatabase.AddObjectToAsset(font.atlasTexture, font);
                    AssetDatabase.AddObjectToAsset(font.material, font);
                    EditorUtility.SetDirty(font);
                }

                if (font.sourceFontFile != source || font.material == null ||
                    font.atlasPopulationMode != AtlasPopulationMode.Dynamic || !font.isMultiAtlasTexturesEnabled)
                    throw new InvalidOperationException("TMP 폰트 설정 확인 필요: " + assetPath);

                const string sample = "한글 테스트 가힣 가나다 ABC abc 0123456789 !?";
                if (!font.TryAddCharacters(sample, out string missing))
                    throw new InvalidOperationException(fontName + " 검증 글자 누락: " + missing);
                report.Add("PASS 원본 참조·동적 아틀라스·한글/영문/숫자: " + fontName);
                // 검증에 사용한 아틀라스가 저장 파일을 불필요하게 키우지 않게 비운다.
                font.ClearFontAssetData(true);
                EditorUtility.SetDirty(font);
            }

            TMP_FontAsset regular = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Folder + "/Galmuri11 SDF.asset");
            TMP_FontAsset bold = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Folder + "/Galmuri11-Bold SDF.asset");
            regular.fontWeightTable[7].regularTypeface = bold;
            EditorUtility.SetDirty(regular);
            AssetDatabase.SaveAssets();
            report.Add("PASS Galmuri11 굵게 스타일 원본 연결");
            File.WriteAllLines(Output + "/setup.txt", report);
            Debug.Log("Galmuri TMP 폰트 9종 준비 완료: " + Folder);
        }
    }
}
