using Nytherion.Core.Managers;
using UnityEngine;

namespace Nytherion.GamePlay.Combat
{
    /// <summary>
    /// 파이로폴에 맞은 적의 위치에서 스프라이트 애니메이션을 한 번 재생합니다.
    /// </summary>
    public sealed class PyrofallHitEffect : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private Sprite[] frames;
        [SerializeField, Min(1f)] private float animationFps = 12f;
        [SerializeField] private int sortingOrder = 20;

        private float animationTime;
        private string poolTag;
        private bool returnToPool;

        private void Awake()
        {
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponent<SpriteRenderer>();
            }

            poolTag = gameObject.name.Replace("(Clone)", string.Empty).Trim();
        }

        private void OnEnable()
        {
            animationTime = 0f;
            if (spriteRenderer != null)
            {
                spriteRenderer.enabled = frames != null && frames.Length > 0;
                spriteRenderer.sortingOrder = sortingOrder;
                spriteRenderer.sprite = GetFrame(0);
            }
        }

        public void Initialize(string effectPoolTag, bool isPooled)
        {
            poolTag = effectPoolTag;
            returnToPool = isPooled;
        }

        private void Update()
        {
            animationTime += Time.deltaTime;
            int frameIndex = Mathf.FloorToInt(animationTime * animationFps);
            if (frames == null || frameIndex >= frames.Length)
            {
                Finish();
                return;
            }

            if (spriteRenderer != null)
            {
                spriteRenderer.sprite = frames[frameIndex];
            }
        }

        private Sprite GetFrame(int index)
        {
            return frames != null && index >= 0 && index < frames.Length ? frames[index] : null;
        }

        private void Finish()
        {
            if (returnToPool && ObjectPoolManager.Instance != null && !string.IsNullOrEmpty(poolTag))
            {
                ObjectPoolManager.Instance.ReturnToPool(poolTag, gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void OnDisable()
        {
            animationTime = 0f;
            returnToPool = false;
        }
    }
}
