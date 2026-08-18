using Framework.Core;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// 单帧采样得到的战斗输入快照。
    /// </summary>
    public struct BattleInputFrame
    {
        /// <summary>
        /// 本帧移动方向；未输入时为零向量。
        /// </summary>
        public Vector3 MoveDirection { get; set; }

        /// <summary>
        /// 是否存在有效移动输入。
        /// </summary>
        public bool HasMoveInput { get; set; }

        /// <summary>
        /// 是否尝试释放近战连段。
        /// </summary>
        public bool TriggerMelee { get; set; }

        /// <summary>
        /// 是否尝试释放火球。
        /// </summary>
        public bool TriggerFireball { get; set; }

        /// <summary>
        /// 是否尝试释放闪避。
        /// </summary>
        public bool TriggerDodge { get; set; }

        /// <summary>
        /// 本帧采样到的英雄朝向提示；优先使用移动方向。
        /// </summary>
        public Vector3 AimDirection { get; set; }

        /// <summary>
        /// 清空一次性命令，避免一个渲染帧内多次重复触发。
        /// </summary>
        public void ConsumeOneShotCommands()
        {
            TriggerFireball = false;
            TriggerDodge = false;
        }
    }
}
