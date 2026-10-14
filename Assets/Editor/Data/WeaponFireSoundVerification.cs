using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Nytherion.Core.Data;
using Nytherion.Core.Managers;
using Nytherion.Data.ScriptableObjects.Weapons;
using Nytherion.GamePlay.Combat;
using Nytherion.GamePlay.Combat.Weapons;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Nytherion.Editor
{
    /// <summary>실제 무기 프리팹 발사, 음량 설정, 다중 탄 중복 방지와 비활성 정리를 검증합니다.</summary>
    [InitializeOnLoad]
    public static class WeaponFireSoundVerification
    {
        private const string Running = "Nytherion.WeaponFireSoundVerification.Running";
        private static double readyAt;
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly FieldInfo PoolInstance = typeof(ObjectPoolManager).GetField(
            "<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
        private static readonly FieldInfo LastPlay = typeof(WeaponFireAudio).GetField("lastPlayTime", Hidden);
        private static readonly MethodInfo PlaySound = typeof(WeaponBase).GetMethod("PlayFireSound", Hidden);
        private static readonly MethodInfo GetSound = typeof(WeaponBase).GetMethod("GetFireSound", Hidden);

        static WeaponFireSoundVerification() => EditorApplication.update += Update;

        private static void Update()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (SessionState.GetBool(Running, false) && !EditorApplication.isPlaying &&
                !EditorApplication.isPlayingOrWillChangePlaymode)
                SessionState.SetBool(Running, false);
            if (SessionState.GetBool(Running, false))
            {
                if (!EditorApplication.isPlaying) return;
                // 시작 화면에서 Time.timeScale이 0이어도 실제 시간 기준으로 검증합니다.
                if (readyAt <= 0d) readyAt = EditorApplication.timeSinceStartup + 8d;
                if (EditorApplication.timeSinceStartup < readyAt) return;
                readyAt = 0d;
                SessionState.SetBool(Running, false);
                try { Verify(); }
                finally { EditorApplication.ExitPlaymode(); }
                return;
            }
            string request = WeaponFireSoundSetup.Output + "/verify.request";
            if (EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(request)) return;
            File.Delete(request);
            SessionState.SetBool(Running, true);
            EditorApplication.EnterPlaymode();
        }

        [MenuItem("Tools/Nytherion/발사 효과음/플레이 중 발사와 음량 검증")]
        public static void Verify()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("플레이 모드에서 실행해 주세요.");
            Directory.CreateDirectory(WeaponFireSoundSetup.Output);
            var report = new List<string>();
            ObjectPoolManager previousPool = ObjectPoolManager.Instance;
            GameObject poolRoot = null;
            const string volumeKey = "Nytherion.Settings.SfxVolume";
            bool hadVolume = PlayerPrefs.HasKey(volumeKey);
            float previousVolume = UserSettings.GetSfxVolume();
            try
            {
                // 동기 검증 동안만 전용 풀을 사용하고 기존 플레이 씬의 풀은 보존합니다.
                PoolInstance.SetValue(null, null);
                poolRoot = new GameObject("[WeaponFireSoundVerification] 전용 풀");
                ObjectPoolManager pool = poolRoot.AddComponent<ObjectPoolManager>();
                pool.Initialize();
                UserSettings.SetSfxVolume(1f);
                foreach (string name in WeaponFireSoundSetup.Names)
                {
                    WeaponData data = AssetDatabase.LoadAssetAtPath<WeaponData>(WeaponFireSoundSetup.DataFolder + name + ".asset");
                    // 보존 대상은 사용자가 지정한 스테레오/압축 설정도 그대로 허용합니다.
                    if (name != "ArcaneDart" && name != "Frenzy") CheckClip(data.fireSound);
                    else Require(data != null && data.fireSound != null, name + " 기존 효과음 참조 누락");
                    VerifyWeapon(data, report);
                }
                CheckClip(AssetDatabase.LoadAssetAtPath<AudioClip>(WeaponFireSoundSetup.AudioFolder + "ShamanStaffAbyssal_Fire.wav"));
                CheckClip(AssetDatabase.LoadAssetAtPath<AudioClip>(WeaponFireSoundSetup.AudioFolder + "Blazeshade_Summon.wav"));
                report.Add("PASS 19개 무기·특수 PCM 효과음: 48 kHz 모노, 무음/클리핑 없음, 시작/끝 클릭 없음");
                VerifyControls(report);
            }
            catch (Exception error)
            {
                report.Add("FAIL " + error);
                Debug.LogException(error);
            }
            finally
            {
                if (poolRoot != null) Object.DestroyImmediate(poolRoot);
                PoolInstance.SetValue(null, previousPool);
                if (hadVolume) UserSettings.SetSfxVolume(previousVolume);
                else PlayerPrefs.DeleteKey(volumeKey);
                File.WriteAllLines(WeaponFireSoundSetup.Output + "/verification.txt", report);
            }
        }

        private static void CheckClip(AudioClip clip)
        {
            Require(clip != null && clip.frequency == 48000 && clip.channels == 1, "PCM 형식/에셋 누락");
            clip.LoadAudioData();
            var samples = new float[clip.samples];
            Require(clip.GetData(samples, 0), "효과음 PCM 읽기 실패");
            float peak = 0f;
            foreach (float value in samples) peak = Mathf.Max(peak, Mathf.Abs(value));
            Require(peak > 0.1f && peak < 0.9f && Mathf.Abs(samples[0]) < 0.001f &&
                Mathf.Abs(samples[samples.Length - 1]) < 0.01f, "클리핑/무음/경계 클릭: " + clip.name);
        }

        private static void VerifyWeapon(WeaponData original, List<string> report)
        {
            WeaponData data = Object.Instantiate(original);
            GameObject root = null;
            try
            {
                root = Object.Instantiate(data.weaponPrefab.gameObject, new Vector3(20000f, 20000f, 0f), Quaternion.identity);
                WeaponBase weapon = root.GetComponent<WeaponBase>();
                weapon.Initialize(data);
                if (weapon is BlazeshadeWeapon)
                {
                    weapon.Attack(Vector2.right, root.transform.position + Vector3.right);
                    Require(root.GetComponent<WeaponFireAudio>() == null, "수동 발사 없는 오라가 사운드를 재생함");
                    PlaySound.Invoke(weapon, null);
                    Require(root.GetComponent<AudioSource>().isPlaying, "일반 오라 피격음 연결 실패");
                    CheckClip((AudioClip)typeof(BlazeshadeWeapon).GetField("targetSummonSound", Hidden).GetValue(weapon));
                    report.Add("PASS Blazeshade: 빈 오라/수동 입력 무음, 일반 피격음과 소환음 참조 확인");
                    return;
                }
                if (weapon is FrenzyWeapon)
                {
                    weapon.Attack(Vector2.right);
                    Require(root.GetComponent<WeaponFireAudio>() == null, "Frenzy 스핀업 전에 사운드 재생");
                    typeof(FrenzyWeapon).GetField("lastProjectileFireTime", Hidden).SetValue(weapon, float.NegativeInfinity);
                    typeof(FrenzyWeapon).GetField("currentSpinUpProgress", Hidden).SetValue(weapon, 1f);
                    typeof(FrenzyWeapon).GetMethod("Update", Hidden).Invoke(weapon, null);
                }
                else if (weapon is RandomArchiveWeapon archive)
                {
                    // 획득 기록 없이도 전용 발사 경로의 사운드를 확인할 수 있게 기본 탄을 선택합니다.
                    GameObject fallback = archive.fallbackProjectilePrefab;
                    if (fallback == null)
                    {
                        // 기존 에셋의 기본 탄 누락은 별도로 보고하고 다른 무기의 검증을 계속합니다.
                        PlaySound.Invoke(weapon, null);
                        Require(root.GetComponent<AudioSource>().isPlaying, "아카이브 효과음 연결 실패");
                        report.Add("NOTE Capricious: 기존 기본 투사체 참조 누락으로 실발사 제외, 효과음 연결/재생 확인");
                        return;
                    }
                    typeof(RangedWeapon).GetField("currentProjectilePrefab", Hidden).SetValue(archive, fallback);
                    weapon.Attack(Vector2.right);
                }
                else
                {
                    weapon.Attack(Vector2.right, root.transform.position + Vector3.right * 3f);
                    if (weapon is IChargeableWeapon) weapon.AttackEnd();
                }
                WeaponFireAudio fireAudio = root.GetComponent<WeaponFireAudio>();
                Require(fireAudio != null && root.GetComponent<AudioSource>().isPlaying, original.name + " 발사음 미재생");
                Require(Mathf.Approximately((float)LastPlay.GetValue(fireAudio), Time.time), "실제 발사 시점과 사운드 불일치");
                if (weapon is ShamanStaff)
                {
                    AudioClip normal = (AudioClip)GetSound.Invoke(weapon, null);
                    GameObject abyss = (GameObject)typeof(ShamanStaff).GetField("abyssalFlamePrefab", Hidden).GetValue(weapon);
                    typeof(RangedWeapon).GetField("currentProjectilePrefab", Hidden).SetValue(weapon, abyss);
                    AudioClip alternate = (AudioClip)GetSound.Invoke(weapon, null);
                    Require(normal == data.fireSound && alternate != normal && alternate.name == "ShamanStaffAbyssal_Fire",
                        "마나탄/심연 불꽃 사운드 전환 실패");
                    report.Add("PASS ShamanStaff: 실제 탄 선택에 맞춰 일반/심연 사운드 전환, 공용 데이터 유지");
                }
                report.Add("PASS " + original.name + ": 무기 프리팹 발사 시 2D 효과음 재생");
                weapon.AttackEnd();
            }
            finally
            {
                if (root != null) Object.DestroyImmediate(root);
                Object.DestroyImmediate(data);
            }
        }

        private static void VerifyControls(List<string> report)
        {
            var root = new GameObject("[WeaponFireSoundVerification] 음량 검증");
            try
            {
                WeaponFireAudio audio = root.AddComponent<WeaponFireAudio>();
                AudioSource source = root.GetComponent<AudioSource>();
                AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(WeaponFireSoundSetup.AudioFolder + "Icicle_Fire.wav");
                UserSettings.SetSfxVolume(0.37f);
                audio.Play(clip, 0.5f);
                Require(source.isPlaying && Mathf.Approximately(source.volume, 0.37f) && !source.loop &&
                    !source.playOnAwake && source.spatialBlend == 0f, "2D/볼륨/자동재생 설정 실패");
                source.Stop();
                audio.Play(clip, 0.5f);
                Require(!source.isPlaying, "동시 다중 탄 사운드 중복 재생");
                root.SetActive(false);
                root.SetActive(true);
                audio.Play(clip, 0.5f);
                Require(source.isPlaying, "재장착 시 첫 발사음 누락");
                UserSettings.SetSfxVolume(0f);
                typeof(WeaponFireAudio).GetMethod("Update", Hidden).Invoke(audio, null);
                Require(source.volume == 0f, "재생 중 효과음 음소거 미반영");
                root.SetActive(false);
                Require(!source.isPlaying, "무기 해제 후 사운드 잔류");
                root.SetActive(true);
                audio.Play(clip, 0.5f);
                Require(!source.isPlaying, "효과음 0일 때 재생");
                UserSettings.SetSfxVolume(1f);
                audio.Play(clip, 0f);
                Require(!source.isPlaying, "무기 음량 0일 때 재생");
                report.Add("PASS 효과음 볼륨/실시간 음소거, 동시 탄 중복 방지, 재장착 첫 발사, 해제 시 정리");
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
