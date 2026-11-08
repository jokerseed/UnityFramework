using System.Collections.Generic;

namespace Framework.Save
{
    /// <summary>
    /// 存档存储后端：按键读写字节。默认实现 <see cref="FileSaveStorage"/>；
    /// 云存档、小游戏平台存储等实现本接口后传给 <see cref="SaveManager.Configure"/>。
    /// </summary>
    public interface ISaveStorage
    {
        /// <summary>主存档或受支持的备份是否存在；不解析内容，也不代表存档有效。</summary>
        /// <param name="key">存档键。</param>
        /// <returns>至少一份副本存在返回 true；两者均不存在返回 false。</returns>
        /// <exception cref="System.IO.IOException">无法检查存档路径。</exception>
        /// <exception cref="System.UnauthorizedAccessException">无权检查存档路径。</exception>
        bool Exists(string key);

        /// <summary>读取主存档字节；仅有备份时返回 false，由调用方单独读取备份。</summary>
        /// <param name="key">存档键。</param>
        /// <param name="data">存在时输出字节。</param>
        /// <returns>存在返回 true。</returns>
        bool TryRead(string key, out byte[] data);

        /// <summary>写入存档字节；实现须保证中途失败不会损坏已有存档。</summary>
        /// <param name="key">存档键。</param>
        /// <param name="data">字节内容。</param>
        void Write(string key, byte[] data);

        /// <summary>删除主存档与受支持的备份；内容损坏不影响显式删除。</summary>
        /// <param name="key">存档键。</param>
        /// <returns>至少一份副本原先存在且删除正常完成返回 true；两者均不存在返回 false。</returns>
        /// <exception cref="System.IO.IOException">检查或删除失败；此前可能已删除部分副本。</exception>
        /// <exception cref="System.UnauthorizedAccessException">无权检查或删除存档。</exception>
        bool Delete(string key);

        /// <summary>列出主存档或受支持的备份对应的键，不解析或恢复内容。</summary>
        /// <returns>按键字符串去重的存档键列表；包含内容损坏但仍存在的副本。</returns>
        /// <exception cref="System.IO.IOException">无法枚举存档。</exception>
        /// <exception cref="System.UnauthorizedAccessException">无权枚举存档。</exception>
        IReadOnlyList<string> ListKeys();
    }

    /// <summary>
    /// 可选能力：保留上一次成功写入的备份。<see cref="SaveManager"/> 在主存档读不出或解析失败时回退到备份。
    /// </summary>
    public interface ISaveBackupStorage
    {
        /// <summary>读取备份字节。</summary>
        /// <param name="key">存档键。</param>
        /// <param name="data">存在时输出字节。</param>
        /// <returns>备份存在返回 true。</returns>
        bool TryReadBackup(string key, out byte[] data);
    }

    /// <summary>
    /// 可选恢复能力：将已成功解析的备份提升为主存档，同时保留原备份。
    /// 会在写入时轮换备份的后端应实现本接口，避免恢复后的下一次写入把坏主档保存为备份。
    /// </summary>
    public interface ISaveRecoveryStorage : ISaveBackupStorage
    {
        /// <summary>
        /// 用已验证的备份字节修复主存档；成功或失败均不得覆盖已有备份。
        /// 失败须抛出异常，不能报告恢复成功或先删除唯一可恢复的副本。
        /// </summary>
        /// <param name="key">存档键。</param>
        /// <param name="data">已成功反序列化的备份字节，不可为 null。</param>
        /// <exception cref="System.ArgumentNullException"><paramref name="data"/> 为 null。</exception>
        /// <exception cref="System.IO.IOException">无法安全修复主存档。</exception>
        void RestoreBackup(string key, byte[] data);
    }
}
