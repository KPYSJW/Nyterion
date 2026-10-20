using UnityEngine;

namespace Nytherion.GamePlay.Skills
{
    /// <summary>게이트의 투사체 진입·발사 애니메이션을 한 번 재생하고 재사용합니다.</summary>
    [RequireComponent(typeof(Animator), typeof(SpriteRenderer))]
    public sealed class ProjectileGateEntryEffect : MonoBehaviour
    {
        [SerializeField] private AnimationClip animationClip;

        private Animator effectAnimator;
        private float elapsed;
        private int spawnFrame;

        public ProjectileGateEntryEffect Prefab { get; private set; }

        public void Play(ProjectileGateEntryEffect prefab, Vector3 point, Vector2 incomingDirection,
            float scale, SpriteRenderer beam)
        {
            // 진입 연출의 오른쪽은 투사체가 날아온 쪽을 향합니다.
            PlayFacing(prefab, point, -incomingDirection, scale, beam);
        }

        public void PlayFacing(ProjectileGateEntryEffect prefab, Vector3 point, Vector2 facingDirection,
            float scale, SpriteRenderer beam)
        {
            Prefab = prefab;
            if (effectAnimator == null) effectAnimator = GetComponent<Animator>();
            if (animationClip == null || effectAnimator.runtimeAnimatorController == null)
            {
                gameObject.SetActive(false);
                return;
            }

            // 왼쪽 끝 피벗을 생성 위치에 고정하고 오른쪽을 지정한 방향에 맞춥니다.
            float angle = Mathf.Atan2(facingDirection.y, facingDirection.x) * Mathf.Rad2Deg;
            transform.SetPositionAndRotation(point, Quaternion.Euler(0f, 0f, angle));
            transform.localScale = Vector3.one * Mathf.Max(0.01f, scale);
            SpriteRenderer renderer = GetComponent<SpriteRenderer>();
            renderer.sortingLayerID = beam.sortingLayerID;
            renderer.sortingOrder = beam.sortingOrder + 1;
            elapsed = 0f;
            spawnFrame = Time.frameCount;
            gameObject.SetActive(true);
            effectAnimator.Rebind();
            effectAnimator.Play(0, 0, 0f);
            effectAnimator.Update(0f);
        }

        private void Update()
        {
            if (Time.frameCount == spawnFrame) return;
            elapsed += Time.deltaTime;
            if (animationClip == null || elapsed >= animationClip.length)
                gameObject.SetActive(false);
        }
    }
}
