from pathlib import Path
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parent.parent

S = 1024
img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
d = ImageDraw.Draw(img)

# Rounded application tile.
d.rounded_rectangle((44, 44, S - 44, S - 44), radius=220,
                    fill=(23, 26, 33, 255), outline=(58, 64, 78, 255), width=16)

# Battery body and terminal.
d.rounded_rectangle((175, 342, 790, 690), radius=48,
                    fill=(34, 38, 47, 255), outline=(242, 242, 242, 255), width=26)
d.rounded_rectangle((790, 438, 878, 600), radius=18, fill=(242, 242, 242, 255))

# 80% green fill.
d.rounded_rectangle((220, 388, 700, 646), radius=26, fill=(48, 209, 88, 255))
d.rounded_rectangle((236, 402, 684, 430), radius=14, fill=(125, 255, 160, 190))

# Lightning bolt.
bolt = [(565, 300), (365, 566), (500, 566), (448, 742), (678, 452), (533, 452)]
d.polygon(bolt, fill=(255, 214, 10, 255), outline=(23, 26, 33, 255), width=18)

img.save(ROOT / "app.ico", sizes=[(16, 16), (24, 24), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)])
(ROOT / "preview").mkdir(exist_ok=True)
img.resize((256, 256), Image.LANCZOS).save(ROOT / "preview" / "app-icon-256.png")
print("wrote app.ico")
