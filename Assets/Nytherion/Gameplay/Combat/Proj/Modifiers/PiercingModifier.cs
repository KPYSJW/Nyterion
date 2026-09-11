using UnityEngine;

namespace Nytherion.GamePlay.Combat
{
    public class PiercingModifier : MonoBehaviour, IProjModifier
    {
        private void Start() { }
        public bool OnHit(Collider2D target)
        {
            return true;
        }
    }
}