using System;
using Framework.Core;
using Framework.FixedMath;

namespace Framework.GamePlay
{
    /// <summary>
    /// 将 <see cref="TSRandom"/> 适配为 Core 层确定性随机契约。
    /// </summary>
    public sealed class TsRandomAdapter : IDeterministicRandom
    {
        readonly TSRandom _random;

        /// <summary>使用已创建的 <see cref="TSRandom"/> 构造适配器。</summary>
        /// <param name="random">确定性随机实例；不可为 null。</param>
        public TsRandomAdapter(TSRandom random)
        {
            _random = random ?? throw new ArgumentNullException(nameof(random));
        }

        /// <inheritdoc />
        public FP Next01() => _random.NextFP();
    }
}
