using Framework.Core;
using UnityEngine;

namespace Framework.GAS.Events
{
    /// <summary>技能成功激活（表现/日志）。</summary>
    public struct AbilityActivatedEvent
    {
        /// <summary>施放技能的单位 ID。</summary>
        public ActorId Caster;
        /// <summary>激活的技能 ID。</summary>
        public string AbilityId;
        /// <summary>施放起点（世界坐标）。</summary>
        public Vector3 Origin;
        /// <summary>施放方向（已归一化）。</summary>
        public Vector3 Direction;
        /// <summary>主目标 ID；无目标时为无效值。</summary>
        public ActorId PrimaryTarget;
    }

    /// <summary>伤害成功结算（表现/UI）。</summary>
    public struct DamageDealtEvent
    {
        /// <summary>伤害来源单位 ID。</summary>
        public ActorId Source;
        /// <summary>受伤单位 ID。</summary>
        public ActorId Target;
        /// <summary>造成伤害的技能 ID。</summary>
        public string AbilityId;
        /// <summary>未减免的原始伤害量。</summary>
        public float RawDamage;
        /// <summary>经管线处理后的最终伤害量。</summary>
        public float FinalDamage;
    }

    /// <summary>伤害被免疫/护甲完全抵消（表现/UI）。</summary>
    public struct DamageBlockedEvent
    {
        /// <summary>伤害来源单位 ID。</summary>
        public ActorId Source;
        /// <summary>阻断伤害的目标单位 ID。</summary>
        public ActorId Target;
        /// <summary>造成伤害的技能 ID。</summary>
        public string AbilityId;
        /// <summary>被阻断的原始伤害量。</summary>
        public float RawDamage;
    }

    /// <summary>属性变化通知（UI）。</summary>
    public struct AttributeChangedEvent
    {
        /// <summary>属性归属的单位 ID。</summary>
        public ActorId Actor;
        /// <summary>发生变化的属性名称。</summary>
        public string AttributeName;
        /// <summary>变化前的值。</summary>
        public float OldValue;
        /// <summary>变化后的值。</summary>
        public float NewValue;
    }

    /// <summary>Tag 变化通知（UI/动画状态机）。</summary>
    public struct TagChangedEvent
    {
        /// <summary>标签归属的单位 ID。</summary>
        public ActorId Actor;
        /// <summary>发生变化的标签名称。</summary>
        public string Tag;
        /// <summary>true 为添加，false 为移除。</summary>
        public bool Added;
    }

    /// <summary>GameplayCue（动画/特效/音效）。</summary>
    public struct GameplayCueEvent
    {
        /// <summary>触发 Cue 的单位 ID。</summary>
        public ActorId Actor;
        /// <summary>Cue 标签（如 <c>Cue.Fireball.Cast</c>）。</summary>
        public string CueTag;
        /// <summary>触发位置（世界坐标）。</summary>
        public Vector3 Position;
        /// <summary>触发方向（已归一化）。</summary>
        public Vector3 Direction;
    }
}
