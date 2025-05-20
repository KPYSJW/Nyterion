using Nytherion.GamePlay.Characters.Skill;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TestSkill : SkillBase
{
   protected override void Activate()
    {
        Debug.Log($"스킬 실행:{skillData.skillName}");
        
    }
}
