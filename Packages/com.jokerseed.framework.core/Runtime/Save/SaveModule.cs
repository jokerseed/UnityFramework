using System;
using System.Collections.Generic;
using System.IO;
using Framework.Core;
using Framework.Logging;
using UnityEngine;

namespace Framework.Save
{
    /// <summary>
    /// 存档模块：配置 <see cref="SaveManager"/>。默认存到 <c>persistentDataPath/Saves</c>，JSON 序列化。
    /// </summary>
    public sealed class SaveModule : IGameModule
    {
        readonly ISaveStorage _storage;
        readonly ISaveSerializer _serializer;

        /// <summary>使用默认存储与序列化器。</summary>
        public SaveModule()
        {
        }

        /// <summary>使用自定义存储与序列化器；传 null 的一项使用默认实现。</summary>
        /// <param name="storage">存储后端。</param>
        /// <param name="serializer">序列化器。</param>
        public SaveModule(ISaveStorage storage, ISaveSerializer serializer)
        {
            _storage = storage;
            _serializer = serializer;
        }

        /// <inheritdoc />
        public string Name => "Save";

        /// <inheritdoc />
        public ModulePhase Phase => ModulePhase.Data;

        /// <inheritdoc />
        public IReadOnlyList<Type> Dependencies => new[] { typeof(LoggingModule) };

        /// <inheritdoc />
        public void Initialize()
        {
            var storage = _storage ?? new FileSaveStorage(Path.Combine(Application.persistentDataPath, "Saves"));
            SaveManager.Instance.Configure(storage, _serializer ?? new JsonUtilitySaveSerializer());
            var where = storage is FileSaveStorage file ? file.Directory : storage.GetType().Name;
            GameLog.Info(LogCategories.Save, $"Ready  Storage={LogStyle.Value(where)}");
        }

        /// <inheritdoc />
        public void Shutdown()
        {
            if (SaveManager.HasInstance)
            {
                SaveManager.Instance.Shutdown();
                SaveManager.DestroyInstance();
            }

            GameLog.Info(LogCategories.Save, LogStyle.Muted("shut down"));
        }
    }
}
