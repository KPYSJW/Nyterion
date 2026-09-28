from pathlib import Path
import cv2
import numpy as np

OUT = Path(__file__).parent
VIDEOS = {
    'air': Path(r'C:\Users\kyung\Videos\Bandicam\bandicam 2026-09-21 13-35-59-661.mp4'),
    'mixed': Path(r'C:\Users\kyung\Videos\Bandicam\bandicam 2026-09-18 13-15-54-909.mp4'),
}

def frame_at(name, number):
    cap = cv2.VideoCapture(str(VIDEOS[name]))
    cap.set(cv2.CAP_PROP_POS_FRAMES, number)
    ok, frame = cap.read()
    cap.release()
    if not ok:
        raise RuntimeError((name, number))
    return frame

def sheet(name, numbers, crop, filename, columns=4, scale=1):
    x, y, w, h = crop
    cells = []
    for number in numbers:
        frame = frame_at(name, number)[y:y+h, x:x+w]
        frame = cv2.resize(frame, (w*scale, h*scale), interpolation=cv2.INTER_NEAREST)
        cell = np.zeros((h*scale+28, w*scale, 3), dtype=np.uint8)
        cell[28:] = frame
        cv2.putText(cell, f'{name} frame {number}  {number/30:.3f}s', (8,20),
                    cv2.FONT_HERSHEY_SIMPLEX, .5, (255,255,255), 1, cv2.LINE_AA)
        cells.append(cell)
    rows = (len(cells)+columns-1)//columns
    canvas = np.zeros((rows*cells[0].shape[0], columns*cells[0].shape[1],3), dtype=np.uint8)
    for i, cell in enumerate(cells):
        top = i//columns*cell.shape[0]
        left = i%columns*cell.shape[1]
        canvas[top:top+cell.shape[0],left:left+cell.shape[1]] = cell
    cv2.imwrite(str(OUT/filename), canvas)

def slow_clip(name, first, last, crop, filename):
    x,y,w,h = crop
    cap = cv2.VideoCapture(str(VIDEOS[name]))
    cap.set(cv2.CAP_PROP_POS_FRAMES, first)
    writer = cv2.VideoWriter(str(OUT/filename), cv2.VideoWriter_fourcc(*'mp4v'), 7.5, (w,h+28))
    for number in range(first,last+1):
        ok, frame = cap.read()
        if not ok:
            break
        cell = np.zeros((h+28,w,3),dtype=np.uint8)
        cell[28:] = frame[y:y+h,x:x+w]
        cv2.putText(cell,f'{name} {number/30:.3f}s  0.25x  original frames', (8,20),
                    cv2.FONT_HERSHEY_SIMPLEX,.48,(255,255,255),1,cv2.LINE_AA)
        writer.write(cell)
    cap.release()
    writer.release()

if __name__ == '__main__':
    for name, path in VIDEOS.items():
        cap = cv2.VideoCapture(str(path))
        print(name, 'fps', cap.get(cv2.CAP_PROP_FPS), 'frames',cap.get(cv2.CAP_PROP_FRAME_COUNT))
        cap.release()
    cv2.imwrite(str(OUT/'air_full.png'), frame_at('air', 24))
    cv2.imwrite(str(OUT/'mixed_full.png'), frame_at('mixed', 150))
    sheet('air', list(range(18,30)), (920,400,400,260), 'air_060_097.png', 3)
    sheet('air', list(range(30,42)), (920,280,400,360), 'air_100_137.png', 3)
    sheet('air', list(range(3,15)), (920,440,420,220), 'air_010_047.png', 3)
    sheet('mixed', list(range(51,63)), (920,490,240,380), 'mixed_air_170_207.png', 4)
    sheet('mixed', list(range(144,156)), (940,380,320,200), 'hit_480_517.png', 3)
    sheet('air', [24], (975,445,325,140), 'air_detail_080.png', 1, 3)
    sheet('air', list(range(210,222)), (900,300,440,290), 'air_700_737.png', 3)
    sheet('air', list(range(270,282)), (800,450,450,330), 'air_900_937.png', 3)
    sheet('mixed', [51,53,55,57,59,61], (825,495,265,345), 'mixed_air_wide.png', 3)
    sheet('air', [20,22,24,26], (920,410,400,210), 'air_evidence.png', 2)
    sheet('mixed', [144,145,149,153], (940,380,320,200), 'hit_evidence.png', 2)
    slow_clip('air', 3, 41, (900,260,440,440), 'air_slow_025x.mp4')
    slow_clip('mixed', 141, 165, (900,260,420,380), 'hit_slow_025x.mp4')
