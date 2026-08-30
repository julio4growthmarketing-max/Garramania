import math
import os
from PIL import Image, ImageDraw, ImageFilter

out_dir = r"C:\Users\julio\.gemini\antigravity\scratch\ClawMachine\Assets\Resources\UI"
os.makedirs(out_dir, exist_ok=True)

def create_rounded_rect_mask(w, h, r):
    scale = 4
    sw, sh, sr = w * scale, h * scale, r * scale
    mask = Image.new("L", (sw, sh), 0)
    draw = ImageDraw.Draw(mask)
    draw.rounded_rectangle([0, 0, sw - 1, sh - 1], radius=sr, fill=255)
    return mask.resize((w, h), Image.Resampling.LANCZOS)

def make_3d_button(w, h, r, top_col, mid_col, bot_col, shadow_col, border_col, gloss=True):
    # Oversampling for anti-aliasing
    scale = 2
    sw, sh, sr = w * scale, h * scale, r * scale
    img = Image.new("RGBA", (sw, sh), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)

    depth = 6 * scale
    
    # 1. Base shadow / depth layer (relevo 3D de base)
    draw.rounded_rectangle([0, depth, sw - 1, sh - 1], radius=sr, fill=shadow_col)
    
    # 2. Main button body (raised)
    body_h = sh - depth
    for y in range(body_h):
        t = y / max(1, body_h - 1)
        # Smooth cubic gradient
        if t < 0.5:
            # top to mid
            t2 = t * 2.0
            r_c = int(top_col[0] * (1 - t2) + mid_col[0] * t2)
            g_c = int(top_col[1] * (1 - t2) + mid_col[1] * t2)
            b_c = int(top_col[2] * (1 - t2) + mid_col[2] * t2)
            a_c = int(top_col[3] * (1 - t2) + mid_col[3] * t2)
        else:
            # mid to bot
            t2 = (t - 0.5) * 2.0
            r_c = int(mid_col[0] * (1 - t2) + bot_col[0] * t2)
            g_c = int(mid_col[1] * (1 - t2) + bot_col[1] * t2)
            b_c = int(mid_col[2] * (1 - t2) + bot_col[2] * t2)
            a_c = int(mid_col[3] * (1 - t2) + bot_col[3] * t2)

        draw.line([(sr // 2, y), (sw - 1 - sr // 2, y)], fill=(r_c, g_c, b_c, a_c))

    # Mask the body to rounded rectangle
    body_mask = Image.new("L", (sw, body_h), 0)
    b_draw = ImageDraw.Draw(body_mask)
    b_draw.rounded_rectangle([0, 0, sw - 1, body_h - 1], radius=sr, fill=255)
    
    # Draw colored body through mask
    body_img = Image.new("RGBA", (sw, body_h), (0, 0, 0, 0))
    body_draw = ImageDraw.Draw(body_img)
    for y in range(body_h):
        t = y / max(1, body_h - 1)
        if t < 0.5:
            t2 = t * 2.0
            col = tuple(int(top_col[i] * (1 - t2) + mid_col[i] * t2) for i in range(4))
        else:
            t2 = (t - 0.5) * 2.0
            col = tuple(int(mid_col[i] * (1 - t2) + bot_col[i] * t2) for i in range(4))
        body_draw.line([(0, y), (sw - 1, y)], fill=col)

    # Gloss overlay
    if gloss:
        gloss_h = int(body_h * 0.45)
        gloss_img = Image.new("RGBA", (sw, gloss_h), (0, 0, 0, 0))
        g_draw = ImageDraw.Draw(gloss_img)
        for y in range(gloss_h):
            alpha = int(120 * (1.0 - (y / gloss_h) ** 1.4))
            g_draw.line([(0, y), (sw - 1, y)], fill=(255, 255, 255, alpha))
        
        # Apply arc to gloss
        gloss_mask = Image.new("L", (sw, gloss_h), 0)
        gm_draw = ImageDraw.Draw(gloss_mask)
        gm_draw.rounded_rectangle([0, 0, sw - 1, body_h - 1], radius=sr, fill=255)
        body_img.paste(gloss_img, (0, 0), gloss_mask)

    # Bevel outline
    body_draw.rounded_rectangle([0, 0, sw - 1, body_h - 1], radius=sr, outline=border_col, width=2 * scale)

    # Combine
    img.paste(body_img, (0, 0), body_mask)
    return img.resize((w, h), Image.Resampling.LANCZOS)

# 1. Emerald Button (Play / Primary)
btn_emerald = make_3d_button(
    w=160, h=80, r=24,
    top_col=(52, 211, 153, 255),    # #34D399 highlight
    mid_col=(16, 185, 129, 255),    # #10B981 emerald
    bot_col=(5, 150, 105, 255),     # #059669 deep
    shadow_col=(4, 120, 87, 255),   # #047857 3d bevel
    border_col=(110, 231, 183, 220) # border highlight
)
btn_emerald.save(os.path.join(out_dir, "btn_emerald_3d.png"))

# 2. Sapphire / Obsidian Button (Secondary / Album)
btn_sapphire = make_3d_button(
    w=160, h=80, r=24,
    top_col=(30, 41, 59, 255),      # #1E293B slate
    mid_col=(15, 23, 42, 255),      # #0F172A
    bot_col=(11, 19, 43, 255),      # #0B132B
    shadow_col=(2, 6, 23, 255),     # #020617
    border_col=(56, 189, 248, 180)  # #38BDF8 cyan glow border
)
btn_sapphire.save(os.path.join(out_dir, "btn_sapphire_3d.png"))

# 3. Gold 24k Button (Daily / Golden Token)
btn_gold = make_3d_button(
    w=160, h=80, r=24,
    top_col=(253, 224, 71, 255),    # #FDE047
    mid_col=(245, 158, 11, 255),    # #F59E0B
    bot_col=(217, 119, 6, 255),     # #D97706
    shadow_col=(180, 83, 9, 255),   # #B45309
    border_col=(254, 240, 138, 240) # highlight
)
btn_gold.save(os.path.join(out_dir, "btn_gold_3d.png"))

# 4. Sanwa Big Red Arcade Button (Circular 3D Dome)
def make_sanwa_button(size):
    scale = 2
    s = size * scale
    img = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)

    r = s // 2
    cx, cy = r, r

    # Outer chrome bezel (radial gradient)
    for i in range(r, r - 12 * scale, -1):
        t = (r - i) / (12 * scale)
        val = int(80 + t * 90)
        draw.ellipse([cx - i, cy - i, cx + i - 1, cy + i - 1], fill=(val, val + 5, val + 15, 255))

    # Dark inner bezel groove
    groove_r = r - 12 * scale
    draw.ellipse([cx - groove_r, cy - groove_r, cx + groove_r - 1, cy + groove_r - 1], fill=(15, 20, 30, 255))

    # Cherry red dome (shifted highlight towards top-left)
    dome_r = groove_r - 4 * scale
    for i in range(dome_r, 0, -1):
        t = (dome_r - i) / dome_r
        # Sphere shading: top is #F43F5E, center #E11D48, base #9F1239
        r_c = int(244 * (1 - t) + 159 * t)
        g_c = int(63 * (1 - t) + 18 * t)
        b_c = int(94 * (1 - t) + 57 * t)
        draw.ellipse([cx - i, cy - i + int(t * 3 * scale), cx + i - 1, cy + i - 1 + int(t * 3 * scale)], fill=(r_c, g_c, b_c, 255))

    # Specular gloss highlight (curved capsule on top half)
    gloss_img = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    g_draw = ImageDraw.Draw(gloss_img)
    gw, gh = int(dome_r * 1.3), int(dome_r * 0.55)
    gx1, gy1 = cx - gw // 2, cy - int(dome_r * 0.75)
    g_draw.ellipse([gx1, gy1, gx1 + gw, gy1 + gh], fill=(255, 255, 255, 160))
    gloss_img = gloss_img.filter(ImageFilter.GaussianBlur(radius=4 * scale))
    img.alpha_composite(gloss_img)

    return img.resize((size, size), Image.Resampling.LANCZOS)

sanwa_btn = make_sanwa_button(160)
sanwa_btn.save(os.path.join(out_dir, "btn_sanwa_red_3d.png"))

# 5. Frosted Glass Card Panel (9-Slice Modal Sheet)
def make_glass_panel(w, h, r):
    scale = 2
    sw, sh, sr = w * scale, h * scale, r * scale
    img = Image.new("RGBA", (sw, sh), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)

    # Base frosted dark sapphire
    draw.rounded_rectangle([0, 0, sw - 1, sh - 1], radius=sr, fill=(11, 19, 43, 245))

    # Top light accent
    for y in range(sr):
        alpha = int(45 * (1.0 - (y / sr)))
        draw.line([(sr, y), (sw - 1 - sr, y)], fill=(56, 189, 248, alpha))

    # Border glow
    draw.rounded_rectangle([0, 0, sw - 1, sh - 1], radius=sr, outline=(56, 189, 248, 70), width=2 * scale)
    return img.resize((w, h), Image.Resampling.LANCZOS)

panel_card = make_glass_panel(128, 128, 24)
panel_card.save(os.path.join(out_dir, "panel_card_3d.png"))

# 6. Slot Pedestal for Album (Recessed 3D Well)
def make_pedestal_slot(w, h, r):
    scale = 2
    sw, sh, sr = w * scale, h * scale, r * scale
    img = Image.new("RGBA", (sw, sh), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)

    # Outer border
    draw.rounded_rectangle([0, 0, sw - 1, sh - 1], radius=sr, fill=(15, 23, 42, 240))
    # Inner inset shadow (recessed)
    draw.rounded_rectangle([2 * scale, 2 * scale, sw - 1 - 2 * scale, sh - 1 - 2 * scale], radius=sr - 2 * scale, fill=(8, 14, 28, 255))
    draw.rounded_rectangle([0, 0, sw - 1, sh - 1], radius=sr, outline=(71, 85, 105, 120), width=2 * scale)
    return img.resize((w, h), Image.Resampling.LANCZOS)

slot_pedestal = make_pedestal_slot(128, 128, 20)
slot_pedestal.save(os.path.join(out_dir, "slot_pedestal_3d.png"))

print("UI Kit generation complete! 6 high-res 3D sprites created in", out_dir)
