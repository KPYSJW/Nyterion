using UnityEngine;

namespace Nytherion.GamePlay.Combat
{
    public interface IProjModifier
    {
        bool OnHit(Collider2D target);
    }
}
