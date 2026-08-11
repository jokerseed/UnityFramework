using System.Collections;
using UnityEngine;
using YooAsset;

namespace Framework.Res
{
    public sealed class ResourceBootstrap : MonoBehaviour
    {
        [SerializeField] ResourceInitOptions _options = new ResourceInitOptions();

        public ResourceManager Manager => ResourceManager.Instance;
        public bool IsReady { get; private set; }

        void Awake()
        {
#if UNITY_EDITOR
            if (_options.PlayMode == EPlayMode.EditorSimulateMode)
            {
                // keep editor simulate by default
            }
#else
            if (_options.PlayMode == EPlayMode.EditorSimulateMode)
            {
                _options.PlayMode = EPlayMode.OfflinePlayMode;
            }
#endif
        }

        IEnumerator Start()
        {
            yield return Manager.InitializeAsync(_options);
            IsReady = true;
            Debug.Log("[Resource] Bootstrap ready.");
        }

        void OnDestroy()
        {
            if (IsReady)
            {
                Manager.Shutdown();
            }
        }
    }
}
