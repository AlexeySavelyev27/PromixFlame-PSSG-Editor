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
        private Dictionary<string, string> renderSourceMap = new Dictionary<string, string>();

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
                // Это служебные узлы типа "materials", "textures"
                Show3DTagDetails(tagString);
                return;
            }

            if (item.Tag is Pssg3DNode node)
            {
                // Если это render node с материалами - рендерим 3D
                if (node.IsRenderNode)
                {
                    Model3DView.Visibility = Visibility.Visible;
                    ViewHelpPanel.Visibility = Visibility.Visible;
                    ModelInfoPanel.Visibility = Visibility.Visible;
                    No3DObjectText.Visibility = Visibility.Collapsed;
                    
                    Render3DObject(node);
                }
                else
                {
                    // Иначе показываем информацию о ноде
                    Show3DNodeDetails(node);
                }
            }
            else if (item.Tag is Pssg3DMaterial material)
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
        /// Построение дерева 3D объектов из PSSG структуры (универсальная версия)
        /// </summary>
        public void Build3DObjectsTree()
        {
            Objects3DTreeView.Items.Clear();
            geometry3DBlocks.Clear();
            renderSources.Clear();
            shaders3D.Clear();
            renderSourceMap.Clear();

            if (rootNode == null)
                return;

            try
            {
                StatusText.Text = "Building 3D objects tree...";

                // Шаг 1: Парсим шейдеры
                ParseShaders(rootNode);

                // Шаг 2: Собираем все DATABLOCK ноды (геометрия)
                Parse3DGeometryBlocks(rootNode);

                // Шаг 3: Собираем RENDERDATASOURCE ноды и строим карту
                Parse3DRenderSources(rootNode);

                // Шаг 4: Строим дерево объектов - добавляем напрямую в TreeView
                var rootNodes = FindAllNodesByName(rootNode, "ROOTNODE");
                var objectItems = new List<TreeViewItem>();

                foreach (var rootNodeItem in rootNodes)
                {
                    string id = GetAttributeValue(rootNodeItem, "id", "");
                    if (string.IsNullOrEmpty(id)) continue;

                    // Получаем имя объекта (убираем " Root" из конца)
                    string objectName = id;
                    if (objectName.EndsWith(" Root"))
                        objectName = objectName.Substring(0, objectName.Length - 5);

                    // Создаем узел дерева
                    var objectItem = new TreeViewItem
                    {
                        Header = objectName,
                        Tag = new Pssg3DNode
                        {
                            Id = id,
                            Node = rootNodeItem,
                            IsRenderNode = false,
                            Type = "ROOTNODE"
                        },
                        IsExpanded = false
                    };

                    // Рекурсивно обрабатываем дочерние узлы
                    ProcessNode(rootNodeItem, objectItem);
                    objectItems.Add(objectItem);
                }

                // Сортируем по имени и добавляем напрямую в TreeView
                objectItems.Sort((a, b) => string.Compare(a.Header.ToString(), b.Header.ToString(), StringComparison.OrdinalIgnoreCase));
                foreach (var item in objectItems)
                    Objects3DTreeView.Items.Add(item);

                StatusText.Text = $"Found {geometry3DBlocks.Count} geometry blocks, {shaders3D.Count} shaders, {renderSources.Count} render sources";
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

                // Найдем RENDERSTREAM child nodes и создадим карту
                foreach (var child in node.Children)
                {
                    if (child.Name == "RENDERSTREAM")
                    {
                        string streamId = GetAttributeValue(child, "id", "");
                        string blockRef = GetAttributeValue(child, "dataBlock", "");
                        blockRef = blockRef.TrimStart('#');
                        
                        if (!string.IsNullOrEmpty(blockRef))
                            source.DataBlockIds.Add(blockRef);
                        
                        // Создаем карту stream -> datablock
                        if (!string.IsNullOrEmpty(streamId) && !string.IsNullOrEmpty(blockRef))
                            renderSourceMap[streamId] = blockRef;
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
        /// Проверяет является ли нода render instance (по атрибутам, а не по имени)
        /// </summary>
        private bool IsRenderInstance(PSSGNode node)
        {
            // Проверяем наличие shader атрибута
            if (node.Attributes.ContainsKey("shader"))
                return true;
            
            // Проверяем наличие RENDERINSTANCESOURCE child
            if (node.Children.Any(c => c.Name == "RENDERINSTANCESOURCE"))
                return true;
                
            return false;
        }

        /// <summary>
        /// Проверяет является ли нода группирующим узлом (контейнером)
        /// </summary>
        private bool IsGroupNode(PSSGNode node)
        {
            // Проверяем наличие id атрибута и дочерних нод
            if (!node.Attributes.ContainsKey("id"))
                return false;

            // Проверяем что есть дочерние элементы
            if (node.Children.Count == 0)
                return false;

            // Игнорируем некоторые специфичные ноды
            if (node.Name == "SHADERINSTANCE" || node.Name == "TEXTURE" || 
                node.Name == "DATABLOCK" || node.Name == "RENDERDATASOURCE")
                return false;

            return true;
        }

        /// <summary>
        /// Рекурсивная обработка узлов дерева (универсальная версия)
        /// </summary>
        private void ProcessNode(PSSGNode pssgNode, TreeViewItem parentItem)
        {
            foreach (var child in pssgNode.Children)
            {
                // Проверяем является ли нода render instance
                bool isRenderInstance = IsRenderInstance(child);
                
                // Проверяем является ли нода группирующим узлом
                bool isGroupNode = IsGroupNode(child);

                if (!isRenderInstance && !isGroupNode)
                    continue;

                string id = GetAttributeValue(child, "id", "");
                string nickname = GetAttributeValue(child, "nickname", "");

                if (string.IsNullOrEmpty(id)) continue;

                string displayName = !string.IsNullOrEmpty(nickname) ? nickname : id;

                // Создаем узел дерева
                var nodeItem = new TreeViewItem
                {
                    Header = displayName,
                    Tag = new Pssg3DNode
                    {
                        Id = id,
                        Node = child,
                        IsRenderNode = isRenderInstance,
                        Type = child.Name
                    },
                    IsExpanded = false
                };

                // Устанавливаем цвет для render nodes
                if (isRenderInstance)
                {
                    nodeItem.Foreground = new SolidColorBrush(Color.FromRgb(0, 102, 204)); // #0066cc
                }

                parentItem.Items.Add(nodeItem);

                // Если это render node, добавляем материалы
                if (isRenderInstance)
                {
                    // Добавляем узел "Materials"
                    var materialsItem = new TreeViewItem
                    {
                        Header = "Materials",
                        Tag = "materials",
                        IsExpanded = false,
                        Foreground = new SolidColorBrush(Color.FromRgb(0, 51, 102)),
                        FontWeight = FontWeights.Bold
                    };
                    nodeItem.Items.Add(materialsItem);

                    // Добавляем материалы
                    AddMaterialsToNode(child, materialsItem);

                    // Проверяем LOD уровни
                    var lodInstancesNode = FindChildByName(child, "LODRENDERINSTANCES");
                    if (lodInstancesNode != null)
                    {
                        var lodLevels = lodInstancesNode.Children.Where(c => c.Name == "LODRENDERINSTANCELIST").ToList();
                        
                        for (int i = 0; i < lodLevels.Count; i++)
                        {
                            var lodLevel = lodLevels[i];
                            string lodValue = GetAttributeValue(lodLevel, "lod", $"{i}");

                            var lodLevelItem = new TreeViewItem
                            {
                                Header = $"lod{i + 1} ({lodValue})",
                                Tag = new Pssg3DNode
                                {
                                    Id = $"{id}_lod{i + 1}",
                                    Node = lodLevel,
                                    IsRenderNode = true,
                                    Type = "LOD_LEVEL",
                                    LodValue = lodValue
                                },
                                IsExpanded = false
                            };
                            nodeItem.Items.Add(lodLevelItem);

                            // Добавляем материалы для LOD уровня
                            var lodMaterialsItem = new TreeViewItem
                            {
                                Header = "Materials",
                                Tag = "materials",
                                IsExpanded = false,
                                Foreground = new SolidColorBrush(Color.FromRgb(0, 51, 102)),
                                FontWeight = FontWeights.Bold
                            };
                            lodLevelItem.Items.Add(lodMaterialsItem);
                            AddMaterialsToNode(lodLevel, lodMaterialsItem);
                        }
                    }
                }
                else if (isGroupNode)
                {
                    // Рекурсивно обрабатываем дочерние узлы группы
                    ProcessNode(child, nodeItem);
                }
            }
        }

        /// <summary>
        /// Добавление материалов к узлу (универсальная версия)
        /// </summary>
        private void AddMaterialsToNode(PSSGNode node, TreeViewItem materialsItem)
        {
            // Ищем все дочерние ноды которые являются render instances
            var renderInstances = node.Children.Where(c => IsRenderInstance(c)).ToList();
            
            if (renderInstances.Count == 0) return;

            // Отслеживаем уже обработанные материалы
            var processedMaterials = new Dictionary<string, bool>();

            foreach (var instance in renderInstances)
            {
                string shaderId = GetAttributeValue(instance, "shader", "").TrimStart('#');
                if (string.IsNullOrEmpty(shaderId)) continue;

                // Пропускаем дубликаты
                if (processedMaterials.ContainsKey(shaderId))
                    continue;

                processedMaterials[shaderId] = true;

                // Находим source reference
                var sourceRef = FindChildByName(instance, "RENDERINSTANCESOURCE");
                if (sourceRef == null) continue;

                string sourceId = GetAttributeValue(sourceRef, "source", "").TrimStart('#');
                if (string.IsNullOrEmpty(sourceId)) continue;

                // Находим geometry block для этого source
                string geometryId = FindGeometryForSource(sourceId);
                if (string.IsNullOrEmpty(geometryId)) continue;

                // Создаем узел материала
                var material = new Pssg3DMaterial
                {
                    ShaderId = shaderId,
                    GeometryId = geometryId,
                    SourceId = sourceId,
                    InstanceNode = instance,
                    ParentNode = node
                };

                var materialItem = new TreeViewItem
                {
                    Header = shaderId,
                    Tag = material,
                    IsExpanded = false
                };
                materialsItem.Items.Add(materialItem);

                // Добавляем текстуры если доступны
                if (shaders3D.TryGetValue(shaderId, out var shader) && shader.Textures.Count > 0)
                {
                    var texturesItem = new TreeViewItem
                    {
                        Header = "Textures",
                        Tag = "textures",
                        IsExpanded = false,
                        Foreground = new SolidColorBrush(Color.FromRgb(0, 102, 0))
                    };
                    materialItem.Items.Add(texturesItem);

                    foreach (string texture in shader.Textures)
                    {
                        texturesItem.Items.Add(new TreeViewItem
                        {
                            Header = texture,
                            Tag = texture
                        });
                    }
                }
            }
        }

        /// <summary>
        /// Поиск geometry block для source
        /// </summary>
        private string FindGeometryForSource(string sourceId)
        {
            if (string.IsNullOrEmpty(sourceId)) return null;

            sourceId = sourceId.TrimStart('#');

            // Прямое совпадение
            if (renderSourceMap.TryGetValue(sourceId, out string value))
                return value;

            // Поиск по началу ключа
            var possibleMatch = renderSourceMap.FirstOrDefault(entry => entry.Key.StartsWith(sourceId + "_"));
            if (!string.IsNullOrEmpty(possibleMatch.Key))
                return possibleMatch.Value;

            // Поиск по подстроке
            foreach (var entry in renderSourceMap)
            {
                string[] parts = entry.Key.Split('_');
                if (parts.Contains(sourceId))
                    return entry.Value;
            }

            // Поиск в renderSources напрямую
            if (renderSources.TryGetValue(sourceId, out var renderSource))
            {
                if (renderSource.DataBlockIds.Count > 0)
                    return renderSource.DataBlockIds[0];
            }

            return null;
        }

        /// <summary>
        /// Показать информацию о служебном узле (materials, textures)
        /// </summary>
        private void Show3DTagDetails(string tag)
        {
            Model3DView.Visibility = Visibility.Collapsed;
            ViewHelpPanel.Visibility = Visibility.Collapsed;
            ModelInfoPanel.Visibility = Visibility.Visible;
            No3DObjectText.Visibility = Visibility.Collapsed;

            switch (tag)
            {
                case "materials":
                    ModelInfoText.Text = "Materials Section\n";
                    ModelInfoText.Text += "Contains a list of material shaders used by this model.";
                    break;
                case "textures":
                    ModelInfoText.Text = "Textures Section\n";
                    ModelInfoText.Text += "Contains a list of textures used by this material.";
                    break;
                default:
                    if (tag != null && tag.Contains(".tga", StringComparison.OrdinalIgnoreCase))
                    {
                        ModelInfoText.Text = $"Texture: {tag}\n";
                        ModelInfoText.Text += "Textures are used to provide surface details for 3D models.";
                    }
                    else
                    {
                        ModelInfoText.Text = "Select an item to view details";
                    }
                    break;
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
                    // Пробуем прочитать как текст
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
                    // Если не получилось прочитать как текст, просто скажем что есть трансформация
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

            // Показываем LOD информацию если есть
            if (node.Type == "LOD_LEVEL" && !string.IsNullOrEmpty(node.LodValue))
            {
                ModelInfoText.Text += $"\nLOD Value: {node.LodValue}\n";
                ModelInfoText.Text += "LOD (Level of Detail) is used to show different mesh detail\n";
                ModelInfoText.Text += "levels based on distance from the camera.\n";
                ModelInfoText.Text += "Lower values indicate models used at greater distances.\n";
            }
        }

        /// <summary>
        /// Отрисовка 3D объекта (render node)
        /// </summary>
        private void Render3DObject(Pssg3DNode node)
        {
            try
            {
                StatusText.Text = $"Rendering 3D object: {node.Id}...";

                int totalVertices = 0;
                int totalTriangles = 0;

                ModelInfoText.Text = $"{node.Id}\n";
                ModelInfoText.Text += $"Type: {node.Type}\n";

                // Собираем материалы
                var materials = new List<Pssg3DMaterial>();
                CollectMaterials(node.Node, materials);

                if (materials.Count == 0)
                {
                    ModelInfoText.Text += "No materials found.";
                    StatusText.Text = "No geometry data found";
                    return;
                }

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

                // Добавляем систему координат
                Model3DContainer.Children.Add(new CoordinateSystemVisual3D { ArrowLengths = 2 });

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
        /// Отрисовка отдельного материала
        /// </summary>
        private void RenderMaterial(Pssg3DMaterial material)
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
                    if (shaders3D.TryGetValue(material.ShaderId, out var shader) && shader.Textures.Count > 0)
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

                // Добавляем систему координат
                Model3DContainer.Children.Add(new CoordinateSystemVisual3D { ArrowLengths = 2 });

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
        /// Сбор материалов из ноды (универсальная версия)
        /// </summary>
        private void CollectMaterials(PSSGNode node, List<Pssg3DMaterial> materials)
        {
            // Ищем все дочерние ноды которые являются render instances
            foreach (var child in node.Children)
            {
                if (IsRenderInstance(child))
                {
                    var material = new Pssg3DMaterial
                    {
                        InstanceNode = child,
                        ParentNode = node
                    };

                    material.ShaderId = GetAttributeValue(child, "shader", "").TrimStart('#');

                    // Находим source reference
                    var sourceRef = FindChildByName(child, "RENDERINSTANCESOURCE");
                    if (sourceRef != null)
                    {
                        string sourceId = GetAttributeValue(sourceRef, "source", "").TrimStart('#');
                        material.SourceId = sourceId;

                        // Находим geometry block
                        string geometryId = FindGeometryForSource(sourceId);
                        if (!string.IsNullOrEmpty(geometryId) && geometry3DBlocks.TryGetValue(geometryId, out var block))
                        {
                            material.GeometryId = geometryId;
                            material.GeometryBlock = block;
                            
                            // Находим render source
                            if (renderSources.TryGetValue(sourceId, out var renderSource))
                                material.RenderSource = renderSource;

                            materials.Add(material);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Создание 3D mesh из материала
        /// </summary>
        private Model3DGroup Create3DMeshFromMaterial(Pssg3DMaterial material)
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
                var transform = Find3DTransform(material.ParentNode);
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
        /// Поиск трансформации в ноде
        /// </summary>
        private Transform3D Find3DTransform(PSSGNode node)
        {
            if (node == null) return null;

            var transformNode = FindChildByName(node, "TRANSFORM");
            if (transformNode != null)
                return PssgMeshBuilder.ParseTransform(transformNode);

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
        public bool IsRenderNode { get; set; }
        public string Type { get; set; }
        public string LodValue { get; set; }
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

    public class Pssg3DMaterial
    {
        public string ShaderId { get; set; }
        public string GeometryId { get; set; }
        public string SourceId { get; set; }
        public Pssg3DGeometryBlock GeometryBlock { get; set; }
        public Pssg3DRenderSource RenderSource { get; set; }
        public PSSGNode InstanceNode { get; set; }
        public PSSGNode ParentNode { get; set; }
    }

    public class Pssg3DShader
    {
        public string Id { get; set; }
        public string ShaderGroup { get; set; }
        public List<string> Textures { get; set; } = new List<string>();
    }

    #endregion
}
