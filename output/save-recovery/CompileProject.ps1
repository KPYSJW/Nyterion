$ErrorActionPreference = 'Stop'
$workspace = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
Set-Location -LiteralPath $workspace
$csc = 'C:/Program Files/dotnet/sdk/9.0.201/Roslyn/bincore/csc.dll'
foreach ($assembly in @('Assembly-CSharp', 'Assembly-CSharp-Editor')) {
    [xml]$project = Get-Content -Raw -LiteralPath ($assembly + '.csproj')
    $defines = @($project.Project.PropertyGroup.DefineConstants | Where-Object { $_ })[0]
    $references = @($project.Project.ItemGroup.Reference.HintPath | Where-Object {
        $_ -and (Test-Path -LiteralPath $_) -and [IO.Path]::GetFileName($_) -ne 'Assembly-CSharp.dll'
    }) + @(Get-ChildItem Library/ScriptAssemblies -Filter *.dll | Where-Object {
        $_.Name -notlike 'Assembly-CSharp*'
    } | ForEach-Object { $_.FullName })
    if ($assembly -eq 'Assembly-CSharp-Editor') { $references += Join-Path $PSScriptRoot 'Assembly-CSharp.dll' }
    $sources = @($project.Project.ItemGroup.Compile.Include | Where-Object { $_ })
    foreach ($source in $sources) { if (-not (Test-Path -LiteralPath $source)) { throw ('컴파일 원본 누락: ' + $source) } }
    $label = if ($assembly -eq 'Assembly-CSharp') { 'runtime' } else { 'editor' }
    $responsePath = Join-Path $PSScriptRoot ($label + '-compile.rsp')
    $arguments = @('/nologo', '/nostdlib+', '/target:library', '/langversion:9.0', ('/define:' + $defines), ('/out:"' + (Join-Path $PSScriptRoot ($assembly + '.dll')) + '"'))
    $arguments += @($references | Sort-Object -Unique | ForEach-Object { '/reference:"' + $_ + '"' })
    $arguments += @($sources | ForEach-Object { '"' + $_ + '"' })
    Set-Content -LiteralPath $responsePath -Value $arguments -Encoding UTF8
    & dotnet $csc ('@' + $responsePath) *> (Join-Path $PSScriptRoot ($label + '-compile.log'))
    if ($LASTEXITCODE -ne 0) { throw ($assembly + ' 컴파일 실패. 로그를 확인하세요.') }
    Write-Output ($assembly + ': 컴파일 통과 (' + $sources.Count + '개 원본)')
}
