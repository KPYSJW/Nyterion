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
            if (weaponRenderers.Length == 0)
            {
                return;
            }

            SpriteRenderer rootRenderer = GetComponent<SpriteRenderer>();
            int currentBaseOrder = rootRenderer != null
                ? rootRenderer.sortingOrder
                : weaponRenderers[0].sortingOrder;
            int targetBaseOrder = playerSpriteRenderer.sortingOrder + playerSortingOrderOffset;

            for (int i = 0; i < weaponRenderers.Length; i++)
            {
                SpriteRenderer weaponRenderer = weaponRenderers[i];
                int relativeOrder = weaponRenderer.sortingOrder - currentBaseOrder;
                weaponRenderer.sortingLayerID = playerSpriteRenderer.sortingLayerID;
                weaponRenderer.sortingOrder = targetBaseOrder + relativeOrder;
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
