using Framework.Battle;
using Framework.FixedMath;
using Framework.GAS.Abilities;
using Framework.GAS.Abilities.Builtin;
using Framework.GAS.Targeting;

namespace Framework.GamePlay
{
    /// <summary>
    /// 无表现的最小战斗：两队各一名，近战与弹道，固定种子。
    /// EditMode 测试与业务层样本脚本都调用 <see cref="Run"/>。
    /// </summary>
    public static class MinimalBattle
    {
        /// <summary>默认随机种子。</summary>
        public const int Seed = 42;

        /// <summary>默认逻辑步数（30Hz 下约 1.6 秒）。</summary>
        public const int DefaultSteps = 48;

        /// <summary>近战技能 ID。</summary>
        public const string MeleeAbilityId = "Sample.Slash";

        /// <summary>弹道技能 ID。</summary>
        public const string ProjectileAbilityId = "Sample.Fireball";

        /// <summary>
        /// 跑一场最小战斗并记录每步校验和。
        /// 队伍 0 在原点发射弹道，队伍 1 在 +X 1.6 米处使用近战。每步消耗一次确定性随机数后再尝试施放。
        /// </summary>
        /// <param name="steps">逻辑步数；小于等于 0 时不推进。</param>
        /// <param name="seed">随机种子。</param>
        /// <returns>每步校验和以及双方生命的起止原值。</returns>
        public static MinimalBattleReport Run(int steps = DefaultSteps, int seed = Seed)
        {
            if (steps < 0)
            {
                steps = 0;
            }

            var checksums = new long[steps];
            using (var director = new BattleDirector())
            {
                var session = director.CreateSession(seed);
                var framework = session.Framework;
                var hero = session.AllocateActorId();
                var enemy = session.AllocateActorId();
                framework.CreateActor(hero, TSVector.zero, (FP)120f, teamId: 0);
                framework.CreateActor(enemy, new TSVector((FP)1.6f, FP.Zero, FP.Zero), (FP)80f, teamId: 1);
                framework.GiveAbility(
                    hero,
                    new GameplayAbilityDef(new ProjectileAbility(
                        ProjectileAbilityId,
                        cooldown: 0.4f,
                        speed: 6f,
                        radius: 0.2f,
                        lifetime: 1f,
                        damage: 12f,
                        teamId: 0)));
                framework.GiveAbility(
                    enemy,
                    new GameplayAbilityDef(new MeleeStrikeAbility(
                        MeleeAbilityId,
                        0.5f,
                        8f,
                        2.5f,
                        new MeleeTargetSelector(framework.QueryNearestEnemy))));

                var heroStart = HealthRaw(framework, hero);
                var enemyStart = HealthRaw(framework, enemy);
                var right = new TSVector(FP.One, FP.Zero, FP.Zero);
                var left = new TSVector(-FP.One, FP.Zero, FP.Zero);
                for (var i = 0; i < steps; i++)
                {
                    session.Step(1, _ =>
                    {
                        framework.Random.Next();
                        TryCast(framework, hero, enemy, ProjectileAbilityId, right);
                        TryCast(framework, enemy, hero, MeleeAbilityId, left);
                    });
                    checksums[i] = BattleFrameChecksum.Compute(framework);
                }

                return new MinimalBattleReport(
                    checksums,
                    heroStart,
                    HealthRaw(framework, hero),
                    enemyStart,
                    HealthRaw(framework, enemy));
            }
        }

        static void TryCast(
            GamePlayFramework framework,
            ActorId caster,
            ActorId target,
            string abilityId,
            TSVector direction)
        {
            if (!framework.Registry.TryGetSimPosition(caster, out var origin))
            {
                return;
            }

            if (!framework.TryGetActor(caster, out var asc) || asc.IsDead)
            {
                return;
            }

            framework.TryActivateAbility(
                caster,
                abilityId,
                new AbilityActivationContext(origin, direction, target, (FP)2.5f));
        }

        static long HealthRaw(GamePlayFramework framework, ActorId actorId)
        {
            if (!framework.TryGetActor(actorId, out var asc))
            {
                return 0;
            }

            return asc.Attributes.GetCurrentValue(BattleConstants.Health).RawValue;
        }
    }

    /// <summary><see cref="MinimalBattle.Run"/> 的结果。</summary>
    public sealed class MinimalBattleReport
    {
        /// <summary>每步结束后的 <see cref="BattleFrameChecksum"/>。</summary>
        public long[] Checksums { get; }

        /// <summary>队伍 0 开场生命原值。</summary>
        public long HeroHealthAtStartRaw { get; }

        /// <summary>队伍 0 结束时生命原值。</summary>
        public long HeroHealthRaw { get; }

        /// <summary>队伍 1 开场生命原值。</summary>
        public long EnemyHealthAtStartRaw { get; }

        /// <summary>队伍 1 结束时生命原值。</summary>
        public long EnemyHealthRaw { get; }

        /// <summary>构造一场最小战斗的结果。</summary>
        /// <param name="checksums">每步校验和；不可为 null。</param>
        /// <param name="heroHealthAtStartRaw">队伍 0 开场生命原值。</param>
        /// <param name="heroHealthRaw">队伍 0 结束生命原值。</param>
        /// <param name="enemyHealthAtStartRaw">队伍 1 开场生命原值。</param>
        /// <param name="enemyHealthRaw">队伍 1 结束生命原值。</param>
        public MinimalBattleReport(
            long[] checksums,
            long heroHealthAtStartRaw,
            long heroHealthRaw,
            long enemyHealthAtStartRaw,
            long enemyHealthRaw)
        {
            Checksums = checksums;
            HeroHealthAtStartRaw = heroHealthAtStartRaw;
            HeroHealthRaw = heroHealthRaw;
            EnemyHealthAtStartRaw = enemyHealthAtStartRaw;
            EnemyHealthRaw = enemyHealthRaw;
        }
    }
}
