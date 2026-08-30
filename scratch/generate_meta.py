import uuid
import os

def make_meta(path, border_x=24, border_y=24, border_z=24, border_w=24):
    g = uuid.uuid4().hex
    content = f"""fileFormatVersion: 2
guid: {g}
TextureImporter:
  internalIDToNameTable: []
  externalObjects: {{}}
  serializedVersion: 13
  mipmaps:
    mipMapMode: 0
    enableMipMap: 0
    sRGBTexture: 1
    linearTexture: 0
    fadeOut: 0
    borderMipMap: 0
    mipMapsPreserveCoverage: 0
    alphaTestReferenceValue: 0.5
    mipMapFadeDistanceStart: 1
    mipMapFadeDistanceEnd: 3
  bumpmap:
    convertToNormalMap: 0
    externalNormalMap: 0
    heightScale: 0.25
    normalMapFilter: 0
    flipGreenChannel: 0
  isReadable: 0
  streamingMipmaps: 0
  streamingMipmapsPriority: 0
  vTOnly: 0
  ignoreMipmapLimit: 0
  grayScaleToAlpha: 0
  generateCubemap: 6
  cubemapConvolution: 0
  seamlessCubemap: 0
  textureFormat: 1
  maxTextureSize: 2048
  textureSettings:
    serializedVersion: 2
    filterMode: 1
    aniso: 1
    mipBias: 0
    wrapU: 0
    wrapV: 0
    wrapW: 0
  nPOTScale: 0
  lightmap: 0
  compressionQuality: 50
  spriteMode: 1
  spriteExtrude: 1
  spriteMeshType: 0
  alignment: 0
  spritePivot: {{x: 0.5, y: 0.5}}
  spritePixelsToUnits: 100
  spriteBorder: {{x: {border_x}, y: {border_y}, z: {border_z}, w: {border_w}}}
  spriteGenerateFallbackPhysicsShape: 1
  alphaUsage: 1
  alphaIsTransparency: 1
  spriteTessellationDetail: -1
  textureType: 8
  textureShape: 1
  singleChannelComponent: 0
  flipbookRows: 1
  flipbookColumns: 1
  maxTextureSizeSet: 0
  compressionQualitySet: 0
  textureFormatSet: 0
  ignorePngGamma: 0
  applyGammaDecoding: 0
  swizzle: 50462976
  cookieLightType: 0
  platformSettings: []
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""
    with open(path + '.meta', 'w') as f:
        f.write(content)

base = r'C:\Users\julio\.gemini\antigravity\scratch\ClawMachine\Assets\Resources\UI'
make_meta(os.path.join(base, 'btn_emerald_3d.png'), 24, 24, 24, 24)
make_meta(os.path.join(base, 'btn_sapphire_3d.png'), 24, 24, 24, 24)
make_meta(os.path.join(base, 'btn_gold_3d.png'), 24, 24, 24, 24)
make_meta(os.path.join(base, 'btn_sanwa_red_3d.png'), 0, 0, 0, 0)
make_meta(os.path.join(base, 'panel_card_3d.png'), 24, 24, 24, 24)
make_meta(os.path.join(base, 'slot_pedestal_3d.png'), 20, 20, 20, 20)
print('Meta files created successfully!')
