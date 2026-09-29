using UnityEngine;

namespace Nytherion.GamePlay.Combat
{
    /// <summary>시간·이동 거리 대신 게임 카메라의 화면과 여유 범위를 벗어나면 투사체를 풀로 반환합니다.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(CollisionObject))]
    public sealed class ProjCameraBoundsLimit : MonoBehaviour
    {
        private Camera gameplayCamera;
        private CollisionObject collisionObject;
        private float viewportMargin;
        private bool isInitialized;

        private void Awake() => collisionObject = GetComponent<CollisionObject>();

        public void Initialize(Camera camera, float margin)
        {
            gameplayCamera = camera;
            viewportMargin = Mathf.Clamp(margin, 0f, 0.5f);
            isInitialized = true;
            enabled = true;
        }

        private void LateUpdate()
        {
            if (!isInitialized) return;
            if (gameplayCamera == null || !gameplayCamera.isActiveAndEnabled) gameplayCamera = Camera.main;
            // 게임 카메라가 준비될 때까지 기다리며 Scene 뷰 카메라의 가시성에는 영향을 받지 않습니다.
            if (gameplayCamera == null) return;
            Vector3 viewport = gameplayCamera.WorldToViewportPoint(transform.position);
            if (viewport.z <= 0f || viewport.x < -viewportMargin || viewport.x > 1f + viewportMargin ||
                viewport.y < -viewportMargin || viewport.y > 1f + viewportMargin)
            {
                collisionObject.ReturnToPool();
            }
        }

        private void OnDisable()
        {
            isInitialized = false;
            gameplayCamera = null;
        }
    }
}
