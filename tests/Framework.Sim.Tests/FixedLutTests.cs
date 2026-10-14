using System;
using System.Security.Cryptography;
using Framework.FixedMath;
using NUnit.Framework;

namespace Framework.Sim.Tests
{
    /// <summary>
    /// 查找表改为编码存储后必须与原始十六进制数组逐位一致。
    /// 期望哈希取自编码前的原始数组（小端 8 字节逐项拼接后的 SHA-256），由 tools/Framework.LutTools 打印。
    /// </summary>
    public sealed class FixedLutTests
    {
        const string SinLutSha256 = "C961C966D351773F613616CBB593E8B4C55DE47D1D39B6C9D8CC3F6D4A0B58A3";
        const string TanLutSha256 = "335DB3AE988EB72ADF47FD97CA02052AF3CB407CFFF3D3954F6D10AB94850EDA";

        [Test]
        public void SinLut_IsBitExact()
        {
            Assert.AreEqual(FP.LUT_SIZE, FP.SinLut.Length);
            Assert.AreEqual(SinLutSha256, Sha256(FP.SinLut));
        }

        [Test]
        public void TanLut_IsBitExact()
        {
            Assert.AreEqual(FP.LUT_SIZE, FP.TanLut.Length);
            Assert.AreEqual(TanLutSha256, Sha256(FP.TanLut));
        }

        [Test]
        public void TrigonometryUsesTables()
        {
            Assert.AreEqual(FP.Zero, FP.Sin(FP.Zero));
            Assert.AreEqual(FP.One.RawValue, FP.SinLut[FP.SinLut.Length - 1]);
        }

        static string Sha256(long[] values)
        {
            var bytes = new byte[values.Length * 8];
            for (var i = 0; i < values.Length; i++)
            {
                var v = (ulong)values[i];
                for (var b = 0; b < 8; b++)
                {
                    bytes[i * 8 + b] = (byte)(v >> (8 * b));
                }
            }

            using (var sha = SHA256.Create())
            {
                return Convert.ToHexString(sha.ComputeHash(bytes));
            }
        }
    }
}
