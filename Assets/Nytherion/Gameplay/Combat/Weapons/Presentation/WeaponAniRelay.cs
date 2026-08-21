using System.Collections;
using System.Collections.Generic;
using Nytherion.GamePlay.Combat;
using UnityEngine;

public class WeaponAniRelay : MonoBehaviour
{
    public MeleeWeapon currentWeapon;

    public void AttackStart()
    {
        if(currentWeapon!=null)
        {
            currentWeapon.EnableHitbox();
        }
    }

    public void AttackEnd()
    {
        if(currentWeapon!=null)
        {
            currentWeapon.DisableHitbox();
        }
    }
}
