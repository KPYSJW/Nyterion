# update_locks_conditions.ps1
# 기존 스킬 14종의 해금 업적 조건을 임시 설정 기획안에 맞춰 업데이트합니다.

$milestoneConditions = @{
    "AS_Milestone"                = @{ Type = 3;  Val = 10 }    # UseSkill 10
    "Atk_Buff_Milestone"          = @{ Type = 1;  Val = 50 }    # KillEnemy 50
    "Lifesteal_Milestone"         = @{ Type = 8;  Val = 200 }   # TakeDamage 200
    "All_Stat_Milestone"          = @{ Type = 2;  Val = 2000 }  # CollectGold 2000
    "Dash_CD_Milestone"           = @{ Type = 4;  Val = 2 }     # ClearFloor 2
    "Aura_Milestone"              = @{ Type = 10; Val = 5 }     # BuyShopItem 5
    "Blackhole_Milestone"         = @{ Type = 15; Val = 1 }     # TangledYarnRoomClear 1
    "Drone_Milestone"             = @{ Type = 9;  Val = 10 }    # TornPouchTrigger 10
    "Turret_Milestone"            = @{ Type = 13; Val = 5 }     # CenterPebblePlace 5
    "Laser_Milestone"             = @{ Type = 7;  Val = 10000 } # DealDamage 10000
    "Shadow_Clone_Milestone"      = @{ Type = 14; Val = 1 }     # SocialDistancingTrigger 1
    "Soul_Eater _Milestone"       = @{ Type = 1;  Val = 300 }   # KillEnemy 300
    "Spiral_Milestone"            = @{ Type = 16; Val = 15 }    # SqueakyGearTrigger 15
    "LuckOverdose_Milestone"      = @{ Type = 18; Val = 1 }     # LuckyCloverResetInOneBattle 1
}

Write-Host "Updating Milestone Conditions..."

foreach ($msName in $milestoneConditions.Keys) {
    $filePath = "Assets/Nytherion/Data/ScriptableObjects/Progression/$msName.asset"
    $cond = $milestoneConditions[$msName]
    $type = $cond.Type
    $val = $cond.Val
    
    if (Test-Path $filePath) {
        $content = Get-Content -Raw -Encoding UTF8 -Path $filePath
        
        # 1. progressionType 처리
        if ($content -match "progressionType:") {
            $content = $content -replace "progressionType:.*", "progressionType: $type"
        } else {
            # progressionType이 없으면 requiredMilestones 바로 위에 기입
            $content = $content -replace "requiredMilestones:", "progressionType: $type`r`n  requiredMilestones:"
        }
        
        # 2. targetValue 처리
        if ($content -match "targetValue:") {
            $content = $content -replace "targetValue:.*", "targetValue: $val"
        } else {
            # targetValue가 없으면 requiredMilestones 바로 위에 기입
            $content = $content -replace "requiredMilestones:", "targetValue: $val`r`n  requiredMilestones:"
        }
        
        [System.IO.File]::WriteAllText($filePath, $content, [System.Text.Encoding]::UTF8)
        Write-Host "Updated Milestone: $msName -> progressionType: $type, targetValue: $val"
    } else {
        Write-Warning "File not found: $filePath"
    }
}

Write-Host "`r`nMilestone conditions updated successfully!"
