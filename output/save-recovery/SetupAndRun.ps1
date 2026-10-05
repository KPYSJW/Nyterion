$ErrorActionPreference = 'Stop'
$workspace = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$fixture = Join-Path $workspace 'tmp/save-recovery-regression'
$production = Join-Path $fixture 'Assets/Production'
$plugins = Join-Path $fixture 'Assets/Plugins'
$editor = Join-Path $fixture 'Assets/Editor'
New-Item -ItemType Directory -Force -Path $production, $plugins, $editor, (Join-Path $fixture 'Packages'), (Join-Path $fixture 'ProjectSettings') | Out-Null
$sources = @(
    'Assets/Nytherion/Services/JsonSaveService.cs',
    'Assets/Nytherion/Core/Managers/SaveLoadManager.cs',
    'Assets/Nytherion/Core/Managers/BaseManager.cs',
    'Assets/Nytherion/Core/Interfaces/ISaveable.cs',
    'Assets/Nytherion/Core/Data/SaveData.cs',
    'Assets/Nytherion/Core/Data/RelicGridState.cs',
    'Assets/Nytherion/Core/Data/ProgressionState.cs',
    'Assets/Nytherion/Core/Enums/CurrencyType.cs',
    'Assets/Nytherion/Core/Enums/EquipmentSlotType.cs',
    'Assets/Nytherion/Core/Enums/Rarity.cs'
)
foreach ($source in $sources) { Copy-Item -LiteralPath (Join-Path $workspace $source) -Destination $production -Force }
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'SaveTestDependencies.cs') -Destination $production -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'SaveRecoveryRegression.cs') -Destination $editor -Force
Copy-Item -LiteralPath (Join-Path $workspace 'Library/ScriptAssemblies/VContainer.dll') -Destination $plugins -Force
Copy-Item -LiteralPath (Join-Path $workspace 'Library/PackageCache/com.unity.nuget.newtonsoft-json@3.2.1/Runtime/Newtonsoft.Json.dll') -Destination $plugins -Force
Copy-Item -LiteralPath (Join-Path $workspace 'ProjectSettings/ProjectVersion.txt') -Destination (Join-Path $fixture 'ProjectSettings/ProjectVersion.txt') -Force
Set-Content -LiteralPath (Join-Path $fixture 'Packages/manifest.json') -Encoding UTF8 -Value '{"dependencies":{"com.unity.modules.jsonserialize":"1.0.0","com.unity.modules.imgui":"1.0.0"}}'
$legacy = & git -C $workspace show 'HEAD:Assets/Nytherion/Core/Managers/SaveLoadManager.cs'
if ($LASTEXITCODE -ne 0) { throw '이전 저장 매니저 원본 읽기 실패' }
$legacy = ($legacy -join "`n").Replace('public class SaveLoadManager :', 'public class LegacySaveLoadManager :')
Set-Content -LiteralPath (Join-Path $production 'LegacySaveLoadManager.cs') -Encoding UTF8 -Value $legacy
$unity = 'C:/Program Files/Unity/Hub/Editor/2022.3.62f2/Editor/Unity.exe'
$process = Start-Process -FilePath $unity -ArgumentList @(
    '-batchmode', '-nographics', '-projectPath', ('"' + $fixture + '"'),
    '-executeMethod', 'SaveRecoveryRegression.RunBatch',
    '-logFile', ('"' + (Join-Path $PSScriptRoot 'unity-regression.log') + '"')
) -WindowStyle Hidden -PassThru
Write-Output ('Unity test PID: ' + $process.Id)
$process.WaitForExit()
exit $process.ExitCode
