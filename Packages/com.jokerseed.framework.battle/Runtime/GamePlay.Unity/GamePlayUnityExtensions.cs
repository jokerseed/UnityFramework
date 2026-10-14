using System;
using Framework.BehaviourTree;
using Framework.Battle;
using Framework.FixedMath;
using Framework.GAS;
using UnityEngine;

namespace Framework.GamePlay
{
    /// <summary>
    /// Unity 侧的便捷重载：把 <see cref="Vector3"/> / float 在入口转成定点后调用引擎无关的模拟 API。
    /// 转换只发生在这里一次，模拟内部不再接触浮点坐标。
    /// </summary>
    public static class GamePlayUnityExtensions
    {
        /// <summary>用 Unity 坐标创建战斗 Actor；内部转成 <see cref="TSVector"/> / <see cref="FP"/> 后调用 <see cref="GamePlayFramework.CreateActor"/>。</summary>
        /// <param name="framework">玩法框架；不可为 null。</param>
        /// <param name="actorId">Actor 的唯一 ID。</param>
        /// <param name="position">初始世界坐标。</param>
        /// <param name="maxHealth">最大生命值，默认 100。</param>
        /// <param name="teamId">所属队伍编号，默认 0。</param>
        /// <returns>已初始化的 <see cref="AbilitySystemComponent"/>。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="framework"/> 为 null。</exception>
        public static AbilitySystemComponent CreateActor(
            this GamePlayFramework framework,
            ActorId actorId,
            Vector3 position,
            float maxHealth = 100f,
            int teamId = 0)
        {
            if (framework == null)
            {
                throw new ArgumentNullException(nameof(framework));
            }

            return framework.CreateActor(actorId, FPConversions.ToFP(position), (FP)maxHealth, teamId);
        }

        /// <summary>从热更资源加载行为树并替换 Agent 当前的树。</summary>
        /// <param name="agent">目标 Agent；不可为 null。</param>
        /// <param name="treeId">树 id（与 Bundles 中资产名一致）。</param>
        /// <param name="clearBlackboard">是否清空黑板。</param>
        /// <param name="customRegistry">自定义节点注册表；可为 null。</param>
        /// <param name="subtrees">子树解析；可为 null。</param>
        /// <exception cref="ArgumentNullException"><paramref name="agent"/> 为 null。</exception>
        public static void ReplaceFromResource(
            this BattleAgent agent,
            string treeId,
            bool clearBlackboard = false,
            IBtNodeRegistry customRegistry = null,
            IBtSubtreeResolver subtrees = null)
        {
            if (agent == null)
            {
                throw new ArgumentNullException(nameof(agent));
            }

            agent.ReplaceTree(BtTreeResource.LoadTree(treeId, customRegistry, subtrees), clearBlackboard);
        }
    }

    /// <summary>按资源 id 装配内置 AI 的便捷入口（行为树模板经 <see cref="BtTreeResource"/> 加载）。</summary>
    public static class BattleAiAssets
    {
        /// <summary>加载近战追击树拓扑（参数请用 <see cref="BattleAiNodes.ApplyMeleeChaserBlackboard"/> 写入黑板）。</summary>
        /// <returns>独立 Runtime 的行为树实例。</returns>
        public static Framework.BehaviourTree.BehaviourTree CreateMeleeChaser()
        {
            BattleAiNodeRegistry.EnsureRegistered();
            return BtTreeResource.LoadTree(BattleAiNodes.MeleeChaserTreeId);
        }

        /// <summary>加载近战追击树并创建 Agent。</summary>
        /// <param name="abilityId">技能 ID。</param>
        /// <param name="targetId">追击目标。</param>
        /// <param name="meleeRange">近战距离；默认 2。</param>
        /// <param name="random">确定性随机源；为 null 时按会话种子和 Agent 的 Actor 派生。</param>
        /// <param name="agentId">该 Agent 所属 Actor。</param>
        /// <param name="sessionSeed">会话种子。</param>
        /// <returns>可交给 <see cref="GamePlayFramework.SetBattleAgent"/> 的 Agent。</returns>
        public static BattleAgent CreateMeleeChaserAgent(
            string abilityId,
            ActorId targetId,
            float meleeRange = 2f,
            TSRandom random = null,
            ActorId agentId = default,
            int sessionSeed = 1)
        {
            BattleAiNodeRegistry.EnsureRegistered();
            var template = BtTreeResource.LoadTemplate(BattleAiNodes.MeleeChaserTreeId);
            return BattleAiNodes.CreateMeleeChaserAgent(
                template,
                abilityId,
                targetId,
                FP.FromFloat(meleeRange),
                random,
                agentId,
                sessionSeed);
        }
    }
}
