using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Framework.BehaviourTree
{
    /// <summary>
    /// 行为树配置 JSON 导入导出。
    /// 使用 Newtonsoft.Json 而不是 Unity <c>JsonUtility</c>：客户端、服务端和纯 .NET 测试解析同一份 <c>.bt.json</c> 时走同一套代码，
    /// 浮点参数逐位一致。字段名、枚举按整数、缺省字段取初始值，这些都与 <c>JsonUtility</c> 输出的格式兼容。
    /// </summary>
    public static class BtTreeSerializer
    {
        static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            MissingMemberHandling = MissingMemberHandling.Ignore,
            ObjectCreationHandling = ObjectCreationHandling.Replace,
            NullValueHandling = NullValueHandling.Include,
        };

        /// <summary>
        /// 将定义序列化为 JSON 字符串。
        /// </summary>
        /// <param name="definition">树定义；不可为 null。</param>
        /// <param name="prettyPrint">是否格式化缩进。</param>
        /// <returns>JSON 文本。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="definition"/> 为 null。</exception>
        public static string ToJson(BtTreeDefinition definition, bool prettyPrint = true)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            definition.Version = BtTreeDefinition.CurrentVersion;
            return JsonConvert.SerializeObject(definition, prettyPrint ? Formatting.Indented : Formatting.None, Settings);
        }

        /// <summary>
        /// 从 JSON 反序列化树定义，并把旧版本格式迁移到 <see cref="BtTreeDefinition.CurrentVersion"/>。
        /// </summary>
        /// <param name="json">JSON 文本；不可为 null 或空。</param>
        /// <returns>树定义。</returns>
        /// <exception cref="ArgumentException"><paramref name="json"/> 为空或无法解析。</exception>
        public static BtTreeDefinition FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new ArgumentException("JSON is empty.", nameof(json));
            }

            BtTreeDefinition definition;
            try
            {
                definition = JsonConvert.DeserializeObject<BtTreeDefinition>(json, Settings);
            }
            catch (JsonException ex)
            {
                throw new ArgumentException("Failed to parse behaviour tree JSON: " + ex.Message, nameof(json), ex);
            }

            if (definition == null)
            {
                throw new ArgumentException("Failed to parse behaviour tree JSON.", nameof(json));
            }

            if (definition.Nodes == null)
            {
                definition.Nodes = new List<BtConfigNode>();
            }

            Migrate(definition);
            return definition;
        }

        static void Migrate(BtTreeDefinition definition)
        {
            if (definition.Version >= BtTreeDefinition.CurrentVersion)
            {
                return;
            }

            if (definition.Version < 2)
            {
                for (var i = 0; i < definition.Nodes.Count; i++)
                {
                    var node = definition.Nodes[i];
                    if (node == null)
                    {
                        continue;
                    }

                    node.FailFast = true;
                    node.SucceedFast = true;
                    if (node.Kind == BtNodeKind.WaitTime ||
                        node.Kind == BtNodeKind.Cooldown ||
                        node.Kind == BtNodeKind.Timeout ||
                        node.Kind == BtNodeKind.TimeLimit)
                    {
                        node.SetDurationSeconds(node.FloatParam);
                    }
                }
            }

            definition.Version = BtTreeDefinition.CurrentVersion;
        }
    }
}
