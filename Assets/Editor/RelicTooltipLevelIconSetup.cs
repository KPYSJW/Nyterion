using Nytherion.UI.RelicBoard;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
internal static class RelicTooltipLevelIconSetup
{
    private const string EmptyIconGuid = "15ff04d6661c4f449aa60bde9fe045eb";

    static RelicTooltipLevelIconSetup()
    {
        EditorApplication.delayCall += AssignOpenScenes;
        EditorSceneManager.sceneOpened += OnSceneOpened;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
    {
        EditorApplication.delayCall += AssignOpenScenes;
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            EditorApplication.delayCall += AssignOpenScenes;
        }
    }

    private static void AssignOpenScenes()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;

        string path = AssetDatabase.GUIDToAssetPath(EmptyIconGuid);
        Sprite emptyIcon = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (emptyIcon == null) return;

        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded) continue;

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (RelicTooltip tooltip in root.GetComponentsInChildren<RelicTooltip>(true))
                {
                    SerializedObject serializedTooltip = new SerializedObject(tooltip);
                    SerializedProperty iconProperty = serializedTooltip.FindProperty("levelEmptyIcon");
                    if (iconProperty == null || iconProperty.objectReferenceValue != null) continue;

                    iconProperty.objectReferenceValue = emptyIcon;
                    serializedTooltip.ApplyModifiedProperties();
                    EditorSceneManager.MarkSceneDirty(scene);
                }
            }
        }
    }
}
