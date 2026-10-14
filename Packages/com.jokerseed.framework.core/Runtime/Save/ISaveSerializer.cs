using System;
using System.Text;
using UnityEngine;

namespace Framework.Save
{
    /// <summary>存档序列化器：对象与字节互转。</summary>
    public interface ISaveSerializer
    {
        /// <summary>序列化。</summary>
        /// <typeparam name="T">数据类型。</typeparam>
        /// <param name="value">数据。</param>
        /// <returns>字节内容。</returns>
        byte[] Serialize<T>(T value);

        /// <summary>反序列化。</summary>
        /// <typeparam name="T">数据类型。</typeparam>
        /// <param name="data">字节内容。</param>
        /// <returns>数据。</returns>
        /// <exception cref="FormatException">内容无法解析为 <typeparamref name="T"/>。</exception>
        T Deserialize<T>(byte[] data);
    }

    /// <summary>
    /// 基于 <see cref="JsonUtility"/> 的 UTF-8 JSON 序列化器。
    /// 受 JsonUtility 限制：类型须标 <see cref="SerializableAttribute"/>，不支持字典与多态；需要时换成自定义实现。
    /// </summary>
    public sealed class JsonUtilitySaveSerializer : ISaveSerializer
    {
        readonly bool _prettyPrint;

        /// <summary>创建序列化器。</summary>
        /// <param name="prettyPrint">是否缩进输出，便于人工查看。</param>
        public JsonUtilitySaveSerializer(bool prettyPrint = false)
        {
            _prettyPrint = prettyPrint;
        }

        /// <inheritdoc />
        public byte[] Serialize<T>(T value) => Encoding.UTF8.GetBytes(JsonUtility.ToJson(value, _prettyPrint));

        /// <inheritdoc />
        public T Deserialize<T>(byte[] data)
        {
            try
            {
                return JsonUtility.FromJson<T>(Encoding.UTF8.GetString(data));
            }
            catch (ArgumentException ex)
            {
                throw new FormatException($"Save data is not valid JSON for {typeof(T).Name}.", ex);
            }
        }
    }
}
