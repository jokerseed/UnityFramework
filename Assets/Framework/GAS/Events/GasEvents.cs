using Framework.Core;
using UnityEngine;

namespace Framework.GAS.Events
{
    /// <summary>技能成功激活（表现/日志）。</summary>
    public struct AbilityActivatedEvent
    {
        public ActorId Caster;
        public string AbilityId;
        public Vector3 Origin;
        public Vector3 Direction;
        public ActorId PrimaryTarget;
    }

    /// <summary>伤害成功结算（表现/UI）。</summary>
    public struct DamageDealtEvent
    {
        public ActorId Source;
        public ActorId Target;
        public string AbilityId;
        public float RawDamage;
        public float FinalDamage;
    }

    /// <summary>伤害被免疫/护甲完全抵消（表现/UI）。</summary>
    public struct DamageBlockedEvent
    {
        public ActorId Source;
        public ActorId Target;
        public string AbilityId;
        public float RawDamage;
    }

    /// <summary>属性变化通知（UI）。</summary>
    public struct AttributeChangedEvent
    {
        public ActorId Actor;
        public string AttributeName;
        public float OldValue;
        public float NewValue;
    }

    /// <summary>Tag 变化通知（UI/动画状态机）。</summary>
    public struct TagChangedEvent
    {
        public ActorId Actor;
        public string Tag;
        public bool Added;
    }

    /// <summary>GameplayCue（动画/特效/音效）。</summary>
    public struct GameplayCueEvent
    {
        public ActorId Actor;
        public string CueTag;
        public Vector3 Position;
        public Vector3 Direction;
    }
}
