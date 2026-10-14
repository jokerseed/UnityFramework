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
}
