using System;
using System.IO;
using cfg;
using Luban;
using UnityEngine;

namespace Framework.Config
{
    public static class BattleConfigPaths
    {
        public const string RelativeBinDir = "Bundles/Configs";

        public static string GetBinDirectory()
        {
            return Path.Combine(Application.dataPath, RelativeBinDir);
        }
    }

    public static class BattleConfigLoader
    {
        public static Tables LoadDefault()
        {
            return LoadBinaryFromDirectory(BattleConfigPaths.GetBinDirectory());
        }

        public static Tables LoadBinaryFromDirectory(string binDirectory)
        {
            if (!Directory.Exists(binDirectory))
            {
                throw new DirectoryNotFoundException($"Battle config bin directory not found: {binDirectory}");
            }

            try
            {
                return new Tables(file => LoadByteBuf(binDirectory, file));
            }
            catch (Exception ex)
            {
                Debug.LogError($"[BattleConfig] Failed to load binary tables from {binDirectory}: {ex.Message}");
                throw;
            }
        }

        public static Tables LoadFromBytes(Func<string, byte[]> loadBytes)
        {
            if (loadBytes == null)
            {
                throw new ArgumentNullException(nameof(loadBytes));
            }

            return new Tables(file =>
            {
                var bytes = loadBytes(file);
                if (bytes == null || bytes.Length == 0)
                {
                    throw new FileNotFoundException($"Battle config bytes not found: {file}");
                }

                return new ByteBuf(bytes);
            });
        }

        public static Tables LoadFromTextAssets(Func<string, TextAsset> loadAsset)
        {
            if (loadAsset == null)
            {
                throw new ArgumentNullException(nameof(loadAsset));
            }

            return new Tables(file =>
            {
                var asset = loadAsset(file);
                if (asset == null)
                {
                    throw new FileNotFoundException($"Battle config TextAsset not found: {file}");
                }

                return new ByteBuf(asset.bytes);
            });
        }

        static ByteBuf LoadByteBuf(string binDirectory, string file)
        {
            var path = Path.Combine(binDirectory, file + ".bytes");
            return new ByteBuf(File.ReadAllBytes(path));
        }
    }
}
