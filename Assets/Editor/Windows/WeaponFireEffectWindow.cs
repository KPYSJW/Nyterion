using System;
using System.Linq;
using Nytherion.Data.ScriptableObjects.Weapons;
using Nytherion.GamePlay.Combat;
using UnityEditor;
using UnityEngine;

namespace Nytherion.Editor
{
    public class WeaponFireEffectWindow : EditorWindow
    {
        [SerializeField] private WeaponData weapon;
        [SerializeField] private Texture2D sheet;
        [SerializeField] private int firstFrame;
        [SerializeField] private int lastFrame;
        [SerializeField] private float fps = 60f;
        [SerializeField] private float effectScale = 0.28f;
        [SerializeField] private Vector3 offset = new Vector3(-0.06f, 0f, 0f);
        [SerializeField] private Vector2Int cell = new Vector2Int(64, 64);
        [SerializeField] private float pixelsPerUnit = 32f;
        private Sprite[] frames = Array.Empty<Sprite>();
        private string message;

        [MenuItem("Tools/Nytherion/발사 연출/무기별 발사 이미지 설정")]
        public static void Open()
        {
            var window = GetWindow<WeaponFireEffectWindow>("무기 발사 이미지");
            window.minSize = new Vector2(420f, 420f);
            if (Selection.activeObject is WeaponData selected) window.LoadWeapon(selected);
            window.Show();
        }

        public static void OpenFor(WeaponData data)
        {
            var window = GetWindow<WeaponFireEffectWindow>("무기 발사 이미지");
            window.minSize = new Vector2(420f, 420f);
            window.LoadWeapon(data);
            window.Show();
        }

        private void OnEnable()
        {
            frames = WeaponFireEffectAssets.LoadFrames(sheet);
        }

        private void LoadWeapon(WeaponData data)
        {
            weapon = data;
            sheet = null;
            frames = Array.Empty<Sprite>();
            firstFrame = lastFrame = 0;
            fps = 60f;
            effectScale = 0.28f;
            offset = new Vector3(-0.06f, 0f, 0f);
            message = null;
            GameObject prefab = weapon != null ? weapon.fireEffectPrefab : null;
            if (prefab == null || prefab.GetComponent<SpriteMuzzleFlashEffect>() == null) return;
            var serialized = new SerializedObject(prefab.GetComponent<SpriteMuzzleFlashEffect>());
            var clip = serialized.FindProperty("animationClip").objectReferenceValue as AnimationClip;
            offset = serialized.FindProperty("muzzleOffset").vector3Value;
            effectScale = prefab.transform.localScale.x;
            if (clip == null) return;
            fps = clip.frameRate;
            var keys = AnimationUtility.GetObjectReferenceCurve(clip,
                EditorCurveBinding.PPtrCurve(string.Empty, typeof(SpriteRenderer), "m_Sprite"));
            if (keys == null || keys.Length == 0 || !(keys[0].value is Sprite first)) return;
            sheet = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GetAssetPath(first));
            frames = WeaponFireEffectAssets.LoadFrames(sheet);
            firstFrame = Mathf.Max(0, Array.IndexOf(frames, first));
            Sprite final = keys.LastOrDefault(value => value.value is Sprite).value as Sprite;
            lastFrame = Mathf.Max(firstFrame, Array.IndexOf(frames, final));
            cell = Vector2Int.RoundToInt(first.rect.size);
            pixelsPerUnit = first.pixelsPerUnit;
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox("무기와 이펙트 이미지를 선택한 뒤 프레임 범위와 속도를 정해 연결하세요. 이미지마다 별도의 발사 프리팹이 만들어집니다.", MessageType.Info);
            WeaponData selected = (WeaponData)EditorGUILayout.ObjectField("무기 데이터", weapon, typeof(WeaponData), false);
            if (selected != weapon) LoadWeapon(selected);
            Texture2D image = (Texture2D)EditorGUILayout.ObjectField("이펙트 이미지", sheet, typeof(Texture2D), false);
            if (image != sheet)
            {
                sheet = image;
                frames = WeaponFireEffectAssets.LoadFrames(sheet);
                firstFrame = 0;
                lastFrame = Mathf.Max(0, frames.Length - 1);
                if (sheet != null) cell = new Vector2Int(sheet.height, sheet.height);
                message = null;
            }
            EditorGUILayout.LabelField("불러온 프레임", frames.Length.ToString());
            if (GUILayout.Button("프레임 다시 불러오기"))
            {
                frames = WeaponFireEffectAssets.LoadFrames(sheet);
                firstFrame = Mathf.Clamp(firstFrame, 0, Mathf.Max(0, frames.Length - 1));
                lastFrame = frames.Length - 1;
            }
            EditorGUILayout.Space();
            cell = EditorGUILayout.Vector2IntField("슬라이스 프레임 크기", cell);
            pixelsPerUnit = EditorGUILayout.FloatField("픽셀 / 유닛", pixelsPerUnit);
            using (new EditorGUI.DisabledScope(sheet == null || EditorApplication.isPlayingOrWillChangePlaymode))
            {
                if (GUILayout.Button("이미지를 격자로 슬라이스"))
                {
                    try
                    {
                        WeaponFireEffectAssets.SliceGrid(sheet, cell, pixelsPerUnit);
                        frames = WeaponFireEffectAssets.LoadFrames(sheet);
                        firstFrame = 0;
                        lastFrame = frames.Length - 1;
                        message = "슬라이스 완료. 위에서 아래로, 각 줄은 왼쪽부터 재생합니다.";
                    }
                    catch (Exception error) { message = error.Message; Debug.LogException(error); }
                }
            }
            EditorGUILayout.Space();
            firstFrame = EditorGUILayout.IntField("시작 프레임 (0부터)", firstFrame);
            lastFrame = EditorGUILayout.IntField("마지막 재생 프레임", lastFrame);
            fps = EditorGUILayout.FloatField("초당 프레임", fps);
            effectScale = EditorGUILayout.FloatField("이펙트 크기", effectScale);
            offset = EditorGUILayout.Vector3Field("총구 기준 위치", offset);
            if (fps > 0f && lastFrame >= firstFrame)
                EditorGUILayout.LabelField("재생 시간", ((lastFrame - firstFrame + 1) / fps).ToString("0.###") + "초");
            if (frames.Length == 0 && sheet != null)
                EditorGUILayout.HelpBox("이미지에 스프라이트가 없습니다. 프레임 크기를 정하고 격자로 슬라이스해 주세요.", MessageType.Warning);
            using (new EditorGUI.DisabledScope(weapon == null || frames.Length == 0 ||
                EditorApplication.isPlayingOrWillChangePlaymode))
            {
                if (GUILayout.Button("스프라이트 이펙트 생성 및 무기에 연결", GUILayout.Height(30f)))
                {
                    try
                    {
                        GameObject prefab = WeaponFireEffectAssets.CreateAndAssign(
                            weapon, frames, firstFrame, lastFrame, fps, effectScale, offset);
                        message = "연결 완료: " + AssetDatabase.GetAssetPath(prefab);
                        EditorGUIUtility.PingObject(prefab);
                    }
                    catch (Exception error) { message = error.Message; Debug.LogException(error); }
                }
            }
            if (!string.IsNullOrEmpty(message)) EditorGUILayout.HelpBox(message, MessageType.None);
        }
    }
}

