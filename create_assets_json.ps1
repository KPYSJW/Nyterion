# create_assets_json.ps1
$jsonPath = "c:\RoguelikePrototype\assets_data.json"
if (!(Test-Path $jsonPath)) {
    Write-Error "JSON database file not found at $jsonPath"
    exit 1
}

# UTF-8 인코딩 명시
$rawJson = Get-Content -Raw -Encoding UTF8 -Path $jsonPath
$assets = $rawJson | ConvertFrom-Json

Write-Host "Starting mass generation of Unity Assets from JSON..."

foreach ($asset in $assets) {
    $fullPath = $asset.Path
    # 디렉토리 생성
    $dir = Split-Path $fullPath -Parent
    if (!(Test-Path $dir)) {
        New-Item -ItemType Directory -Force -Path $dir | Out-Null
        Write-Host "Created directory: $dir"
    }

    # UTF-8으로 파일 쓰기
    [System.IO.File]::WriteAllText($fullPath, $asset.Content, [System.Text.Encoding]::UTF8)
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
    [System.IO.File]::WriteAllText($metaPath, $metaContent, [System.Text.Encoding]::UTF8)
    Write-Host "Created Meta: $metaPath"
}

Write-Host "All assets generated successfully!"
