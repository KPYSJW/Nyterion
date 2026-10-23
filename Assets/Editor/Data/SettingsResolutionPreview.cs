using System;
using System.Reflection;
using Nytherion.Core.Data;
using UnityEditor;
using UnityEngine;

namespace Nytherion.Editor
{
    /// <summary>설정에서 선택한 해상도를 에디터의 Game 뷰에도 미리보기합니다.</summary>
    [InitializeOnLoad]
    public static class SettingsResolutionPreview
    {
        private const string PreviousSizeKey = "Nytherion.Settings.PreviousGameViewSize";
        private const string PreviewLabel = "Nytherion 설정 미리보기";

        static SettingsResolutionPreview()
        {
            UserSettings.ResolutionApplied += Apply;
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredEditMode) Restore();
            };
        }

        private static void Apply(int width, int height)
        {
            if (!EditorApplication.isPlayingOrWillChangePlaymode) return;
            try
            {
                Type viewType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
                EditorWindow view = EditorWindow.GetWindow(viewType);
                PropertyInfo selected = viewType.GetProperty("selectedSizeIndex");
                if (SessionState.GetInt(PreviousSizeKey, -1) < 0)
                    SessionState.SetInt(PreviousSizeKey, (int)selected.GetValue(view));

                Type sizesType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameViewSizes");
                object sizes = typeof(ScriptableSingleton<>).MakeGenericType(sizesType)
                    .GetProperty("instance").GetValue(null);
                object groupType = sizesType.GetProperty("currentGroupType").GetValue(sizes);
                object group = sizesType.GetMethod("GetGroup").Invoke(sizes, new[] { groupType });
                Type groupClass = group.GetType();
                int count = (int)groupClass.GetMethod("GetBuiltinCount").Invoke(group, null) +
                    (int)groupClass.GetMethod("GetCustomCount").Invoke(group, null);
                MethodInfo getSize = groupClass.GetMethod("GetGameViewSize");
                object size = null;
                int index;
                for (index = 0; index < count; index++)
                {
                    object candidate = getSize.Invoke(group, new object[] { index });
                    if ((string)candidate.GetType().GetProperty("baseText").GetValue(candidate) == PreviewLabel)
                    {
                        size = candidate;
                        break;
                    }
                }
                if (size == null)
                {
                    Type sizeType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameViewSize");
                    Type kind = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameViewSizeType");
                    size = Activator.CreateInstance(sizeType,
                        new[] { Enum.ToObject(kind, 1), (object)width, height, PreviewLabel });
                    groupClass.GetMethod("AddCustomSize").Invoke(group, new[] { size });
                }
                else
                {
                    size.GetType().GetProperty("width").SetValue(size, width);
                    size.GetType().GetProperty("height").SetValue(size, height);
                }
                selected.SetValue(view, index);
                viewType.GetMethod("OnSizeSelected", BindingFlags.Instance | BindingFlags.Public |
                    BindingFlags.NonPublic)?.Invoke(view, new object[] { index, null });
                view.Repaint();
            }
            catch (Exception error)
            {
                Debug.LogWarning("[설정] Game 뷰 미리보기 변경 실패: " + error.Message);
            }
        }

        private static void Restore()
        {
            int index = SessionState.GetInt(PreviousSizeKey, -1);
            if (index < 0) return;
            SessionState.EraseInt(PreviousSizeKey);
            Type viewType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
            foreach (UnityEngine.Object view in Resources.FindObjectsOfTypeAll(viewType))
            {
                viewType.GetProperty("selectedSizeIndex").SetValue(view, index);
                ((EditorWindow)view).Repaint();
            }
        }
    }
}
