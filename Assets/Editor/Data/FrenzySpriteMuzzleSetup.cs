using System;
using System.IO;
using Nytherion.Data.ScriptableObjects.Weapons;
using UnityEditor;
using UnityEngine;

namespace Nytherion.Editor
{
    [InitializeOnLoad]
    public static class FrenzySpriteMuzzleSetup
    {
        static FrenzySpriteMuzzleSetup() => EditorApplication.update += Update;
        private static void Update()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            string request = MuzzleFlashSetup.Output + "/sprite-sheet.request";
            if (!File.Exists(request)) return;
            File.Delete(request);
            try { Apply(); }
            catch (Exception error)
            {
                File.WriteAllText(MuzzleFlashSetup.Output + "/sprite-setup.txt", "FAIL " + error);
                Debug.LogException(error);
            }
        }

        [MenuItem("Tools/Nytherion/발사 연출/Frenzy 기본 스프라이트 다시 생성")]
        public static void Apply()
        {
            var data = AssetDatabase.LoadAssetAtPath<WeaponData>("Assets/Nytherion/Data/ScriptableObjects/Weapons/Frenzy.asset");
            var sheet = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Nytherion/Art/Combat/FrenzyMuzzleFlash.png");
            Sprite[] frames = WeaponFireEffectAssets.LoadFrames(sheet);
            if (frames.Length != 6) throw new InvalidOperationException("Frenzy 시트에 6프레임이 필요합니다.");
            WeaponFireEffectAssets.CreateAndAssign(data, frames, 0, 4, 60f, 0.28f, new Vector3(-0.06f, 0f, 0f),
                MuzzleFlashSetup.RapidPath, "Assets/Nytherion/Art/Combat/VFX/Animations/Frenzy/FrenzyMuzzleFlash");
            Directory.CreateDirectory(MuzzleFlashSetup.Output);
            MuzzleFlashSetup.WriteDependencies();
            File.WriteAllText(MuzzleFlashSetup.Output + "/sprite-setup.txt",
                "PASS 공용 스프라이트 생성기로 Frenzy 연결 / 원본 6프레임 중 0~4번 / 60fps\n");
        }
    }
}

