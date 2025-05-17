using Nytherion.GamePlay.Combat;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace Nytherion.GamePlay.Combat.Weapon
{
    public class Bow : RangedWeapon
    {
        public override void Attack(Vector2 direction)
        {
            // ���� ���� ���� �� �ʼ� ������Ʈ �˻�
            if (!CanAttack() || weaponData?.projectilePrefab == null || firePoint == null)
            {
                Debug.LogWarning("������ ������ �� �����ϴ�. �ʿ��� ������Ʈ�� Ȯ���ϼ���.");
                return;
            }

            try
            {
                Projectile(direction);
                lastAttackTime = Time.time;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"�߻�ü ���� �� ���� �߻�: {ex.Message}");
            }
        }

        public override void AttackEnd()
        {

        }
    }
}

