using System;
using System.Collections.Generic;

namespace Framework.GAS.Tags
{
    /// <summary>轻量级标签值类型，用字符串名称唯一标识一个 GameplayTag。</summary>
    public readonly struct GameplayTag : IEquatable<GameplayTag>
    {
        /// <summary>标签名称（点号分隔层级，如 <c>State.CrowdControl.Stunned</c>）。</summary>
        public string Name { get; }

        /// <summary>使用标签名称构造实例；name 为 null 时等同于空标签。</summary>
        /// <param name="name">标签名称；可为 null（将被视为空字符串）。</param>
        public GameplayTag(string name) => Name = name ?? string.Empty;

        /// <summary>标签是否有效（名称非空）。</summary>
        public bool IsValid => !string.IsNullOrEmpty(Name);

        /// <summary>比较两个标签是否相等（按名称）。</summary>
        /// <param name="other">要比较的标签。</param>
        /// <returns>名称相同则返回 true。</returns>
        public bool Equals(GameplayTag other) => Name == other.Name;

        /// <inheritdoc/>
        public override bool Equals(object obj) => obj is GameplayTag other && Equals(other);

        /// <inheritdoc/>
        public override int GetHashCode() => Name?.GetHashCode() ?? 0;

        /// <inheritdoc/>
        public override string ToString() => Name;

        /// <summary>判断两个标签是否相等。</summary>
        /// <param name="left">左操作数。</param>
        /// <param name="right">右操作数。</param>
        /// <returns>名称相同时返回 <see langword="true"/>。</returns>
        public static bool operator ==(GameplayTag left, GameplayTag right) => left.Equals(right);

        /// <summary>判断两个标签是否不等。</summary>
        /// <param name="left">左操作数。</param>
        /// <param name="right">右操作数。</param>
        /// <returns>名称不同时返回 <see langword="true"/>。</returns>
        public static bool operator !=(GameplayTag left, GameplayTag right) => !left.Equals(right);
    }

    /// <summary>支持层级匹配：拥有 State.CrowdControl.Stunned 则匹配 State.CrowdControl。</summary>
    public static class GameplayTagMatcher
    {
        /// <summary>判断 <paramref name="owned"/> 是否匹配查询标签 <paramref name="query"/>（支持前缀层级匹配）。</summary>
        /// <param name="owned">已持有的标签。</param>
        /// <param name="query">查询标签；若 owned 以 query 为前缀（含相等）则匹配。</param>
        /// <returns>匹配则返回 true；任一标签无效则返回 false。</returns>
        public static bool Matches(GameplayTag owned, GameplayTag query)
        {
            if (!owned.IsValid || !query.IsValid)
            {
                return false;
            }

            if (owned.Name == query.Name)
            {
                return true;
            }

            return owned.Name.StartsWith(query.Name + ".", StringComparison.Ordinal);
        }
    }

    /// <summary>GameplayTag 容器，管理单个单位持有的所有标签，支持层级查询与引用计数。</summary>
    public sealed class GameplayTagContainer
    {
        readonly Dictionary<string, int> _counts = new Dictionary<string, int>();

        /// <summary>当前持有的所有标签名称集合（只读视图，计数大于 0 的键）。</summary>
        public IReadOnlyCollection<string> Tags => _counts.Keys;

        /// <summary>检查容器是否持有与 <paramref name="tag"/> 匹配的标签（支持层级前缀匹配）。</summary>
        /// <param name="tag">要查询的标签；无效标签始终返回 false。</param>
        /// <returns>若持有匹配标签则返回 true。</returns>
        public bool HasTag(GameplayTag tag)
        {
            if (!tag.IsValid)
            {
                return false;
            }

            foreach (var owned in _counts.Keys)
            {
                if (GameplayTagMatcher.Matches(new GameplayTag(owned), tag))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>检查容器是否持有 <paramref name="tags"/> 中任意一个标签（OR 语义）。</summary>
        /// <param name="tags">要查询的标签集合；为空时返回 false。</param>
        /// <returns>持有任意一个则返回 true。</returns>
        public bool HasAny(IEnumerable<GameplayTag> tags)
        {
            if (tags == null)
            {
                return false;
            }

            foreach (var tag in tags)
            {
                if (HasTag(tag))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>检查容器是否持有 <paramref name="required"/> 中所有标签（AND 语义）。</summary>
        /// <param name="required">必须全部匹配的标签集合；为空时返回 true。</param>
        /// <returns>全部匹配则返回 true。</returns>
        public bool HasAll(IEnumerable<GameplayTag> required)
        {
            if (required == null)
            {
                return true;
            }

            foreach (var tag in required)
            {
                if (!HasTag(tag))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>增加标签引用计数；从 0 变为 1 时视为新增。</summary>
        /// <param name="tag">要添加的标签；无效标签将被忽略。</param>
        /// <returns>标签为新增（计数 0→1）时返回 true；仅增加计数或无效则返回 false。</returns>
        public bool AddTag(GameplayTag tag)
        {
            if (!tag.IsValid)
            {
                return false;
            }

            if (_counts.TryGetValue(tag.Name, out var count))
            {
                _counts[tag.Name] = count + 1;
                return false;
            }

            _counts[tag.Name] = 1;
            return true;
        }

        /// <summary>减少标签引用计数；减到 0 时真正移除。</summary>
        /// <param name="tag">要移除的标签；无效标签将被忽略。</param>
        /// <returns>计数降为 0 并移除时返回 true；仅减少计数或不存在则返回 false。</returns>
        public bool RemoveTag(GameplayTag tag)
        {
            if (!tag.IsValid || !_counts.TryGetValue(tag.Name, out var count))
            {
                return false;
            }

            if (count > 1)
            {
                _counts[tag.Name] = count - 1;
                return false;
            }

            _counts.Remove(tag.Name);
            return true;
        }

        /// <summary>清空容器中所有标签及计数。</summary>
        public void Clear() => _counts.Clear();
    }
}
