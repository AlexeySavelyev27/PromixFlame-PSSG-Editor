# Анализ структуры фронтенда F1 2012 Career Mode

## Обзор системы

Фронтенд режима Career в F1 2012 построен на **PSSG (Processed Scene Structure Graph)** формате
и разделен на два типа файлов:

### 1. b_career.pssg (108 KB) - Библиотека текстур
**Экспортированный файл:** b_career.json

**Назначение:** Хранилище всех текстур (спрайтов) для UI

**Структура:**
```
PSSGDATABASE
└── LIBRARY (type="YYY")  ← Специальный тип для текстур
    └── TEXTURE (×24 текстуры)
        ├── Атрибуты (width, height, texelFormat, etc.)
        └── TEXTUREIMAGEBLOCK
            └── TEXTUREIMAGEBLOCKDATA
                └── [Raw DDS pixel data]
```

**Содержимое:**
- 24 текстуры в форматах DXT3 и DXT5 (сжатые)
- Логотипы команд F1 (l_ferrari_dc, l_redbull_dc, etc.)
- UI элементы (career_long, career_short, career_ydt)
- Индикаторы (led_green_0, led_green_1, led_red_0, led_red_1, led_off)
- Элементы выбора (rival_select, difficulty_easy/normal/hard)

**Формат хранения текстур:**
- Только пиксельные данные (без DDS заголовка)
- Атрибуты описывают формат: DXT3 (16 байт/блок), DXT5 (16 байт/блок)
- Размеры: от 32×32 до 512×256 пикселей
- Все используют мипмапы (numberMipMapLevels = 0 означает автогенерацию)

### 2. d_career.pssg (62 MB) - 3D сцена и логика
**Экспортированный файл:** d_career.json (не загружен полностью из-за размера)

**Назначение:** Основной файл frontend, содержащий:

#### A. 3D Модели и геометрия
- **LIBRARY (type="RENDERSTREAM")** - Потоки данных для рендеринга
  - DATABLOCKSTREAM → Вершинные и индексные буферы
  - Данные о меше (vertices, normals, UV coordinates, tangents)

#### B. Материалы и шейдеры  
- **LIBRARY (type="SHADER")** - Шейдерный код
  - SHADERGROUP → Группы шейдеров
  - SHADER → Отдельные шейдеры (vertex, pixel)
  
- **LIBRARY (type="MATERIAL")** - Определения материалов
  - MATERIAL → Настройки материала
  - MATERIALPARAMETER → Параметры (цвета, константы)
  - TEXTURECONST → Ссылки на текстуры по ID (из b_career)

#### C. Сцена и иерархия
- **RENDERDATASOURCE** - Корень scene graph
  - NODE (рекурсивная структура)
    - TRANSFORM → 4×4 матрица трансформации (позиция, поворот, масштаб)
    - BOUNDINGBOX → Границы объекта для culling
    - RENDERNODE → Renderable объект
      - MESH → Ссылка на геометрию
        - material → Ссылка на материал (который ссылается на текстуры)
    - NODE (дочерние узлы) → Рекурсивно

## Как работает система рендеринга

### 1. Загрузка ресурсов
```
Игровой движок загружает оба файла:
├── b_career.pssg → Загружает все текстуры в VRAM
│   └── Создает таблицу: texture_id → GPU texture handle
│
└── d_career.pssg → Парсит всю структуру
    ├── Компилирует шейдеры
    ├── Создает материалы (привязывая texture_id к шейдерным параметрам)
    └── Строит scene graph из NODE иерархии
```

### 2. Рендеринг фрейма
```
Для каждого кадра UI:

1. Traversal scene graph (обход дерева NODE)
   ├── Применить TRANSFORM матрицу
   ├── Проверить BOUNDINGBOX (frustum culling)
   └── Если виден:

2. Для каждого RENDERNODE:
   ├── Получить MESH данные
   │   ├── Вершины из DATABLOCKSTREAM
   │   └── Индексы из того же потока
   │
   ├── Получить MATERIAL
   │   ├── Активировать SHADER
   │   ├── Установить MATERIALPARAMETER
   │   └── Bind текстуры через TEXTURECONST
   │       └── Lookup texture по ID в b_career
   │
   └── Draw call (GPU рисует меш)
```

### 3. Интерактивность
```
UI элементы реагируют на:
├── Hover → Смена текстуры (например led_off → led_green_1)
├── Click → Trigger события
└── Animation → Изменение TRANSFORM (позиция, поворот)
```

## Язык "фронтенда"

На самом деле это не отдельный "язык" в традиционном смысле, а **декларативный формат данных**:

### Концепция:
1. **Данные, а не код** - Фронтенд описывается данными, а не скриптами
2. **Scene Graph паттерн** - Иерархическая структура объектов
3. **Separation of Concerns** - Текстуры отдельно, геометрия отдельно, логика в движке

### Основные примитивы:

#### 1. NODE - Базовый элемент UI
```json
{
  "nodeName": "NODE",
  "attributes": {
    "id": "menu_button_start"
  },
  "children": [
    {
      "nodeName": "TRANSFORM",
      "data": "[4×4 matrix]"  // Позиция/размер элемента
    },
    {
      "nodeName": "RENDERNODE",
      "children": [
        {
          "nodeName": "MESH",
          "attributes": {
            "material": "ui_button_material"  // Ссылка на материал
          }
        }
      ]
    }
  ]
}
```

#### 2. MATERIAL - Визуальный стиль
```json
{
  "nodeName": "MATERIAL",
  "attributes": {
    "id": "ui_button_material",
    "shaderId": "ui_shader"
  },
  "children": [
    {
      "nodeName": "TEXTURECONST",
      "attributes": {
        "textureId": "career_long"  // Ссылка на текстуру из b_career
      }
    },
    {
      "nodeName": "MATERIALPARAMETER",
      "attributes": {
        "name": "DiffuseColor",
        "value": [1.0, 1.0, 1.0, 1.0]  // RGBA
      }
    }
  ]
}
```

#### 3. TEXTURE - Визуальный контент
```json
{
  "nodeName": "TEXTURE",
  "attributes": {
    "id": "career_long",
    "width": 256,
    "height": 256,
    "texelFormat": "dxt3"
  },
  "children": [
    {
      "nodeName": "TEXTUREIMAGEBLOCK",
      "children": [
        {
          "nodeName": "TEXTUREIMAGEBLOCKDATA",
          "rawData": "[compressed pixel data]"
        }
      ]
    }
  ]
}
```

## Аналогия с современными системами

Эта архитектура похожа на:

### Unity SceneGraph:
```
NODE             → GameObject
TRANSFORM        → Transform component
RENDERNODE+MESH  → MeshRenderer + MeshFilter
MATERIAL         → Material
TEXTURE          → Texture2D
```

### HTML + CSS:
```
NODE             → <div> element
TRANSFORM        → CSS transform property
MATERIAL+TEXTURE → CSS background-image
SHADER           → CSS filters/shaders
```

### React/3D:
```jsx
<Node id="menu_button">
  <Transform position={[0,0,0]} />
  <Mesh material="ui_button_material">
    <geometry vertices={...} />
  </Mesh>
</Node>
```

## Взаимодействие файлов

```
┌──────────────────┐
│  b_career.pssg   │  ← Только текстуры (108 KB)
│  ═══════════════ │
│  • l_ferrari_dc  │
│  • career_long   │
│  • led_green_0   │
│  • ...           │
└──────────────────┘
         ▲
         │ Reference by ID
         │
┌──────────────────┐
│  d_career.pssg   │  ← Сцена + логика (62 MB)
│  ═══════════════ │
│  MATERIAL        │
│  └─ TEXTURECONST │
│     └─ textureId │ ──────────┘
│  NODE            │
│  └─ TRANSFORM    │
│  └─ MESH         │
│     └─ material  │ ─────► MATERIAL
└──────────────────┘
```

## Практическое применение

### Замена текстуры логотипа команды:
```
1. Экспорт: PSSG Editor → Export texture "l_ferrari_dc" → ferrari.dds
2. Редактирование: Photoshop/GIMP → Изменить ferrari.dds
3. Импорт: PSSG Editor → Import texture "l_ferrari_dc" ← ferrari_new.dds
4. Сохранение: PSSG Editor → Save b_career.pssg
5. Результат: Новый логотип Ferrari в игре!
```

### Добавление нового UI элемента:
```
1. Создать текстуру в b_career.pssg (например "custom_button")
2. В d_career.pssg:
   a. Создать MATERIAL с textureId="custom_button"
   b. Создать NODE с TRANSFORM (позиция)
   c. Добавить MESH с material=новый_материал
3. Движок автоматически отрисует новый элемент
```

## Выводы

### Преимущества такой архитектуры:
✅ **Modular** - Текстуры и геометрия разделены
✅ **Efficient** - Текстуры загружаются один раз, используются многократно
✅ **Artists-friendly** - Можно менять текстуры без изменения кода
✅ **Scalable** - Легко добавлять новые элементы

### Недостатки:
❌ **Proprietary** - Формат закрыт, нужны специальные инструменты
❌ **Rigid** - Логика жестко закодирована в движке
❌ **No scripting** - Нельзя добавить динамическое поведение через данные
❌ **Binary** - Сложно редактировать вручную

### Это не "язык программирования", а:
- **Declarative Data Format** - Описывает "что", а не "как"
- **Scene Description Language** - Похоже на X3D, COLLADA, glTF
- **Asset Container** - Упакованные ресурсы для игрового движка

Логика (анимации, реакции на клики, переходы меню) находится в **движке игры** (C++ код),
а PSSG файлы предоставляют только **данные** для отображения.
