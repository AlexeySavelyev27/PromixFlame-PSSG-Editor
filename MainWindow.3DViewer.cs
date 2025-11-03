using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using HelixToolkit.Wpf;

namespace PSSGEditor
{
    public partial class MainWindow
    {
        // Хранение данных для 3D viewer
        private Dictionary<string, Pssg3DGeometryBlock> geometry3DBlocks = new Dictionary<string, Pssg3DGeometryBlock>();
        private Dictionary<string, Pssg3DRenderSource> renderSources = new Dictionary<string, Pssg3DRenderSource>();
        private Dictionary<string, Pssg3DShader> shaders3D = new Dictionary<string, Pssg3DShader>();
        private List<MaterialWithPath> allMaterials = new List<MaterialWithPath>();

        // Track current camera state to lock roll
        private Vector3D camera3DUpDirection = new Vector3D(0, 0, 1);

        // Camera control
        private bool isRotating3D = false;
        private Point lastMouse3DPosition;

        /// <summary>
        /// Инициализация 3D Viewer
        /// </summary>
        private void Initialize3DViewer()
        {
            // Настройка camera controls
            Model3DView.CameraMode = CameraMode.Inspect;
            Model3DView.Camera.UpDirection = camera3DUpDirection;

            // Subscribe to mouse events
            Model3DView.MouseDown += Model3DView_MouseDown;
            Model3DView.MouseMove += Model3DView_MouseMove;
            Model3DView.MouseUp += Model3DView_MouseUp;
            Model3DView.MouseWheel += Model3DView_MouseWheel;
        }

        /// <summary>
        /// Создание кастомной системы координат с grid плоскостью
        /// </summary>
        private ModelVisual3D CreateCustomCoordinateSystem()
        {
            var group = new Model3DGroup();

            // 1. Создаем grid плоскость (XY плоскость, полупрозрачная)
            CreateGridPlane(group);

            // 2. Создаем оси координат
            CreateCoordinateAxes(group);

            // 3. Создаем центральную точку
            CreateCenterPoint(group);

            var visual = new ModelVisual3D { Content = group };
            return visual;
        }

        /// <summary>
        /// Создание grid плоскости в клетку
        /// </summary>
        private void CreateGridPlane(Model3DGroup group)
        {
            double gridSize = 10;
            double gridStep = 1;
            int gridLines = (int)(gridSize / gridStep);

            var gridGeometry = new MeshGeometry3D();

            // Создаем линии сетки
            for (int i = -gridLines; i <= gridLines; i++)
            {
                double pos = i * gridStep;

                // Линии параллельные X
                AddGridLine(gridGeometry,
                    new Point3D(-gridSize, pos, 0),
                    new Point3D(gridSize, pos, 0),
                    0.02);

                // Линии параллельные Y
                AddGridLine(gridGeometry,
                    new Point3D(pos, -gridSize, 0),
                    new Point3D(pos, gridSize, 0),
                    0.02);
            }

            // Материал для сетки (полупрозрачный серый)
            var gridBrush = new SolidColorBrush(Color.FromArgb(60, 128, 128, 128));
            var gridMaterial = new DiffuseMaterial(gridBrush);

            var gridModel = new GeometryModel3D
            {
                Geometry = gridGeometry,
                Material = gridMaterial,
                BackMaterial = gridMaterial
            };

            group.Children.Add(gridModel);
        }

        /// <summary>
        /// Добавление линии сетки
        /// </summary>
        private void AddGridLine(MeshGeometry3D geometry, Point3D start, Point3D end, double thickness)
        {
            Vector3D direction = end - start;
            Vector3D perpendicular = new Vector3D(-direction.Y, direction.X, 0);
            perpendicular.Normalize();
            perpendicular *= thickness / 2;

            int baseIndex = geometry.Positions.Count;

            // 4 угла линии
            geometry.Positions.Add(start - perpendicular);
            geometry.Positions.Add(start + perpendicular);
            geometry.Positions.Add(end + perpendicular);
            geometry.Positions.Add(end - perpendicular);

            // 2 треугольника для линии
            geometry.TriangleIndices.Add(baseIndex);
            geometry.TriangleIndices.Add(baseIndex + 1);
            geometry.TriangleIndices.Add(baseIndex + 2);

            geometry.TriangleIndices.Add(baseIndex);
            geometry.TriangleIndices.Add(baseIndex + 2);
            geometry.TriangleIndices.Add(baseIndex + 3);
        }

        /// <summary>
        /// Создание осей координат (X - красная, Y - зеленая, Z - синяя)
        /// </summary>
        private void CreateCoordinateAxes(Model3DGroup group)
        {
            double axisLength = 5;
            double axisThickness = 0.05;

            // X ось (красная)
            CreateAxis(group, new Point3D(0, 0, 0), new Point3D(axisLength, 0, 0),
                Colors.Red, axisThickness);

            // Y ось (зеленая)
            CreateAxis(group, new Point3D(0, 0, 0), new Point3D(0, axisLength, 0),
                Colors.Lime, axisThickness);

            // Z ось (синяя)
            CreateAxis(group, new Point3D(0, 0, 0), new Point3D(0, 0, axisLength),
                Colors.Blue, axisThickness);
        }

        /// <summary>
        /// Создание одной оси
        /// </summary>
        private void CreateAxis(Model3DGroup group, Point3D start, Point3D end,
            Color color, double thickness)
        {
            var geometry = new MeshGeometry3D();
            Vector3D direction = end - start;
            double length = direction.Length;
            direction.Normalize();

            // Находим перпендикулярные векторы
            Vector3D perpendicular1;
            if (Math.Abs(direction.X) < 0.9)
                perpendicular1 = Vector3D.CrossProduct(direction, new Vector3D(1, 0, 0));
            else
                perpendicular1 = Vector3D.CrossProduct(direction, new Vector3D(0, 1, 0));
            perpendicular1.Normalize();
            perpendicular1 *= thickness;

            Vector3D perpendicular2 = Vector3D.CrossProduct(direction, perpendicular1);
            perpendicular2.Normalize();
            perpendicular2 *= thickness;

            // Создаем цилиндр для оси
            int segments = 8;
            for (int i = 0; i < segments; i++)
            {
                double angle = 2 * Math.PI * i / segments;
                double nextAngle = 2 * Math.PI * (i + 1) / segments;

                Vector3D offset1 = perpendicular1 * Math.Cos(angle) + perpendicular2 * Math.Sin(angle);
                Vector3D offset2 = perpendicular1 * Math.Cos(nextAngle) + perpendicular2 * Math.Sin(nextAngle);

                int baseIndex = geometry.Positions.Count;

                geometry.Positions.Add(start + offset1);
                geometry.Positions.Add(start + offset2);
                geometry.Positions.Add(end + offset2);
                geometry.Positions.Add(end + offset1);

                geometry.TriangleIndices.Add(baseIndex);
                geometry.TriangleIndices.Add(baseIndex + 1);
                geometry.TriangleIndices.Add(baseIndex + 2);

                geometry.TriangleIndices.Add(baseIndex);
                geometry.TriangleIndices.Add(baseIndex + 2);
                geometry.TriangleIndices.Add(baseIndex + 3);
            }

            // Добавляем стрелку на конце
            CreateArrowHead(geometry, end, direction, color, thickness * 3, length * 0.1);

            var material = new DiffuseMaterial(new SolidColorBrush(color));
            var model = new GeometryModel3D
            {
                Geometry = geometry,
                Material = material,
                BackMaterial = material
            };

            group.Children.Add(model);
        }

        /// <summary>
        /// Создание стрелки на конце оси
        /// </summary>
        private void CreateArrowHead(MeshGeometry3D geometry, Point3D tip, Vector3D direction,
            Color color, double radius, double length)
        {
            Point3D basePoint = tip - direction * length;

            Vector3D perpendicular1;
            if (Math.Abs(direction.X) < 0.9)
                perpendicular1 = Vector3D.CrossProduct(direction, new Vector3D(1, 0, 0));
            else
                perpendicular1 = Vector3D.CrossProduct(direction, new Vector3D(0, 1, 0));
            perpendicular1.Normalize();
            perpendicular1 *= radius;

            Vector3D perpendicular2 = Vector3D.CrossProduct(direction, perpendicular1);
            perpendicular2.Normalize();
            perpendicular2 *= radius;

            int segments = 8;
            int tipIndex = geometry.Positions.Count;
            geometry.Positions.Add(tip);

            for (int i = 0; i <= segments; i++)
            {
                double angle = 2 * Math.PI * i / segments;
                Vector3D offset = perpendicular1 * Math.Cos(angle) + perpendicular2 * Math.Sin(angle);
                geometry.Positions.Add(basePoint + offset);
            }

            for (int i = 0; i < segments; i++)
            {
                geometry.TriangleIndices.Add(tipIndex);
                geometry.TriangleIndices.Add(tipIndex + i + 1);
                geometry.TriangleIndices.Add(tipIndex + i + 2);
            }
        }

        /// <summary>
        /// Создание центральной точки
        /// </summary>
        private void CreateCenterPoint(Model3DGroup group)
        {
            var sphereGeometry = new MeshGeometry3D();
            double radius = 0.1;
            int segments = 16;

            // Создаем простую сферу
            for (int lat = 0; lat <= segments; lat++)
            {
                double theta = lat * Math.PI / segments;
                double sinTheta = Math.Sin(theta);
                double cosTheta = Math.Cos(theta);

                for (int lon = 0; lon <= segments; lon++)
                {
                    double phi = lon * 2 * Math.PI / segments;
                    double sinPhi = Math.Sin(phi);
                    double cosPhi = Math.Cos(phi);

                    double x = radius * cosPhi * sinTheta;
                    double y = radius * sinPhi * sinTheta;
                    double z = radius * cosTheta;

                    sphereGeometry.Positions.Add(new Point3D(x, y, z));
                }
            }

            // Индексы для треугольников
            for (int lat = 0; lat < segments; lat++)
            {
                for (int lon = 0; lon < segments; lon++)
                {
                    int current = lat * (segments + 1) + lon;
                    int next = current + segments + 1;

                    sphereGeometry.TriangleIndices.Add(current);
                    sphereGeometry.TriangleIndices.Add(next);
                    sphereGeometry.TriangleIndices.Add(current + 1);

                    sphereGeometry.TriangleIndices.Add(current + 1);
                    sphereGeometry.TriangleIndices.Add(next);
                    sphereGeometry.TriangleIndices.Add(next + 1);
                }
            }

            var material = new DiffuseMaterial(Brushes.White);
            var sphere = new GeometryModel3D
            {
                Geometry = sphereGeometry,
                Material = material,
                BackMaterial = material
            };

            group.Children.Add(sphere);
        }

        /// <summary>
        /// Обработчик выбора 3D объекта
        /// </summary>
        private void On3DObjectSelected(TreeViewItem item)
        {
            // Очистка предыдущего содержимого
            Model3DContainer.Children.Clear();
            ModelInfoText.Text = "";

            // Проверяем тип выбранного элемента
            if (item.Tag is string tagString)
            {
                // Это служебные узлы типа "materials"
                Show3DTagDetails(tagString);
                return;
            }

            if (item.Tag is Pssg3DNode node)
            {
                // ✅ ИСПРАВЛЕНИЕ: Ищем ВСЕ материалы в поддереве выбранной ноды
                // СТАРАЯ ЛОГИКА: m.PathToRoot.Last() == node.Node (только прямые дети)
                // НОВАЯ ЛОГИКА: ищем все материалы где нода есть в пути или в поддереве
                var nodeMaterials = allMaterials.Where(m =>
                    m.PathToRoot.Contains(node.Node) || // Нода есть где-то в пути к корню
                    IsNodeInSubtree(m.InstanceNode, node.Node) // Или render instance в поддереве
                ).ToList();

                if (nodeMaterials.Count > 0)
                {
                    // Рендерим все материалы этой ноды вместе
                    Model3DView.Visibility = Visibility.Visible;
                    ViewHelpPanel.Visibility = Visibility.Visible;
                    ModelInfoPanel.Visibility = Visibility.Visible;
                    No3DObjectText.Visibility = Visibility.Collapsed;

                    RenderAllMaterialsForNode(node, nodeMaterials);
                }
                else
                {
                    // Показываем информацию о ноде
                    Show3DNodeDetails(node);
                }
            }
            else if (item.Tag is MaterialWithPath material)
            {
                Model3DView.Visibility = Visibility.Visible;
                ViewHelpPanel.Visibility = Visibility.Visible;
                ModelInfoPanel.Visibility = Visibility.Visible;
                No3DObjectText.Visibility = Visibility.Collapsed;

                RenderMaterial(material);
            }
            else
            {
                // Неизвестный тип - скрываем 3D view
                Model3DView.Visibility = Visibility.Collapsed;
                ViewHelpPanel.Visibility = Visibility.Collapsed;
                ModelInfoPanel.Visibility = Visibility.Collapsed;
                No3DObjectText.Visibility = Visibility.Visible;
            }
        }

        /// <summary>
        /// Построение дерева 3D объектов из PSSG структуры (универсальный bottom-up подход)
        /// </summary>
        public void Build3DObjectsTree()
        {
            Objects3DTreeView.Items.Clear();
            geometry3DBlocks.Clear();
            renderSources.Clear();
            shaders3D.Clear();
            allMaterials.Clear();

            if (rootNode == null)
                return;

            try
            {
                StatusText.Text = "Building 3D objects tree...";

                // ═══════════════════════════════════════════════════════
                // ФАЗА 1: ИНДЕКСАЦИЯ - собираем все ресурсы
                // ═══════════════════════════════════════════════════════
                ParseShaders(rootNode);
                Parse3DGeometryBlocks(rootNode);
                Parse3DRenderSources(rootNode);

                // ═══════════════════════════════════════════════════════
                // ФАЗА 2: ПОИСК RENDER INSTANCES (bottom-up от данных)
                // ═══════════════════════════════════════════════════════
                FindRenderInstances(rootNode, new List<PSSGNode>(), allMaterials);

                // ═══════════════════════════════════════════════════════
                // ФАЗА 3: ПОСТРОЕНИЕ UI ДЕРЕВА (группировка по родителям)
                // ═══════════════════════════════════════════════════════
                BuildUIFromMaterials();

                StatusText.Text = $"Found {geometry3DBlocks.Count} geometry blocks, {shaders3D.Count} shaders, {allMaterials.Count} materials";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Error building 3D tree: {ex.Message}";
            }
        }

        /// <summary>
        /// Парсинг шейдеров
        /// </summary>
        private void ParseShaders(PSSGNode node)
        {
            if (node.Name == "SHADERINSTANCE" && node.Attributes.ContainsKey("id"))
            {
                string id = GetAttributeValue(node, "id", "");
                if (!string.IsNullOrEmpty(id))
                {
                    var shader = new Pssg3DShader
                    {
                        Id = id,
                        ShaderGroup = GetAttributeValue(node, "shaderGroup", "")
                    };

                    // Парсим текстуры
                    foreach (var child in node.Children)
                    {
                        if (child.Name == "SHADERINPUT")
                        {
                            string type = GetAttributeValue(child, "type", "");
                            if (type == "texture")
                            {
                                string texture = GetAttributeValue(child, "texture", "");
                                if (!string.IsNullOrEmpty(texture))
                                    shader.Textures.Add(texture);
                            }
                        }
                    }

                    shaders3D[id] = shader;
                }
            }

            foreach (var child in node.Children)
                ParseShaders(child);
        }

        /// <summary>
        /// Парсинг DATABLOCK нод для геометрии
        /// </summary>
        private void Parse3DGeometryBlocks(PSSGNode node)
        {
            if (node.Name == "DATABLOCK" && node.Attributes.ContainsKey("id"))
            {
                string id = GetAttributeValue(node, "id", "");

                var block = new Pssg3DGeometryBlock
                {
                    Id = id,
                    Node = node
                };

                block.ElementCount = GetAttributeIntValue(node, "elementCount", 0);
                block.StreamCount = GetAttributeIntValue(node, "streamCount", 0);

                // Парсим streams
                foreach (var child in node.Children)
                {
                    if (child.Name == "DATABLOCKSTREAM")
                    {
                        var stream = new DataStream
                        {
                            RenderType = GetAttributeValue(child, "renderType", ""),
                            DataType = GetAttributeValue(child, "dataType", ""),
                            Offset = GetAttributeIntValue(child, "offset", 0),
                            Stride = GetAttributeIntValue(child, "stride", 0)
                        };
                        block.Streams.Add(stream);
                    }
                    else if (child.Name == "DATABLOCKDATA")
                    {
                        block.DataNode = child;
                    }
                }

                geometry3DBlocks[id] = block;
            }

            foreach (var child in node.Children)
                Parse3DGeometryBlocks(child);
        }

        /// <summary>
        /// Парсинг RENDERDATASOURCE нод
        /// </summary>
        private void Parse3DRenderSources(PSSGNode node)
        {
            if (node.Name == "RENDERDATASOURCE" && node.Attributes.ContainsKey("id"))
            {
                string id = GetAttributeValue(node, "id", "");

                var source = new Pssg3DRenderSource
                {
                    Id = id,
                    Node = node
                };

                // Найдем RENDERSTREAM child nodes
                foreach (var child in node.Children)
                {
                    if (child.Name == "RENDERSTREAM")
                    {
                        string blockRef = GetAttributeValue(child, "dataBlock", "");
                        blockRef = blockRef.TrimStart('#');

                        if (!string.IsNullOrEmpty(blockRef))
                            source.DataBlockIds.Add(blockRef);
                    }
                    else if (child.Name == "RENDERINDEXSOURCE")
                    {
                        source.IndexSourceNode = child;
                    }
                }

                renderSources[id] = source;
            }

            foreach (var child in node.Children)
                Parse3DRenderSources(child);
        }

        /// <summary>
        /// Поиск render instances с сохранением пути к корню (ФАЗА 2)
        /// </summary>
        private void FindRenderInstances(PSSGNode node, List<PSSGNode> pathFromRoot, List<MaterialWithPath> materials)
        {
            // Проверяем: есть ли child RENDERINSTANCESOURCE?
            var sourceRefNode = FindChildByName(node, "RENDERINSTANCESOURCE");

            if (sourceRefNode != null)
            {
                // ЭТО RENDER INSTANCE! (независимо от имени ноды)
                string sourceId = GetAttributeValue(sourceRefNode, "source", "").TrimStart('#');
                string shaderId = GetAttributeValue(node, "shader", "").TrimStart('#');

                // Находим цепочку: source -> datablock -> geometry
                if (!string.IsNullOrEmpty(sourceId) && renderSources.TryGetValue(sourceId, out var renderSource))
                {
                    if (renderSource.DataBlockIds.Count > 0)
                    {
                        string dataBlockId = renderSource.DataBlockIds[0];

                        if (geometry3DBlocks.TryGetValue(dataBlockId, out var geometryBlock))
                        {
                            // Сохраняем материал с полным путем!
                            var material = new MaterialWithPath
                            {
                                InstanceNode = node,
                                SourceId = sourceId,
                                GeometryId = dataBlockId,
                                ShaderId = shaderId,
                                PathToRoot = new List<PSSGNode>(pathFromRoot), // копируем путь
                                RenderSource = renderSource,
                                GeometryBlock = geometryBlock
                            };

                            materials.Add(material);
                        }
                    }
                }
            }

            // Продолжаем рекурсию для всех детей
            foreach (var child in node.Children)
            {
                var newPath = new List<PSSGNode>(pathFromRoot);
                newPath.Add(node);
                FindRenderInstances(child, newPath, materials);
            }
        }

        /// <summary>
        /// Построение UI дерева из найденных материалов (ФАЗА 3)
        /// </summary>
        private void BuildUIFromMaterials()
        {
            if (allMaterials.Count == 0)
            {
                StatusText.Text = "No geometry data found";
                return;
            }

            // Группируем материалы по их прямому родителю
            var groupedByParent = new Dictionary<PSSGNode, List<MaterialWithPath>>();

            foreach (var material in allMaterials)
            {
                // Прямой родитель - это нода в которой находится render instance
                PSSGNode parent = material.PathToRoot.Count > 0
                    ? material.PathToRoot.Last()
                    : rootNode;

                if (!groupedByParent.ContainsKey(parent))
                    groupedByParent[parent] = new List<MaterialWithPath>();

                groupedByParent[parent].Add(material);
            }

            // Находим все потенциальные корневые ноды
            var topLevelNodes = FindTopLevelNodes();

            // Строим дерево для каждой корневой ноды
            foreach (var topNode in topLevelNodes)
            {
                var uiNode = CreateUINode(topNode);
                if (BuildUINodeRecursive(topNode, uiNode, groupedByParent))
                {
                    // Добавляем только если у ноды есть материалы в поддереве
                    Objects3DTreeView.Items.Add(uiNode);
                }
            }
        }

        /// <summary>
        /// Находим все ноды верхнего уровня (ROOTNODE или другие контейнеры)
        /// </summary>
        private List<PSSGNode> FindTopLevelNodes()
        {
            var topNodes = new List<PSSGNode>();

            // Сначала ищем ROOTNODE
            var rootNodes = FindAllNodesByName(rootNode, "ROOTNODE");
            if (rootNodes.Count > 0)
            {
                topNodes.AddRange(rootNodes);
                return topNodes;
            }

            // Если нет ROOTNODE, берем детей корня с id атрибутом
            foreach (var child in rootNode.Children)
            {
                if (child.Attributes.ContainsKey("id"))
                    topNodes.Add(child);
            }

            return topNodes;
        }

        /// <summary>
        /// Рекурсивное построение UI ноды с фильтрацией "значимых" нод
        /// </summary>
        private bool BuildUINodeRecursive(PSSGNode pssgNode, TreeViewItem uiNode, Dictionary<PSSGNode, List<MaterialWithPath>> groupedByParent)
        {
            bool hasContent = false;

            // Проверяем: есть ли для этой ноды материалы?
            if (groupedByParent.ContainsKey(pssgNode))
            {
                var materialsForNode = groupedByParent[pssgNode];

                // Создаем папку Materials
                var materialsFolder = new TreeViewItem
                {
                    Header = "Materials",
                    IsExpanded = false
                };

                // Добавляем материалы
                foreach (var material in materialsForNode)
                {
                    string materialName = !string.IsNullOrEmpty(material.ShaderId)
                        ? material.ShaderId
                        : $"Material ({material.GeometryId})";

                    var materialItem = new TreeViewItem
                    {
                        Header = materialName,
                        Tag = material,
                        IsExpanded = false
                    };

                    // Добавляем текстуры если есть
                    if (!string.IsNullOrEmpty(material.ShaderId) &&
                        shaders3D.TryGetValue(material.ShaderId, out var shader) &&
                        shader.Textures.Count > 0)
                    {
                        var texturesFolder = new TreeViewItem
                        {
                            Header = "Textures",
                            IsExpanded = false
                        };

                        foreach (string texture in shader.Textures)
                        {
                            texturesFolder.Items.Add(new TreeViewItem
                            {
                                Header = texture,
                                Tag = texture
                            });
                        }

                        materialItem.Items.Add(texturesFolder);
                    }

                    materialsFolder.Items.Add(materialItem);
                }

                uiNode.Items.Add(materialsFolder);
                hasContent = true;
            }

            // Рекурсивно обрабатываем детей
            foreach (var child in pssgNode.Children)
            {
                // Проверяем: значима ли эта нода?
                if (IsSignificantNode(child, groupedByParent))
                {
                    var childUI = CreateUINode(child);

                    if (BuildUINodeRecursive(child, childUI, groupedByParent))
                    {
                        uiNode.Items.Add(childUI);
                        hasContent = true;
                    }
                }
            }

            return hasContent;
        }

        /// <summary>
        /// Проверяет значима ли нода (есть ли у неё или её детей материалы)
        /// </summary>
        private bool IsSignificantNode(PSSGNode node, Dictionary<PSSGNode, List<MaterialWithPath>> groupedByParent)
        {
            // Нода значима если у неё есть id
            if (!node.Attributes.ContainsKey("id"))
                return false;

            // Игнорируем служебные ноды
            if (node.Name == "SHADERINSTANCE" || node.Name == "TEXTURE" ||
                node.Name == "DATABLOCK" || node.Name == "RENDERDATASOURCE")
                return false;

            // У самой ноды есть материалы?
            if (groupedByParent.ContainsKey(node))
                return true;

            // У детей есть материалы?
            return HasMaterialsInSubtree(node, groupedByParent);
        }

        /// <summary>
        /// Проверяет есть ли материалы в поддереве
        /// </summary>
        private bool HasMaterialsInSubtree(PSSGNode node, Dictionary<PSSGNode, List<MaterialWithPath>> groupedByParent)
        {
            foreach (var child in node.Children)
            {
                if (groupedByParent.ContainsKey(child))
                    return true;

                if (HasMaterialsInSubtree(child, groupedByParent))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Создание UI ноды для TreeView (без цветов)
        /// </summary>
        private TreeViewItem CreateUINode(PSSGNode pssgNode)
        {
            string displayName = GetAttributeValue(pssgNode, "nickname", "");
            if (string.IsNullOrEmpty(displayName))
                displayName = GetAttributeValue(pssgNode, "id", pssgNode.Name);

            return new TreeViewItem
            {
                Header = displayName,
                Tag = new Pssg3DNode
                {
                    Id = GetAttributeValue(pssgNode, "id", ""),
                    Node = pssgNode,
                    Type = pssgNode.Name
                },
                IsExpanded = false
            };
        }

        /// <summary>
        /// Показать информацию о служебном узле
        /// </summary>
        private void Show3DTagDetails(string tag)
        {
            Model3DView.Visibility = Visibility.Collapsed;
            ViewHelpPanel.Visibility = Visibility.Collapsed;
            ModelInfoPanel.Visibility = Visibility.Visible;
            No3DObjectText.Visibility = Visibility.Collapsed;

            if (tag == "materials")
            {
                ModelInfoText.Text = "Materials Section\n";
                ModelInfoText.Text += "Contains a list of material shaders used by this object.";
            }
            else if (tag != null && tag.Contains(".tga", StringComparison.OrdinalIgnoreCase))
            {
                ModelInfoText.Text = $"Texture: {tag}\n";
                ModelInfoText.Text += "Textures are used to provide surface details for 3D models.";
            }
            else
            {
                ModelInfoText.Text = "Select an item to view details";
            }
        }

        /// <summary>
        /// Показать информацию о ноде (без рендеринга 3D)
        /// </summary>
        private void Show3DNodeDetails(Pssg3DNode node)
        {
            Model3DView.Visibility = Visibility.Collapsed;
            ViewHelpPanel.Visibility = Visibility.Collapsed;
            ModelInfoPanel.Visibility = Visibility.Visible;
            No3DObjectText.Visibility = Visibility.Collapsed;

            ModelInfoText.Text = $"Node ID: {node.Id}\n";
            ModelInfoText.Text += $"Type: {node.Type}\n";

            // Показываем матрицу трансформации если есть
            var transformNode = FindChildByName(node.Node, "TRANSFORM");
            if (transformNode != null && transformNode.Data != null && transformNode.Data.Length > 0)
            {
                ModelInfoText.Text += "\nTransform Matrix:\n";

                try
                {
                    string transformText = System.Text.Encoding.UTF8.GetString(transformNode.Data);
                    string[] values = transformText.Trim().Split(
                        new[] { ' ', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries);

                    if (values.Length >= 16)
                    {
                        for (int i = 0; i < 4; i++)
                        {
                            ModelInfoText.Text += $"  [{values[i * 4],-10} {values[i * 4 + 1],-10} {values[i * 4 + 2],-10} {values[i * 4 + 3],-10}]\n";
                        }
                    }
                }
                catch
                {
                    ModelInfoText.Text += "  [Transform data present]\n";
                }
            }

            // Показываем bounding box если есть
            var boundingBoxNode = FindChildByName(node.Node, "BOUNDINGBOX");
            if (boundingBoxNode != null && boundingBoxNode.Data != null && boundingBoxNode.Data.Length > 0)
            {
                ModelInfoText.Text += "\nBounding Box:\n";

                try
                {
                    string boxText = System.Text.Encoding.UTF8.GetString(boundingBoxNode.Data);
                    string[] bounds = boxText.Trim().Split(
                        new[] { ' ', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries);

                    if (bounds.Length >= 6)
                    {
                        ModelInfoText.Text += $"  Min: ({bounds[0]}, {bounds[1]}, {bounds[2]})\n";
                        ModelInfoText.Text += $"  Max: ({bounds[3]}, {bounds[4]}, {bounds[5]})\n";
                    }
                }
                catch
                {
                    ModelInfoText.Text += "  [Bounding box data present]\n";
                }
            }
        }

        /// <summary>
        /// Отрисовка отдельного материала
        /// </summary>
        private void RenderMaterial(MaterialWithPath material)
        {
            try
            {
                StatusText.Text = "Building material mesh...";

                ModelInfoText.Text = $"Material: {material.ShaderId}\n";
                if (!string.IsNullOrEmpty(material.GeometryId))
                    ModelInfoText.Text += $"Geometry: {material.GeometryId}\n";

                var model = Create3DMeshFromMaterial(material);
                if (model != null)
                {
                    Model3DContainer.Children.Add(new ModelVisual3D { Content = model });

                    // Подсчет статистики
                    if (model.Children.Count > 0 && model.Children[0] is GeometryModel3D geoModel &&
                        geoModel.Geometry is MeshGeometry3D mesh)
                    {
                        ModelInfoText.Text += $"Vertices: {mesh.Positions.Count}\n";
                        ModelInfoText.Text += $"Triangles: {mesh.TriangleIndices.Count / 3}";
                    }

                    // Добавляем информацию о текстурах
                    if (!string.IsNullOrEmpty(material.ShaderId) &&
                        shaders3D.TryGetValue(material.ShaderId, out var shader) &&
                        shader.Textures.Count > 0)
                    {
                        ModelInfoText.Text += $"\nTextures: {shader.Textures.Count}";
                        for (int i = 0; i < Math.Min(3, shader.Textures.Count); i++)
                        {
                            string texture = shader.Textures[i];
                            if (texture.Length > 40)
                                texture = "..." + texture.Substring(texture.Length - 37);
                            ModelInfoText.Text += $"\n  {texture}";
                        }
                    }
                }

                // Добавляем кастомную систему координат
                Model3DContainer.Children.Add(CreateCustomCoordinateSystem());

                // Настраиваем камеру
                Model3DView.ResetCamera();
                Model3DView.ZoomExtents();

                StatusText.Text = "Material rendered successfully";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Error rendering material: {ex.Message}";
            }
        }

        /// <summary>
        /// Отрисовка всех материалов ноды вместе
        /// </summary>
        private void RenderAllMaterialsForNode(Pssg3DNode node, List<MaterialWithPath> materials)
        {
            try
            {
                StatusText.Text = $"Rendering 3D object: {node.Id}...";

                int totalVertices = 0;
                int totalTriangles = 0;

                ModelInfoText.Text = $"{node.Id}\n";
                ModelInfoText.Text += $"Type: {node.Type}\n";
                ModelInfoText.Text += $"Materials: {materials.Count}\n";

                // Отрисовываем каждый материал
                foreach (var material in materials)
                {
                    var model = Create3DMeshFromMaterial(material);
                    if (model != null)
                    {
                        Model3DContainer.Children.Add(new ModelVisual3D { Content = model });

                        // Подсчет вертексов и треугольников
                        if (model.Children.Count > 0 && model.Children[0] is GeometryModel3D geoModel &&
                            geoModel.Geometry is MeshGeometry3D mesh)
                        {
                            totalVertices += mesh.Positions.Count;
                            totalTriangles += mesh.TriangleIndices.Count / 3;
                        }
                    }
                }

                ModelInfoText.Text += $"Total Vertices: {totalVertices}\n";
                ModelInfoText.Text += $"Total Triangles: {totalTriangles}";

                // Добавляем кастомную систему координат
                Model3DContainer.Children.Add(CreateCustomCoordinateSystem());

                // Настраиваем камеру
                Model3DView.ResetCamera();
                Model3DView.ZoomExtents();

                StatusText.Text = "3D object rendered successfully";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Error rendering 3D object: {ex.Message}";
                ModelInfoText.Text = $"Error: {ex.Message}";
            }
        }

        /// <summary>
        /// Создание 3D mesh из материала
        /// </summary>
        private Model3DGroup Create3DMeshFromMaterial(MaterialWithPath material)
        {
            try
            {
                if (material.GeometryBlock == null || material.GeometryBlock.DataNode == null)
                    return null;

                // Парсим геометрию используя PssgMeshBuilder
                PssgMeshBuilder.ParseGeometryData(
                    material.GeometryBlock.Node,
                    out Point3DCollection rawPositions,
                    out Vector3DCollection rawNormals,
                    out PointCollection texCoords
                );

                if (rawPositions.Count == 0)
                    return null;

                // Конвертируем координаты (Y-up -> Z-up)
                var positions = new Point3DCollection();
                var normals = new Vector3DCollection();

                foreach (Point3D pos in rawPositions)
                    positions.Add(new Point3D(pos.X, pos.Z, pos.Y));

                foreach (Vector3D norm in rawNormals)
                    normals.Add(new Vector3D(norm.X, norm.Z, norm.Y));

                // Парсим индексы
                var indices = new Int32Collection();
                if (material.RenderSource?.IndexSourceNode != null)
                {
                    indices = PssgMeshBuilder.ParseIndices(material.RenderSource.IndexSourceNode);
                }

                // Генерируем индексы если не найдены
                if (indices.Count == 0 && positions.Count >= 3)
                {
                    for (int i = 0; i < positions.Count; i += 3)
                    {
                        if (i + 2 < positions.Count)
                        {
                            indices.Add(i);
                            indices.Add(i + 1);
                            indices.Add(i + 2);
                        }
                    }
                }

                // Создаем геометрию
                var geometry = new MeshGeometry3D
                {
                    Positions = positions,
                    TriangleIndices = indices
                };

                if (normals.Count == positions.Count)
                    geometry.Normals = normals;

                if (texCoords.Count == positions.Count)
                    geometry.TextureCoordinates = texCoords;

                // Создаем материал с цветом
                Color color = Get3DMaterialColor(material.ShaderId);
                var wpfMaterial = new DiffuseMaterial(new SolidColorBrush(color));

                // Создаем модель
                var group = new Model3DGroup();
                var model = new GeometryModel3D
                {
                    Geometry = geometry,
                    Material = wpfMaterial,
                    BackMaterial = wpfMaterial
                };
                group.Children.Add(model);

                // Добавляем wireframe
                if (positions.Count > 0 && indices.Count > 0)
                    Add3DWireframe(group, indices, positions);

                // Применяем трансформацию если есть
                var transform = Find3DTransform(material);
                if (transform != null)
                    group.Transform = transform;

                return group;
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Error creating mesh: {ex.Message}";
                return null;
            }
        }

        /// <summary>
        /// Поиск трансформации в материале (идем вверх по pathToRoot)
        /// </summary>
        private Transform3D Find3DTransform(MaterialWithPath material)
        {
            // Идем вверх по пути к корню
            foreach (var node in material.PathToRoot.AsEnumerable().Reverse())
            {
                var transformNode = FindChildByName(node, "TRANSFORM");
                if (transformNode != null)
                    return PssgMeshBuilder.ParseTransform(transformNode);
            }

            return null;
        }

        /// <summary>
        /// Добавление wireframe к модели
        /// </summary>
        private void Add3DWireframe(Model3DGroup group, Int32Collection indices, Point3DCollection positions)
        {
            var wireframeGeometry = new MeshGeometry3D();

            for (int i = 0; i < indices.Count; i += 3)
            {
                if (i + 2 < indices.Count)
                {
                    int a = indices[i];
                    int b = indices[i + 1];
                    int c = indices[i + 2];

                    if (a < positions.Count && b < positions.Count && c < positions.Count)
                    {
                        Add3DLine(wireframeGeometry, positions[a], positions[b]);
                        Add3DLine(wireframeGeometry, positions[b], positions[c]);
                        Add3DLine(wireframeGeometry, positions[c], positions[a]);
                    }
                }
            }

            if (wireframeGeometry.Positions.Count > 0)
            {
                var wireframe = new GeometryModel3D
                {
                    Geometry = wireframeGeometry,
                    Material = new DiffuseMaterial(Brushes.Black)
                };
                group.Children.Add(wireframe);
            }
        }

        /// <summary>
        /// Добавление линии в геометрию
        /// </summary>
        private void Add3DLine(MeshGeometry3D geometry, Point3D p1, Point3D p2)
        {
            double thickness = 0.01;
            Vector3D dir = p2 - p1;

            Vector3D up = new Vector3D(0, 0, 1);
            if (Math.Abs(Vector3D.DotProduct(dir, up)) > 0.9)
                up = new Vector3D(1, 0, 0);

            Vector3D right = Vector3D.CrossProduct(dir, up);
            right.Normalize();
            right *= thickness / 2;

            up = Vector3D.CrossProduct(right, dir);
            up.Normalize();
            up *= thickness / 2;

            int baseIndex = geometry.Positions.Count;

            Point3D[] corners = new Point3D[]
            {
                p1 + right + up, p1 + right - up, p1 - right - up, p1 - right + up,
                p2 + right + up, p2 + right - up, p2 - right - up, p2 - right + up
            };

            foreach (var corner in corners)
                geometry.Positions.Add(corner);

            int[][] faces = new int[][]
            {
                new int[] {0, 1, 2, 2, 3, 0},
                new int[] {4, 7, 6, 6, 5, 4},
                new int[] {0, 3, 7, 7, 4, 0},
                new int[] {1, 5, 6, 6, 2, 1},
                new int[] {0, 4, 5, 5, 1, 0},
                new int[] {2, 6, 7, 7, 3, 2}
            };

            foreach (var face in faces)
                foreach (var idx in face)
                    geometry.TriangleIndices.Add(baseIndex + idx);
        }

        /// <summary>
        /// Генерация цвета из shader ID
        /// </summary>
        private Color Get3DMaterialColor(string shaderId)
        {
            if (string.IsNullOrEmpty(shaderId))
                return Colors.LightGray;

            int hash = shaderId.GetHashCode();
            byte r = (byte)((hash & 0xFF0000) >> 16);
            byte g = (byte)((hash & 0x00FF00) >> 8);
            byte b = (byte)(hash & 0x0000FF);

            r = (byte)Math.Max((int)r, 64);
            g = (byte)Math.Max((int)g, 64);
            b = (byte)Math.Max((int)b, 64);

            return Color.FromRgb(r, g, b);
        }

        #region Helper Methods

        private List<PSSGNode> FindAllNodesByName(PSSGNode root, string name)
        {
            var result = new List<PSSGNode>();
            FindAllNodesByNameRecursive(root, name, result);
            return result;
        }

        private void FindAllNodesByNameRecursive(PSSGNode node, string name, List<PSSGNode> result)
        {
            if (node.Name == name)
                result.Add(node);

            foreach (var child in node.Children)
                FindAllNodesByNameRecursive(child, name, result);
        }

        private PSSGNode FindChildByName(PSSGNode parent, string name)
        {
            return parent.Children.FirstOrDefault(c => c.Name == name);
        }

        private string GetAttributeValue(PSSGNode node, string attrName, string defaultValue)
        {
            if (node.Attributes.TryGetValue(attrName, out var attrBytes))
                return PSSGFormat.DecodeString(attrBytes);
            return defaultValue;
        }

        private int GetAttributeIntValue(PSSGNode node, string attrName, int defaultValue)
        {
            if (node.Attributes.TryGetValue(attrName, out var attrBytes))
            {
                if (attrBytes != null && attrBytes.Length >= 4)
                    return (int)PSSGFormat.ReadUInt32(attrBytes);
            }
            return defaultValue;
        }

        /// <summary>
        /// Проверяет, находится ли нода в поддереве родительской ноды
        /// Используется для правильного поиска материалов в LOD нодах
        /// </summary>
        private bool IsNodeInSubtree(PSSGNode childNode, PSSGNode parentNode)
        {
            if (childNode == null || parentNode == null)
                return false;

            // Проверяем прямое вхождение
            if (parentNode.Children.Contains(childNode))
                return true;

            // Рекурсивно проверяем все дочерние ноды
            foreach (var child in parentNode.Children)
            {
                if (IsNodeInSubtree(childNode, child))
                    return true;
            }

            return false;
        }

        #endregion

        #region Camera Control

        private void Model3DView_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.RightButton == MouseButtonState.Pressed)
            {
                isRotating3D = true;
                lastMouse3DPosition = e.GetPosition(Model3DView);
                Model3DView.CaptureMouse();
                e.Handled = true;
            }
        }

        private void Model3DView_MouseMove(object sender, MouseEventArgs e)
        {
            if (isRotating3D)
            {
                Point currentPosition = e.GetPosition(Model3DView);
                Vector delta = currentPosition - lastMouse3DPosition;

                double yaw = delta.X * 0.5;
                double pitch = delta.Y * 0.5;

                Rotate3DCamera(yaw, pitch);

                lastMouse3DPosition = currentPosition;
                e.Handled = true;
            }
        }

        private void Model3DView_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.RightButton == MouseButtonState.Released && isRotating3D)
            {
                isRotating3D = false;
                Model3DView.ReleaseMouseCapture();
                e.Handled = true;
            }
        }

        private void Model3DView_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            double zoomFactor = e.Delta > 0 ? 0.9 : 1.1;

            if (Model3DView.Camera is PerspectiveCamera perspectiveCamera)
            {
                perspectiveCamera.FieldOfView *= zoomFactor;
                perspectiveCamera.FieldOfView = Math.Max(10, Math.Min(90, perspectiveCamera.FieldOfView));
            }

            e.Handled = true;
        }

        private void Rotate3DCamera(double yaw, double pitch)
        {
            Vector3D lookDirection = Model3DView.Camera.LookDirection;
            Quaternion yawRotation = new Quaternion(camera3DUpDirection, yaw);

            Vector3D right = Vector3D.CrossProduct(lookDirection, camera3DUpDirection);
            right.Normalize();

            Quaternion pitchRotation = new Quaternion(right, pitch);
            Quaternion totalRotation = Quaternion.Multiply(yawRotation, pitchRotation);

            Matrix3D rotationMatrix = Matrix3D.Identity;
            rotationMatrix.Rotate(totalRotation);

            Vector3D newLookDirection = rotationMatrix.Transform(lookDirection);

            double upDot = Vector3D.DotProduct(newLookDirection, camera3DUpDirection);
            if (Math.Abs(upDot) > 0.999)
                return;

            Model3DView.Camera.LookDirection = newLookDirection;
            Model3DView.Camera.UpDirection = camera3DUpDirection;
        }

        #endregion
    }

    #region 3D Data Classes

    public class Pssg3DNode
    {
        public string Id { get; set; }
        public PSSGNode Node { get; set; }
        public string Type { get; set; }
    }

    public class Pssg3DGeometryBlock
    {
        public string Id { get; set; }
        public int ElementCount { get; set; }
        public int StreamCount { get; set; }
        public List<DataStream> Streams { get; set; } = new List<DataStream>();
        public PSSGNode Node { get; set; }
        public PSSGNode DataNode { get; set; }
    }

    public class Pssg3DRenderSource
    {
        public string Id { get; set; }
        public List<string> DataBlockIds { get; set; } = new List<string>();
        public PSSGNode Node { get; set; }
        public PSSGNode IndexSourceNode { get; set; }
    }

    public class Pssg3DShader
    {
        public string Id { get; set; }
        public string ShaderGroup { get; set; }
        public List<string> Textures { get; set; } = new List<string>();
    }

    /// <summary>
    /// Материал с полным путем к корню для трансформаций
    /// </summary>
    public class MaterialWithPath
    {
        public PSSGNode InstanceNode { get; set; }
        public string SourceId { get; set; }
        public string GeometryId { get; set; }
        public string ShaderId { get; set; }
        public List<PSSGNode> PathToRoot { get; set; }
        public Pssg3DRenderSource RenderSource { get; set; }
        public Pssg3DGeometryBlock GeometryBlock { get; set; }
    }

    #endregion
}