using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Nytherion.Core.Systems
{
    /// <summary>
    /// 전투 중에는 조준점을, UI 조작 중에는 일반 커서를 표시한다.
    /// </summary>
    public class GameCursorController : MonoBehaviour
    {
        [Header("Cursor Textures")]
        [SerializeField] private Texture2D aimCursor;
        [SerializeField] private Texture2D uiCursor;

        [Header("Cursor Settings")]
        [SerializeField, Min(1f)] private float cursorScale = 1.5f;
        [SerializeField] private CursorMode cursorMode = CursorMode.ForceSoftware;

        [Header("Scene Settings")]
        [SerializeField] private string gameplaySceneName = "GameScene";

        private Texture2D scaledAimCursor;
        private Texture2D scaledUiCursor;
        private Texture2D currentCursor;
        private bool cursorApplied;

        private void OnEnable()
        {
            CreateScaledCursors();
            SceneManager.sceneLoaded += HandleSceneLoaded;
            ApplyCursor(true);
        }

        private void Update()
        {
            ApplyCursor(false);
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            Cursor.SetCursor(null, Vector2.zero, cursorMode);
            ReleaseScaledCursors();
            currentCursor = null;
            cursorApplied = false;
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            ApplyCursor(true);
        }

        private void ApplyCursor(bool force)
        {
            bool isGameplayScene = SceneManager.GetActiveScene().name.StartsWith(
                gameplaySceneName,
                StringComparison.Ordinal);
            Texture2D targetCursor = isGameplayScene && !UIPanelBase.IsAnyPanelOpen
                ? scaledAimCursor
                : scaledUiCursor;

            if (!force && cursorApplied && currentCursor == targetCursor)
            {
                return;
            }

            Vector2 hotspot = targetCursor == scaledAimCursor && targetCursor != null
                ? new Vector2(targetCursor.width * 0.5f, targetCursor.height * 0.5f)
                : Vector2.zero;

            Cursor.SetCursor(targetCursor, hotspot, cursorMode);
            Cursor.visible = true;
            currentCursor = targetCursor;
            cursorApplied = true;
        }

        private void CreateScaledCursors()
        {
            scaledAimCursor = CreateScaledCursor(aimCursor);
            scaledUiCursor = CreateScaledCursor(uiCursor);
        }

        private Texture2D CreateScaledCursor(Texture2D source)
        {
            if (source == null || cursorScale <= 1f)
            {
                return source;
            }

            int width = Mathf.Max(1, Mathf.RoundToInt(source.width * cursorScale));
            int height = Mathf.Max(1, Mathf.RoundToInt(source.height * cursorScale));
            Color32[] sourcePixels = source.GetPixels32();
            Color32[] scaledPixels = new Color32[width * height];

            for (int y = 0; y < height; y++)
            {
                int sourceY = y * source.height / height;

                for (int x = 0; x < width; x++)
                {
                    int sourceX = x * source.width / width;
                    scaledPixels[x + y * width] = sourcePixels[sourceX + sourceY * source.width];
                }
            }

            Texture2D scaledCursor = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = $"{source.name}_{cursorScale:0.##}x",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave
            };
            scaledCursor.SetPixels32(scaledPixels);
            scaledCursor.Apply(false, false);

            return scaledCursor;
        }

        private void ReleaseScaledCursors()
        {
            ReleaseScaledCursor(ref scaledAimCursor, aimCursor);
            ReleaseScaledCursor(ref scaledUiCursor, uiCursor);
        }

        private void ReleaseScaledCursor(ref Texture2D texture, Texture2D source)
        {
            if (texture != null && texture != source)
            {
                Destroy(texture);
            }

            texture = null;
        }
    }
}
