using System;

namespace Framework.FixedMath
{
    /// <summary>
    /// 配置数值进入定点模拟的统一入口。
    /// 配置表存的是 float（客户端读 Luban 二进制），服务端或工具可能把同一列读成 double 或 decimal。
    /// 直接截断时，同一个 "0.1" 从 float 得到 0x1999999F，从 double 得到 0x19999999，两端会算出不同结果。
    /// 这里先把数值换成十进制并四舍五入到 <see cref="Decimals"/> 位小数，再用 decimal 精确乘以 2^32 截断成 FP，
    /// 所以无论来源是 float、double 还是 decimal，同一个十进制写法都得到同一个 FP。
    /// float 转 decimal 按 7 位有效数字、double 按 15 位有效数字舍入，这是托管代码里的确定算法，
    /// Mono、IL2CPP、CoreCLR 结果一致。
    /// </summary>
    public static class FixedConfig
    {
        /// <summary>量化保留的小数位数。配置精度要求高于万分之一时不要用这个入口；float 表只有 7 位有效数字，超过的部分本来就存不下。</summary>
        public const int Decimals = 4;

        // Q31.32 的整数部分范围：[-2^31, 2^31)。
        const decimal Limit = 2147483648m; // determinism-allow: 配置边界

        /// <summary>把配置里的 float 量化后转成 FP。</summary>
        /// <param name="value">配置值。</param>
        /// <returns>定点值。</returns>
        /// <exception cref="ArgumentOutOfRangeException">值是 NaN、无穷大，或超出 FP 整数范围。</exception>
        public static FP FromConfig(float value) // determinism-allow: 配置边界
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) // determinism-allow: 配置边界
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "Config value must be a finite number.");
            }

            if (value >= 2147483648f || value < -2147483648f) // determinism-allow: 配置边界
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "Config value is out of FP range.");
            }

            return Quantize((decimal)value); // determinism-allow: 配置边界
        }

        /// <summary>把 double 形式的配置值量化后转成 FP（如服务端从 JSON 读到的数值）。</summary>
        /// <param name="value">配置值。</param>
        /// <returns>定点值。</returns>
        /// <exception cref="ArgumentOutOfRangeException">值是 NaN、无穷大，或超出 FP 整数范围。</exception>
        public static FP FromConfig(double value) // determinism-allow: 配置边界
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) // determinism-allow: 配置边界
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "Config value must be a finite number.");
            }

            if (value >= 2147483648d || value < -2147483648d) // determinism-allow: 配置边界
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "Config value is out of FP range.");
            }

            return Quantize((decimal)value); // determinism-allow: 配置边界
        }

        /// <summary>把 decimal 形式的配置值量化后转成 FP。</summary>
        /// <param name="value">配置值。</param>
        /// <returns>定点值。</returns>
        /// <exception cref="ArgumentOutOfRangeException">值超出 FP 整数范围。</exception>
        public static FP FromConfig(decimal value) // determinism-allow: 配置边界
        {
            return Quantize(value);
        }

        static FP Quantize(decimal value) // determinism-allow: 配置边界
        {
            var rounded = Math.Round(value, Decimals, MidpointRounding.AwayFromZero); // determinism-allow: 配置边界
            if (rounded >= Limit || rounded < -Limit)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "Config value is out of FP range.");
            }

            return FP.FromRaw((long)(rounded * FP.ONE));
        }
    }
}
