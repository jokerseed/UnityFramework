using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using Framework.Logging;
using Framework.Res;
using UnityEngine;

namespace Framework.HotUpdate
{
    /// <summary>一个热更程序集：程序集名 + 其 DLL 字节（TextAsset，扩展名 .bytes）在资源系统中的寻址。</summary>
    public readonly struct HotUpdateAssemblyInfo
    {
        /// <summary>创建描述。</summary>
        /// <param name="assemblyName">程序集名（不带 .dll），Editor 下按此名查找已编译的程序集。</param>
        /// <param name="location">DLL 字节在资源系统中的寻址。</param>
        public HotUpdateAssemblyInfo(string assemblyName, string location)
        {
            AssemblyName = assemblyName;
            Location = location;
        }

        /// <summary>程序集名（不带 .dll）。</summary>
        public string AssemblyName { get; }

        /// <summary>DLL 字节在资源系统中的寻址。</summary>
        public string Location { get; }
    }

    /// <summary>
    /// HybridCLR 热更程序集加载：先补充 AOT 元数据，再按依赖顺序 <see cref="Assembly.Load(byte[])"/> 热更 DLL。
    /// 字节通过 <see cref="ResourceManager"/> 加载，因此须在资源初始化（及补丁下载）之后调用。
    /// 工程安装 <c>com.code-philosophy.hybridclr</c> 后定义 <c>FRAMEWORK_HYBRIDCLR</c>，才会执行 AOT 元数据补充。
    /// Editor 下热更代码已作为普通程序集编译，直接返回已加载的程序集，不读取字节。
    /// </summary>
    public static class HotUpdateLoader
    {
        /// <summary>补充 AOT 元数据并加载热更程序集。</summary>
        /// <param name="aotMetadataLocations">AOT 元数据 DLL 字节的寻址（HybridCLR 生成的裁剪后 AOT DLL）；可为空。</param>
        /// <param name="assemblies">热更程序集，按依赖顺序排列（被依赖的在前）。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>按 <paramref name="assemblies"/> 顺序加载完成的程序集。</returns>
        /// <exception cref="InvalidOperationException">资源加载失败、元数据补充失败，或 Editor 下找不到同名程序集。</exception>
        /// <exception cref="NotSupportedException">非 Editor 下需要补充 AOT 元数据，但工程未安装 HybridCLR。</exception>
        public static async UniTask<Assembly[]> LoadAsync(
            IReadOnlyList<string> aotMetadataLocations,
            IReadOnlyList<HotUpdateAssemblyInfo> assemblies,
            CancellationToken cancellationToken = default)
        {
            if (assemblies == null)
            {
                throw new ArgumentNullException(nameof(assemblies));
            }

            var result = new Assembly[assemblies.Count];
            if (Application.isEditor)
            {
                for (var i = 0; i < assemblies.Count; i++)
                {
                    result[i] = FindLoadedAssembly(assemblies[i].AssemblyName);
                }

                GameLog.Info(LogCategories.Patch, $"HotUpdate (Editor): use {LogStyle.Value(result.Length)} compiled assemblies");
                return result;
            }

            if (aotMetadataLocations != null)
            {
                foreach (var location in aotMetadataLocations)
                {
                    var bytes = await LoadBytesAsync(location, cancellationToken);
                    LoadAotMetadata(location, bytes);
                }
            }

            for (var i = 0; i < assemblies.Count; i++)
            {
                var bytes = await LoadBytesAsync(assemblies[i].Location, cancellationToken);
                result[i] = Assembly.Load(bytes);
                GameLog.Info(LogCategories.Patch, $"HotUpdate assembly loaded: {LogStyle.Name(result[i].GetName().Name)}");
            }

            return result;
        }

        /// <summary>调用热更程序集中的静态无参入口方法，例如 <c>Game.HotfixEntry.Start</c>。</summary>
        /// <param name="assembly">热更程序集。</param>
        /// <param name="typeName">入口类型全名。</param>
        /// <param name="methodName">静态无参方法名。</param>
        /// <exception cref="MissingMemberException">找不到入口类型或方法。</exception>
        public static void InvokeEntry(Assembly assembly, string typeName, string methodName)
        {
            if (assembly == null)
            {
                throw new ArgumentNullException(nameof(assembly));
            }

            var type = assembly.GetType(typeName, throwOnError: false)
                       ?? throw new MissingMemberException($"[HotUpdate] Type '{typeName}' not found in {assembly.GetName().Name}.");
            var method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, null, Type.EmptyTypes, null)
                         ?? throw new MissingMemberException($"[HotUpdate] Static method '{typeName}.{methodName}()' not found.");
            method.Invoke(null, null);
        }

        static Assembly FindLoadedAssembly(string assemblyName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.GetName().Name == assemblyName)
                {
                    return assembly;
                }
            }

            throw new InvalidOperationException($"[HotUpdate] Assembly '{assemblyName}' is not compiled in the Editor. Check the asmdef name.");
        }

        static async UniTask<byte[]> LoadBytesAsync(string location, CancellationToken cancellationToken)
        {
            var handle = await ResourceManager.Instance.LoadAssetAsync<TextAsset>(location, cancellationToken: cancellationToken);
            using (handle)
            {
                var asset = handle.GetAsset<TextAsset>();
                if (!handle.Succeeded || asset == null)
                {
                    throw new InvalidOperationException($"[HotUpdate] Failed to load '{location}': {handle.Error}");
                }

                return asset.bytes;
            }
        }

        static void LoadAotMetadata(string location, byte[] bytes)
        {
#if FRAMEWORK_HYBRIDCLR
            var code = HybridCLR.RuntimeApi.LoadMetadataForAOTAssembly(bytes, HybridCLR.HomologousImageMode.SuperSet);
            if (code != HybridCLR.LoadImageErrorCode.OK)
            {
                throw new InvalidOperationException($"[HotUpdate] LoadMetadataForAOTAssembly '{location}' failed: {code}");
            }

            GameLog.Info(LogCategories.Patch, $"AOT metadata loaded: {LogStyle.Name(location)}");
#else
            throw new NotSupportedException($"[HotUpdate] '{location}': AOT metadata requires HybridCLR (com.code-philosophy.hybridclr) to be installed.");
#endif
        }
    }
}
