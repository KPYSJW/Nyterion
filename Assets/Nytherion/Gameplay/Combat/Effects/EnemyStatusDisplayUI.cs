using UnityEngine;
using System.Collections.Generic;

namespace Nytherion.GamePlay.Combat
{
    public class EnemyStatusDisplayUI : MonoBehaviour
    {
        private SpriteRenderer enemySpriteRenderer;
        private List<GameObject> activeIconObjects = new List<GameObject>();
        private float offsetY = 1.0f;

        [Header("Icon Display Settings")]
        [Tooltip("아이콘 간의 가로 간격")]
        [SerializeField] private float iconSpacing = 0.25f;
        
        [Tooltip("아이콘 크기 조절")]
        [SerializeField] private Vector3 iconScale = new Vector3(0.25f, 0.25f, 1f);
        
        [Tooltip("머리 위 추가 높이 오프셋")]
        [SerializeField] private float heightExtraOffset = 0f;

        public void Initialize(SpriteRenderer spriteRenderer)
        {
            enemySpriteRenderer = spriteRenderer;
            if (enemySpriteRenderer != null)
            {
                offsetY = enemySpriteRenderer.bounds.extents.y + heightExtraOffset;
            }
        }

        public void UpdateDisplay(List<StatusEffect> activeEffects)
        {
            // 기존에 생성된 아이콘 오브젝트 제거
            for (int i = 0; i < activeIconObjects.Count; i++)
            {
                if (activeIconObjects[i] != null)
                {
                    Destroy(activeIconObjects[i]);
                }
            }
            activeIconObjects.Clear();

            if (activeEffects == null || activeEffects.Count == 0)
            {
                return;
            }

            // 상태이상에 직접 담겨진 스프라이트 참조를 꺼내어 리스트 구성
            List<Sprite> loadedSprites = new List<Sprite>();
            for (int i = 0; i < activeEffects.Count; i++)
            {
                Sprite sprite = activeEffects[i].EffectIcon;
                if (sprite != null)
                {
                    loadedSprites.Add(sprite);
                }
            }

            int count = loadedSprites.Count;
            if (count == 0) return;

            // 중앙 정렬을 위한 시작 X 좌표 계산
            float startX = -((count - 1) * iconSpacing) / 2f;

            for (int i = 0; i < count; i++)
            {
                GameObject iconObj = new GameObject("StatusIcon_" + i);
                iconObj.transform.SetParent(transform);
                
                SpriteRenderer sr = iconObj.AddComponent<SpriteRenderer>();
                sr.sprite = loadedSprites[i];
                
                // 정렬 레이어 및 순서 설정 (몬스터보다 앞에 그리도록)
                if (enemySpriteRenderer != null)
                {
                    sr.sortingLayerID = enemySpriteRenderer.sortingLayerID;
                    sr.sortingOrder = enemySpriteRenderer.sortingOrder + 10;
                }
                else
                {
                    sr.sortingOrder = 10;
                }

                iconObj.transform.localScale = iconScale;
                
                // 가로 정렬 위치 계산 및 머리 위 오프셋 적용
                float posX = startX + (i * iconSpacing);
                iconObj.transform.localPosition = new Vector3(posX, offsetY, 0f);

                activeIconObjects.Add(iconObj);
            }
        }
    }
}
