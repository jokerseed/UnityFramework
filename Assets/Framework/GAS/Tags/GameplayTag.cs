using System;
using System.Collections.Generic;

namespace Framework.GAS.Tags
{
    public readonly struct GameplayTag : IEquatable<GameplayTag>
    {
        public string Name { get; }

        public GameplayTag(string name) => Name = name ?? string.Empty;

        public bool IsValid => !string.IsNullOrEmpty(Name);

        public bool Equals(GameplayTag other) => Name == other.Name;

        public override bool Equals(object obj) => obj is GameplayTag other && Equals(other);

        public override int GetHashCode() => Name?.GetHashCode() ?? 0;

        public override string ToString() => Name;

        public static bool operator ==(GameplayTag left, GameplayTag right) => left.Equals(right);

        public static bool operator !=(GameplayTag left, GameplayTag right) => !left.Equals(right);
    }

    /// <summary>支持层级匹配：拥有 State.CrowdControl.Stunned 则匹配 State.CrowdControl。</summary>
    public static class GameplayTagMatcher
    {
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

    public sealed class GameplayTagContainer
    {
        readonly HashSet<string> _tags = new HashSet<string>();

        public IReadOnlyCollection<string> Tags => _tags;

        public bool HasTag(GameplayTag tag)
        {
            if (!tag.IsValid)
            {
                return false;
            }

            foreach (var owned in _tags)
            {
                if (GameplayTagMatcher.Matches(new GameplayTag(owned), tag))
                {
                    return true;
                }
            }

            return false;
        }

        public bool HasAny(IEnumerable<GameplayTag> tags)
        {
            foreach (var tag in tags)
            {
                if (HasTag(tag))
                {
                    return true;
                }
            }

            return false;
        }

        public bool HasAll(IEnumerable<GameplayTag> required)
        {
            foreach (var tag in required)
            {
                if (!HasTag(tag))
                {
                    return false;
                }
            }

            return true;
        }

        public bool AddTag(GameplayTag tag)
        {
            if (!tag.IsValid)
            {
                return false;
            }

            return _tags.Add(tag.Name);
        }

        public bool RemoveTag(GameplayTag tag)
        {
            if (!tag.IsValid)
            {
                return false;
            }

            return _tags.Remove(tag.Name);
        }

        public void Clear() => _tags.Clear();
    }
}
