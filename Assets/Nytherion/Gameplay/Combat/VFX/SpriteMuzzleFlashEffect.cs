using Nytherion.Core.Managers;
using UnityEngine;

namespace Nytherion.GamePlay.Combat
{
    /// <summary>총구를 따라가는 스프라이트 발사 애니메이션을 풀에서 재사용합니다.</summary>
    [DefaultExecutionOrder(1100)]
    [RequireComponent(typeof(Animator), typeof(SpriteRenderer))]
    public class SpriteMuzzleFlashEffect : MonoBehaviour
    {
        [SerializeField] private AnimationClip animationClip;
        [SerializeField] private Vector3 muzzleOffset = new Vector3(-0.06f, 0f, 0f);

        [Tooltip("총구의 로컬 위치 오프셋·크기·좌우 반전을 함께 따릅니다.")]
        [SerializeField] private bool inheritMuzzleTransform;

        [Tooltip("프리팹 루트의 Position을 발사 지점 기준 오프셋으로 사용합니다.")]
        [SerializeField] private bool usePrefabPositionOffset;

        [Header("표시 순서")]
        [SerializeField] private int sortingOrderOffset = 2;
        [SerializeField] private int minimumSortingOrder = -32768;

        public float Duration => animationClip != null ? animationClip.length : 0f;

        private Animator effectAnimator;
        private SpriteRenderer effectRenderer;
        private SpriteRenderer weaponRenderer;
        private ObjectPoolManager pool;
        private Transform muzzle;
        private bool hasMuzzle;
        private bool playing;
        private int spawnFrame;
        private float elapsed;
        private Vector3 effectScale;
        private Vector3 positionOffset;

        public void Play(ObjectPoolManager effectPool, Transform firePoint, Vector3 prefabOffset = default)
        {
            if (effectAnimator == null) effectAnimator = GetComponent<Animator>();
            if (effectRenderer == null) effectRenderer = GetComponent<SpriteRenderer>();
            pool = effectPool;
            muzzle = firePoint;
            hasMuzzle = muzzle != null;
            WeaponBase weapon = hasMuzzle ? muzzle.GetComponentInParent<WeaponBase>() : null;
            weaponRenderer = weapon != null ? weapon.GetComponent<SpriteRenderer>() : null;
            if (weaponRenderer == null && weapon != null && inheritMuzzleTransform)
                weaponRenderer = weapon.GetComponentInChildren<SpriteRenderer>();
            effectScale = transform.localScale;
            positionOffset = usePrefabPositionOffset ? prefabOffset : muzzleOffset;
            if (animationClip == null || effectAnimator.runtimeAnimatorController == null)
            {
                ReturnEffect();
                return;
            }

            elapsed = 0f;
            spawnFrame = Time.frameCount;
            playing = true;
            effectRenderer.enabled = true;
            effectRenderer.color = Color.white;
            // 자동 진행을 끄고 직접 갱신해 풀 재사용 때 항상 첫 프레임부터 표시합니다.
            effectAnimator.enabled = false;
            effectAnimator.speed = 1f;
            effectAnimator.Rebind();
            effectAnimator.Play(0, 0, 0f);
            effectAnimator.Update(0f);
            FollowMuzzle();
        }

        private void LateUpdate()
        {
            if (!playing) return;
            if (hasMuzzle && (muzzle == null || !muzzle.gameObject.activeInHierarchy))
            {
                ReturnEffect();
                return;
            }
            if (Time.frameCount != spawnFrame)
            {
                elapsed += Time.deltaTime;
                effectAnimator.Update(Time.deltaTime);
                // 이벤트가 삭제되어도 연출 인스턴스가 계속 남지 않도록 반환합니다.
                if (playing && elapsed >= Duration + 0.05f) ReturnEffect();
            }
            if (playing) FollowMuzzle();
        }

        private void FollowMuzzle()
        {
            if (muzzle != null)
            {
                if (inheritMuzzleTransform)
                {
                    transform.position = muzzle.TransformPoint(positionOffset);
                    transform.rotation = muzzle.rotation;
                    transform.localScale = Vector3.Scale(effectScale, muzzle.lossyScale);
                }
                else
                {
                    transform.position = muzzle.position + muzzle.rotation * positionOffset;
                    transform.rotation = muzzle.rotation;
                }
            }
            if (weaponRenderer != null)
            {
                effectRenderer.sortingLayerID = weaponRenderer.sortingLayerID;
                effectRenderer.sortingOrder = Mathf.Max(minimumSortingOrder, weaponRenderer.sortingOrder + sortingOrderOffset);
            }
        }

        // 마지막 스프라이트 프레임의 Animation Event에서 호출합니다.
        public void AnimationFinished()
        {
            if (playing) ReturnEffect();
        }

        private void ReturnEffect()
        {
            ObjectPoolManager effectPool = pool;
            ClearState();
            if (effectPool != null)
                effectPool.ReturnToPool(gameObject.name.Replace("(Clone)", "").Trim(), gameObject);
            else Destroy(gameObject);
        }

        private void ClearState()
        {
            playing = false;
            pool = null;
            muzzle = null;
            hasMuzzle = false;
            weaponRenderer = null;
            if (effectRenderer != null)
            {
                effectRenderer.enabled = false;
                effectRenderer.sprite = null;
            }
        }

        private void OnDisable() => ClearState();
    }
}
