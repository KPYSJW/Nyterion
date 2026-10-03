using System.Collections.Generic;
using Nytherion.Data.ScriptableObjects.Weapons;
using UnityEditor;
using UnityEngine;

namespace Nytherion.Editor
{
    [CustomEditor(typeof(WeaponData), true), CanEditMultipleObjects]
    public class WeaponDataEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            SerializedProperty property = serializedObject.GetIterator();
            bool enterChildren = true;
            while (property.NextVisible(enterChildren))
            {
                enterChildren = false;
                // 상속된 Icon 바로 아래에 표시하므로 원래 선언 위치에서는 생략합니다.
                if (property.name == nameof(WeaponData.useDiagonalIcon) ||
                    property.name == nameof(WeaponData.iconRotationOffset) ||
                    property.name == nameof(WeaponData.iconSlotScale)) continue;

                using (new EditorGUI.DisabledScope(property.name == "m_Script"))
                {
                    EditorGUILayout.PropertyField(property, true);
                }

                if (property.name == nameof(WeaponData.icon))
                {
                    EditorGUILayout.PropertyField(
                        serializedObject.FindProperty(nameof(WeaponData.useDiagonalIcon)),
                        new GUIContent("대각선 배치", "체크: 45도 대각선 배치 / 해제: 원본 방향 배치"));
                    using (new EditorGUI.DisabledScope(!serializedObject.FindProperty(nameof(WeaponData.useDiagonalIcon)).boolValue &&
                        !serializedObject.FindProperty(nameof(WeaponData.useDiagonalIcon)).hasMultipleDifferentValues))
                    {
                        EditorGUILayout.PropertyField(
                            serializedObject.FindProperty(nameof(WeaponData.iconRotationOffset)),
                            new GUIContent("아이콘 각도 보정", "대각 배치에 더할 각도입니다. 5를 입력하면 5도 덜 기울입니다."));
                    }
                    EditorGUILayout.PropertyField(
                        serializedObject.FindProperty(nameof(WeaponData.iconSlotScale)),
                        new GUIContent("Icon Slot Scale", "슬롯과 드래그 이미지에 적용할 아이콘 크기 배율"));
                }
            }
            serializedObject.ApplyModifiedProperties();
            DrawPlayModeOffsetSave();
        }

        private void DrawPlayModeOffsetSave()
        {
            if (!EditorApplication.isPlaying || targets.Length != 1) return;

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "Visual Position Offset을 조정한 뒤 아래 버튼을 누르세요. 클릭 시점의 값만 보관하고 플레이 종료 후 원본 에셋에 저장합니다. 다시 누르면 예약한 값을 갱신합니다.",
                MessageType.Info);
            if (GUILayout.Button("현재 위치 오프셋을 플레이 종료 후 원본에 저장"))
            {
                WeaponOffsetSaveReservation.Reserve((WeaponData)target);
            }
            if (GUILayout.Button("모든 위치 오프셋 저장 예약 취소"))
            {
                WeaponOffsetSaveReservation.CancelAll();
            }
        }
    }

    // SessionState로 도메인 리로드를 넘어 클릭 시점의 값만 전달합니다.
    [InitializeOnLoad]
    internal static class WeaponOffsetSaveReservation
    {
        private const string SessionKey = "Nytherion.WeaponOffsetSaveReservation";

        [System.Serializable]
        private class Entry
        {
            public string guid;
            public Vector3 offset;
        }

        [System.Serializable]
        private class PendingOffsets
        {
            public List<Entry> entries = new List<Entry>();
        }

        static WeaponOffsetSaveReservation()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            if (!EditorApplication.isPlayingOrWillChangePlaymode)
                EditorApplication.delayCall += ApplyPending;
        }

        internal static void Reserve(WeaponData data)
        {
            WeaponData source = EditorUtility.IsPersistent(data) ? data : null;
            if (source == null && !string.IsNullOrEmpty(data.ID))
            {
                foreach (string guid in AssetDatabase.FindAssets("t:WeaponData"))
                {
                    WeaponData candidate = AssetDatabase.LoadAssetAtPath<WeaponData>(AssetDatabase.GUIDToAssetPath(guid));
                    if (candidate == null || candidate.ID != data.ID) continue;
                    if (source != null)
                    {
                        Debug.LogWarning("동일한 ID의 무기 에셋이 여러 개여서 위치 오프셋을 예약하지 않았습니다. Project 창에서 원본 에셋을 선택해 주세요.", data);
                        return;
                    }
                    source = candidate;
                }
            }

            string path = source != null ? AssetDatabase.GetAssetPath(source) : string.Empty;
            if (string.IsNullOrEmpty(path))
            {
                Debug.LogWarning("원본 무기 에셋을 찾을 수 없어 위치 오프셋을 예약하지 않았습니다.", data);
                return;
            }

            PendingOffsets pending = ReadPending();
            string sourceGuid = AssetDatabase.AssetPathToGUID(path);
            pending.entries.RemoveAll(entry => entry.guid == sourceGuid);
            pending.entries.Add(new Entry { guid = sourceGuid, offset = data.visualPositionOffset });
            SessionState.SetString(SessionKey, JsonUtility.ToJson(pending));
            Debug.Log($"무기 위치 오프셋 저장 예약: {path} → {data.visualPositionOffset}. 플레이 종료 후 반영됩니다.", source);
        }

        internal static void CancelAll()
        {
            SessionState.EraseString(SessionKey);
            Debug.Log("모든 무기 위치 오프셋 저장 예약을 취소했습니다.");
        }

        private static PendingOffsets ReadPending()
        {
            return JsonUtility.FromJson<PendingOffsets>(SessionState.GetString(SessionKey, "")) ?? new PendingOffsets();
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode)
                EditorApplication.delayCall += ApplyPending;
        }

        private static void ApplyPending()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            PendingOffsets pending = ReadPending();
            foreach (Entry entry in pending.entries)
            {
                string path = AssetDatabase.GUIDToAssetPath(entry.guid);
                WeaponData source = AssetDatabase.LoadAssetAtPath<WeaponData>(path);
                if (source == null)
                {
                    Debug.LogWarning($"위치 오프셋 저장 대상이 없어 건너뛰었습니다: {entry.guid}");
                    continue;
                }
                Undo.RecordObject(source, "무기 위치 오프셋 저장");
                source.visualPositionOffset = entry.offset;
                EditorUtility.SetDirty(source);
                AssetDatabase.SaveAssetIfDirty(source);
                Debug.Log($"무기 위치 오프셋 저장 완료: {path} → {entry.offset}", source);
            }
            SessionState.EraseString(SessionKey);
        }
    }
}
