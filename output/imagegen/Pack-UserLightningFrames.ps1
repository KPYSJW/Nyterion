$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$sourceDirectory = 'C:/Users/rkswl/OneDrive/Desktop/VFX1/Frames'
$destination = 'C:/Users/rkswl/OneDrive/Desktop/Unity/Nyterion/Assets/Nytherion/Art/VFX/BossKnight/BlueLightning_User_1x4.png'
if (Test-Path -LiteralPath $destination) { throw 'Destination already exists.' }
$atlas = [System.Drawing.Bitmap]::new(256, 512, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
try {
    for ($frame = 0; $frame -lt 4; $frame++) {
        $source = [System.Drawing.Bitmap]::new((Join-Path $sourceDirectory "lightning_skill1_frame$($frame + 1).png"))
        try {
            if ($source.Width -ne 256 -or $source.Height -ne 128) { throw 'Unexpected frame dimensions.' }
            for ($y = 0; $y -lt 128; $y++) {
                for ($x = 0; $x -lt 256; $x++) {
                    $pixel = $source.GetPixel($x, $y)
                    if (($y -lt 2 -or $y -ge 126) -and $pixel.A -ne 0) { throw 'Frame boundary is not transparent.' }
                    $atlas.SetPixel($x, $frame * 128 + $y, $pixel)
                }
            }
        } finally { $source.Dispose() }
    }
    $atlas.Save($destination, [System.Drawing.Imaging.ImageFormat]::Png)
} finally { $atlas.Dispose() }
$packed = [System.Drawing.Bitmap]::new($destination)
try {
    for ($frame = 0; $frame -lt 4; $frame++) {
        $source = [System.Drawing.Bitmap]::new((Join-Path $sourceDirectory "lightning_skill1_frame$($frame + 1).png"))
        try {
            for ($y = 0; $y -lt 128; $y++) {
                for ($x = 0; $x -lt 256; $x++) {
                    if ($packed.GetPixel($x, $frame * 128 + $y).ToArgb() -ne $source.GetPixel($x, $y).ToArgb()) { throw 'Pixel mismatch.' }
                }
            }
            Write-Output "Frame $($frame + 1): every RGBA pixel matches; transparent row boundaries verified."
        } finally { $source.Dispose() }
    }
} finally { $packed.Dispose() }
