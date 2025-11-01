# Визуальная архитектура фронтенда F1 2012 Career Mode

## Файловая структура

```
╔════════════════════════════════════════════════════════════════╗
║                      F1 2012 Career Frontend                   ║
╚════════════════════════════════════════════════════════════════╝
                              │
                ┌─────────────┴─────────────┐
                │                           │
        ┌───────▼───────┐           ┌───────▼───────┐
        │ b_career.pssg │           │ d_career.pssg │
        │   (108 KB)    │           │    (62 MB)    │
        └───────────────┘           └───────────────┘
                │                           │
     ┌──────────┴────────┐       ┌─────────┴──────────┐
     │ TEXTURE LIBRARY   │       │  COMPLETE 3D SCENE │
     │  ═══════════════  │       │  ═══════════════   │
     │ • 24 Textures     │       │ • Shaders          │
     │ • DXT3/DXT5       │       │ • Materials ───┐   │
     │ • Team logos      │◄──────┤ • Geometry     │   │
     │ • UI elements     │  ID   │ • Scene Graph  │   │
     │ • Indicators      │ refs  │ • Animations   │   │
     └───────────────────┘       └─────────┬──────┴───┘
                                           │
                                    ┌──────▼──────┐
                                    │  Codemasters │
                                    │  EGO Engine  │
                                    │  (Runtime)   │
                                    └──────┬──────┘
                                           │
                                    ┌──────▼──────┐
                                    │   Screen    │
                                    │  ┌───────┐  │
                                    │  │ Menu  │  │
                                    │  └───────┘  │
                                    └─────────────┘
```

## Структура b_career.pssg (Текстурная библиотека)

```
b_career.pssg
│
└─ PSSGDATABASE (root)
   └─ LIBRARY (type="YYY")
      │
      ├─ TEXTURE: l_caterham_dc (256×128, DXT5)
      │  └─ TEXTUREIMAGEBLOCK
      │     └─ TEXTUREIMAGEBLOCKDATA [32,768 bytes]
      │
      ├─ TEXTURE: l_redbull_dc (256×128, DXT5)
      │  └─ TEXTUREIMAGEBLOCK
      │     └─ TEXTUREIMAGEBLOCKDATA [32,768 bytes]
      │
      ├─ TEXTURE: rival_select (512×256, DXT5)
      │  └─ TEXTUREIMAGEBLOCK
      │     └─ TEXTUREIMAGEBLOCKDATA [131,072 bytes]
      │
      ├─ TEXTURE: career_long (256×256, DXT3)
      │  └─ TEXTUREIMAGEBLOCK
      │     └─ TEXTUREIMAGEBLOCKDATA [65,536 bytes]
      │
      ├─ TEXTURE: led_green_0 (32×32, DXT3)
      │  └─ TEXTUREIMAGEBLOCK
      │     └─ TEXTUREIMAGEBLOCKDATA [1,024 bytes]
      │
      ├─ TEXTURE: led_green_1 (32×32, DXT3)
      │  └─ ... (animated states)
      │
      ├─ TEXTURE: led_red_0 (32×32, DXT3)
      ├─ TEXTURE: led_red_1 (32×32, DXT3)
      ├─ TEXTURE: led_off (32×32, DXT3)
      │
      ├─ TEXTURE: difficulty_easy (256×256, DXT3)
      ├─ TEXTURE: difficulty_normal (256×256, DXT3)
      ├─ TEXTURE: difficulty_hard (256×256, DXT3)
      │
      ├─ TEXTURE: career_short (256×256, DXT3)
      ├─ TEXTURE: career_ydt (256×256, DXT3)
      │
      └─ TEXTURE: (11 team logos) [256×128 each]
         ├─ l_ferrari_dc
         ├─ l_mercedes_dc
         ├─ l_redbull_dc
         ├─ l_mclaren_dc
         ├─ l_lotus_dc
         ├─ l_williams_dc
         ├─ l_sauber_dc
         ├─ l_tororosso_dc
         ├─ l_forceindia_dc
         ├─ l_caterham_dc
         ├─ l_marussia_dc
         └─ l_hrt_dc
```

## Структура d_career.pssg (Основная сцена)

```
d_career.pssg (62 MB)
│
└─ PSSGDATABASE (root)
   │
   ├─ LIBRARY (type="SHADER")
   │  └─ SHADERGROUP
   │     ├─ SHADER: ui_vertex_shader
   │     │  └─ [HLSL bytecode]
   │     │
   │     └─ SHADER: ui_pixel_shader
   │        └─ [HLSL bytecode]
   │
   ├─ LIBRARY (type="MATERIAL")
   │  │
   │  ├─ MATERIAL: button_material
   │  │  ├─ shaderId: "ui_shader"
   │  │  ├─ MATERIALPARAMETER: DiffuseColor [1,1,1,1]
   │  │  └─ TEXTURECONST
   │  │     └─ textureId: "career_long" ──► (from b_career)
   │  │
   │  ├─ MATERIAL: team_logo_material
   │  │  ├─ shaderId: "ui_shader"
   │  │  └─ TEXTURECONST
   │  │     └─ textureId: "l_ferrari_dc" ──► (from b_career)
   │  │
   │  └─ MATERIAL: led_indicator_material
   │     ├─ MATERIALPARAMETER: EmissiveColor [0,1,0,1]
   │     └─ TEXTURECONST
   │        └─ textureId: "led_green_1" ──► (from b_career)
   │
   ├─ LIBRARY (type="RENDERSTREAM")
   │  └─ RENDERSTREAM: ui_geometry_stream
   │     └─ DATABLOCKSTREAM
   │        └─ DATABLOCKDATA
   │           ├─ [Vertex positions: X,Y,Z]
   │           ├─ [UV coordinates: U,V]
   │           ├─ [Normals: NX,NY,NZ]
   │           └─ [Indices: triangle list]
   │
   ├─ LIBRARY (type="MATRIXPALETTE")
   │  └─ MATRIXPALETTE: ui_animations
   │     └─ [Animation matrices]
   │
   └─ RENDERDATASOURCE (scene graph)
      └─ DATABLOCKSTREAM
         │
         └─ NODE: career_menu_root
            ├─ id: "career_menu"
            │
            ├─ TRANSFORM
            │  └─ [4×4 matrix: position=(0,0,0)]
            │
            ├─ BOUNDINGBOX
            │  └─ [min=(-100,-100,0), max=(100,100,10)]
            │
            ├─ NODE: background_panel
            │  ├─ TRANSFORM [position=(0,0,-1)]
            │  └─ RENDERNODE
            │     └─ MESH
            │        ├─ material: "panel_material"
            │        ├─ vertexCount: 4
            │        ├─ indexCount: 6
            │        └─ streamOffset: 0x1000
            │
            ├─ NODE: start_button
            │  ├─ TRANSFORM [position=(-50,20,0)]
            │  ├─ BOUNDINGBOX [...]
            │  └─ RENDERNODE
            │     └─ MESH
            │        ├─ material: "button_material"
            │        │   └─ texture: "career_long"
            │        ├─ primitiveType: TRIANGLES
            │        └─ streamOffset: 0x2000
            │
            ├─ NODE: team_selection_grid
            │  ├─ TRANSFORM [position=(0,-50,0)]
            │  │
            │  ├─ NODE: ferrari_slot
            │  │  ├─ TRANSFORM [position=(-80,0,0)]
            │  │  └─ RENDERNODE
            │  │     └─ MESH
            │  │        └─ material: (texture: "l_ferrari_dc")
            │  │
            │  ├─ NODE: redbull_slot
            │  │  ├─ TRANSFORM [position=(-40,0,0)]
            │  │  └─ RENDERNODE
            │  │     └─ MESH
            │  │        └─ material: (texture: "l_redbull_dc")
            │  │
            │  └─ NODE: mercedes_slot
            │     ├─ TRANSFORM [position=(0,0,0)]
            │     └─ RENDERNODE
            │        └─ MESH
            │           └─ material: (texture: "l_mercedes_dc")
            │     ... [8 more team slots]
            │
            ├─ NODE: difficulty_selector
            │  ├─ TRANSFORM [position=(60,30,0)]
            │  │
            │  ├─ NODE: easy_button
            │  │  ├─ RENDERNODE
            │  │  │  └─ MESH
            │  │  │     └─ material: (texture: "difficulty_easy")
            │  │  │
            │  │  └─ NODE: led_indicator
            │  │     └─ RENDERNODE
            │  │        └─ MESH
            │  │           └─ material: (texture: "led_green_1")
            │  │
            │  ├─ NODE: normal_button
            │  │  └─ ... (texture: "difficulty_normal")
            │  │
            │  └─ NODE: hard_button
            │     └─ ... (texture: "difficulty_hard")
            │
            └─ NODE: rival_selection_overlay
               ├─ TRANSFORM [position=(0,0,5)]
               └─ RENDERNODE
                  └─ MESH
                     └─ material: (texture: "rival_select")
```

## Рендеринг Pipeline

```
┌─────────────────────────────────────────────────────────────────┐
│                         LOADING PHASE                           │
└─────────────────────────────────────────────────────────────────┘
                              │
        ┌─────────────────────┴─────────────────────┐
        │                                           │
   ┌────▼────┐                                 ┌────▼────┐
   │ Load    │                                 │ Load    │
   │ b_career│                                 │ d_career│
   └────┬────┘                                 └────┬────┘
        │                                           │
        ├─► Parse TEXTURE nodes                    ├─► Parse SHADER
        ├─► Upload to GPU VRAM                     ├─► Compile shaders
        │   • Create texture handles               │   • Create shader programs
        │   • Generate mipmaps                     │
        │                                          ├─► Parse MATERIAL
        └─► Build texture map:                     │   • Link texture IDs
            ["l_ferrari_dc" → GPU_TEX_0]           │   • Set parameters
            ["career_long"  → GPU_TEX_1]           │
            [...]                                  ├─► Parse RENDERSTREAM
                                                   │   • Upload vertex buffers
                                                   │   • Upload index buffers
                                                   │
                                                   └─► Build scene graph:
                                                       • Hierarchy of NODEs
                                                       • Precompute transforms
                                                       • Setup bounding boxes

┌─────────────────────────────────────────────────────────────────┐
│                        RENDER LOOP (60 FPS)                     │
└─────────────────────────────────────────────────────────────────┘
                              │
                    ┌─────────▼─────────┐
                    │  Input Handling   │
                    │  • Mouse position │
                    │  • Click events   │
                    │  • Keyboard       │
                    └─────────┬─────────┘
                              │
                    ┌─────────▼─────────┐
                    │   Update Phase    │
                    │  • UI animations  │
                    │  • LED states     │
                    │  • Hover effects  │
                    └─────────┬─────────┘
                              │
                    ┌─────────▼─────────┐
                    │   Render Phase    │
                    └─────────┬─────────┘
                              │
              ┌───────────────┴───────────────┐
              │ Traverse scene graph (DFS)    │
              │                               │
              │  foreach NODE in tree:        │
              │  ┌─────────────────────────┐  │
              │  │ 1. Apply TRANSFORM      │  │
              │  │    • Matrix multiply    │  │
              │  │    • World position     │  │
              │  │                         │  │
              │  │ 2. Culling test         │  │
              │  │    • Check BOUNDINGBOX  │  │
              │  │    • Skip if off-screen │  │
              │  │                         │  │
              │  │ 3. For each RENDERNODE: │  │
              │  │  ┌────────────────────┐ │  │
              │  │  │ Get MESH           │ │  │
              │  │  │  ├─ Get buffers    │ │  │
              │  │  │  │  from stream    │ │  │
              │  │  │  │                 │ │  │
              │  │  │ Get MATERIAL       │ │  │
              │  │  │  ├─ Activate       │ │  │
              │  │  │  │  SHADER         │ │  │
              │  │  │  ├─ Set params     │ │  │
              │  │  │  └─ Bind TEXTURE   │ │  │
              │  │  │     └─ Lookup ID:  │ │  │
              │  │  │        "l_ferrari" │ │  │
              │  │  │        → GPU_TEX_0 │ │  │
              │  │  │                    │ │  │
              │  │  │ GPU Draw Call      │ │  │
              │  │  │  └─ Draw triangles │ │  │
              │  │  └────────────────────┘ │  │
              │  │                         │  │
              │  │ 4. Recurse to children  │  │
              │  └─────────────────────────┘  │
              └───────────────────────────────┘
                              │
                    ┌─────────▼─────────┐
                    │   Present Frame   │
                    │  • Swap buffers   │
                    │  • Display to     │
                    │    screen         │
                    └───────────────────┘
```

## Data Flow диаграмма

```
┌─────────────┐     ┌──────────────┐     ┌─────────────┐
│             │     │              │     │             │
│  Artist     │────►│  DDS File    │────►│  b_career   │
│  (Photoshop)│     │  ferrari.dds │     │  .pssg      │
│             │     │              │     │             │
└─────────────┘     └──────────────┘     └──────┬──────┘
                                                 │
                                                 │ Texture ID:
                                                 │ "l_ferrari_dc"
                                                 │
┌─────────────┐     ┌──────────────┐            │
│             │     │              │            │
│  3D Artist  │────►│  Scene File  │────►┌──────▼──────┐
│  (Maya/Max) │     │  menu.ma     │     │  d_career   │
│             │     │              │     │  .pssg      │
└─────────────┘     └──────────────┘     └──────┬──────┘
                                                 │
                                                 │ Material
                                                 │ references
                                                 │ texture ID
                                                 │
                    ┌────────────────────────────▼──────┐
                    │     Codemasters Build Tool        │
                    │  • Compiles shaders               │
                    │  • Optimizes geometry             │
                    │  • Packs into PSSG format         │
                    └────────────────┬──────────────────┘
                                     │
                                     ▼
                    ┌────────────────────────────────────┐
                    │        Game Runtime (EGO)          │
                    │  • Loads both PSSG files           │
                    │  • Renders UI at 60 FPS            │
                    │  • Handles user input              │
                    └────────────────┬──────────────────┘
                                     │
                                     ▼
                    ┌────────────────────────────────────┐
                    │         Player's Screen            │
                    │  ┌──────────────────────────────┐  │
                    │  │   Career Mode Menu           │  │
                    │  │  ┌────────┐  ┌────────┐     │  │
                    │  │  │Ferrari │  │Red Bull│     │  │
                    │  │  │  Logo  │  │  Logo  │     │  │
                    │  │  └────────┘  └────────┘     │  │
                    │  │                             │  │
                    │  │  [Start Career] [Options]  │  │
                    │  └──────────────────────────────┘  │
                    └────────────────────────────────────┘
```

## Модификация workflow

```
┌──────────────────────────────────────────────────────────┐
│              MODDER WORKFLOW EXAMPLE                     │
│         (Changing Ferrari logo to custom)                │
└──────────────────────────────────────────────────────────┘

1. Extract texture:
   ┌────────────┐
   │ PSSG Editor│
   └──────┬─────┘
          │ Export
          ▼
   ┌────────────────┐
   │ l_ferrari_dc   │
   │ .dds           │
   │ 256×128, DXT5  │
   └────────────────┘

2. Edit texture:
   ┌────────────┐
   │ Photoshop  │
   └──────┬─────┘
          │ Edit + Save
          ▼
   ┌────────────────┐
   │ custom_logo.dds│
   │ 256×128, DXT5  │
   │ (keep format!) │
   └────────────────┘

3. Import back:
   ┌────────────┐
   │ PSSG Editor│
   └──────┬─────┘
          │ Import
          │ (replaces texture
          │  but keeps all
          │  other attributes)
          ▼
   ┌────────────────┐
   │ b_career.pssg  │
   │ (modified)     │
   └────────┬───────┘
            │
            │ Replace in game folder:
            │ C:\...\F1 2012\frontend\
            ▼
   ┌────────────────┐
   │ Launch game!   │
   │ See new logo   │
   └────────────────┘

IMPORTANT: d_career.pssg doesn't need modification!
           It references texture by ID, not by content.
```

## Сравнение с другими UI системами

```
╔═══════════════════════════════════════════════════════════════╗
║  System        │  F1 2012 PSSG  │  Unity    │  Web (HTML5)   ║
╠═══════════════════════════════════════════════════════════════╣
║  Structure     │  Scene Graph   │  GameObject│  DOM Tree      ║
║                │  (PSSG nodes)  │  Hierarchy │  (HTML tags)   ║
╟───────────────────────────────────────────────────────────────╢
║  Positioning   │  TRANSFORM     │  Transform │  CSS transform ║
║                │  (4×4 matrix)  │  Component │  / position    ║
╟───────────────────────────────────────────────────────────────╢
║  Visuals       │  MATERIAL +    │  Material +│  CSS + images  ║
║                │  TEXTURE refs  │  Texture   │  background    ║
╟───────────────────────────────────────────────────────────────╢
║  Geometry      │  MESH in       │  MeshFilter│  Canvas API /  ║
║                │  RENDERSTREAM  │  Component │  WebGL         ║
╟───────────────────────────────────────────────────────────────╢
║  Rendering     │  Custom engine │  Unity     │  Browser       ║
║                │  (Codemasters) │  Renderer  │  Renderer      ║
╟───────────────────────────────────────────────────────────────╢
║  Logic         │  Hardcoded in  │  C# Scripts│  JavaScript    ║
║                │  C++ engine    │  attached  │  event handlers║
╟───────────────────────────────────────────────────────────────╢
║  Assets        │  PSSG binary   │  Unity     │  Separate      ║
║  Storage       │  container     │  Asset DB  │  files (png,   ║
║                │  (textures in  │            │  jpg, etc.)    ║
║                │  separate file)│            │                ║
╚═══════════════════════════════════════════════════════════════╝
```
