using Nytherion.Core.Interfaces;
using Nytherion.Core.Managers;
using System.Collections.Generic;
using UnityEngine;

namespace Nytherion.GamePlay.Skills
{
public class LaserBeam : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private Animator animator;
    [SerializeField] private BoxCollider2D hitBox;
    
    private float damage;
    private float fireDuration;
    private float tickRate;
    private string poolTag;
    
    private Transform caster;
    private Transform firePoint;
    
    private enum LaserState { None, Charging, Firing, Vanishing }
    private LaserState currentState = LaserState.None;
    
    private float stateTimer = 0f;
    private float nextTickTime = 0f;
    
    private HashSet<IDamageable> targetsInRange = new HashSet<IDamageable>();
    private List<IDamageable> deadTargetsList = new List<IDamageable>();
    
    public void Initialize(Transform caster, Transform firePoint, float damage, float fireDuration, float tickRate, string poolTag)
    {
        this.caster = caster;
        this.firePoint = firePoint;
        this.damage = damage;
        this.fireDuration = fireDuration;
        this.tickRate = tickRate;
        this.poolTag = poolTag;
        
        targetsInRange.Clear();
        deadTargetsList.Clear();
        hitBox.enabled = false;
        
        ChangeState(LaserState.Charging);
    }
    
    private void Update()
    {
        if (currentState == LaserState.None) return;
        
        UpdatePositionAndRotation();
        
        if (animator == null) return;
        
        AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
        
        switch (currentState)
        {
            case LaserState.Charging:
                if (stateInfo.IsName("Laser_Charge"))
                {
                    if (stateInfo.normalizedTime >= 0.95f)
                    {
                        ChangeState(LaserState.Firing);
                    }
                }
                break;
                
            case LaserState.Firing:
                stateTimer -= Time.deltaTime;
                
                if (Time.time >= nextTickTime)
                {
                    DealTickDamage();
                    nextTickTime = Time.time + tickRate;
                }
                
                if (stateTimer <= 0f)
                {
                    ChangeState(LaserState.Vanishing);
                }
                break;
                
            case LaserState.Vanishing:
                if (stateInfo.IsName("Laser_Vanish"))
                {
                    if (stateInfo.normalizedTime >= 0.95f)
                    {
                        ReturnToPool();
                    }
                }
                break;
        }
    }
    
    private void ChangeState(LaserState newState)
    {
        currentState = newState;
        
        switch (newState)
        {
            case LaserState.Charging:
                animator.Play("Laser_Charge", 0, 0f);
                hitBox.enabled = false;
                break;
                
            case LaserState.Firing:
                animator.Play("Laser_Fire", 0, 0f);
                hitBox.enabled = true;
                stateTimer = fireDuration;
                nextTickTime = Time.time;
                break;
                
            case LaserState.Vanishing:
                animator.Play("Laser_Vanish", 0, 0f);
                hitBox.enabled = false;
                break;
        }
    }
    
    private void UpdatePositionAndRotation()
    {
        if (caster == null)
        {
            ReturnToPool();
            return;
        }
        
        Transform spawnBase = firePoint != null ? firePoint : caster;
        transform.position = spawnBase.position;
        
        if (Camera.main != null)
        {
            Vector3 mouseWorldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            mouseWorldPos.z = 0f;
            Vector3 direction = (mouseWorldPos - transform.position).normalized;
            
            if (direction != Vector3.zero)
            {
                float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                transform.rotation = Quaternion.Euler(0f, 0f, angle);
            }
        }
    }
    
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (currentState != LaserState.Firing) return;
        
        if (other.TryGetComponent<IDamageable>(out IDamageable damageable))
        {
            targetsInRange.Add(damageable);
        }
    }
    
    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.TryGetComponent<IDamageable>(out IDamageable damageable))
        {
            targetsInRange.Remove(damageable);
        }
    }
    
    private void DealTickDamage()
    {
        deadTargetsList.Clear();
        
        foreach (var target in targetsInRange)
        {
            if (target != null && target is MonoBehaviour mb && mb.gameObject.activeInHierarchy)
            {
                target.TakeDamage(damage);
            }
            else
            {
                deadTargetsList.Add(target);
            }
        }
        
        foreach (var dead in deadTargetsList)
        {
            targetsInRange.Remove(dead);
        }
    }
    
    private void ReturnToPool()
    {
        currentState = LaserState.None;
        hitBox.enabled = false;
        targetsInRange.Clear();
        
        if (ObjectPoolManager.Instance != null && !string.IsNullOrEmpty(poolTag))
        {
            ObjectPoolManager.Instance.ReturnToPool(poolTag, gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
}
}
