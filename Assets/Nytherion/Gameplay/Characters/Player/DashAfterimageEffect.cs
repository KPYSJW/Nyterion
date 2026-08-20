using System.Collections.Generic;
using UnityEngine;

namespace Nytherion.GamePlay.Characters.Player
{
    /// <summary>
    /// 대쉬 중 현재 플레이어 스프라이트의 흰색 실루엣을 남기고 서서히 투명하게 만듭니다.
    /// 잔상은 Player 하위의 풀 컨테이너에 보관하지만, 활성 중에는 월드 좌표를 유지합니다.
    /// </summary>
    public class DashAfterimageEffect : MonoBehaviour
    {
        private const float SpawnInterval = 0.045f;
        private const float Lifetime = 0.22f;
        private const float InitialAlpha = 0.7f;
        private const int SortingOrderOffset = -1;

        [SerializeField] private SpriteRenderer sourceRenderer;
        [SerializeField] private Material afterimageMaterial;

        private readonly List<AfterimageInstance> activeAfterimages = new List<AfterimageInstance>();
        private readonly Stack<AfterimageInstance> inactiveAfterimages = new Stack<AfterimageInstance>();

        private Transform afterimageRoot;
        private bool ownsRuntimeMaterial;
        private bool isPlaying;
        private float nextSpawnTime;

        public void Play()
        {
            if (!TryCacheSourceRenderer() || !TryCacheMaterial())
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
            for (int i = 0; i < activeAfterimages.Count; i++)
            {
                AfterimageInstance afterimage = activeAfterimages[i];
                afterimage.root.transform.SetPositionAndRotation(afterimage.worldPosition, afterimage.worldRotation);
            }
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
                Debug.LogWarning("[DashAfterimageEffect] 플레이어 SpriteRenderer를 찾을 수 없습니다.", this);
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
                Debug.LogWarning("[DashAfterimageEffect] Dash Afterimage 셰이더를 찾을 수 없습니다.", this);
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
