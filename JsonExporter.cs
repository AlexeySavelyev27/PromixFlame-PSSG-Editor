using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PSSGEditor
{
    /// <summary>
    /// Exports PSSG node tree to JSON format for analysis and sharing.
    /// </summary>
    public class JsonExporter
    {
        private readonly bool includeRawDataPreview;
        private readonly int maxRawDataPreviewBytes;
        private readonly int maxDepth;

        public JsonExporter(bool includeRawDataPreview = true, int maxRawDataPreviewBytes = 128, int maxDepth = -1)
        {
            this.includeRawDataPreview = includeRawDataPreview;
            this.maxRawDataPreviewBytes = maxRawDataPreviewBytes;
            this.maxDepth = maxDepth;
        }

        /// <summary>
        /// Exports a PSSG node and its children to JSON string.
        /// </summary>
        public string ExportToJson(PSSGNode node, bool indented = true)
        {
            var jsonNode = ConvertNode(node, 0);
            var options = new JsonSerializerOptions
            {
                WriteIndented = indented,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };
            return JsonSerializer.Serialize(jsonNode, options);
        }

        /// <summary>
        /// Exports a PSSG node to a JSON file.
        /// </summary>
        public void ExportToFile(PSSGNode node, string filePath)
        {
            var json = ExportToJson(node, indented: true);
            File.WriteAllText(filePath, json, Encoding.UTF8);
        }

        private JsonNode ConvertNode(PSSGNode node, int depth)
        {
            if (maxDepth >= 0 && depth > maxDepth)
            {
                return new JsonNode
                {
                    NodeName = node.Name,
                    Note = $"Max depth ({maxDepth}) reached"
                };
            }

            var jsonNode = new JsonNode
            {
                NodeName = node.Name,
                Attributes = new Dictionary<string, JsonAttribute>(),
                Children = new List<JsonNode>()
            };

            // Convert attributes
            foreach (var attr in node.Attributes)
            {
                var jsonAttr = ConvertAttribute(attr.Key, attr.Value);
                jsonNode.Attributes[attr.Key] = jsonAttr;
            }

            // Convert children
            if (node.Children != null && node.Children.Count > 0)
            {
                foreach (var child in node.Children)
                {
                    jsonNode.Children.Add(ConvertNode(child, depth + 1));
                }
                jsonNode.ChildrenCount = node.Children.Count;
            }
            else
            {
                jsonNode.Children = null;
            }

            // Handle raw data
            if (node.Data != null && node.Data.Length > 0)
            {
                jsonNode.RawDataSize = node.Data.Length;
                
                if (includeRawDataPreview)
                {
                    int previewSize = Math.Min(node.Data.Length, maxRawDataPreviewBytes);
                    jsonNode.RawDataPreview = BitConverter.ToString(node.Data, 0, previewSize).Replace("-", " ");
                    
                    if (node.Data.Length > previewSize)
                    {
                        jsonNode.RawDataPreview += " ...";
                    }
                }
            }

            return jsonNode;
        }

        private JsonAttribute ConvertAttribute(string name, byte[] data)
        {
            var attr = new JsonAttribute
            {
                RawHex = BitConverter.ToString(data).Replace("-", " "),
                ByteCount = data.Length
            };

            // Try to decode the value based on common patterns
            try
            {
                // String (length-prefixed UTF-8)
                if (data.Length >= 4)
                {
                    uint strLen = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(0, 4));
                    if (strLen == data.Length - 4 && strLen < 1024)
                    {
                        try
                        {
                            string str = Encoding.UTF8.GetString(data, 4, (int)strLen);
                            if (!string.IsNullOrEmpty(str) && str.All(c => c >= 32 || c == '\n' || c == '\r' || c == '\t'))
                            {
                                attr.DecodedAs = "string";
                                attr.Value = str;
                                return attr;
                            }
                        }
                        catch { }
                    }
                }

                // UInt32
                if (data.Length == 4)
                {
                    uint val = BinaryPrimitives.ReadUInt32BigEndian(data);
                    attr.DecodedAs = "uint32";
                    attr.Value = val;
                    return attr;
                }

                // UInt16
                if (data.Length == 2)
                {
                    ushort val = BinaryPrimitives.ReadUInt16BigEndian(data);
                    attr.DecodedAs = "uint16";
                    attr.Value = val;
                    return attr;
                }

                // UInt8
                if (data.Length == 1)
                {
                    attr.DecodedAs = "uint8";
                    attr.Value = data[0];
                    return attr;
                }

                // Float
                if (data.Length == 4)
                {
                    var floatBytes = new byte[4];
                    if (BitConverter.IsLittleEndian)
                    {
                        floatBytes[0] = data[3];
                        floatBytes[1] = data[2];
                        floatBytes[2] = data[1];
                        floatBytes[3] = data[0];
                    }
                    else
                    {
                        Array.Copy(data, floatBytes, 4);
                    }
                    float val = BitConverter.ToSingle(floatBytes, 0);
                    
                    // Check if it's a reasonable float value
                    if (!float.IsNaN(val) && !float.IsInfinity(val) && Math.Abs(val) < 1e10)
                    {
                        attr.AlternativeDecoding = new Dictionary<string, object>
                        {
                            ["float"] = Math.Round(val, 6)
                        };
                    }
                }

                // Float array (Transform, BoundingBox, etc.)
                if (data.Length % 4 == 0 && data.Length >= 8 && data.Length <= 256)
                {
                    int floatCount = data.Length / 4;
                    var floats = new List<float>();
                    bool allValid = true;

                    for (int i = 0; i < floatCount; i++)
                    {
                        var floatBytes = new byte[4];
                        int offset = i * 4;
                        if (BitConverter.IsLittleEndian)
                        {
                            floatBytes[0] = data[offset + 3];
                            floatBytes[1] = data[offset + 2];
                            floatBytes[2] = data[offset + 1];
                            floatBytes[3] = data[offset];
                        }
                        else
                        {
                            Array.Copy(data, offset, floatBytes, 0, 4);
                        }
                        float val = BitConverter.ToSingle(floatBytes, 0);
                        
                        if (float.IsNaN(val) || float.IsInfinity(val) || Math.Abs(val) > 1e10)
                        {
                            allValid = false;
                            break;
                        }
                        floats.Add(val);
                    }

                    if (allValid)
                    {
                        if (attr.AlternativeDecoding == null)
                            attr.AlternativeDecoding = new Dictionary<string, object>();
                        
                        attr.AlternativeDecoding[$"float[{floatCount}]"] = floats.Select(f => Math.Round(f, 6)).ToArray();

                        // Special cases
                        if (floatCount == 16 && (name == "Transform" || name.Contains("transform")))
                        {
                            attr.DecodedAs = "float[16] (Transform Matrix)";
                            attr.Value = floats.Select(f => Math.Round(f, 6)).ToArray();
                            return attr;
                        }
                        else if (floatCount == 6 && (name == "BoundingBox" || name.Contains("bound")))
                        {
                            attr.DecodedAs = "float[6] (BoundingBox)";
                            attr.Value = new
                            {
                                min = new { x = Math.Round(floats[0], 6), y = Math.Round(floats[1], 6), z = Math.Round(floats[2], 6) },
                                max = new { x = Math.Round(floats[3], 6), y = Math.Round(floats[4], 6), z = Math.Round(floats[5], 6) }
                            };
                            return attr;
                        }
                    }
                }

                // If nothing else worked, just use raw hex
                attr.DecodedAs = "raw";
            }
            catch
            {
                attr.DecodedAs = "raw";
            }

            return attr;
        }

        // JSON model classes
        public class JsonNode
        {
            [JsonPropertyName("nodeName")]
            public string NodeName { get; set; }

            [JsonPropertyName("attributes")]
            public Dictionary<string, JsonAttribute> Attributes { get; set; }

            [JsonPropertyName("childrenCount")]
            [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
            public int? ChildrenCount { get; set; }

            [JsonPropertyName("children")]
            [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
            public List<JsonNode> Children { get; set; }

            [JsonPropertyName("rawDataSize")]
            [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
            public int? RawDataSize { get; set; }

            [JsonPropertyName("rawDataPreview")]
            [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
            public string RawDataPreview { get; set; }

            [JsonPropertyName("note")]
            [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
            public string Note { get; set; }
        }

        public class JsonAttribute
        {
            [JsonPropertyName("decodedAs")]
            public string DecodedAs { get; set; }

            [JsonPropertyName("value")]
            [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
            public object Value { get; set; }

            [JsonPropertyName("rawHex")]
            public string RawHex { get; set; }

            [JsonPropertyName("byteCount")]
            public int ByteCount { get; set; }

            [JsonPropertyName("alternativeDecoding")]
            [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
            public Dictionary<string, object> AlternativeDecoding { get; set; }
        }
    }
}
