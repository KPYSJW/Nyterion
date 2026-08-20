using UnityEngine;

namespace Nytherion.GamePlay.Combat
{
    [DefaultExecutionOrder(1000)]
    [DisallowMultipleComponent]
    public class WeaponSortingOrderSync : MonoBehaviour
    {
        [SerializeField] private int playerSortingOrderOffset = -1;

        private SpriteRenderer playerSpriteRenderer;

        private void OnTransformParentChanged()
        {
            playerSpriteRenderer = null;
        }

        private void LateUpdate()
        {
            if (playerSpriteRenderer == null)
            {
                CachePlayerSpriteRenderer();
            }

            if (playerSpriteRenderer == null)
            {
                return;
            }

            SpriteRenderer[] weaponRenderers = GetComponentsInChildren<SpriteRenderer>(true);
            int sortingOrder = playerSpriteRenderer.sortingOrder + playerSortingOrderOffset;

            for (int i = 0; i < weaponRenderers.Length; i++)
            {
                SpriteRenderer weaponRenderer = weaponRenderers[i];
                weaponRenderer.sortingLayerID = playerSpriteRenderer.sortingLayerID;
                weaponRenderer.sortingOrder = sortingOrder;
            }
        }

        private void CachePlayerSpriteRenderer()
        {
            if (transform.parent != null)
            {
                playerSpriteRenderer = transform.parent.GetComponentInParent<SpriteRenderer>();
            }
        }
    }
}
