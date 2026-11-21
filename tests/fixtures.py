from pathlib import Path
import io, struct, hashlib, json
import pycdlib
from PIL import Image

root=Path(__file__).resolve().parent
fixtures=root/'fixtures'
fixtures.mkdir(exist_ok=True)
def png(size,color):
    buf=io.BytesIO();Image.new('RGB',size,color).save(buf,format='PNG');return buf.getvalue()
icon=png((144,80),'#6595b9'); background=png((480,272),'#182b42')
def iso(name, missing=False, ps1=False, ps2=False):
    image=pycdlib.PyCdlib();image.new(interchange_level=3,vol_ident='CHDCONVERTER_TEST')
    entries={}
    if ps1 or ps2:
        entries['/SYSTEM.CNF;1']=(b'BOOT2 = cdrom0:\\SLUS_000.01;1\r\n' if ps2 else b'BOOT = cdrom:\\SCUS_941.63;1\r\nTCB = 4\r\nEVENT = 16\r\nSTACK = 801fff00\r\n')
        entries['/SCUS_941.63;1']=b'PS-X EXE'+bytes(16384)
    else:
        image.add_directory(iso_path='/PSP_GAME');image.add_directory(iso_path='/PSP_GAME/USRDIR')
        entries['/PSP_GAME/PARAM.SFO;1']=b'\x00PSF'+bytes(256)
        entries['/PSP_GAME/ICON0.PNG;1']=icon
        if not missing:entries['/PSP_GAME/PIC1.PNG;1']=background
        entries['/PSP_GAME/USRDIR/GAME.BIN;1']=bytes(range(256))*512
    streams=[]
    for path,data in entries.items():
        buf=io.BytesIO(data);streams.append(buf);image.add_fp(buf,len(data),iso_path=path)
    image.write(str(fixtures/name));image.close()
    return entries
iso('psp.iso');iso('psp-missing.iso',missing=True);iso('ps1.iso',ps1=True);iso('ps2-small.iso',ps2=True)
payload=(fixtures/'ps1.iso').read_bytes()
with (fixtures/'Test Game (CD1).bin').open('wb') as f:
    for offset in range(0,len(payload),2048):
        f.write(b'\x00'+b'\xff'*10+b'\x00'+b'\x00\x02\x00\x02'+bytes(8)+payload[offset:offset+2048]+bytes(280))
(fixtures/'Test Game (CD2).bin').write_bytes((fixtures/'Test Game (CD1).bin').read_bytes())
(fixtures/'audio.bin').write_bytes(bytes(range(256))* (2352*75//256)+bytes((2352*75)%256))
(fixtures/'multitrack.cue').write_text('FILE "Test Game (CD1).bin" BINARY\n  TRACK 01 MODE2/2352\n    INDEX 01 00:00:00\nFILE "audio.bin" BINARY\n  TRACK 02 AUDIO\n    INDEX 01 00:00:00\n',encoding='utf-8')
parts=[b'\x00PSF'+bytes(31),icon,b'ANIMATION',b'PIC0-KEEP',background,b'AUDIO-KEEP',b'DATA-PSP-KEEP'*100,b'DATA-PSAR-KEEP'*10000]
offsets=[];position=40
for part in parts:offsets.append(position);position+=len(part)
(fixtures/'source.pbp').write_bytes(b'\x00PBP'+struct.pack('<I8I',0x10000,*offsets)+b''.join(parts))
(fixtures/'bad.pbp').write_bytes(b'\x00PBP'+struct.pack('<I8I',0x10000,*([999999]*8)))
print('Generated synthetic ISO, BIN/CUE and PBP fixtures.')
