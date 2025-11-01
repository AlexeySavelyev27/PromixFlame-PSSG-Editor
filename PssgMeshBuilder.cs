using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace PSSGEditor
{
    /// <summary>
    /// Класс для построения 3D мешей из PSSG данных
    /// Адаптирован из PssgViewer для работы с нативными PSSG нодами
    /// </summary>
    public class PssgMeshBuilder
    {
        /// <summary>
        /// Парсинг геометрических данных из PSSG DATABLOCK ноды
        /// </summary>
        public static void ParseGeometryData(PSSGNode dataBlockNode,
                                       out Point3DCollection positions,
                                       out Vector3DCollection normals,
                                       out PointCollection texCoords)
        {
            positions = new Point3DCollection();
            normals = new Vector3DCollection();
            texCoords = new PointCollection();

            // Получаем метаданные блока
            int elementCount = GetAttributeIntValue(dataBlockNode, "elementCount", 0);
            int streamCount = GetAttributeIntValue(dataBlockNode, "streamCount", 0);

            if (elementCount == 0 || streamCount == 0)
            {
                Debug.WriteLine($"Invalid element count or stream count: {elementCount}, {streamCount}");
                GenerateTestVertices(positions, normals, texCoords);
                return;
            }

            // Ищем потоки данных
            var streams = ParseDataBlockStreams(dataBlockNode);
            var posStream = streams.FirstOrDefault(s => s.RenderType == "Vertex");
            var normStream = streams.FirstOrDefault(s => s.RenderType == "Normal");
            var texStream = streams.FirstOrDefault(s => s.RenderType == "ST");

            if (posStream == null)
            {
                Debug.WriteLine("No position stream found");
                GenerateTestVertices(positions, normals, texCoords);
                return;
            }

            // Находим DATABLOCKDATA ноду
            var dataNode = FindChildByName(dataBlockNode, "DATABLOCKDATA");
            if (dataNode == null || dataNode.Data == null || dataNode.Data.Length == 0)
            {
                Debug.WriteLine("No DATABLOCKDATA found or it's empty");
                GenerateTestVertices(positions, normals, texCoords);
                return;
            }

            byte[] binaryData = dataNode.Data;

            // Обрабатываем каждый вертекс
            int stride = posStream.Stride;
            for (int i = 0; i < elementCount; i++)
            {
                int baseOffset = i * stride;
                if (baseOffset + stride > binaryData.Length) break;

                try
                {
                    // Извлекаем позицию
                    if (posStream != null && baseOffset + posStream.Offset + 12 <= binaryData.Length)
                    {
                        Vector3 pos = ReadVector3BigEndian(binaryData, baseOffset + posStream.Offset);
                        positions.Add(new Point3D(pos.X, pos.Y, pos.Z));
                    }

                    // Извлекаем нормаль
                    if (normStream != null && baseOffset + normStream.Offset + 12 <= binaryData.Length)
                    {
                        Vector3 norm = ReadVector3BigEndian(binaryData, baseOffset + normStream.Offset);
                        normals.Add(new Vector3D(norm.X, norm.Y, norm.Z));
                    }

                    // Извлекаем текстурные координаты
                    if (texStream != null && baseOffset + texStream.Offset + 4 <= binaryData.Length)
                    {
                        Vector2 tex;
                        if (texStream.DataType == "half2")
                            tex = ReadVector2HalfBigEndian(binaryData, baseOffset + texStream.Offset);
                        else
                            tex = ReadVector2BigEndian(binaryData, baseOffset + texStream.Offset);

                        texCoords.Add(new Point(tex.X, tex.Y));
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error processing vertex {i}: {ex.Message}");
                }
            }

            if (positions.Count == 0)
                GenerateTestVertices(positions, normals, texCoords);
        }

        /// <summary>
        /// Парсинг индексов из PSSG INDEXSOURCEDATA ноды
        /// </summary>
        public static Int32Collection ParseIndices(PSSGNode indexSourceNode)
        {
            Int32Collection indices = new Int32Collection();

            if (indexSourceNode == null) return indices;

            // Получаем формат и количество
            string format = GetAttributeValue(indexSourceNode, "format", "ushort");
            int count = GetAttributeIntValue(indexSourceNode, "count", 0);

            // Находим INDEXSOURCEDATA
            var dataNode = FindChildByName(indexSourceNode, "INDEXSOURCEDATA");
            if (dataNode == null || dataNode.Data == null || dataNode.Data.Length == 0)
            {
                GenerateTestIndices(indices);
                return indices;
            }

            try
            {
                byte[] binaryData = dataNode.Data;
                int stride = format == "ushort" ? 2 : 4;

                // Парсинг индексов
                for (int i = 0; i < binaryData.Length - stride + 1; i += stride)
                {
                    int index;
                    if (format == "ushort")
                        index = BinaryPrimitives.ReadUInt16BigEndian(binaryData.AsSpan(i));
                    else
                        index = (int)BinaryPrimitives.ReadUInt32BigEndian(binaryData.AsSpan(i));

                    indices.Add(index);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error parsing indices: {ex.Message}");
                GenerateTestIndices(indices);
            }

            return indices;
        }

        /// <summary>
        /// Парсинг трансформации из PSSG TRANSFORM ноды
        /// </summary>
        public static Transform3D ParseTransform(PSSGNode transformNode)
        {
            if (transformNode == null || transformNode.Data == null || transformNode.Data.Length < 64)
                return new MatrixTransform3D(Matrix3D.Identity);

            try
            {
                byte[] binaryData = transformNode.Data;
                float[] matrixValues = new float[16];
                bool validMatrix = true;

                for (int i = 0; i < 16; i++)
                {
                    float value = BinaryPrimitives.ReadSingleBigEndian(binaryData.AsSpan(i * 4));
                    if (float.IsNaN(value) || float.IsInfinity(value) || Math.Abs(value) > 10000)
                    {
                        validMatrix = false;
                        break;
                    }
                    matrixValues[i] = value;
                }

                if (validMatrix)
                {
                    Matrix3D matrix = new Matrix3D(
                        matrixValues[0], matrixValues[1], matrixValues[2], matrixValues[3],
                        matrixValues[4], matrixValues[5], matrixValues[6], matrixValues[7],
                        matrixValues[8], matrixValues[9], matrixValues[10], matrixValues[11],
                        matrixValues[12], matrixValues[13], matrixValues[14], matrixValues[15]
                    );

                    if (matrix.HasInverse)
                        return new MatrixTransform3D(matrix);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error parsing transform: {ex.Message}");
            }

            return new MatrixTransform3D(Matrix3D.Identity);
        }

        #region Вспомогательные методы

        private static List<DataStreamInfo> ParseDataBlockStreams(PSSGNode dataBlockNode)
        {
            var streams = new List<DataStreamInfo>();

            foreach (var child in dataBlockNode.Children)
            {
                if (child.Name == "DATABLOCKSTREAM")
                {
                    streams.Add(new DataStreamInfo
                    {
                        RenderType = GetAttributeValue(child, "renderType", ""),
                        DataType = GetAttributeValue(child, "dataType", ""),
                        Offset = GetAttributeIntValue(child, "offset", 0),
                        Stride = GetAttributeIntValue(child, "stride", 0)
                    });
                }
            }

            return streams;
        }

        private static string GetAttributeValue(PSSGNode node, string attrName, string defaultValue)
        {
            if (node.Attributes.TryGetValue(attrName, out var attrBytes))
            {
                return PSSGFormat.DecodeString(attrBytes);
            }
            return defaultValue;
        }

        private static int GetAttributeIntValue(PSSGNode node, string attrName, int defaultValue)
        {
            if (node.Attributes.TryGetValue(attrName, out var attrBytes))
            {
                if (attrBytes != null && attrBytes.Length >= 4)
                {
                    return (int)PSSGFormat.ReadUInt32(attrBytes);
                }
            }
            return defaultValue;
        }

        private static PSSGNode FindChildByName(PSSGNode parent, string name)
        {
            return parent.Children.FirstOrDefault(c => c.Name == name);
        }

        private static void GenerateTestVertices(Point3DCollection positions,
                                            Vector3DCollection normals,
                                            PointCollection texCoords)
        {
            // Создаём куб для тестирования
            positions.Add(new Point3D(-1, -1, 1));
            positions.Add(new Point3D(1, -1, 1));
            positions.Add(new Point3D(1, 1, 1));
            positions.Add(new Point3D(-1, 1, 1));
            positions.Add(new Point3D(-1, -1, -1));
            positions.Add(new Point3D(1, -1, -1));
            positions.Add(new Point3D(1, 1, -1));
            positions.Add(new Point3D(-1, 1, -1));

            for (int i = 0; i < 8; i++)
            {
                normals.Add(new Vector3D(0, 0, 1));
                texCoords.Add(new Point(0, 0));
            }
        }

        private static void GenerateTestIndices(Int32Collection indices)
        {
            // Индексы куба
            indices.Add(0); indices.Add(1); indices.Add(2);
            indices.Add(0); indices.Add(2); indices.Add(3);
            indices.Add(4); indices.Add(7); indices.Add(6);
            indices.Add(4); indices.Add(6); indices.Add(5);
            indices.Add(3); indices.Add(2); indices.Add(6);
            indices.Add(3); indices.Add(6); indices.Add(7);
            indices.Add(0); indices.Add(4); indices.Add(5);
            indices.Add(0); indices.Add(5); indices.Add(1);
            indices.Add(0); indices.Add(3); indices.Add(7);
            indices.Add(0); indices.Add(7); indices.Add(4);
            indices.Add(1); indices.Add(5); indices.Add(6);
            indices.Add(1); indices.Add(6); indices.Add(2);
        }

        #endregion

        #region Binary Reading Methods

        private static Vector3 ReadVector3BigEndian(byte[] data, int offset)
        {
            if (offset + 12 > data.Length)
                return new Vector3();

            return new Vector3(
                BinaryPrimitives.ReadSingleBigEndian(data.AsSpan(offset)),
                BinaryPrimitives.ReadSingleBigEndian(data.AsSpan(offset + 4)),
                BinaryPrimitives.ReadSingleBigEndian(data.AsSpan(offset + 8))
            );
        }

        private static Vector2 ReadVector2BigEndian(byte[] data, int offset)
        {
            if (offset + 8 > data.Length)
                return new Vector2();

            return new Vector2(
                BinaryPrimitives.ReadSingleBigEndian(data.AsSpan(offset)),
                BinaryPrimitives.ReadSingleBigEndian(data.AsSpan(offset + 4))
            );
        }

        private static Vector2 ReadVector2HalfBigEndian(byte[] data, int offset)
        {
            if (offset + 4 > data.Length)
                return new Vector2();

            return new Vector2(
                HalfToFloat(BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(offset))),
                HalfToFloat(BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(offset + 2)))
            );
        }

        private static float HalfToFloat(ushort half)
        {
            int sign = (half >> 15) & 0x1;
            int exp = (half >> 10) & 0x1F;
            int mantissa = half & 0x3FF;

            if (exp == 0)
            {
                if (mantissa == 0)
                    return sign == 1 ? -0.0f : 0.0f;

                float num = (float)mantissa / 1024.0f * (float)Math.Pow(2, -14);
                return sign == 1 ? -num : num;
            }
            else if (exp == 31)
            {
                if (mantissa == 0)
                    return sign == 1 ? float.NegativeInfinity : float.PositiveInfinity;
                else
                    return float.NaN;
            }

            float value = (1.0f + (float)mantissa / 1024.0f) * (float)Math.Pow(2, exp - 15);
            return sign == 1 ? -value : value;
        }

        #endregion

        private class DataStreamInfo
        {
            public string RenderType { get; set; }
            public string DataType { get; set; }
            public int Offset { get; set; }
            public int Stride { get; set; }
        }
    }
}
