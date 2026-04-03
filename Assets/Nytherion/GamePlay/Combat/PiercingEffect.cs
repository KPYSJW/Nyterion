using UnityEngine;

namespace Nytherion.GamePlay.Combat
{
    public class PiercingEffect : MonoBehaviour, IProjectileEffect
    {
        private void Start() { }
        public bool OnHit(Collider2D target)
        {
            return true;
        }
    }
}