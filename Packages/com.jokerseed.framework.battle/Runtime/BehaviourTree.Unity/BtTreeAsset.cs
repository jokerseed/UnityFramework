using UnityEngine;

namespace Framework.BehaviourTree
{
    /// <summary>
    /// 行为树 ScriptableObject 资产；Editor 编辑并导出 JSON，运行时经 YooAsset 或 Resources 加载。
    /// </summary>
    [CreateAssetMenu(fileName = "BehaviourTree", menuName = "Framework/Behaviour Tree")]
    public sealed class BtTreeAsset : ScriptableObject
    {
        [SerializeField]
        BtTreeDefinition _definition = new BtTreeDefinition();

        /// <summary>树定义。</summary>
        public BtTreeDefinition Definition
        {
            get => _definition;
            set => _definition = value ?? new BtTreeDefinition();
        }

        void Reset()
        {
            _definition = new BtTreeDefinition();
        }

        void OnValidate()
        {
            if (_definition == null)
            {
                _definition = new BtTreeDefinition();
            }
        }
    }
}
