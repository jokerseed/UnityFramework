using System;
using System.IO;
using Framework.Logging;
using Luban;

namespace Framework.Config
{
    /// <summary>Editor 下调试用的 Luban 表直读（不走 YooAsset）。</summary>
    public static class ConfigLoader
    {
#if UNITY_EDITOR
        /// <summary>从默认 bin 目录（<see cref="ConfigPaths.GetBinDirectory"/>）加载表对象。</summary>
        /// <typeparam name="TTables">Luban 生成的表聚合类型。</typeparam>
        /// <param name="factory">表构造工厂，如 <c>loader =&gt; new cfg.CfgTables(loader)</c>。</param>
        /// <returns>表对象。</returns>
        /// <exception cref="DirectoryNotFoundException">bin 目录不存在。</exception>
        public static TTables LoadDefault<TTables>(Func<Func<string, ByteBuf>, TTables> factory)
        {
            return LoadBinaryFromDirectory(factory, ConfigPaths.GetBinDirectory());
        }

        /// <summary>从指定 bin 目录加载表对象。</summary>
        /// <typeparam name="TTables">Luban 生成的表聚合类型。</typeparam>
        /// <param name="factory">表构造工厂。</param>
        /// <param name="binDirectory">含 <c>*.bytes</c> 的目录。</param>
        /// <returns>表对象。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="factory"/> 为 null。</exception>
        /// <exception cref="DirectoryNotFoundException">bin 目录不存在。</exception>
        public static TTables LoadBinaryFromDirectory<TTables>(Func<Func<string, ByteBuf>, TTables> factory, string binDirectory)
        {
            if (factory == null)
            {
                throw new ArgumentNullException(nameof(factory));
            }

            if (!Directory.Exists(binDirectory))
            {
                throw new DirectoryNotFoundException($"Config bin directory not found: {binDirectory}");
            }

            try
            {
                return factory(file => new ByteBuf(File.ReadAllBytes(Path.Combine(binDirectory, file + ".bytes"))));
            }
            catch (Exception ex)
            {
                GameLog.Error(LogCategories.Config, $"Failed to load binary tables from {binDirectory}: {ex.Message}");
                throw;
            }
        }
#else
        /// <summary>Editor 直读；Player 请使用 <see cref="ConfigManager.LoadTables{TTables}"/>。</summary>
        /// <typeparam name="TTables">Luban 生成的表聚合类型。</typeparam>
        /// <param name="factory">表构造工厂。</param>
        /// <returns>不返回。</returns>
        /// <exception cref="NotSupportedException">非 Editor 环境。</exception>
        public static TTables LoadDefault<TTables>(Func<Func<string, ByteBuf>, TTables> factory) => throw CreateEditorOnlyException();

        /// <summary>Editor 直读；Player 请使用 <see cref="ConfigManager.LoadTables{TTables}"/>。</summary>
        /// <typeparam name="TTables">Luban 生成的表聚合类型。</typeparam>
        /// <param name="factory">表构造工厂。</param>
        /// <param name="binDirectory">含 <c>*.bytes</c> 的目录。</param>
        /// <returns>不返回。</returns>
        /// <exception cref="NotSupportedException">非 Editor 环境。</exception>
        public static TTables LoadBinaryFromDirectory<TTables>(Func<Func<string, ByteBuf>, TTables> factory, string binDirectory) => throw CreateEditorOnlyException();

        static NotSupportedException CreateEditorOnlyException() =>
            new NotSupportedException("Editor-only config load. Use ConfigManager.Instance.LoadTables<T>() at runtime.");
#endif
    }
}
