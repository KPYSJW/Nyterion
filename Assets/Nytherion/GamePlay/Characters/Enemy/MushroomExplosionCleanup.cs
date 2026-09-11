using UnityEngine;

namespace Nytherion.GamePlay.Characters.Enemy
{
    [DisallowMultipleComponent]
    public sealed class MushroomExplosionCleanup : MonoBehaviour
    {
        public void DestroyAfterAnimation()
        {
            Destroy(gameObject);
        }
    }
}
