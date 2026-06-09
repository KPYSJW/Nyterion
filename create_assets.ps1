# create_assets.ps1
# 이 스크립트는 Nytherion 로그라이크 프로토타입의 4회차~9회차 신규 에셋 및 .meta 파일들을 일괄 생성합니다.

$assets = @(
    # ==================== 4회차 ====================
    # 유물 4: 빛바랜 반지
    @{
        Path = "Assets/Nytherion/Data/ScriptableObjects/Relics/SimpleStats/DullRings.asset"
        Content = '%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 0}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: 43c7997b6cdbe61438756d24e848ec0c, type: 3}
  m_Name: DullRings
  m_EditorClassIdentifier: 
  relicName: Dull Rings
  koreanName: 빛바랜 반지
  description_KR: 근접 및 원거리 공격 속도를 소폭 증가시킵니다.
  description_EN: Slightly increases melee and ranged attack speed.
  Image: {fileID: 0}
  rarity: 2
  level: 1
  isDisabled: 0
  effectModules: []
  shape:
  - {x: 0, y: 0}
  influenceZones:
  - offset: {x: 0, y: 1}
    type: 1
  - offset: {x: 0, y: -1}
    type: 1
  synergySeriesId: 
  unlockMilestoneID: 
  simpleStatModifiers:
  - stat: 5
    value: 0.08
    valuePerLevel: 0.02
    isPercentage: 1
  - stat: 6
    value: 0.08
    valuePerLevel: 0.02
    isPercentage: 1'
        MetaGuid = "a421e2760ade8e4cae771239b9c1d0ef"
    } # 쉼표 제거
    # 스킬 4: Frost Nova
    @{
        Path = "Assets/Nytherion/Data/ScriptableObjects/Skill/FrostNova_Skill.asset"
        Content = '%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 0}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: 76c76a15f5956734698ddc4f8e8d05f5, type: 3}
  m_Name: FrostNova_Skill
  m_EditorClassIdentifier: 
  skillID: skill_frost_nova
  skillType: 5
  skillName: Frost Nova
  description: "주변의 넓은 영역에 냉기 피해를 입히고 잠시 동안 빙결 상태로 만듭니다."
  skillLevel: 1
  exp: 0
  coolDown: 8
  manaCost: 25
  damage: 35
  range: 4
  icon: {fileID: 0}
  skillPrefab: {fileID: 0}
  unlockMilestoneID: '
        MetaGuid = "f290c741e6c38da4c8efef123f11daef"
    }
    # 업적 4: 스킬 전문가 I
    @{
        Path = "Assets/Nytherion/Data/ScriptableObjects/Progression/SkillExpert1_Milestone.asset"
        Content = '%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 0}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: e2eae4ef8975327449a1091606fd9690, type: 3}
  m_Name: SkillExpert1_Milestone
  m_EditorClassIdentifier: 
  milestoneID: milestone_skill_expert_1
  title: "스킬 전문가 I"
  description: "던전에서 누적 50회의 스킬을 사용하십시오."
  icon: {fileID: 0}
  progressionType: 3
  requiredMilestones: []
  targetValue: 50
  rewards:
  - rewardType: 1
    amount: 300
    skillData: {fileID: 0}
    itemData: {fileID: 0}
    relicData: {fileID: 0}'
        MetaGuid = "a39b1f728cde489aab6286fa918cfdef"
    }
    # 무기 4: 철퇴
    @{
        Path = "Assets/Nytherion/Data/ScriptableObjects/Weapons/IronMace.asset"
        Content = '%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 0}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: 7fcc6bc6f7a9e7d4998a440e0763228a, type: 3}
  m_Name: IronMace
  m_EditorClassIdentifier: 
  uniqueID: weapon_iron_mace
  itemName_KR: "철퇴"
  itemName_EN: Iron Mace
  icon: {fileID: 0}
  description_KR: "육중한 철퇴로 적을 타격하여 높은 피해를 입히고 뒤로 밀쳐냅니다."
  description_EN: "A heavy iron mace that deals high damage and knocks back enemies."
  isStackable: 0
  maxStack: 1
  baseValue: 160
  equipmentType: 0
  rarity: 2
  traits: []
  statModifiers: []
  damage: 38
  range: 2.2
  cooldown: 1.5
  weaponType: 1
  weaponSprite: {fileID: 0}
  firePointOffset: {x: 0, y: 0, z: 0}
  projectilePrefab: {fileID: 0}
  projectileSpeed: 10
  extraProjectileMode: 0
  maxChargeTime: 1
  weaponPrefab: {fileID: 0}
  isArchivable: 1'
        MetaGuid = "d242cb37f689e4726bfcfc928421d01f"
    }
    # 장신구 1: 청동 반지
    @{
        Path = "Assets/Nytherion/Data/ScriptableObjects/Weapons/BronzeRing.asset"
        Content = '%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 0}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: a9c8ddff15885af42a6a2ca2b11dacc4, type: 3}
  m_Name: BronzeRing
  m_EditorClassIdentifier: 
  uniqueID: armor_bronze_ring
  itemName_KR: "청동 반지"
  itemName_EN: Bronze Ring
  icon: {fileID: 0}
  description_KR: "착용자의 신체 능력을 아주 미세하게 보조해 주는 낡은 청동 반지입니다."
  description_EN: "An old bronze ring that slightly assists the wearer''s physical abilities."
  isStackable: 0
  maxStack: 1
  baseValue: 70
  equipmentType: 1
  rarity: 0
  traits: []
  statModifiers:
  - stat: 0
    value: 15
    valuePerLevel: 0
    isPercentage: 0
  - stat: 1
    value: 2
    valuePerLevel: 0
    isPercentage: 0
  armorType: 3'
        MetaGuid = "c902bc33f679e3947bdebc82c163d03f"
    }

    # ==================== 5회차 ====================
    # 유물 5: 유리 파편
    @{
        Path = "Assets/Nytherion/Data/ScriptableObjects/Relics/SimpleStats/GlassShard.asset"
        Content = '%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 0}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: 43c7997b6cdbe61438756d24e848ec0c, type: 3}
  m_Name: GlassShard
  m_EditorClassIdentifier: 
  relicName: Glass Shard
  koreanName: 유리 파편
  description_KR: 공격력이 비약적으로 늘어나지만, 최대 체력이 크게 감소합니다.
  description_EN: Significantly increases damage, but greatly reduces maximum health.
  Image: {fileID: 0}
  rarity: 3
  level: 1
  isDisabled: 0
  effectModules: []
  shape:
  - {x: 0, y: 0}
  influenceZones:
  - offset: {x: 1, y: 1}
    type: 1
  - offset: {x: -1, y: -1}
    type: 2
  synergySeriesId: 
  unlockMilestoneID: 
  simpleStatModifiers:
  - stat: 3
    value: 15
    valuePerLevel: 3
    isPercentage: 0
  - stat: 4
    value: 15
    valuePerLevel: 3
    isPercentage: 0
  - stat: 0
    value: -30
    valuePerLevel: -5
    isPercentage: 0'
        MetaGuid = "a521e2760ade8e4cae771239b9c1d0ef"
    }
    # 스킬 5: Overdrive
    @{
        Path = "Assets/Nytherion/Data/ScriptableObjects/Skill/Overdrive_Skill.asset"
        Content = '%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 0}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: 76c76a15f5956734698ddc4f8e8d05f5, type: 3}
  m_Name: Overdrive_Skill
  m_EditorClassIdentifier: 
  skillID: skill_overdrive
  skillType: 5
  skillName: Overdrive
  description: "과부하 상태에 진입하여 일정 시간 공격 속도와 대쉬 속도가 극대화됩니다."
  skillLevel: 1
  exp: 0
  coolDown: 25
  manaCost: 40
  damage: 0
  range: 0
  icon: {fileID: 0}
  skillPrefab: {fileID: 0}
  unlockMilestoneID: '
        MetaGuid = "f390c741e6c38da4c8efef123f11daef"
    }
    # 업적 5: 거인 사냥꾼
    @{
        Path = "Assets/Nytherion/Data/ScriptableObjects/Progression/GiantSlayer_Milestone.asset"
        Content = '%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 0}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: e2eae4ef8975327449a1091606fd9690, type: 3}
  m_Name: GiantSlayer_Milestone
  m_EditorClassIdentifier: 
  milestoneID: milestone_giant_slayer
  title: "거인 사냥꾼"
  description: "던전의 보스를 처치하고 3층을 돌파하십시오."
  icon: {fileID: 0}
  progressionType: 4
  requiredMilestones: []
  targetValue: 3
  rewards:
  - rewardType: 2
    amount: 150
    skillData: {fileID: 0}
    itemData: {fileID: 0}
    relicData: {fileID: 0}'
        MetaGuid = "a40b1f728cde489aab6286fa918cfdef"
    }
    # 무기 5: 암살자 단검
    @{
        Path = "Assets/Nytherion/Data/ScriptableObjects/Weapons/AssassinDagger.asset"
        Content = '%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 0}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: 7fcc6bc6f7a9e7d4998a440e0763228a, type: 3}
  m_Name: AssassinDagger
  m_EditorClassIdentifier: 
  uniqueID: weapon_assassin_dagger
  itemName_KR: "암살자 단검"
  itemName_EN: Assassin Dagger
  icon: {fileID: 0}
  description_KR: "빠르고 치명적인 기습에 어울리는 날카로운 단검입니다. 치명타 확률이 증가합니다."
  description_EN: "A sharp dagger suitable for fast and deadly surprise attacks. Increases critical chance."
  isStackable: 0
  maxStack: 1
  baseValue: 220
  equipmentType: 0
  rarity: 3
  traits: []
  statModifiers:
  - stat: 13
    value: 0.15
    valuePerLevel: 0
    isPercentage: 1
  damage: 14
  range: 1.8
  cooldown: 0.5
  weaponType: 1
  weaponSprite: {fileID: 0}
  firePointOffset: {x: 0, y: 0, z: 0}
  projectilePrefab: {fileID: 0}
  projectileSpeed: 10
  extraProjectileMode: 0
  maxChargeTime: 1
  weaponPrefab: {fileID: 0}
  isArchivable: 1'
        MetaGuid = "d342cb37f689e4726bfcfc928421d01f"
    }
    # 방어구 2: 기사의 판금 갑옷
    @{
        Path = "Assets/Nytherion/Data/ScriptableObjects/Weapons/KnightPlateMail.asset"
        Content = '%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 0}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: a9c8ddff15885af42a6a2ca2b11dacc4, type: 3}
  m_Name: KnightPlateMail
  m_EditorClassIdentifier: 
  uniqueID: armor_knight_plate_mail
  itemName_KR: "기사의 판금 갑옷"
  itemName_EN: Knight''s Plate Mail
  icon: {fileID: 0}
  description_KR: "철제 판금으로 제작되어 매우 뛰어난 방어력을 제공하지만, 무게 때문에 속도가 약간 느려집니다."
  description_EN: "Crafted with steel plates, it provides outstanding defense, but slightly slows you down due to its weight."
  isStackable: 0
  maxStack: 1
  baseValue: 250
  equipmentType: 1
  rarity: 3
  traits: []
  statModifiers:
  - stat: 0
    value: 50
    valuePerLevel: 0
    isPercentage: 0
  - stat: 1
    value: 20
    valuePerLevel: 0
    isPercentage: 0
  - stat: 2
    value: -0.5
    valuePerLevel: 0
    isPercentage: 0
  armorType: 1'
        MetaGuid = "c903bc33f679e3947bdebc82c163d03f"
    }

    # ==================== 6회차 ====================
    # 유물 6: 불사조의 깃털
    @{
        Path = "Assets/Nytherion/Data/ScriptableObjects/Relics/SimpleStats/PhoenixFeather.asset"
        Content = '%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 0}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: 43c7997b6cdbe61438756d24e848ec0c, type: 3}
  m_Name: PhoenixFeather
  m_EditorClassIdentifier: 
  relicName: Phoenix Feather
  koreanName: 불사조의 깃털
  description_KR: 뜨거운 열기를 품은 깃털입니다. 착용자의 생명력을 극대화합니다.
  description_EN: A feather carrying immense warmth. Significantly boosts the wearer''s vitality.
  Image: {fileID: 0}
  rarity: 4
  level: 1
  isDisabled: 0
  effectModules: []
  shape:
  - {x: 0, y: 0}
  influenceZones:
  - offset: {x: 0, y: 1}
    type: 1
  - offset: {x: 1, y: 0}
    type: 1
  - offset: {x: 0, y: -1}
    type: 1
  - offset: {x: -1, y: 0}
    type: 1
  synergySeriesId: 
  unlockMilestoneID: 
  simpleStatModifiers:
  - stat: 0
    value: 100
    valuePerLevel: 25
    isPercentage: 0'
        MetaGuid = "a621e2760ade8e4cae771239b9c1d0ef"
    }
    # 스킬 6: Meteor Strike
    @{
        Path = "Assets/Nytherion/Data/ScriptableObjects/Skill/MeteorStrike_Skill.asset"
        Content = '%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 0}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: 76c76a15f5956734698ddc4f8e8d05f5, type: 3}
  m_Name: MeteorStrike_Skill
  m_EditorClassIdentifier: 
  skillID: skill_meteor_strike
  skillType: 5
  skillName: Meteor Strike
  description: "우주로부터 거대한 운석을 낙하시켜 지정 범위 내의 모든 적에게 폭발적인 파괴를 선사합니다."
  skillLevel: 1
  exp: 0
  coolDown: 15
  manaCost: 60
  damage: 120
  range: 10
  icon: {fileID: 0}
  skillPrefab: {fileID: 0}
  unlockMilestoneID: '
        MetaGuid = "f490c741e6c38da4c8efef123f11daef"
    }
    # 업적 6: 무결점 클리어
    @{
        Path = "Assets/Nytherion/Data/ScriptableObjects/Progression/Untouchable_Milestone.asset"
        Content = '%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 0}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: e2eae4ef8975327449a1091606fd9690, type: 3}
  m_Name: Untouchable_Milestone
  m_EditorClassIdentifier: 
  milestoneID: milestone_untouchable
  title: "무결점 보스 격파"
  description: "누적 5층까지 돌파하며 진정한 생존을 증명하십시오."
  icon: {fileID: 0}
  progressionType: 4
  requiredMilestones: []
  targetValue: 5
  rewards:
  - rewardType: 1
    amount: 1000
    skillData: {fileID: 0}
    itemData: {fileID: 0}
    relicData: {fileID: 0}'
        MetaGuid = "a41b1f728cde489aab6286fa918cfdef"
    }
    # 무기 6: 묠니르
    @{
        Path = "Assets/Nytherion/Data/ScriptableObjects/Weapons/Mjolnir.asset"
        Content = '%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 0}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: 7fcc6bc6f7a9e7d4998a440e0763228a, type: 3}
  m_Name: Mjolnir
  m_EditorClassIdentifier: 
  uniqueID: weapon_mjolnir
  itemName_KR: "묠니르"
  itemName_EN: Mjolnir
  icon: {fileID: 0}
  description_KR: "천둥의 힘이 깃든 전설적인 망치입니다. 적을 강타하면 주변의 적들에게 번개가 연쇄적으로 튑니다."
  description_EN: "A legendary hammer infused with the power of thunder. Striking an enemy chains lightning to nearby foes."
  isStackable: 0
  maxStack: 1
  baseValue: 500
  equipmentType: 0
  rarity: 4
  traits: []
  statModifiers: []
  damage: 45
  range: 2.5
  cooldown: 1.4
  weaponType: 1
  weaponSprite: {fileID: 0}
  firePointOffset: {x: 0, y: 0, z: 0}
  projectilePrefab: {fileID: 0}
  projectileSpeed: 10
  extraProjectileMode: 0
  maxChargeTime: 1
  weaponPrefab: {fileID: 0}
  isArchivable: 1'
        MetaGuid = "d442cb37f689e4726bfcfc928421d01f"
    }
    # 투구 2: 기사의 판금 투구
    @{
        Path = "Assets/Nytherion/Data/ScriptableObjects/Weapons/KnightPlateHelmet.asset"
        Content = '%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 0}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: a9c8ddff15885af42a6a2ca2b11dacc4, type: 3}
  m_Name: KnightPlateHelmet
  m_EditorClassIdentifier: 
  uniqueID: armor_knight_plate_helmet
  itemName_KR: "기사의 판금 투구"
  itemName_EN: Knight''s Plate Helmet
  icon: {fileID: 0}
  description_KR: "기사가 주로 장착하는 단단한 철제 투구입니다. 머리를 안전하게 감쌉니다."
  description_EN: "A heavy steel helmet worn by knights. Safely wraps the head."
  isStackable: 0
  maxStack: 1
  baseValue: 180
  equipmentType: 1
  rarity: 3
  traits: []
  statModifiers:
  - stat: 0
    value: 25
    valuePerLevel: 0
    isPercentage: 0
  - stat: 1
    value: 8
    valuePerLevel: 0
    isPercentage: 0
  armorType: 0'
        MetaGuid = "c904bc33f679e3947bdebc82c163d03f"
    }

    # ==================== 7회차 ====================
    # 유물 7: 납덩이 추
    @{
        Path = "Assets/Nytherion/Data/ScriptableObjects/Relics/SimpleStats/LeadenWeight.asset"
        Content = '%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 0}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: 43c7997b6cdbe61438756d24e848ec0c, type: 3}
  m_Name: LeadenWeight
  m_EditorClassIdentifier: 
  relicName: Leaden Weight
  koreanName: 납덩이 추
  description_KR: 무거운 납덩이의 무게로 인해 속도가 느려지지만 공격력이 강화됩니다.
  description_EN: Heavy weight slows you down, but increases your attack power.
  Image: {fileID: 0}
  rarity: 0
  level: 1
  isDisabled: 0
  effectModules: []
  shape:
  - {x: 0, y: 0}
  influenceZones:
  - offset: {x: 0, y: 1}
    type: 1
  - offset: {x: 0, y: -1}
    type: 2
  synergySeriesId: 
  unlockMilestoneID: 
  simpleStatModifiers:
  - stat: 3
    value: 8
    valuePerLevel: 2
    isPercentage: 0
  - stat: 2
    value: -0.3
    valuePerLevel: 0
    isPercentage: 0'
        MetaGuid = "a721e2760ade8e4cae771239b9c1d0ef"
    }
    # 유물 8: 날카로운 이빨
    @{
        Path = "Assets/Nytherion/Data/ScriptableObjects/Relics/SimpleStats/SharpTooth.asset"
        Content = '%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 0}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: 43c7997b6cdbe61438756d24e848ec0c, type: 3}
  m_Name: SharpTooth
  m_EditorClassIdentifier: 
  relicName: Sharp Tooth
  koreanName: 날카로운 이빨
  description_KR: 맹수의 날카로운 이빨입니다. 적을 공격할 때 극소량의 생명력을 흡수합니다.
  description_EN: The sharp tooth of a beast. Absorbs a very small amount of life when attacking.
  Image: {fileID: 0}
  rarity: 2
  level: 1
  isDisabled: 0
  effectModules: []
  shape:
  - {x: 0, y: 0}
  influenceZones:
  - offset: {x: 1, y: 0}
    type: 1
  - offset: {x: -1, y: 0}
    type: 1
  synergySeriesId: 
  unlockMilestoneID: 
  simpleStatModifiers:
  - stat: 11
    value: 0.03
    valuePerLevel: 0.01
    isPercentage: 1'
        MetaGuid = "a821e2760ade8e4cae771239b9c1d0ef"
    }
    # 유물 9: 행운의 주화
    @{
        Path = "Assets/Nytherion/Data/ScriptableObjects/Relics/SimpleStats/LuckyCoin.asset"
        Content = '%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 0}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: 43c7997b6cdbe61438756d24e848ec0c, type: 3}
  m_Name: LuckyCoin
  m_EditorClassIdentifier: 
  relicName: Lucky Coin
  koreanName: 행운의 주화
  description_KR: 주머니에 지니면 사소한 행운이 따릅니다. 약간의 대쉬 쿨다운 감소 효과를 제공합니다.
  description_EN: Carrying this coin brings small fortunes, slightly reducing dash cooldown.
  Image: {fileID: 0}
  rarity: 2
  level: 1
  isDisabled: 0
  effectModules: []
  shape:
  - {x: 0, y: 0}
  influenceZones:
  - offset: {x: 1, y: 1}
    type: 1
  synergySeriesId: 
  unlockMilestoneID: 
  simpleStatModifiers:
  - stat: 9
    value: -0.15
    valuePerLevel: -0.03
    isPercentage: 0'
        MetaGuid = "a921e2760ade8e4cae771239b9c1d0ef"
    }
    # 업적 7: 타임어택
    @{
        Path = "Assets/Nytherion/Data/ScriptableObjects/Progression/Speedrunner_Milestone.asset"
        Content = '%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 0}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: e2eae4ef8975327449a1091606fd9690, type: 3}
  m_Name: Speedrunner_Milestone
  m_EditorClassIdentifier: 
  milestoneID: milestone_speedrunner
  title: "속도광"
  description: "누적 플레이 타임 300초(5분)를 달성하여 던전 탐험 기초 지식을 습득하십시오."
  icon: {fileID: 0}
  progressionType: 10
  requiredMilestones: []
  targetValue: 300
  rewards:
  - rewardType: 2
    amount: 200
    skillData: {fileID: 0}
    itemData: {fileID: 0}
    relicData: {fileID: 0}'
        MetaGuid = "a42b1f728cde489aab6286fa918cfdef"
    }
    # 신발 2: 신속의 장화
    @{
        Path = "Assets/Nytherion/Data/ScriptableObjects/Weapons/SwiftBoots.asset"
        Content = '%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 0}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: a9c8ddff15885af42a6a2ca2b11dacc4, type: 3}
  m_Name: SwiftBoots
  m_EditorClassIdentifier: 
  uniqueID: armor_swift_boots
  itemName_KR: "신속의 장화"
  itemName_EN: Swift Boots
  icon: {fileID: 0}
  description_KR: "가볍고 바람의 마법이 약하게 깃든 장화입니다. 이동 속도와 대쉬 속도가 대폭 향상됩니다."
  description_EN: "Light boots with a trace of wind magic. Greatly improves movement speed and dash speed."
  isStackable: 0
  maxStack: 1
  baseValue: 200
  equipmentType: 1
  rarity: 3
  traits: []
  statModifiers:
  - stat: 2
    value: 0.8
    valuePerLevel: 0
    isPercentage: 0
  - stat: 7
    value: 1.5
    valuePerLevel: 0
    isPercentage: 0
  - stat: 9
    value: -0.2
    valuePerLevel: 0
    isPercentage: 0
  armorType: 2'
        MetaGuid = "c905bc33f679e3947bdebc82c163d03f"
    }

    # ==================== 8회차 ====================
    # 유물 10: 깨진 모래시계
    @{
        Path = "Assets/Nytherion/Data/ScriptableObjects/Relics/SimpleStats/ShatteredHourglass.asset"
        Content = '%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 0}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: 43c7997b6cdbe61438756d24e848ec0c, type: 3}
  m_Name: ShatteredHourglass
  m_EditorClassIdentifier: 
  relicName: Shattered Hourglass
  koreanName: 깨진 모래시계
  description_KR: 시간이 뒤틀린 모래시계 파편입니다. 대쉬 쿨다운과 공격 차지 타임이 줄어듭니다.
  description_EN: A fragment of an hourglass with warped time, reducing dash cooldown and attack charge time.
  Image: {fileID: 0}
  rarity: 3
  level: 1
  isDisabled: 0
  effectModules: []
  shape:
  - {x: 0, y: 0}
  influenceZones:
  - offset: {x: 1, y: 0}
    type: 1
  - offset: {x: -1, y: 0}
    type: 1
  synergySeriesId: 
  unlockMilestoneID: 
  simpleStatModifiers:
  - stat: 9
    value: -0.3
    valuePerLevel: -0.05
    isPercentage: 0
  - stat: 16
    value: 0.15
    valuePerLevel: 0.03
    isPercentage: 1'
        MetaGuid = "a101e2760ade8e4cae771239b9c1d0ef"
    }
    # 유물 11: 흡혈귀의 송곳니
    @{
        Path = "Assets/Nytherion/Data/ScriptableObjects/Relics/SimpleStats/VampiricFang.asset"
        Content = '%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 0}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: 43c7997b6cdbe61438756d24e848ec0c, type: 3}
  m_Name: VampiricFang
  m_EditorClassIdentifier: 
  relicName: Vampiric Fang
  koreanName: 흡혈귀의 송곳니
  description_KR: 어둠의 피가 맺힌 송곳니입니다. 생명력 흡수율과 최대 체력이 늘어납니다.
  description_EN: A fang tainted with dark blood, boosting lifesteal rate and maximum health.
  Image: {fileID: 0}
  rarity: 3
  level: 1
  isDisabled: 0
  effectModules: []
  shape:
  - {x: 0, y: 0}
  influenceZones:
  - offset: {x: 0, y: 1}
    type: 1
  - offset: {x: 0, y: -1}
    type: 1
  synergySeriesId: 
  unlockMilestoneID: 
  simpleStatModifiers:
  - stat: 11
    value: 0.05
    valuePerLevel: 0.015
    isPercentage: 1
  - stat: 0
    value: 15
    valuePerLevel: 3
    isPercentage: 0'
        MetaGuid = "a111e2760ade8e4cae771239b9c1d0ef"
    }
    # 업적 8: 자산가
    @{
        Path = "Assets/Nytherion/Data/ScriptableObjects/Progression/RichMan_Milestone.asset"
        Content = '%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 0}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: e2eae4ef8975327449a1091606fd9690, type: 3}
  m_Name: RichMan_Milestone
  m_EditorClassIdentifier: 
  milestoneID: milestone_rich_man
  title: "재벌 데뷔"
  description: "던전 탐험 중 누적 5,000 골드를 획득하십시오."
  icon: {fileID: 0}
  progressionType: 2
  requiredMilestones: []
  targetValue: 5000
  rewards:
  - rewardType: 2
    amount: 150
    skillData: {fileID: 0}
    itemData: {fileID: 0}
    relicData: {fileID: 0}'
        MetaGuid = "a43b1f728cde489aab6286fa918cfdef"
    }
    # 업적 9: 유물 수집가
    @{
        Path = "Assets/Nytherion/Data/ScriptableObjects/Progression/RelicCollector_Milestone.asset"
        Content = '%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 0}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: e2eae4ef8975327449a1091606fd9690, type: 3}
  m_Name: RelicCollector_Milestone
  m_EditorClassIdentifier: 
  milestoneID: milestone_relic_collector
  title: "던전의 정복자"
  description: "던전의 중간 관문(3층)을 누적 3회 정복하십시오."
  icon: {fileID: 0}
  progressionType: 4
  requiredMilestones: []
  targetValue: 3
  rewards:
  - rewardType: 4
    amount: 1
    skillData: {fileID: 0}
    itemData: {fileID: 0}
    relicData: {fileID: 11400000, guid: b242e2760ade8e4cae771239b9c1d0ef, type: 3}'
        MetaGuid = "a44b1f728cde489aab6286fa918cfdef"
    }
    # 장신구 2: 은빛 아뮬렛
    @{
        Path = "Assets/Nytherion/Data/ScriptableObjects/Weapons/SilverAmulet.asset"
        Content = '%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 0}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: a9c8ddff15885af42a6a2ca2b11dacc4, type: 3}
  m_Name: SilverAmulet
  m_EditorClassIdentifier: 
  uniqueID: armor_silver_amulet
  itemName_KR: "은빛 아뮬렛"
  itemName_EN: Silver Amulet
  icon: {fileID: 0}
  description_KR: "빛나는 은빛 목걸이입니다. 착용자의 치명타 확률과 치명타 피해량이 증가합니다."
  description_EN: "A glowing silver necklace. Increases the wearer''s critical chance and critical damage."
  isStackable: 0
  maxStack: 1
  baseValue: 180
  equipmentType: 1
  rarity: 2
  traits: []
  statModifiers:
  - stat: 13
    value: 0.08
    valuePerLevel: 0
    isPercentage: 1
  - stat: 14
    value: 0.2
    valuePerLevel: 0
    isPercentage: 1
  armorType: 3'
        MetaGuid = "c906bc33f679e3947bdebc82c163d03f"
    }

    # ==================== 9회차 ====================
    # 유물 12: 오라 호박석
    @{
        Path = "Assets/Nytherion/Data/ScriptableObjects/Relics/SimpleStats/AuraAmber.asset"
        Content = '%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 0}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: 43c7997b6cdbe61438756d24e848ec0c, type: 3}
  m_Name: AuraAmber
  m_EditorClassIdentifier: 
  relicName: Aura Amber
  koreanName: 오라 호박석
  description_KR: 마력 오라가 깃든 호박 보석입니다. 기본 생명력과 방어력을 골고루 돋웁니다.
  description_EN: An amber gemstone carrying aura energy, steadily boosting basic health and defense.
  Image: {fileID: 0}
  rarity: 3
  level: 1
  isDisabled: 0
  effectModules: []
  shape:
  - {x: 0, y: 0}
  influenceZones:
  - offset: {x: 1, y: 0}
    type: 1
  - offset: {x: -1, y: 0}
    type: 1
  synergySeriesId: 
  unlockMilestoneID: 
  simpleStatModifiers:
  - stat: 0
    value: 30
    valuePerLevel: 6
    isPercentage: 0
  - stat: 1
    value: 5
    valuePerLevel: 1
    isPercentage: 0'
        MetaGuid = "a121e2760ade8e4cae771239b9c1d0ef"
    }
    # 유물 13: 선봉장의 방패
    @{
        Path = "Assets/Nytherion/Data/ScriptableObjects/Relics/SimpleStats/VanguardShield.asset"
        Content = '%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 0}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: 43c7997b6cdbe61438756d24e848ec0c, type: 3}
  m_Name: VanguardShield
  m_EditorClassIdentifier: 
  relicName: Vanguard Shield
  koreanName: 선봉장의 방패
  description_KR: 전선에서 쓰이는 거대한 방패 형상의 각인입니다. 방어력과 생명력을 대폭 부여합니다.
  description_EN: A heavy battlefield shield engraving, granting immense defense and health.
  Image: {fileID: 0}
  rarity: 4
  level: 1
  isDisabled: 0
  effectModules: []
  shape:
  - {x: 0, y: 0}
  influenceZones:
  - offset: {x: 0, y: 1}
    type: 1
  - offset: {x: 1, y: 0}
    type: 1
  - offset: {x: 0, y: -1}
    type: 1
  - offset: {x: -1, y: 0}
    type: 1
  synergySeriesId: 
  unlockMilestoneID: 
  simpleStatModifiers:
  - stat: 1
    value: 15
    valuePerLevel: 3
    isPercentage: 0
  - stat: 0
    value: 60
    valuePerLevel: 10
    isPercentage: 0'
        MetaGuid = "a131e2760ade8e4cae771239b9c1d0ef"
    }
    # 업적 10: 스킬 마스터
    @{
        Path = "Assets/Nytherion/Data/ScriptableObjects/Progression/SkillMaster_Milestone.asset"
        Content = '%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 0}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: e2eae4ef8975327449a1091606fd9690, type: 3}
  m_Name: SkillMaster_Milestone
  m_EditorClassIdentifier: 
  milestoneID: milestone_skill_master
  title: "마법 학회 졸업"
  description: "던전 탐험 도중 스킬을 누적 200회 시전하십시오."
  icon: {fileID: 0}
  progressionType: 3
  requiredMilestones: []
  targetValue: 200
  rewards:
  - rewardType: 2
    amount: 300
    skillData: {fileID: 0}
    itemData: {fileID: 0}
    relicData: {fileID: 0}'
        MetaGuid = "a45b1f728cde489aab6286fa918cfdef"
    }
    # 업적 11: 불굴의 의지
    @{
        Path = "Assets/Nytherion/Data/ScriptableObjects/Progression/Survivor_Milestone.asset"
        Content = '%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 0}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: e2eae4ef8975327449a1091606fd9690, type: 3}
  m_Name: Survivor_Milestone
  m_EditorClassIdentifier: 
  milestoneID: milestone_survivor
  title: "산전수전"
  description: "적들의 공격을 버텨내어 누적 1,000 데미지 피해를 받으십시오."
  icon: {fileID: 0}
  progressionType: 13
  requiredMilestones: []
  targetValue: 1000
  rewards:
  - rewardType: 2
    amount: 200
    skillData: {fileID: 0}
    itemData: {fileID: 0}
    relicData: {fileID: 0}'
        MetaGuid = "a46b1f728cde489aab6286fa918cfdef"
    }
    # 업적 12: 최종 보스 처치
    @{
        Path = "Assets/Nytherion/Data/ScriptableObjects/Progression/NytherionConqueror_Milestone.asset"
        Content = '%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 0}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: e2eae4ef8975327449a1091606fd9690, type: 3}
  m_Name: NytherionConqueror_Milestone
  m_EditorClassIdentifier: 
  milestoneID: milestone_nytherion_conqueror
  title: "심연의 정복자"
  description: "마지막 5층의 강력한 수호자를 처치하고 니더리온 심층을 정복하십시오."
  icon: {fileID: 0}
  progressionType: 4
  requiredMilestones: []
  targetValue: 5
  rewards:
  - rewardType: 2
    amount: 500
    skillData: {fileID: 0}
    itemData: {fileID: 0}
    relicData: {fileID: 0}'
        MetaGuid = "a47b1f728cde489aab6286fa918cfdef"
    }
)

Write-Host "Starting mass generation of Unity Assets (Rounds 4 to 9)..."

foreach ($asset in $assets) {
    $fullPath = $asset.Path
    # 디렉토리 생성
    $dir = Split-Path $fullPath -Parent
    if (!(Test-Path $dir)) {
        New-Item -ItemType Directory -Force -Path $dir | Out-Null
        Write-Host "Created directory: $dir"
    }

    # .asset 파일 작성
    [System.IO.File]::WriteAllText($fullPath, $asset.Content)
    Write-Host "Created Asset: $fullPath"

    # .meta 파일 작성
    $metaPath = "$fullPath.meta"
    $metaContent = "fileFormatVersion: 2
guid: $($asset.MetaGuid)
DefaultImporter:
  externalObjects: {}
  userData: 
  assetBundleName: 
  assetBundleVariant: "
    [System.IO.File]::WriteAllText($metaPath, $metaContent)
    Write-Host "Created Meta: $metaPath"
}

Write-Host "All 30 assets generated successfully!"
