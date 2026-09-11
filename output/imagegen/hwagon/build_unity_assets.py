from pathlib import Path
import base64, json, re, shutil, struct, uuid

ROOT = Path(r"C:\RoguelikePrototype")
OUT = ROOT / "output/imagegen/hwagon"
SOURCE = Path(r"C:\Users\kyung\.codex\generated_images\01a08192-ddc5-7980-a3b6-5a5f0bce6813\exec-0be61478-1503-477d-92d8-2510d3a072a5.png")
SHEET = ROOT / "Assets/Nytherion/Art/Combat/VFX/Sprites/SwordEnergy/WhiteHwagon_8F.png"
ANIM_DIR = ROOT / "Assets/Nytherion/Art/Combat/VFX/Animations/WhiteHwagon"
CLIP = ANIM_DIR / "WhiteHwagon_Loop.anim"
CONTROLLER = ANIM_DIR / "WhiteHwagon.controller"
MAT = ROOT / "Assets/Nytherion/Art/Common/Materials/WhiteHwagon_Additive.mat"
PREFAB = ROOT / "Assets/Prefabs/Gameplay/Combat/VFX/WhiteHwagonVFX.prefab"
targets = [SHEET, CLIP, CONTROLLER, MAT, PREFAB]
for path in targets:
    if path.exists():
        raise FileExistsError(path)
ids = {name: uuid.uuid4().hex for name in ["sheet", "folder", "clip", "controller", "material", "prefab"]}
def write(path, text):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(text, encoding="utf-8")
def meta(path, guid, importer, main=None):
    text = f"fileFormatVersion: 2\nguid: {guid}\n{importer}:\n  externalObjects: {{}}\n"
    if main is not None:
        text += f"  mainObjectFileID: {main}\n"
    write(Path(str(path) + ".meta"), text + "  userData: \n  assetBundleName: \n  assetBundleVariant: \n")

width, height = struct.unpack(">II", SOURCE.read_bytes()[16:24])
assert width == height * 2, (width, height)
size = min(width // 4, height // 2)
frames = []
for i in range(8):
    x = round(i % 4 * width / 4)
    top = round(i // 4 * height / 2)
    y = height - top - size
    frames.append(dict(name=f"WhiteHwagon_{i:02}", x=x, y=y, top=top,
                       width=size, height=size, id=21300000+i, spriteId=uuid.uuid4().hex))
assert all(f["x"]+size <= width and f["y"] >= 0 for f in frames)
SHEET.parent.mkdir(parents=True, exist_ok=True)
shutil.copyfile(SOURCE, SHEET)
texture = (ROOT / "Assets/Nytherion/Art/Combat/VFX/Sprites/SwordEnergy/SwordEnergy_Circular_White.png.meta").read_text(encoding="utf-8")
texture = re.sub(r"guid: [0-9a-f]+", "guid: "+ids["sheet"], texture, count=1)
texture = texture.replace("spriteMode: 1", "spriteMode: 2").replace("spritePixelsToUnits: 384", "spritePixelsToUnits: 112")
texture = texture.replace("spriteMeshType: 0", "spriteMeshType: 1")
texture = texture.replace("spriteID: 72cae005dccb4e44817477064e3eedce", "spriteID: "+uuid.uuid4().hex)
entries = []
for f in frames:
    entries.append(f"""    - serializedVersion: 2
      name: {f["name"]}
      rect:
        serializedVersion: 2
        x: {f["x"]}
        y: {f["y"]}
        width: {size}
        height: {size}
      alignment: 0
      pivot: {{x: 0.5, y: 0.5}}
      border: {{x: 0, y: 0, z: 0, w: 0}}
      outline: []
      physicsShape: []
      tessellationDetail: 0
      bones: []
      spriteID: {f["spriteId"]}
      internalID: {f["id"]}
      vertices: []
      indices:
      edges: []
      weights: []""")
texture = texture.replace("    sprites: []", "    sprites:\n" + "\n".join(entries))
texture = texture.replace("    nameFileIdTable: {}", "    nameFileIdTable:\n" + "\n".join(f'      {f["name"]}: {f["id"]}' for f in frames))
write(Path(str(SHEET)+".meta"), texture)

ANIM_DIR.mkdir(parents=True, exist_ok=True)
write(Path(str(ANIM_DIR)+".meta"), f"""fileFormatVersion: 2
guid: {ids["folder"]}
folderAsset: yes
DefaultImporter:
  externalObjects: {{}}
  userData:
  assetBundleName:
  assetBundleVariant:
""")
clip = (ROOT / "Assets/Nytherion/Art/Combat/VFX/Animations/Slash/Idle.anim").read_text(encoding="utf-8")
clip = clip.replace("m_Name: Idle", "m_Name: WhiteHwagon_Loop")
keys = "\n".join(f'    - time: {i/24:.9f}\n      value: {{fileID: {f["id"]}, guid: {ids["sheet"]}, type: 3}}' for i, f in enumerate(frames))
mapping = "\n".join(f'    - {{fileID: {f["id"]}, guid: {ids["sheet"]}, type: 3}}' for f in frames)
clip = re.sub(r"    curve:\n.*?(?=    attribute:)", "    curve:\n"+keys+"\n", clip, flags=re.S)
clip = re.sub(r"    pptrCurveMapping:\n.*?(?=  m_AnimationClipSettings:)", "    pptrCurveMapping:\n"+mapping+"\n", clip, flags=re.S)
clip = clip.replace("m_SampleRate: 12", "m_SampleRate: 24").replace("m_StopTime: 0.4166667", "m_StopTime: 0.33333334").replace("m_LoopTime: 0", "m_LoopTime: 1")
clip = re.sub(r"  m_Events:.*", "  m_Events: []\n", clip, flags=re.S)
write(CLIP, clip)
meta(CLIP, ids["clip"], "NativeFormatImporter", 7400000)

controller = (ROOT / "Assets/Nytherion/Art/Combat/VFX/Animations/Slash/Slash_0.controller").read_text(encoding="utf-8")
controller = controller.replace("m_Name: Slash_0", "m_Name: WhiteHwagon").replace("m_Name: Idle", "m_Name: WhiteHwagon_Loop")
controller = controller.replace("63d7ddcc672377343b9eaac457835fa3", ids["clip"])
controller = controller.replace("m_DefaultWeight: 0", "m_DefaultWeight: 1")
write(CONTROLLER, controller)
meta(CONTROLLER, ids["controller"], "NativeFormatImporter", 9100000)

material = (ROOT / "Assets/Nytherion/Art/Common/Materials/PFX_StraightChainLightning.mat").read_text(encoding="utf-8")
material = "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n" + material[material.index("--- !u!21"):]
material = material.replace("PFX_StraightChainLightning", "WhiteHwagon_Additive")
material = material.replace("4d09dbcf8b1e8f147a418a9c93e460fb", ids["sheet"])
material = material.replace("m_InvalidKeywords:\n  - _FLIPBOOKBLENDING_OFF", "m_InvalidKeywords: []")
material = material.replace("m_Scale: {x: 5, y: 5}", "m_Scale: {x: 1, y: 1}")
material = material.replace("- _Cull: 2", "- _Cull: 0").replace("- _SrcBlendAlpha: 1", "- _SrcBlendAlpha: 0")
material = material.replace("_CameraFadeParams: {r: 0, g: Infinity, b: 0, a: 0}", "_CameraFadeParams: {r: 0, g: 0, b: 0, a: 0}")
write(MAT, material)
meta(MAT, ids["material"], "NativeFormatImporter", 2100000)

prefab = (ROOT / "Assets/Prefabs/Gameplay/Combat/VFX/GustSwingVFX.prefab").read_text(encoding="utf-8")
prefab = prefab[:prefab.index("--- !u!61")]
prefab = prefab.replace("  - component: {fileID: 6409853803765162145}\n", "").replace("  - component: {fileID: 6250345618756432161}\n", "")
prefab = prefab.replace("m_Name: GustSwingVFX", "m_Name: WhiteHwagonVFX").replace("m_IsActive: 0", "m_IsActive: 1")
prefab = prefab.replace("m_LocalRotation: {x: 0, y: 0, z: 0.38268343, w: 0.92387956}", "m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}")
prefab = prefab.replace("m_LocalPosition: {x: 1, y: 1, z: 0}", "m_LocalPosition: {x: 0, y: 0, z: 0}")
prefab = prefab.replace("m_LocalEulerAnglesHint: {x: 0, y: 0, z: 45}", "m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}")
prefab = prefab.replace("a97c105638bdf8b4a8650670310a4cd3", ids["material"]).replace("835cb4d92abc6ab4f987b08b1e867891", ids["controller"])
prefab = prefab.replace("fileID: -1843220968, guid: 63e9297dc48d8844cb2ffad3be4e25c5", f'fileID: {frames[0]["id"]}, guid: {ids["sheet"]}')
prefab = prefab.replace("m_Size: {x: 1.25, y: 2}", f"m_Size: {{x: {size/112:.6f}, y: {size/112:.6f}}}")
write(PREFAB, prefab)
meta(PREFAB, ids["prefab"], "PrefabImporter")
manifest = dict(width=width, height=height, cellSize=size, fps=24, duration=8/24, frames=frames, guids=ids,
                files=[str(p.relative_to(ROOT)).replace("\\", "/") for p in targets])
write(OUT / "manifest.json", json.dumps(manifest, ensure_ascii=False, indent=2))

html = """<!doctype html>
<html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>흰색 화곤 · 8프레임 미리보기</title>
<style>
*{box-sizing:border-box}body{margin:0;background:#12161c;color:#f3f4f6;font:16px system-ui,sans-serif;display:grid;place-items:center;min-height:100vh}
main{max-width:740px;padding:24px;text-align:center}h1{font-size:24px;margin:0 0 8px}p{color:#aeb8c6;line-height:1.6}
canvas{width:min(70vw,420px);height:min(70vw,420px);image-rendering:pixelated;border:1px solid #424b5b;border-radius:16px}
.controls{display:flex;justify-content:center;align-items:center;gap:16px;margin:18px;flex-wrap:wrap}button,select{background:#2c3747;color:white;border:1px solid #718096;padding:8px 16px;border-radius:8px;font:inherit}
input{width:200px}#counter{font-variant-numeric:tabular-nums}a{color:#b8d4fc}small{color:#aeb8c6}
</style><main><h1>흰색 화곤</h1><p>원형 궤적과 내부 회전 잔상 · 8프레임</p>
<canvas id="view" width="512" height="512"></canvas>
<div class="controls"><button id="toggle">일시정지</button><label>속도 <select id="fps"><option>6</option><option>12</option><option selected>24</option><option>48</option></select> FPS</label><span id="counter"></span></div>
<div class="controls"><label>프레임 <input id="frame" type="range" min="0" max="7" step="1" value="0"></label></div>
<p>검정 원본을 가산 합성해 흰 궤적만 표시합니다.<br>Unity에서는 WhiteHwagonVFX 프리팹을 사용하세요.</p>
<small><a href="https://www.youtube.com/watch?v=eQaSGHbRCmI&t=40s">세피리아 대력곤 참고 장면</a> · 형태를 참고해 새로 생성한 이펙트</small>
<script>
const frames=__FRAMES__,image=new Image(),canvas=document.getElementById('view'),ctx=canvas.getContext('2d');
let running=true,fps=24,current=0,last=0,elapsed=0;
const slider=document.getElementById('frame'),counter=document.getElementById('counter'),button=document.getElementById('toggle');
function draw(){
ctx.globalCompositeOperation='source-over';ctx.fillStyle='#263449';ctx.fillRect(0,0,512,512);
ctx.strokeStyle='#33445c';ctx.lineWidth=1;
for(let i=0;i<=512;i+=32){ctx.beginPath();ctx.moveTo(i,0);ctx.lineTo(i,512);ctx.stroke();ctx.beginPath();ctx.moveTo(0,i);ctx.lineTo(512,i);ctx.stroke();}
ctx.globalCompositeOperation='lighter';ctx.imageSmoothingEnabled=false;
const f=frames[current];ctx.drawImage(image,f.x,f.top,f.width,f.height,0,0,512,512);
ctx.globalCompositeOperation='source-over';counter.textContent=(current+1)+' / 8';slider.value=current;
}
function tick(t){const dt=last?Math.min(t-last,100):0;last=t;if(running){elapsed+=dt;while(elapsed>=1000/fps){elapsed-=1000/fps;current=(current+1)%8;}}draw();requestAnimationFrame(tick);}
button.onclick=()=>{running=!running;button.textContent=running?'일시정지':'재생';elapsed=0};
document.getElementById('fps').onchange=e=>{fps=Number(e.target.value);elapsed=0};
slider.oninput=e=>{running=false;button.textContent='재생';current=Number(e.target.value);draw();};
image.onload=()=>requestAnimationFrame(tick);image.src='data:image/png;base64,__IMAGE__';
</script></main></html>"""
html = html.replace("__FRAMES__", json.dumps(frames)).replace("__IMAGE__", base64.b64encode(SOURCE.read_bytes()).decode())
write(OUT / "preview.html", html)
print(json.dumps(manifest, ensure_ascii=False, indent=2))

