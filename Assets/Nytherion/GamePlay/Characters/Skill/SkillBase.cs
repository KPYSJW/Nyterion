using Nytherion.Data.ScriptableObjects.Skill;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace Nytherion.GamePlay.Characters.Skill
{
    public abstract class SkillBase : MonoBehaviour
    {
        public SkillData skillData;
        [System.NonSerialized] private float lastUseTime= -Mathf.Infinity;

 
        public bool CanUse() => Time.time > lastUseTime + skillData.coolDown;

        public void TryUse()
        {

            if (CanUse())
            {
                Activate();
                lastUseTime = Time.time;
            }
        }

        protected abstract void Activate();

        public float GetCooldownTime()=>skillData.coolDown;
        public float GetRemainingCooldown()
        {
            float remaining=(lastUseTime+skillData.coolDown)-Time.time;
            return Mathf.Max(0f,remaining);   
        }
    }
}

