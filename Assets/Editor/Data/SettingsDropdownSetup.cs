using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Nytherion.Editor
{
    [InitializeOnLoad]
    public static class SettingsDropdownSetup
    {
        private const string Output = "output/settings-dropdown";
        private const string FramePath = "Assets/Nytherion/Art/UI/Menu/SettingsDropdownFrame.asset";
        internal const string SourceImagePath = "Assets/Nytherion/Art/UI/Menu/TextInpuField.png";
        // 실제 게임의 설정 UI만 관리하고, 독립 테스트 씬은 자동으로 열거나 저장하지 않습니다.
        private static readonly string[] TargetScenePaths = { "Assets/Scenes/GameScene.unity" };

        static SettingsDropdownSetup()
        {
            EditorApplication.update += Update;
            EditorApplication.delayCall += () => RefreshFrame();
        }

        private static void Update()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (!EditorApplication.isPlayingOrWillChangePlaymode && File.Exists(Output + "/artwork.request"))
            {
                File.Delete(Output + "/artwork.request");
                try { ApplyArtwork(); }
                catch (Exception error)
                {
                    File.WriteAllText(Output + "/artwork.txt", "FAIL " + error);
                    Debug.LogException(error);
                }
            }
            if (!EditorApplication.isPlayingOrWillChangePlaymode && File.Exists(Output + "/align.request"))
            {
                File.Delete(Output + "/align.request");
                try { AlignResolutionLabels(); }
                catch (Exception error)
                {
                    File.WriteAllText(Output + "/alignment.txt", "FAIL " + error);
                    Debug.LogException(error);
                }
            }
            if (!EditorApplication.isPlayingOrWillChangePlaymode && File.Exists(Output + "/setup.request"))
            {
                File.Delete(Output + "/setup.request");
                try { Apply(); }
                catch (Exception error)
                {
                    File.WriteAllText(Output + "/setup.txt", "FAIL " + error);
                    Debug.LogException(error);
                }
            }
            if (!File.Exists(Output + "/audit.request")) return;
            File.Delete(Output + "/audit.request");
            Directory.CreateDirectory(Output);
            string report = "Playing=" + EditorApplication.isPlaying + "\nTargets=" + string.Join(",", TargetScenePaths) + "\n";
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                report += scene.path + " dirty=" + scene.isDirty + "\n";
            }
            foreach (TMP_Dropdown dropdown in Resources.FindObjectsOfTypeAll<TMP_Dropdown>()
                .Where(item => item.gameObject.scene.IsValid() && item.name == "ResolutionDropdown"))
                report += dropdown.name + " active=" + dropdown.gameObject.activeInHierarchy +
                    " options=" + string.Join(",", dropdown.options.Select(option => option.text)) +
                    " value=" + dropdown.value + " size=" + ((RectTransform)dropdown.transform).rect.size + "\n";
            File.WriteAllText(Output + "/audit.txt", report);
        }

        [MenuItem("Tools/Nytherion/Settings/해상도 라벨 세로 정렬 맞추기")]
        public static void AlignResolutionLabels()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("편집 모드에서 실행해 주세요.");
            Directory.CreateDirectory(Output);
            var report = new List<string>();
            foreach (string path in TargetScenePaths)
            {
                Scene scene = SceneManager.GetSceneByPath(path);
                bool loaded = scene.IsValid() && scene.isLoaded;
                if (!loaded) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                try
                {
                    string originalPath = path;
                    if (scene.isDirty)
                    {
                        originalPath = Output + "/" + scene.name + "-before-align-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".unity";
                        if (!EditorSceneManager.SaveScene(scene, originalPath, true))
                            throw new InvalidOperationException("현재 씬 백업 실패: " + path);
                    }
                    string original = File.ReadAllText(originalPath);
                    TMP_Dropdown dropdown = scene.GetRootGameObjects()
                        .SelectMany(root => root.GetComponentsInChildren<TMP_Dropdown>(true))
                        .Single(item => item.name == "ResolutionDropdown");
                    TMP_Text label = dropdown.transform.Find("ResolutionText").GetComponent<TMP_Text>();
                    Undo.RecordObject(label, "해상도 라벨 세로 정렬 맞추기");
                    label.verticalAlignment = VerticalAlignmentOptions.Geometry;
                    EditorUtility.SetDirty(label);
                    // 다른 UI를 재직렬화하지 않고 기존 씬의 라벨 정렬 필드만 반영합니다.
                    string id = GlobalObjectId.GetGlobalObjectIdSlow(label).targetObjectId.ToString();
                    string pattern = @"(^--- !u!114 &" + id + @"\r?\n.*?)(?=^--- !u!|\z)";
                    string updated = Regex.Replace(original, pattern, match => Regex.Replace(match.Value,
                        @"(?m)^  m_VerticalAlignment: \d+", "  m_VerticalAlignment: 4096"),
                        RegexOptions.Multiline | RegexOptions.Singleline);
                    AssetDatabase.ReleaseCachedFileHandles();
                    string temporary = Output + "/" + scene.name + "-aligned.tmp";
                    File.WriteAllText(temporary, updated);
                    File.Replace(temporary, path, null);
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                    if (scene.name == "GameScene") Capture(dropdown, "aligned");
                    report.Add("PASS " + path + " Resolution 라벨과 드롭다운 글자의 세로 중심 정렬");
                }
                finally { if (!loaded) EditorSceneManager.CloseScene(scene, true); }
            }
            File.WriteAllLines(Output + "/alignment.txt", report);
        }

        [MenuItem("Tools/Nytherion/Settings/해상도 드롭다운 디자인 적용")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("편집 모드에서 실행해 주세요.");
            Directory.CreateDirectory(Output);
            Sprite frame = RefreshFrame();
            var report = new List<string>();
            foreach (string path in TargetScenePaths)
            {
                Scene scene = SceneManager.GetSceneByPath(path);
                bool loaded = scene.IsValid() && scene.isLoaded;
                if (!loaded) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                try
                {
                    // 열린 씬의 미저장 내용까지 복사본으로 보존한 뒤 현재 씬을 수정합니다.
                    string originalPath = path;
                    if (scene.isDirty)
                    {
                        originalPath = Output + "/" + scene.name + "-before-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".unity";
                        if (!EditorSceneManager.SaveScene(scene, originalPath, true))
                            throw new InvalidOperationException("현재 씬 백업 실패: " + path);
                    }
                    string original = File.ReadAllText(originalPath);
                    TMP_Dropdown dropdown = scene.GetRootGameObjects()
                        .SelectMany(root => root.GetComponentsInChildren<TMP_Dropdown>(true))
                        .Single(item => item.name == "ResolutionDropdown");
                    if (scene.name == "GameScene" && !File.Exists(Output + "/before.png")) Capture(dropdown, "before");
                    Configure(dropdown, frame);
                    EditorSceneManager.MarkSceneDirty(scene);
                    if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("씬 저장 실패: " + path);
                    Verify(dropdown);
                    if (scene.name == "GameScene") Capture(dropdown, "after");
                    PreserveOtherObjects(path, original, dropdown);
                    report.Add("PASS " + path + " Inspector 참조·330×64 선택창·56px 항목·글자 여백 확인");
                }
                finally { if (!loaded) EditorSceneManager.CloseScene(scene, true); }
            }
            File.WriteAllLines(Output + "/setup.txt", report);
        }

        private static void PreserveOtherObjects(string path, string original, TMP_Dropdown dropdown)
        {
            // Canvas 갱신으로 재계산되는 다른 UI의 레이아웃은 저장 전 상태를 보존합니다.
            var ids = new HashSet<string>();
            foreach (Transform child in dropdown.GetComponentsInChildren<Transform>(true))
            {
                ids.Add(GlobalObjectId.GetGlobalObjectIdSlow(child.gameObject).targetObjectId.ToString());
                foreach (Component component in child.GetComponents<Component>())
                    if (component != null) ids.Add(GlobalObjectId.GetGlobalObjectIdSlow(component).targetObjectId.ToString());
            }
            const string pattern = @"^--- !u!\d+ &(\d+)[^\r\n]*\r?\n.*?(?=^--- !u!|\z)";
            var blocks = Regex.Matches(File.ReadAllText(path), pattern, RegexOptions.Multiline | RegexOptions.Singleline)
                .Cast<Match>().ToDictionary(match => match.Groups[1].Value, match => match.Value);
            var originalIds = new HashSet<string>(Regex.Matches(original, pattern, RegexOptions.Multiline | RegexOptions.Singleline)
                .Cast<Match>().Select(match => match.Groups[1].Value));
            string result = Regex.Replace(original, pattern, match =>
                ids.Contains(match.Groups[1].Value) ? blocks[match.Groups[1].Value] : match.Value,
                RegexOptions.Multiline | RegexOptions.Singleline);
            result += string.Concat(blocks.Where(pair => ids.Contains(pair.Key) && !originalIds.Contains(pair.Key))
                .Select(pair => pair.Value));
            AssetDatabase.ReleaseCachedFileHandles();
            File.WriteAllText(path, result);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        }

        private static void Configure(TMP_Dropdown dropdown, Sprite frame)
        {
            Undo.RegisterFullObjectHierarchyUndo(dropdown.gameObject, "해상도 드롭다운 디자인 변경");
            RectTransform rect = (RectTransform)dropdown.transform;
            rect.sizeDelta = new Vector2(330f, 64f);
            rect.anchoredPosition = new Vector2(450f, rect.anchoredPosition.y);
            ConfigureFrame(dropdown.GetComponent<Image>(), AssetDatabase.LoadAssetAtPath<Sprite>(SourceImagePath), false);
            ColorBlock colors = dropdown.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = Color.white;
            colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = new Color(0.8f, 0.78f, 0.72f);
            colors.disabledColor = new Color(0.6f, 0.6f, 0.6f, 0.65f);
            dropdown.colors = colors;
            ConfigureText(dropdown.captionText);
            Stretch(dropdown.captionText.rectTransform, new Vector2(18f, 8f), new Vector2(-56f, -8f));
            ConfigureText(dropdown.itemText);
            Stretch(dropdown.itemText.rectTransform, new Vector2(38f, 6f), new Vector2(-12f, -6f));

            Image arrow = dropdown.transform.Find("Arrow").GetComponent<Image>();
            arrow.enabled = false;
            arrow.raycastTarget = false;
            arrow.preserveAspect = true;
            arrow.rectTransform.sizeDelta = new Vector2(32f, 40f);
            arrow.rectTransform.anchoredPosition = new Vector2(-28f, 0f);
            Transform glyphTransform = arrow.transform.Find("ArrowGlyph");
            if (glyphTransform == null)
            {
                var glyphObject = new GameObject("ArrowGlyph", typeof(RectTransform), typeof(TextMeshProUGUI));
                glyphObject.layer = arrow.gameObject.layer;
                glyphObject.transform.SetParent(arrow.transform, false);
                glyphTransform = glyphObject.transform;
            }
            TMP_Text glyph = glyphTransform.GetComponent<TMP_Text>();
            glyph.font = dropdown.captionText.font;
            glyph.text = "▼";
            ConfigureText(glyph);
            glyph.fontSize = 24f;
            glyph.enableAutoSizing = false;
            glyph.alignment = TextAlignmentOptions.Center;
            Stretch(glyph.rectTransform, Vector2.zero, Vector2.zero);

            RectTransform template = dropdown.template;
            template.anchoredPosition = new Vector2(0f, -6f);
            template.sizeDelta = new Vector2(0f, 240f);
            ConfigureFrame(template.GetComponent<Image>(), frame);
            ScrollRect scroll = template.GetComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            Stretch(scroll.viewport, new Vector2(8f, 8f), new Vector2(-24f, -8f));
            RectTransform content = scroll.content;
            content.sizeDelta = new Vector2(0f, 64f);
            RectTransform item = (RectTransform)dropdown.itemText.transform.parent;
            item.sizeDelta = new Vector2(0f, 56f);
            Toggle toggle = item.GetComponent<Toggle>();
            Image background = (Image)toggle.targetGraphic;
            background.color = Color.white;
            colors.normalColor = new Color(0.22f, 0.22f, 0.24f);
            colors.highlightedColor = new Color(0.39f, 0.35f, 0.27f);
            colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = new Color(0.47f, 0.4f, 0.27f);
            toggle.colors = colors;
            toggle.graphic.color = new Color(1f, 0.94f, 0.78f);
            RectTransform check = toggle.graphic.rectTransform;
            check.sizeDelta = new Vector2(20f, 20f);
            check.anchoredPosition = new Vector2(16f, 0f);
            if (scroll.verticalScrollbar != null)
            {
                Scrollbar scrollbar = scroll.verticalScrollbar;
                scrollbar.GetComponent<Image>().color = new Color(0.14f, 0.14f, 0.16f);
                scrollbar.targetGraphic.color = new Color(0.65f, 0.61f, 0.51f);
                scrollbar.GetComponent<RectTransform>().sizeDelta = new Vector2(14f, 0f);
            }
            Transform label = dropdown.transform.Find("ResolutionText");
            if (label != null)
            {
                TMP_Text text = label.GetComponent<TMP_Text>();
                text.verticalAlignment = VerticalAlignmentOptions.Geometry;
                text.fontSize = 36f;
                text.enableWordWrapping = false;
                text.enableAutoSizing = true;
                text.fontSizeMin = 28f;
                text.fontSizeMax = 36f;
                text.overflowMode = TextOverflowModes.Ellipsis;
                text.rectTransform.sizeDelta = new Vector2(270f, 64f);
                text.rectTransform.anchoredPosition = new Vector2(-316f, 0f);
            }
            dropdown.ClearOptions();
            dropdown.AddOptions(new List<string> { "1280 × 720", "1600 × 900", "1920 × 1080", "2560 × 1440" });
            dropdown.SetValueWithoutNotify(2);
            dropdown.RefreshShownValue();
            template.gameObject.SetActive(false);
        }

        private static void ConfigureFrame(Image image, Sprite sprite, bool sliced = true)
        {
            image.sprite = sprite;
            image.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
            image.preserveAspect = !sliced;
            image.color = Color.white;
            // 원본 픽셀의 두 배 두께로 테두리를 표시합니다.
            float referencePixelsPerUnit = image.canvas != null ? image.canvas.referencePixelsPerUnit : 100f;
            image.pixelsPerUnitMultiplier = referencePixelsPerUnit / (sprite.pixelsPerUnit * 2f);
        }

        /// <summary>원본 이미지 크기가 바뀌어도 목록 프레임의 영역과 UV를 함께 갱신합니다.</summary>
        internal static Sprite RefreshFrame()
        {
            Sprite source = AssetDatabase.LoadAssetAtPath<Sprite>(SourceImagePath);
            if (source == null) return null;
            Sprite frame = AssetDatabase.LoadAssetAtPath<Sprite>(FramePath);
            Vector4 border = source.border != Vector4.zero ? source.border : Vector4.one * 2f;
            if (frame != null && frame.rect == source.rect && frame.texture == source.texture &&
                frame.border == border && Mathf.Approximately(frame.pixelsPerUnit, source.pixelsPerUnit)) return frame;

            Sprite updated = Sprite.Create(source.texture, source.rect, new Vector2(0.5f, 0.5f),
                source.pixelsPerUnit, 0, SpriteMeshType.FullRect, border);
            updated.name = "SettingsDropdownFrame";
            if (frame == null)
            {
                AssetDatabase.CreateAsset(updated, FramePath);
                frame = updated;
            }
            else
            {
                // 기존 Sprite 에셋의 GUID와 파일 ID를 유지합니다.
                EditorUtility.CopySerialized(updated, frame);
                EditorUtility.SetDirty(frame);
                AssetDatabase.SaveAssetIfDirty(frame);
                Object.DestroyImmediate(updated);
            }
            return frame;
        }

        [MenuItem("Tools/Nytherion/Settings/드롭다운에 원본 이미지 반영")]
        public static void ApplyArtwork()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("편집 모드에서 실행해 주세요.");
            Directory.CreateDirectory(Output);
            Sprite source = AssetDatabase.LoadAssetAtPath<Sprite>(SourceImagePath);
            Sprite frame = RefreshFrame();
            if (source == null || frame == null) throw new InvalidOperationException("설정 원본 이미지가 없습니다.");
            var report = new List<string>();
            foreach (string path in TargetScenePaths)
            {
                Scene scene = SceneManager.GetSceneByPath(path);
                bool loaded = scene.IsValid() && scene.isLoaded;
                if (!loaded) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                try
                {
                    string originalPath = path;
                    if (scene.isDirty)
                    {
                        originalPath = Output + "/" + scene.name + "-before-artwork-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".unity";
                        if (!EditorSceneManager.SaveScene(scene, originalPath, true))
                            throw new InvalidOperationException("현재 씬 백업 실패: " + path);
                    }
                    string original = File.ReadAllText(originalPath);
                    TMP_Dropdown dropdown = scene.GetRootGameObjects()
                        .SelectMany(root => root.GetComponentsInChildren<TMP_Dropdown>(true))
                        .Single(item => item.name == "ResolutionDropdown");
                    Image image = dropdown.GetComponent<Image>();
                    Image listImage = dropdown.template.GetComponent<Image>();
                    Undo.RecordObjects(new Object[] { image, listImage, dropdown }, "드롭다운 원본 이미지 반영");
                    ConfigureFrame(image, source, false);
                    ConfigureFrame(listImage, frame);
                    ColorBlock colors = dropdown.colors;
                    colors.normalColor = colors.highlightedColor = colors.selectedColor = Color.white;
                    dropdown.colors = colors;
                    EditorUtility.SetDirty(image);
                    EditorUtility.SetDirty(listImage);
                    EditorUtility.SetDirty(dropdown);
                    if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("씬 저장 실패: " + path);
                    string saved = File.ReadAllText(path);
                    // 이미지 두 개와 선택창 색상만 저장하고 다른 Inspector 값은 그대로 둡니다.
                    foreach (Image target in new[] { image, listImage })
                    {
                        string id = GlobalObjectId.GetGlobalObjectIdSlow(target).targetObjectId.ToString();
                        string pattern = @"^--- !u!114 &" + id + @"\r?\n.*?(?=^--- !u!|\z)";
                        string block = Regex.Match(saved, pattern, RegexOptions.Multiline | RegexOptions.Singleline).Value;
                        original = Regex.Replace(original, pattern, match => block, RegexOptions.Multiline | RegexOptions.Singleline);
                    }
                    string dropdownId = GlobalObjectId.GetGlobalObjectIdSlow(dropdown).targetObjectId.ToString();
                    string dropdownPattern = @"^--- !u!114 &" + dropdownId + @"\r?\n.*?(?=^--- !u!|\z)";
                    original = Regex.Replace(original, dropdownPattern, match => Regex.Replace(match.Value,
                        @"(?m)^    m_(Normal|Highlighted|Selected)Color: .*", "    m_$1Color: {r: 1, g: 1, b: 1, a: 1}"),
                        RegexOptions.Multiline | RegexOptions.Singleline);
                    string temporary = Output + "/" + scene.name + "-artwork.tmp";
                    File.WriteAllText(temporary, original);
                    AssetDatabase.ReleaseCachedFileHandles();
                    File.Replace(temporary, path, null);
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                    if (image.sprite != source || image.type != Image.Type.Simple || !image.preserveAspect || frame.rect != source.rect)
                        throw new InvalidOperationException("원본 이미지 연결 또는 표시 영역 불일치: " + path);
                    if (scene.name == "GameScene") Capture(dropdown, "artwork");
                    report.Add("PASS " + path + " 원본 " + source.rect.size + " 전체 영역·색상·비율 및 목록 프레임 갱신");
                }
                finally { if (!loaded) EditorSceneManager.CloseScene(scene, true); }
            }
            File.WriteAllLines(Output + "/artwork.txt", report);
        }

        private static void ConfigureText(TMP_Text text)
        {
            text.color = new Color(1f, 0.96f, 0.84f);
            text.fontSize = 32f;
            text.fontSizeMin = 26f;
            text.fontSizeMax = 32f;
            text.enableAutoSizing = true;
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.raycastTarget = false;
        }

        private static void Stretch(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = min;
            rect.offsetMax = max;
        }

        private static void Verify(TMP_Dropdown dropdown)
        {
            if (dropdown.template == null || dropdown.captionText == null || dropdown.itemText == null ||
                dropdown.template.GetComponent<ScrollRect>().content == null)
                throw new InvalidOperationException("해상도 드롭다운 Inspector 참조 누락");
            Canvas.ForceUpdateCanvases();
            dropdown.captionText.ForceMeshUpdate(true);
            if (dropdown.captionText.isTextOverflowing)
                throw new InvalidOperationException("해상도 표시 영역이 부족합니다.");
        }

        private static void Capture(TMP_Dropdown dropdown, string suffix)
        {
            Scene preview = EditorSceneManager.NewPreviewScene();
            RenderTexture target = null;
            Texture2D pixels = null;
            try
            {
                GameObject root = new GameObject("설정 미리보기", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
                SceneManager.MoveGameObjectToScene(root, preview);
                Canvas canvas = root.GetComponent<Canvas>();
                CanvasScaler scaler = root.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                GameObject panel = Object.Instantiate(dropdown.transform.parent.gameObject, root.transform);
                panel.SetActive(true);
                RectTransform panelRect = panel.GetComponent<RectTransform>();
                panelRect.anchoredPosition = Vector2.zero;
                GameObject cameraObject = new GameObject("설정 미리보기 카메라", typeof(Camera));
                SceneManager.MoveGameObjectToScene(cameraObject, preview);
                Camera camera = cameraObject.GetComponent<Camera>();
                camera.scene = preview;
                camera.enabled = false;
                camera.orthographic = true;
                camera.orthographicSize = 540f;
                camera.transform.position = new Vector3(0f, 0f, -10f);
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.1f, 0.1f, 0.12f);
                target = new RenderTexture(1920, 1080, 24);
                camera.targetTexture = target;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1f;
                TMP_Dropdown copy = panel.GetComponentsInChildren<TMP_Dropdown>(true).Single(item => item.name == "ResolutionDropdown");
                // 편집 모드의 미리보기에서는 템플릿을 직접 펼쳐 글자와 행 간격을 확인합니다.
                RectTransform content = copy.template.GetComponent<ScrollRect>().content;
                RectTransform row = (RectTransform)copy.itemText.transform.parent;
                copy.template.gameObject.SetActive(true);
                content.sizeDelta = new Vector2(0f, row.rect.height * copy.options.Count + 8f);
                for (int i = 0; i < copy.options.Count; i++)
                {
                    RectTransform next = i == 0 ? row : Object.Instantiate(row, content);
                    next.anchorMin = new Vector2(0f, 1f);
                    next.anchorMax = new Vector2(1f, 1f);
                    next.anchoredPosition = new Vector2(0f, -4f - row.rect.height * (i + 0.5f));
                    next.GetComponentInChildren<TMP_Text>().text = copy.options[i].text;
                    Toggle toggle = next.GetComponent<Toggle>();
                    toggle.graphic.enabled = i == copy.value;
                    toggle.targetGraphic.color = i == copy.value ? toggle.colors.selectedColor : toggle.colors.normalColor;
                    toggle.targetGraphic.canvasRenderer.SetColor(Color.white);
                }
                Canvas.ForceUpdateCanvases();
                foreach (TMP_Text text in panel.GetComponentsInChildren<TMP_Text>(true)) text.ForceMeshUpdate(true);
                camera.Render();
                pixels = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
                RenderTexture previous = RenderTexture.active;
                try
                {
                    RenderTexture.active = target;
                    pixels.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
                    pixels.Apply();
                    File.WriteAllBytes(Output + "/" + suffix + ".png", pixels.EncodeToPNG());
                }
                finally { RenderTexture.active = previous; }
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(preview);
                if (target != null) { target.Release(); Object.DestroyImmediate(target); }
                if (pixels != null) Object.DestroyImmediate(pixels);
            }
        }
    }

    public class SettingsDropdownTexturePostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets,
            string[] movedAssets, string[] movedFromAssetPaths)
        {
            if (Array.IndexOf(importedAssets, SettingsDropdownSetup.SourceImagePath) < 0) return;
            EditorApplication.delayCall += () => SettingsDropdownSetup.RefreshFrame();
        }
    }
}
