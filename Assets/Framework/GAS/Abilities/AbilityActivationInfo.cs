using System.Collections.Generic;
using Framework.Core;
using Framework.FixedMath;
using Framework.GAS.Tags;

namespace Framework.GAS.Abilities
{
    /// <summary>技能激活附加信息：触发来源、目标与 SetByCaller 幅度字典。</summary>
    public sealed class AbilityActivationInfo
    {
        readonly Dictionary<string, FP> _setByCaller = new Dictionary<string, FP>();

        /// <summary>发起激活的单位（通常为技能拥有者）。</summary>
        public ActorId Instigator { get; set; }

        /// <summary>触发时的主目标。</summary>
        public ActorId TriggerTarget { get; set; }

        /// <summary>触发事件标签（被动技能由 GameplayEvent 激活时使用）。</summary>
        public GameplayTag TriggerTag { get; set; }

        /// <summary>SetByCaller 幅度字典（只读视图）。</summary>
        public IReadOnlyDictionary<string, FP> SetByCaller => _setByCaller;

        /// <summary>设置 SetByCaller 幅度。</summary>
        /// <param name="key">键名。</param>
        /// <param name="value">幅度值。</param>
        public void SetSetByCaller(string key, FP value) => _setByCaller[key] = value;

        /// <summary>尝试读取 SetByCaller 幅度。</summary>
        /// <param name="key">键名。</param>
        /// <param name="value">找到时输出幅度。</param>
        /// <returns>存在该键时返回 true。</returns>
        public bool TryGetSetByCaller(string key, out FP value) => _setByCaller.TryGetValue(key, out value);

        /// <summary>清空 SetByCaller 字典。</summary>
        public void ClearSetByCaller() => _setByCaller.Clear();
    }
}
