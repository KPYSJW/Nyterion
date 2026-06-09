# update_locks.ps1
# 1. 20개 스킬 에셋에 unlockMilestoneID를 기입합니다.
$skillMappings = @{
    "AS_Skill"            = "AS"
    "All_Stat_Skill"      = "All_Stat"
    "Atk_Buff_Skill"      = "Atk_Buff"
    "Aura_Skill"          = "Aura"
    "Blackhole_Skill"     = "Blackhole"
    "Dash_CD_Skill"       = "Dash_CD"
    "Drone_Skill"         = "Drone"
    "Laser_Skill"         = "Laser"
    "Lifesteal_Skill"     = "Lifesteal"
    "MultiShot_Skill"     = "LuckOverdose"
    "Shadow_Clone_Skill"  = "Shadow_Clone"
    "Soul_Eater_Skill"    = "Soul_Eater"
    "Spiral_Skill"        = "Spiral"
    "Turret_Skill"        = "Turret"
    "FireWave_Skill"      = "milestone_kill_100"
    "CallLightning_Skill" = "milestone_gold_collector_1"
    "IceShard_Skill"      = "milestone_first_steps"
    "FrostNova_Skill"     = "milestone_skill_expert_1"
    "Overdrive_Skill"     = "milestone_giant_slayer"
    "MeteorStrike_Skill"  = "milestone_untouchable"
}

Write-Host "Updating Skill Assets..."
foreach ($skillName in $skillMappings.Keys) {
    $filePath = "Assets/Nytherion/Data/ScriptableObjects/Skill/$skillName.asset"
    $milestoneId = $skillMappings[$skillName]
    
    if (Test-Path $filePath) {
        $content = Get-Content -Raw -Encoding UTF8 -Path $filePath
        if ($content -match "unlockMilestoneID:") {
            # 이미 있으면 값 교체
            $content = $content -replace "unlockMilestoneID:.*", "unlockMilestoneID: $milestoneId"
        } else {
            # 없으면 끝에 추가 (MonoBehaviour 블록 내부이므로 2칸 들여쓰기)
            $content += "`r`n  unlockMilestoneID: $milestoneId"
        }
        [System.IO.File]::WriteAllText($filePath, $content, [System.Text.Encoding]::UTF8)
        Write-Host "Updated Skill: $skillName -> unlockMilestoneID: $milestoneId"
    } else {
        Write-Warning "File not found: $filePath"
    }
}

# 2. 7개 마일스톤 에셋의 보상을 스킬 해금 보상으로 변경합니다.
$milestoneRewards = @{
    "Kill100_Milestone"          = @{ Guid = "f190c741e6c38da4c8efef123f11daef"; Amount = 1 } # FireWave
    "GoldCollector1_Milestone"   = @{ Guid = "cb3776a15f5956734698ddc4f8e8d05f"; Amount = 1 } # CallLightning
    "FirstSteps_Milestone"       = @{ Guid = "db3776a15f5956734698ddc4f8e8d05e"; Amount = 1 } # IceShard
    "SkillExpert1_Milestone"     = @{ Guid = "f290c741e6c38da4c8efef123f11daef"; Amount = 1 } # FrostNova
    "GiantSlayer_Milestone"      = @{ Guid = "f390c741e6c38da4c8efef123f11daef"; Amount = 1 } # Overdrive
    "Untouchable_Milestone"      = @{ Guid = "f490c741e6c38da4c8efef123f11daef"; Amount = 1 } # MeteorStrike
    "LuckOverdose_Milestone"     = @{ Guid = "b6abdc4cb5d96f3458e4d6085efa6f16"; Amount = 1 } # MultiShot
}

Write-Host "`r`nUpdating Milestone Rewards..."
foreach ($msName in $milestoneRewards.Keys) {
    $filePath = "Assets/Nytherion/Data/ScriptableObjects/Progression/$msName.asset"
    $reward = $milestoneRewards[$msName]
    $guid = $reward.Guid
    $amt = $reward.Amount
    
    if (Test-Path $filePath) {
        $content = Get-Content -Raw -Encoding UTF8 -Path $filePath
        $newRewards = "  rewards:`r`n  - rewardType: 0`r`n    amount: $amt`r`n    skillData: {fileID: 11400000, guid: $guid, type: 2}`r`n    itemData: {fileID: 0}`r`n    relicData: {fileID: 0}"
        
        # rewards: 블록을 정규식으로 통째로 교체합니다. (?s)는 single-line 모드로 dot(.)이 개행도 포함하도록 만듭니다.
        if ($content -match "(?s)  rewards:.*") {
            $content = $content -replace "(?s)  rewards:.*", $newRewards
        } else {
            $content += "`r`n$newRewards"
        }
        
        [System.IO.File]::WriteAllText($filePath, $content, [System.Text.Encoding]::UTF8)
        Write-Host "Updated Milestone Reward: $msName -> Skill GUID: $guid"
    } else {
        Write-Warning "File not found: $filePath"
    }
}

Write-Host "`r`nAll mappings updated successfully!"
