using Nytherion.Data.ScriptableObjects.Skill;
using UnityEditor;
using UnityEngine;

namespace Nytherion.Editor
{
    /// <summary>시전하지 않고 폭발 이미지와 실제 피해 반경을 비교하며 조절합니다.</summary>
    [CustomEditor(typeof(ChainIgnitionSkillData))]
    public sealed class ChainIgnitionSkillDataEditor : UnityEditor.Editor
    {
        private int previewFrame;
        private int previewWave;
        private float previewZoom = 1f;
        private bool showScenePreview;
        private bool showAllWaves;
        private Transform previewOrigin;
        private Vector3 sceneOrigin;
        private readonly Vector3[] ellipsePoints = new Vector3[65];
        private static readonly string[] WaveLabels = { "1차 폭발", "2차 폭발", "3차 폭발" };

        private void OnEnable()
        {
            ChainIgnitionSkillData data = (ChainIgnitionSkillData)target;
            previewFrame = data.damageFrame;
            SceneView sceneView = SceneView.lastActiveSceneView;
            sceneOrigin = sceneView != null ? sceneView.pivot : Vector3.zero;
            sceneOrigin.z = 0f;
            SceneView.duringSceneGui += OnSceneGUI;
            Undo.undoRedoPerformed += RepaintPreview;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            Undo.undoRedoPerformed -= RepaintPreview;
            SceneView.RepaintAll();
        }

        public override void OnInspectorGUI()
        {
            ChainIgnitionSkillData data = (ChainIgnitionSkillData)target;
            EditorGUILayout.LabelField("피해 범위 미리보기", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("표시된 범위는 유물 보정 전의 기본 피해 범위입니다. 오른쪽 손잡이는 가로 반경, 타원의 위쪽 손잡이는 세로 반경을 조절합니다. 미리보기는 피해를 주지 않습니다.", MessageType.Info);
            if (data.HasValidAnimation)
            {
                EditorGUI.BeginChangeCheck();
                previewFrame = EditorGUILayout.IntSlider("확인할 이미지 프레임", Mathf.Clamp(previewFrame, 0, data.explosionFrames.Length - 1),
                    0, data.explosionFrames.Length - 1);
                previewZoom = EditorGUILayout.Slider("미리보기 확대", previewZoom, 0.25f, 2f);
                if (EditorGUI.EndChangeCheck()) RepaintPreview();
                DrawInspectorPreview(GUILayoutUtility.GetRect(100f, 240f, GUILayout.ExpandWidth(true)), data);
                Vector2 radii = data.GetExplosionRadii();
                EditorGUILayout.LabelField($"반경: 가로 {radii.x:0.###} / 세로 {radii.y:0.###} / 피해 프레임: {data.damageFrame}", EditorStyles.miniLabel);
                serializedObject.Update();
                EditorGUILayout.PropertyField(serializedObject.FindProperty("useEllipticalHitRange"), new GUIContent("타원형 피해 범위"));
                bool elliptical = serializedObject.FindProperty("useEllipticalHitRange").boolValue;
                EditorGUILayout.PropertyField(serializedObject.FindProperty("explosionRadius"), new GUIContent(elliptical ? "가로 피해 반경" : "폭발 피해 반경"));
                if (elliptical) EditorGUILayout.PropertyField(serializedObject.FindProperty("explosionVerticalRadius"), new GUIContent("세로 피해 반경"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("visualScale"), new GUIContent("폭발 이미지 크기"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("explosionVisualOffset"), new GUIContent("이미지 위치 보정"));
                if (serializedObject.ApplyModifiedProperties()) RepaintPreview();
            }
            else EditorGUILayout.HelpBox("폭발 이미지 프레임을 연결하면 미리보기가 표시됩니다.", MessageType.Warning);

            EditorGUI.BeginChangeCheck();
            showScenePreview = EditorGUILayout.Toggle("씬 뷰에서도 범위 보기", showScenePreview);
            if (showScenePreview)
            {
                previewOrigin = (Transform)EditorGUILayout.ObjectField("기준 오브젝트 (선택)", previewOrigin, typeof(Transform), true);
                previewWave = EditorGUILayout.Popup("조절할 파동", previewWave, WaveLabels);
                showAllWaves = EditorGUILayout.Toggle("세 파동 함께 보기", showAllWaves);
                if (GUILayout.Button("씬 뷰에서 범위 위치로 이동"))
                {
                    SceneView sceneView = SceneView.lastActiveSceneView;
                    if (sceneView == null) sceneView = EditorWindow.GetWindow<SceneView>();
                    Vector3 center = GetSceneCenter(data);
                    Vector2 radii = data.GetExplosionRadii();
                    float diameter = (data.GetRingRadius(2) + Mathf.Max(radii.x, radii.y) + data.visualScale) * 2f;
                    sceneView.Frame(new Bounds(center, Vector3.one * Mathf.Max(2f, diameter)), false);
                    sceneView.Repaint();
                }
            }
            if (EditorGUI.EndChangeCheck()) RepaintPreview();
            EditorGUILayout.Space();
            if (DrawDefaultInspector()) RepaintPreview();
        }

        private void DrawInspectorPreview(Rect rect, ChainIgnitionSkillData data)
        {
            EditorGUI.DrawRect(rect, new Color(0.08f, 0.09f, 0.12f));
            GUI.BeginClip(rect);
            Vector2 floor = new Vector2(rect.width * 0.5f, rect.height * 0.65f);
            float pixelsPerUnit = Mathf.Min(rect.width, rect.height) * 0.3f * previewZoom;
            Sprite sprite = data.explosionFrames[previewFrame];
            Rect spriteRect = GetSpriteWorldRect(sprite, Vector2.zero, data);
            Rect imageRect = new Rect(floor.x + spriteRect.xMin * pixelsPerUnit, floor.y - spriteRect.yMax * pixelsPerUnit,
                spriteRect.width * pixelsPerUnit, spriteRect.height * pixelsPerUnit);
            GUI.DrawTextureWithTexCoords(imageRect, sprite.texture, GetTextureCoordinates(sprite));
            Color color = data.hitRangeColor;
            Vector2 screenRadii = data.GetExplosionRadii() * pixelsPerUnit;
            for (int point = 0; point < 64; point++)
            {
                float angle = point * Mathf.PI * 2f / 64;
                float nextAngle = (point + 1) * Mathf.PI * 2f / 64;
                DrawLine(floor + Vector2.Scale(new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)), screenRadii),
                    floor + Vector2.Scale(new Vector2(Mathf.Cos(nextAngle), Mathf.Sin(nextAngle)), screenRadii), 2f, color);
            }
            DrawLine(floor - Vector2.right * 4f, floor + Vector2.right * 4f, 1f, color);
            DrawLine(floor - Vector2.up * 4f, floor + Vector2.up * 4f, 1f, color);
            DrawRadiusHandle(floor, screenRadii, pixelsPerUnit, color, 0);
            if (data.useEllipticalHitRange) DrawRadiusHandle(floor, screenRadii, pixelsPerUnit, color, 1);
            GUI.EndClip();
        }

        private void DrawRadiusHandle(Vector2 floor, Vector2 screenRadii, float pixelsPerUnit, Color color, int axis)
        {
            Vector2 handle = axis == 0 ? floor + Vector2.right * screenRadii.x : floor - Vector2.up * screenRadii.y;
            Rect handleRect = new Rect(handle.x - 5f, handle.y - 5f, 10f, 10f);
            EditorGUI.DrawRect(handleRect, color);
            EditorGUIUtility.AddCursorRect(handleRect, axis == 0 ? MouseCursor.ResizeHorizontal : MouseCursor.ResizeVertical);
            int control = GUIUtility.GetControlID(FocusType.Passive);
            Event current = Event.current;
            if (current.type == EventType.MouseDown && current.button == 0 &&
                new Rect(handle.x - 10f, handle.y - 10f, 20f, 20f).Contains(current.mousePosition))
            {
                GUIUtility.hotControl = control;
                current.Use();
            }
            if (GUIUtility.hotControl == control)
            {
                if (current.type == EventType.MouseDrag)
                {
                    float distance = axis == 0 ? current.mousePosition.x - floor.x : floor.y - current.mousePosition.y;
                    SetAxisRadius(axis, Mathf.Max(0.01f, distance / pixelsPerUnit));
                    current.Use();
                }
                else if (current.type == EventType.MouseUp)
                {
                    GUIUtility.hotControl = 0;
                    AssetDatabase.SaveAssetIfDirty(target);
                    current.Use();
                }
            }
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            if (!showScenePreview || target == null) return;
            ChainIgnitionSkillData data = (ChainIgnitionSkillData)target;
            if (!data.HasValidAnimation) return;
            previewFrame = Mathf.Clamp(previewFrame, 0, data.explosionFrames.Length - 1);
            Vector3 center = GetSceneCenter(data);
            Vector2 radii = data.GetExplosionRadii();
            Color previousColor = Handles.color;
            Handles.color = data.hitRangeColor;
            int firstWave = showAllWaves ? 0 : previewWave;
            int lastWave = showAllWaves ? 2 : previewWave;
            for (int wave = firstWave; wave <= lastWave; wave++)
            {
                Vector3 floor = center + Vector3.right * data.GetRingRadius(wave);
                if (sceneView.in2DMode && Event.current.type == EventType.Repaint)
                {
                    Sprite sprite = data.explosionFrames[previewFrame];
                    Rect worldRect = GetSpriteWorldRect(sprite, floor, data);
                    Vector2 topLeft = HandleUtility.WorldToGUIPoint(new Vector3(worldRect.xMin, worldRect.yMax, center.z));
                    Vector2 bottomRight = HandleUtility.WorldToGUIPoint(new Vector3(worldRect.xMax, worldRect.yMin, center.z));
                    Handles.BeginGUI();
                    GUI.DrawTextureWithTexCoords(new Rect(topLeft.x, topLeft.y, bottomRight.x - topLeft.x, bottomRight.y - topLeft.y),
                        sprite.texture, GetTextureCoordinates(sprite));
                    Handles.EndGUI();
                }
                for (int point = 0; point <= 64; point++)
                {
                    float angle = point * Mathf.PI * 2f / 64;
                    ellipsePoints[point] = floor + new Vector3(Mathf.Cos(angle) * radii.x, Mathf.Sin(angle) * radii.y);
                }
                Handles.DrawAAPolyLine(2f, ellipsePoints);
                Handles.Label(floor, $"{wave + 1}차 / 가로 {radii.x:0.###}, 세로 {radii.y:0.###}");
            }
            Vector3 selectedFloor = center + Vector3.right * data.GetRingRadius(previewWave);
            EditorGUI.BeginChangeCheck();
            Vector3 moved = Handles.Slider(selectedFloor + Vector3.right * radii.x, Vector3.right,
                HandleUtility.GetHandleSize(selectedFloor) * 0.06f, Handles.DotHandleCap, 0f);
            if (EditorGUI.EndChangeCheck()) SetRadius(Mathf.Max(0.01f, moved.x - selectedFloor.x));
            if (data.useEllipticalHitRange)
            {
                EditorGUI.BeginChangeCheck();
                Vector3 vertical = Handles.Slider(selectedFloor + Vector3.up * radii.y, Vector3.up,
                    HandleUtility.GetHandleSize(selectedFloor) * 0.06f, Handles.DotHandleCap, 0f);
                if (EditorGUI.EndChangeCheck()) SetAxisRadius(1, Mathf.Max(0.01f, vertical.y - selectedFloor.y));
            }
            if (Event.current.rawType == EventType.MouseUp) AssetDatabase.SaveAssetIfDirty(target);
            Handles.color = previousColor;
        }

        private Vector3 GetSceneCenter(ChainIgnitionSkillData data)
        {
            return (previewOrigin != null ? previewOrigin.position : sceneOrigin) + (Vector3)data.castCenterOffset;
        }

        private static Rect GetSpriteWorldRect(Sprite sprite, Vector2 floor, ChainIgnitionSkillData data)
        {
            float scale = Mathf.Max(0.01f, data.visualScale);
            Vector2 imageCenter = floor + data.explosionVisualOffset * scale;
            Vector2 minimum = imageCenter - sprite.pivot / sprite.pixelsPerUnit * scale;
            Vector2 size = sprite.rect.size / sprite.pixelsPerUnit * scale;
            return new Rect(minimum, size);
        }

        private static Rect GetTextureCoordinates(Sprite sprite)
        {
            Rect rect = sprite.textureRect;
            return new Rect(rect.x / sprite.texture.width, rect.y / sprite.texture.height,
                rect.width / sprite.texture.width, rect.height / sprite.texture.height);
        }

        private static void DrawLine(Vector2 start, Vector2 end, float width, Color color)
        {
            Matrix4x4 previousMatrix = GUI.matrix;
            Vector2 difference = end - start;
            GUIUtility.RotateAroundPivot(Mathf.Atan2(difference.y, difference.x) * Mathf.Rad2Deg, start);
            EditorGUI.DrawRect(new Rect(start.x, start.y - width * 0.5f, difference.magnitude, width), color);
            GUI.matrix = previousMatrix;
        }

        private void SetRadius(float radius)
        {
            SetAxisRadius(0, radius);
        }

        private void SetAxisRadius(int axis, float radius)
        {
            serializedObject.Update();
            serializedObject.FindProperty(axis == 0 ? "explosionRadius" : "explosionVerticalRadius").floatValue = Mathf.Max(0.01f, radius);
            serializedObject.ApplyModifiedProperties();
            RepaintPreview();
        }

        private void RepaintPreview()
        {
            Repaint();
            SceneView.RepaintAll();
        }
    }
}
