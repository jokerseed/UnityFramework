using System;
using UnityEngine;

namespace Framework.HotUpdate
{
    /// <summary>Opt-in typed HybridCLR bridge. Import only after installing HybridCLR.</summary>
    public static class HybridCLRMetadataBackend
    {
        // After the core's SubsystemRegistration reset, before scene Awake/startup.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Register()
        {
            HotUpdateLoader.RegisterAotMetadataLoader(Load);
        }

        static void Load(string location, byte[] bytes)
        {
            var code = HybridCLR.RuntimeApi.LoadMetadataForAOTAssembly(bytes, HybridCLR.HomologousImageMode.SuperSet);
            if (code != HybridCLR.LoadImageErrorCode.OK)
                throw new InvalidOperationException($"[HotUpdate] LoadMetadataForAOTAssembly '{location}' failed: {code}");
        }
    }
}
