using Nytherion.Core.Managers;
using UnityEngine;

namespace Nytherion.GamePlay.Skills
{
    /// <summary>처치 연출을 투사체·적의 수명과 별개로 끝까지 재생합니다.</summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class SoulEaterVFX : MonoBehaviour
    {
        [SerializeField] private Sprite[] frames;
        [SerializeField, Min(1f)] private float animationFps = 12f;
        private SpriteRenderer spriteRenderer;
        private ObjectPoolManager pool;
        private Vector3 startPosition;
        private float elapsed;
        private float riseDistance;
        private bool playing;
        private int spawnFrame;

        public void Play(ObjectPoolManager effectPool, Vector3 scale, float rise)
        {
            if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
            pool = effectPool;
            transform.localScale = scale;
            startPosition = transform.position;
            riseDistance = rise;
            elapsed = 0f;
            spawnFrame = Time.frameCount;
            playing = true;
            if (frames == null || frames.Length == 0) { Finish(); return; }
            spriteRenderer.sprite = frames[0];
        }

        private void Update()
        {
            if (!playing || spawnFrame == Time.frameCount) return;
            elapsed += Time.deltaTime;
            float fps = Mathf.Max(1f, animationFps);
            int frame = Mathf.FloorToInt(elapsed * fps);
            if (frame >= frames.Length) { Finish(); return; }
            spriteRenderer.sprite = frames[frame];
            transform.position = startPosition + Vector3.up * (riseDistance * Mathf.Clamp01(elapsed * fps / frames.Length));
        }

        private void Finish()
        {
            ObjectPoolManager effectPool = pool;
            playing = false;
            if (effectPool != null) effectPool.ReturnToPool(gameObject.name.Replace("(Clone)", "").Trim(), gameObject);
            else Destroy(gameObject);
        }

        private void OnDisable()
        {
            playing = false;
            pool = null;
            elapsed = 0f;
        }
    }
}
