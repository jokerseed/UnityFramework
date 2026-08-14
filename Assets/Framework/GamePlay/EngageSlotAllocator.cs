using System;
using System.Collections.Generic;
using Framework.Core;
using UnityEngine;

namespace Framework.GamePlay
{
    /// <summary>
    /// 按追击目标把存活杂兵均匀铺在环上，避免全挤到同一点。
    /// 槽位按当前方位角排序后保持相对顺序，减少交叉换位。
    /// </summary>
    public sealed class EngageSlotAllocator
    {
        const float DefaultMinRadius = 1.45f;
        const float DefaultMaxRadius = 1.85f;
        const float SlotSpacing = 1.05f;

        readonly Dictionary<uint, Vector3> _points = new Dictionary<uint, Vector3>(32);
        readonly List<ActorId> _focusIds = new List<ActorId>(8);
        readonly List<ActorId> _ring = new List<ActorId>(32);
        readonly Dictionary<uint, float> _angles = new Dictionary<uint, float>(32);
        readonly Comparison<ActorId> _compareAngle;

        /// <summary>创建围攻槽位分配器。</summary>
        public EngageSlotAllocator()
        {
            _compareAngle = CompareAngle;
        }

        /// <summary>根据当前 Agent 与存活状态重建围攻槽位。</summary>
        /// <param name="registry">Actor 注册表；不可为 null。</param>
        /// <param name="agents">已绑定的 AI；不可为 null。</param>
        /// <param name="minRadius">环半径下限（米）。</param>
        /// <param name="maxRadius">环半径上限（米）；需落在近战技能射程内。</param>
        public void Rebuild(
            ActorRegistry registry,
            Dictionary<ActorId, BattleAgent> agents,
            float minRadius = DefaultMinRadius,
            float maxRadius = DefaultMaxRadius)
        {
            _points.Clear();
            if (registry == null || agents == null || agents.Count == 0)
            {
                return;
            }

            var min = minRadius > 0f ? minRadius : DefaultMinRadius;
            var max = maxRadius >= min ? maxRadius : DefaultMaxRadius;

            _focusIds.Clear();
            foreach (var pair in agents)
            {
                var focus = pair.Value.FocusTarget;
                if (!focus.IsValid || ContainsFocus(focus))
                {
                    continue;
                }

                _focusIds.Add(focus);
            }

            for (var i = 0; i < _focusIds.Count; i++)
            {
                AssignRing(registry, agents, _focusIds[i], min, max);
            }
        }

        /// <summary>查询该攻击者本帧应站的世界坐标。</summary>
        /// <param name="attacker">攻击者 Actor。</param>
        /// <param name="point">槽位坐标。</param>
        /// <returns>本帧已分配槽位时返回 true。</returns>
        public bool TryGetPoint(ActorId attacker, out Vector3 point) =>
            _points.TryGetValue(attacker.Value, out point);

        bool ContainsFocus(ActorId focus)
        {
            for (var i = 0; i < _focusIds.Count; i++)
            {
                if (_focusIds[i] == focus)
                {
                    return true;
                }
            }

            return false;
        }

        void AssignRing(
            ActorRegistry registry,
            Dictionary<ActorId, BattleAgent> agents,
            ActorId focus,
            float minRadius,
            float maxRadius)
        {
            _ring.Clear();
            _angles.Clear();
            if (!registry.TryGet(focus, out var focusActor) || focusActor.AbilitySystem.IsDead)
            {
                return;
            }

            foreach (var pair in agents)
            {
                if (pair.Value.FocusTarget != focus)
                {
                    continue;
                }

                if (!registry.TryGet(pair.Key, out var actor) || actor.AbilitySystem.IsDead)
                {
                    continue;
                }

                var delta = actor.Position - focusActor.Position;
                delta.y = 0f;
                _ring.Add(pair.Key);
                _angles[pair.Key.Value] = Mathf.Atan2(delta.z, delta.x);
            }

            var count = _ring.Count;
            if (count == 0)
            {
                return;
            }

            _ring.Sort(_compareAngle);

            var packed = count * SlotSpacing / (Mathf.PI * 2f);
            var radius = Mathf.Clamp(packed, minRadius, maxRadius);
            var step = (Mathf.PI * 2f) / count;
            var baseAngle = _angles[_ring[0].Value];
            var origin = focusActor.Position;

            for (var i = 0; i < count; i++)
            {
                var angle = baseAngle + step * i;
                _points[_ring[i].Value] = new Vector3(
                    origin.x + Mathf.Cos(angle) * radius,
                    origin.y,
                    origin.z + Mathf.Sin(angle) * radius);
            }
        }

        int CompareAngle(ActorId a, ActorId b) =>
            _angles[a.Value].CompareTo(_angles[b.Value]);
    }
}
