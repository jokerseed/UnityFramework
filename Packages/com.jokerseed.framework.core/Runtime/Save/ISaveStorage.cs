using System.Collections.Generic;

namespace Framework.Save
{
    /// <summary>
    /// 存档存储后端：按键读写字节。默认实现 <see cref="FileSaveStorage"/>；
    /// 云存档、小游戏平台存储等实现本接口后传给 <see cref="SaveManager.Configure"/>。
    /// </summary>
    public interface ISaveStorage
    {
        /// <summary>键是否存在。</summary>
        /// <param name="key">存档键。</param>
        /// <returns>存在返回 true。</returns>
        bool Exists(string key);

        /// <summary>读取存档字节。</summary>
        /// <param name="key">存档键。</param>
        /// <param name="data">存在时输出字节。</param>
        /// <returns>存在返回 true。</returns>
        bool TryRead(string key, out byte[] data);

        /// <summary>写入存档字节；实现须保证中途失败不会损坏已有存档。</summary>
        /// <param name="key">存档键。</param>
        /// <param name="data">字节内容。</param>
        void Write(string key, byte[] data);

        /// <summary>删除存档。</summary>
        /// <param name="key">存档键。</param>
        /// <returns>存在并删除返回 true。</returns>
        bool Delete(string key);

        /// <summary>列出全部存档键。</summary>
        /// <returns>存档键列表。</returns>
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
