"""Rebuild the app icon from the same vector geometry as brand.svg. Pillow only."""
from PIL import Image, ImageDraw
from pathlib import Path
import math
root=Path(__file__).resolve().parent.parent
scale=4
im=Image.new('RGBA',(512*scale,512*scale))
d=ImageDraw.Draw(im)
def points(values):return [(round(x*scale),round(y*scale)) for x,y in values]
def stroke(values):
 p=points(values);d.line(p,fill='white',width=42*scale,joint='curve')
 for x,y in (p[0],p[-1]):d.ellipse((x-21*scale,y-21*scale,x+21*scale,y+21*scale),fill='white')
d.rounded_rectangle((0,0,512*scale-1,512*scale-1),radius=100*scale,fill='#0D514D')
# Cubic Bezier samples match the SVG bowl.
curve=[]
for i in range(81):
 t=i/80;u=1-t
 curve.append((u**3*266+3*u*u*t*346+3*u*t*t*346+t**3*266,u**3*166+3*u*u*t*166+3*u*t*t*270+t**3*270))
stroke([(168,366),(168,166),(266,166)]+curve+[(168,270)])
stroke([(258,270),(344,366)])
d.ellipse((349*scale,111*scale,399*scale,161*scale),fill='#78E7C1')
im=im.resize((512,512),Image.Resampling.LANCZOS)
im.save(root/'assets/brand.png')
im.save(root/'native/Client/Assets/Brand.ico',sizes=[(n,n)for n in(16,24,32,48,64,128,256)])
