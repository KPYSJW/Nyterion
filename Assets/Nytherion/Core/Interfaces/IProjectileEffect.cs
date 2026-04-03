using UnityEngine;

public interface IProjectileEffect
{
    bool OnHit(Collider2D target);
}