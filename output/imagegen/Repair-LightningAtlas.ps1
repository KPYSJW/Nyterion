param(
    [string]$SourcePath = 'C:/Users/rkswl/OneDrive/Desktop/Unity/Nyterion/Assets/Nytherion/Art/VFX/BossKnight/PurpleLightningBeam_1x6_v4_pixel.png',
    [string]$OutputPath = 'C:/Users/rkswl/OneDrive/Desktop/Unity/Nyterion/Assets/Nytherion/Art/VFX/BossKnight/PurpleLightningBeam_1x6_v5_aligned.png',
    [switch]$WriteOutput,
    [int]$FrameCount = 6,
    [int]$CellHeight = 320
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -TypeDefinition @'
using System;
using System.Collections.Generic;
public sealed class LightningComponent {
    public int MinX = int.MaxValue, MinY = int.MaxValue, MaxX, MaxY, Count;
    public long WhiteY, WhiteCount;
    public List<int> Pixels = new List<int>();
    public int CenterY { get { return WhiteCount > 0 ? (int)Math.Round((double)WhiteY/WhiteCount) : (MinY+MaxY)/2; } }
}
public static class LightningAtlasRepair {
    public static List<LightningComponent> Components(byte[] bgra, int width, int height, int stride, byte minimumAlpha) {
        bool[] visited = new bool[width*height];
        int[] queue = new int[width*height];
        var components = new List<LightningComponent>();
        for(int seed=0; seed<visited.Length; seed++) {
            int sx=seed%width, sy=seed/width;
            if(visited[seed] || bgra[sy*stride+sx*4+3] < minimumAlpha) continue;
            var c = new LightningComponent();
            int head=0, tail=0; queue[tail++]=seed; visited[seed]=true;
            while(head<tail) {
                int p=queue[head++], x=p%width, y=p/width, offset=y*stride+x*4;
                c.Pixels.Add(p); c.Count++;
                c.MinX=Math.Min(c.MinX,x); c.MaxX=Math.Max(c.MaxX,x);
                c.MinY=Math.Min(c.MinY,y); c.MaxY=Math.Max(c.MaxY,y);
                if(bgra[offset+3]>128 && bgra[offset]>220 && bgra[offset+1]>220 && bgra[offset+2]>220) {
                    c.WhiteCount++; c.WhiteY+=y;
                }
                for(int dy=-1;dy<=1;dy++) for(int dx=-1;dx<=1;dx++) {
                    int nx=x+dx, ny=y+dy;
                    if(nx<0 || ny<0 || nx>=width || ny>=height) continue;
                    int np=ny*width+nx;
                    if(!visited[np] && bgra[ny*stride+nx*4+3]>=minimumAlpha) {
                        visited[np]=true; queue[tail++]=np;
                    }
                }
            }
            components.Add(c);
        }
        components.Sort((a,b)=>a.CenterY.CompareTo(b.CenterY));
        return components;
    }
    public static byte[] Pack(byte[] source, int width, int stride, LightningComponent[] beams, int cellHeight) {
        const int margin=16;
        int outputHeight=cellHeight*beams.Length;
        byte[] result=new byte[width*outputHeight*4];
        int extent=1;
        foreach(var b in beams) extent=Math.Max(extent,Math.Max(b.CenterY-b.MinY,b.MaxY-b.CenterY));
        double scale=Math.Min(1.0,(cellHeight/2.0-margin-2)/extent);
        for(int frame=0;frame<beams.Length;frame++) {
            var beam=beams[frame];
            var member=new HashSet<int>(beam.Pixels);
            for(int dy=margin;dy<cellHeight-margin;dy++) {
                int sy=(int)Math.Round(beam.CenterY+(dy-cellHeight/2.0)/scale);
                if(sy<beam.MinY || sy>beam.MaxY) continue;
                for(int x=0;x<width;x++) {
                    if(!member.Contains(sy*width+x)) continue;
                    int src=sy*stride+x*4, dst=((frame*cellHeight+dy)*width+x)*4;
                    Array.Copy(source,src,result,dst,4);
                }
            }
            // Move the tail profile smoothly toward the start profile, then
            // make the two seam columns identical, preserving hard pixels.
            int row=frame*cellHeight;
            int startCenter=CoreCenter(result,width,row,cellHeight,0);
            int endCenter=CoreCenter(result,width,row,cellHeight,width-1);
            byte[] before=(byte[])result.Clone();
            const int seamSpan=48;
            for(int x=width-seamSpan;x<width;x++) {
                double progress=(double)(x-(width-seamSpan))/(seamSpan-1);
                int shift=(int)Math.Round((startCenter-endCenter)*progress);
                for(int y=0;y<cellHeight;y++) {
                    int dst=((row+y)*width+x)*4, srcY=y-shift;
                    Array.Clear(result,dst,4);
                    if(srcY>=margin && srcY<cellHeight-margin && y>=margin && y<cellHeight-margin)
                        Array.Copy(before,((row+srcY)*width+x)*4,result,dst,4);
                }
            }
            for(int y=0;y<cellHeight;y++)
                Array.Copy(result,((row+y)*width)*4,result,((row+y)*width+width-1)*4,4);
        }
        return result;
    }
    private static int CoreCenter(byte[] pixels,int width,int row,int height,int x) {
        int count=0,total=0;
        for(int y=0;y<height;y++) {
            int p=((row+y)*width+x)*4;
            if(pixels[p+3]>=128 && pixels[p]>220 && pixels[p+1]>220 && pixels[p+2]>220) {count++;total+=y;}
        }
        return count>0 ? total/count : height/2;
    }
    public static string Validate(byte[] pixels,int width,int cellHeight,int frames) {
        for(int f=0;f<frames;f++) {
            bool hasCore=false;
            for(int y=0;y<cellHeight;y++) for(int x=0;x<width;x++) {
                int p=((f*cellHeight+y)*width+x)*4;
                if((y<16 || y>=cellHeight-16) && pixels[p+3]!=0)
                    throw new Exception("Opaque pixel in frame margin.");
                if(pixels[p+3]>128 && pixels[p]>220 && pixels[p+1]>220 && pixels[p+2]>220) hasCore=true;
            }
            if(!hasCore) throw new Exception("Frame has no lightning core.");
            for(int y=0;y<cellHeight;y++) for(int c=0;c<4;c++)
                if(pixels[((f*cellHeight+y)*width)*4+c]!=pixels[((f*cellHeight+y)*width+width-1)*4+c])
                    throw new Exception("Horizontal seam does not match.");
        }
        return "PASS: " + frames + " populated frames; all 16px top/bottom margins transparent; left/right seams match exactly.";
    }
}
'@

$source = [System.Drawing.Bitmap]::new($SourcePath)
try {
    $rect = [System.Drawing.Rectangle]::new(0, 0, $source.Width, $source.Height)
    $locked = $source.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::ReadOnly, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    try {
        if ($locked.Stride -le 0) { throw 'Expected top-to-bottom bitmap stride.' }
        $stride = $locked.Stride
        $pixels = [byte[]]::new($stride * $source.Height)
        [System.Runtime.InteropServices.Marshal]::Copy($locked.Scan0, $pixels, 0, $pixels.Length)
    } finally { $source.UnlockBits($locked) }
    $components = [LightningAtlasRepair]::Components($pixels, $source.Width, $source.Height, $stride, 16)
    $beams = @($components | Where-Object { ($_.MaxX - $_.MinX + 1) -ge $source.Width * 0.9 })
    "Connected components: $($components.Count); full-width beams: $($beams.Count)"
    foreach ($beam in $beams) {
        "Beam Y $($beam.MinY)-$($beam.MaxY); white center $($beam.CenterY); pixels $($beam.Count)"
    }
    if (-not $WriteOutput) {
        foreach ($threshold in @(16,32,64,192,240)) {
            $parts = [LightningAtlasRepair]::Components($pixels, $source.Width, $source.Height, $stride, $threshold)
            $wide = @($parts | Where-Object { ($_.MaxX - $_.MinX + 1) -ge $source.Width * 0.9 })
            "Alpha threshold ${threshold}: $($wide.Count) wide beams"
        }
    }
    if (-not $WriteOutput) { return }
    if ($beams.Count -ne $FrameCount) { throw "Expected $FrameCount isolated full-width beams. No output written." }
    if (Test-Path -LiteralPath $OutputPath) { throw 'Output already exists; refusing to overwrite.' }
    if ($CellHeight -lt 64 -or $FrameCount -lt 1) { throw 'Invalid atlas dimensions.' }
    $cellHeight = $CellHeight
    $packed = [LightningAtlasRepair]::Pack($pixels, $source.Width, $stride, [LightningComponent[]]$beams, $cellHeight)
    [LightningAtlasRepair]::Validate($packed, $source.Width, $cellHeight, $FrameCount)
    $output = [System.Drawing.Bitmap]::new($source.Width, $cellHeight*$FrameCount, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    try {
        $outRect = [System.Drawing.Rectangle]::new(0, 0, $output.Width, $output.Height)
        $outLock = $output.LockBits($outRect, [System.Drawing.Imaging.ImageLockMode]::WriteOnly, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        try {
            if($outLock.Stride -ne $source.Width*4) { throw 'Unexpected output stride.' }
            [System.Runtime.InteropServices.Marshal]::Copy($packed, 0, $outLock.Scan0, $packed.Length)
        } finally { $output.UnlockBits($outLock) }
        $output.Save($OutputPath, [System.Drawing.Imaging.ImageFormat]::Png)
        "Saved $OutputPath ($($output.Width)x$($output.Height), frame $($output.Width)x$cellHeight)"
    } finally { $output.Dispose() }
} finally { $source.Dispose() }
