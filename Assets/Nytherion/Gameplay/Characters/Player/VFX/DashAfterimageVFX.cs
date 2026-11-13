using System.Collections.Generic;
using UnityEngine;

namespace Nytherion.GamePlay.Characters.Player
{
    /// <summary>
    /// 첨부 스프라이트 시트로 대쉬를 재생하며, Inspector에서 기존 흰색 잔상으로 전환할 수 있습니다.
    /// </summary>
    public class DashAfterimageVFX : MonoBehaviour
    {
        private const float SpawnInterval = 0.045f;
        private const float Lifetime = 0.22f;
        private const float InitialAlpha = 0.7f;
        private const int SortingOrderOffset = -1;

        [SerializeField] private SpriteRenderer sourceRenderer;
        [SerializeField] private Material afterimageMaterial;
        [Header("대쉬 시트 미리 적용")]
        [Tooltip("해제하면 기존 흰색 잔상으로 재생합니다.")]
        [SerializeField] private bool useSpriteSheet = true;
        [SerializeField] private Sprite[] dashFrames;
        [SerializeField] private Material spriteSheetMaterial;
        [Tooltip("이펙트 재생 속도입니다. 0.5는 기존의 절반 속도이며 대쉬 이동에는 영향을 주지 않습니다.")]
        [SerializeField, Range(0.1f, 2f)] private float sheetPlaybackSpeed = 0.5f;

        private SpriteRenderer sheetRenderer;
        private Vector3 sheetPosition;
        private Quaternion sheetRotation;
        private Vector3 sheetScale;
        private float sheetElapsed;
        private float sheetFrameDuration;
        private bool sheetPlaying;

        private readonly List<AfterimageInstance> activeAfterimages = new List<AfterimageInstance>();
        private readonly Stack<AfterimageInstance> inactiveAfterimages = new Stack<AfterimageInstance>();

        private Transform afterimageRoot;
        private bool ownsRuntimeMaterial;
        private bool isPlaying;
        private float nextSpawnTime;

        public void Play(Vector2 direction, float distance, float duration)
        {
            isPlaying = false;
            if (!TryCacheSourceRenderer()) return;
            if (useSpriteSheet && dashFrames != null && dashFrames.Length > 0 && spriteSheetMaterial != null)
            {
                PlaySpriteSheet(direction, distance, duration);
                return;
            }

            if (!TryCacheMaterial())
            {
                return;
            }

            EnsureAfterimageRoot();
            isPlaying = true;
            nextSpawnTime = 0f;
        }

        public void Stop()
        {
            isPlaying = false;
        }

        private void Update()
        {
            UpdateSpriteSheet();
            UpdateActiveAfterimages();

            if (!isPlaying || sourceRenderer == null || sourceRenderer.sprite == null)
            {
                return;
            }

            if (Time.time < nextSpawnTime)
            {
                return;
            }

            SpawnAfterimage();
            nextSpawnTime = Time.time + SpawnInterval;
        }

        private void LateUpdate()
        {
            if (sheetPlaying) ApplySheetPose();
            for (int i = 0; i < activeAfterimages.Count; i++)
            {
                AfterimageInstance afterimage = activeAfterimages[i];
                afterimage.root.transform.SetPositionAndRotation(afterimage.worldPosition, afterimage.worldRotation);
            }
        }

        private void PlaySpriteSheet(Vector2 direction, float distance, float duration)
        {
            EnsureAfterimageRoot();
            if (sheetRenderer == null)
            {
                GameObject root = new GameObject("Dash Sprite Sheet");
                root.transform.SetParent(afterimageRoot, false);
                sheetRenderer = root.AddComponent<SpriteRenderer>();
                sheetRenderer.sharedMaterial = spriteSheetMaterial;
            }

            // 4열×2행의 8프레임을 재생 속도에 맞춰 한 번 재생합니다.
            // 기존 시트의 출발점과 이동 폭을 유지해 진행 방향으로 펼칩니다.
            sheetPosition = sourceRenderer.bounds.center;
            sheetRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
            sheetScale = new Vector3(Mathf.Max(0.01f, distance) * 32f / 82f,
                Mathf.Max(0.01f, sourceRenderer.bounds.size.y) * 32f / 34f, 1f);
            sheetFrameDuration = Mathf.Max(0.01f,
                duration / dashFrames.Length / Mathf.Max(0.1f, sheetPlaybackSpeed));
            sheetElapsed = 0f;
            sheetPlaying = true;
            sheetRenderer.sortingLayerID = sourceRenderer.sortingLayerID;
            sheetRenderer.sortingOrder = sourceRenderer.sortingOrder + 1;
            sheetRenderer.sprite = dashFrames[0];
            sheetRenderer.gameObject.SetActive(true);
            ApplySheetPose();
        }

        private void UpdateSpriteSheet()
        {
            if (!sheetPlaying) return;
            sheetElapsed += Time.deltaTime;
            int frame = Mathf.FloorToInt(sheetElapsed / sheetFrameDuration);
            if (frame >= dashFrames.Length)
            {
                sheetPlaying = false;
                sheetRenderer.gameObject.SetActive(false);
                return;
            }
            sheetRenderer.sprite = dashFrames[frame];
        }

        private void ApplySheetPose()
        {
            Transform effect = sheetRenderer.transform;
            effect.SetPositionAndRotation(sheetPosition, sheetRotation);
            Vector3 parentScale = effect.parent.lossyScale;
            effect.localScale = new Vector3(
                sheetScale.x / Mathf.Max(0.0001f, Mathf.Abs(parentScale.x)),
                sheetScale.y / Mathf.Max(0.0001f, Mathf.Abs(parentScale.y)), 1f);
        }

        private void OnDisable()
        {
            isPlaying = false;
            sheetPlaying = false;
            if (sheetRenderer != null) sheetRenderer.gameObject.SetActive(false);
            foreach (AfterimageInstance afterimage in activeAfterimages)
            {
                afterimage.root.SetActive(false);
                inactiveAfterimages.Push(afterimage);
            }
            activeAfterimages.Clear();
        }

        private bool TryCacheSourceRenderer()
        {
            if (sourceRenderer != null)
            {
                return true;
            }

            sourceRenderer = GetComponent<SpriteRenderer>();
            if (sourceRenderer == null)
            {
                sourceRenderer = GetComponentInChildren<SpriteRenderer>();
            }

            if (sourceRenderer == null)
            {
                Debug.LogWarning("[DashAfterimageVFX] 플레이어 SpriteRenderer를 찾을 수 없습니다.", this);
                return false;
            }

            return true;
        }

        private bool TryCacheMaterial()
        {
            if (afterimageMaterial != null)
            {
                return true;
            }

            Shader shader = Shader.Find("Nytherion/2D/Dash Afterimage");
            if (shader == null)
            {
                Debug.LogWarning("[DashAfterimageVFX] Dash Afterimage 셰이더를 찾을 수 없습니다.", this);
                return false;
            }

            afterimageMaterial = new Material(shader);
            ownsRuntimeMaterial = true;
            return true;
        }

        private void EnsureAfterimageRoot()
        {
            if (afterimageRoot != null)
            {
                return;
            }

            GameObject root = new GameObject("Dash Afterimages");
            afterimageRoot = root.transform;
            afterimageRoot.SetParent(transform, false);
        }

        private void SpawnAfterimage()
        {
            AfterimageInstance afterimage = inactiveAfterimages.Count > 0
                ? inactiveAfterimages.Pop()
                : CreateAfterimage();

            int sortingOrder = sourceRenderer.sortingOrder + SortingOrderOffset;
            Transform afterimageTransform = afterimage.root.transform;
            afterimage.worldPosition = sourceRenderer.transform.position;
            afterimage.worldRotation = sourceRenderer.transform.rotation;
            afterimageTransform.SetPositionAndRotation(afterimage.worldPosition, afterimage.worldRotation);
            afterimageTransform.localScale = sourceRenderer.transform.localScale;

            afterimage.renderer.sprite = sourceRenderer.sprite;
            afterimage.renderer.flipX = sourceRenderer.flipX;
            afterimage.renderer.flipY = sourceRenderer.flipY;
            afterimage.renderer.sortingLayerID = sourceRenderer.sortingLayerID;
            afterimage.renderer.sortingOrder = sortingOrder;
            afterimage.renderer.color = new Color(1f, 1f, 1f, InitialAlpha);
            afterimage.elapsed = 0f;
            afterimage.root.SetActive(true);

            activeAfterimages.Add(afterimage);
        }

        private AfterimageInstance CreateAfterimage()
        {
            GameObject root = new GameObject("Dash Afterimage");
            root.transform.SetParent(afterimageRoot, false);

            SpriteRenderer renderer = root.AddComponent<SpriteRenderer>();
            renderer.sharedMaterial = afterimageMaterial;

            root.SetActive(false);
            return new AfterimageInstance(root, renderer);
        }

        private void UpdateActiveAfterimages()
        {
            for (int i = activeAfterimages.Count - 1; i >= 0; i--)
            {
                AfterimageInstance afterimage = activeAfterimages[i];
                afterimage.elapsed += Time.deltaTime;

                float progress = afterimage.elapsed / Lifetime;
                if (progress >= 1f)
                {
                    afterimage.root.SetActive(false);
                    activeAfterimages.RemoveAt(i);
                    inactiveAfterimages.Push(afterimage);
                    continue;
                }

                Color color = afterimage.renderer.color;
                color.a = Mathf.Lerp(InitialAlpha, 0f, progress);
                afterimage.renderer.color = color;
            }
        }

        private void OnDestroy()
        {
            foreach (AfterimageInstance afterimage in activeAfterimages)
            {
                Destroy(afterimage.root);
            }

            foreach (AfterimageInstance afterimage in inactiveAfterimages)
            {
                Destroy(afterimage.root);
            }

            if (afterimageRoot != null)
            {
                Destroy(afterimageRoot.gameObject);
            }

            if (ownsRuntimeMaterial && afterimageMaterial != null)
            {
                Destroy(afterimageMaterial);
            }
        }

        private class AfterimageInstance
        {
            public readonly GameObject root;
            public readonly SpriteRenderer renderer;
            public Vector3 worldPosition;
            public Quaternion worldRotation;
            public float elapsed;

            public AfterimageInstance(GameObject root, SpriteRenderer renderer)
            {
                this.root = root;
                this.renderer = renderer;
            }
        }
    }
}
