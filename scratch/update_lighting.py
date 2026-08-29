import re

scene_path = r"c:\Users\julio\.gemini\antigravity\scratch\ClawMachine\Assets\Scenes\SampleScene.unity"

with open(scene_path, 'r', encoding='utf-8') as f:
    content = f.read()

# 1. Update Directional Light (GameObject 410087039 / Light 410087040)
# Find Light 410087040 block
pattern = r'(!\s*u!108 &410087040[\s\S]*?m_Intensity:\s*)([\d\.]+)([\s\S]*?m_Shadows:[\s\S]*?m_Type:\s*)(\d+)([\s\S]*?m_Lightmapping:\s*)(\d+)'

def repl(match):
    prefix = match.group(1)
    intensity = "1.2" # crisp key light
    mid1 = match.group(3)
    shadow_type = "2" # soft shadows
    mid2 = match.group(5)
    lightmapping = "1" # Mixed mode
    return f"{prefix}{intensity}{mid1}{shadow_type}{mid2}{lightmapping}"

new_content = re.sub(pattern, repl, content)
if new_content != content:
    print("Directional Light successfully updated to Mixed + Soft Shadows (Type 2)!")
    content = new_content
else:
    print("WARNING: Directional Light block pattern did not match!")

# 2. Boost Spotlight_Arcade lights (increase intensity from 4.5 to 9.5 and ensure warm rich color)
# Search for all Light components attached to Spotlight_Arcade
spotlight_pattern = r'(m_Name: Spotlight_Arcade[\s\S]*?--- !u!108 &\d+[\s\S]*?m_Intensity:\s*)([\d\.]+)'
count = 0
def spot_repl(match):
    global count
    count += 1
    return f"{match.group(1)}9.5"

new_content = re.sub(spotlight_pattern, spot_repl, content)
print(f"Updated {count} Spotlight_Arcade intensity to 9.5")
content = new_content

with open(scene_path, 'w', encoding='utf-8') as f:
    f.write(content)

print("Scene file saved successfully.")
