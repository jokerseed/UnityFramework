using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Framework.FixedMath;

namespace Framework.LutTools
{
    /// <summary>重新编码 FixedMath 查找表并打印 SHA-256。</summary>
    internal static class Program
    {
        static readonly Regex HexLiteral = new Regex(@"0x([0-9A-Fa-f]+)L", RegexOptions.Compiled);
        static readonly Regex EncodedData = new Regex("Data = @\"(?<data>[^\"]*)\"", RegexOptions.Compiled);
        static readonly Regex EncodedCount = new Regex(@"Count = (?<count>\d+);", RegexOptions.Compiled);

        static int Main(string[] args)
        {
            var root = args.Length > 0 ? args[0] : FindRepoRoot();
            var dir = Path.Combine(root, "Packages", "com.jokerseed.framework.battle", "Runtime", "FixedMath");
            var ok = Process(Path.Combine(dir, "Fix64SinLut.cs"), "SinLut")
                     & Process(Path.Combine(dir, "Fix64TanLut.cs"), "TanLut");
            return ok ? 0 : 1;
        }

        static bool Process(string path, string field)
        {
            var source = File.ReadAllText(path);
            var values = Parse(source);
            var hash = Sha256(values);
            var output = FixedLutCodec.WriteSource(field, values);

            var match = EncodedData.Match(output);
            var roundTrip = FixedLutCodec.Decode(match.Groups["data"].Value, values.Length);
            for (var i = 0; i < values.Length; i++)
            {
                if (roundTrip[i] != values[i])
                {
                    Console.Error.WriteLine($"{field}: round-trip mismatch at {i}");
                    return false;
                }
            }

            File.WriteAllText(path, output);
            Console.WriteLine($"{field}: count={values.Length} sha256={hash} bytes {source.Length} -> {output.Length}");
            return true;
        }

        static long[] Parse(string source)
        {
            var data = EncodedData.Match(source);
            if (data.Success)
            {
                var count = int.Parse(EncodedCount.Match(source).Groups["count"].Value, CultureInfo.InvariantCulture);
                return FixedLutCodec.Decode(data.Groups["data"].Value, count);
            }

            var list = new List<long>(1 << 18);
            foreach (Match m in HexLiteral.Matches(source))
            {
                list.Add(unchecked((long)ulong.Parse(m.Groups[1].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture)));
            }

            return list.ToArray();
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

        static string FindRepoRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "Packages", "com.jokerseed.framework.battle")))
            {
                dir = dir.Parent;
            }

            if (dir == null)
            {
                throw new DirectoryNotFoundException("repo root not found; pass it as the first argument");
            }

            return dir.FullName;
        }
    }
}
