using System;
using System.IO;
using cfg;
using Framework.Logging;
using Luban;

namespace Framework.Config
{
    /// <summary>Editor 下调试用的 Luban 表直读（不走 YooAsset）。</summary>
    public static class ConfigLoader
    {
#if UNITY_EDITOR
        /// <summary>Editor 直读默认 bin 目录加载 Tables。</summary>
        /// <returns>Luban Tables。</returns>
        public static Tables LoadDefault()
        {
            return LoadBinaryFromDirectory(ConfigPaths.GetBinDirectory());
        }

        /// <summary>从指定 bin 目录加载 Tables。</summary>
        /// <param name="binDirectory">含 <c>*.bytes</c> 的目录。</param>
        /// <returns>Luban Tables。</returns>
        public static Tables LoadBinaryFromDirectory(string binDirectory)
        {
            if (!Directory.Exists(binDirectory))
            {
                throw new DirectoryNotFoundException($"Config bin directory not found: {binDirectory}");
            }

            try
            {
                return new Tables(file => new ByteBuf(File.ReadAllBytes(Path.Combine(binDirectory, file + ".bytes"))));
            }
            catch (Exception ex)
            {
                GameLog.Error(LogCategories.Config, $"Failed to load binary tables from {binDirectory}: {ex.Message}");
                throw;
            }
        }
#else
        /// <summary>Editor 直读；Player 请使用 <see cref="Framework.Res.ResourceManager.LoadLubanTables"/>。</summary>
        /// <exception cref="NotSupportedException">非 Editor 环境。</exception>
        public static Tables LoadDefault() => throw CreateEditorOnlyException();

        /// <summary>Editor 直读；Player 请使用 <see cref="Framework.Res.ResourceManager.LoadLubanTables"/>。</summary>
        /// <exception cref="NotSupportedException">非 Editor 环境。</exception>
        public static Tables LoadBinaryFromDirectory(string binDirectory) => throw CreateEditorOnlyException();
#endif

        static NotSupportedException CreateEditorOnlyException() =>
            new NotSupportedException("Editor-only config load. Use ResourceManager.Instance.LoadLubanTables() at runtime.");
    }
}
