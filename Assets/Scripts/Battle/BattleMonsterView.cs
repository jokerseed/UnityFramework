using Framework.ObjectPool;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// 战斗杂兵视图池：Prefab 由 <see cref="ResourceScope"/> 持 Asset，池内持 Instance。
    /// </summary>
    public sealed class BattleMonsterView : PooledObject<BattleMonsterView>
    {
        const int Capacity = 16;

        static GameObject _prefab;

        static BattleMonsterView()
        {
            UseAutoSetup(EnsurePool);
        }

        /// <summary>关联的场景对象。</summary>
        public GameObject View => (GameObject)Target;

        /// <summary>配置杂兵 Prefab；须在 <see cref="Setup"/> / <see cref="Spawn"/> 前由战斗 Scope 加载后调用。</summary>
        /// <param name="prefab">已加载的 Prefab 资源。</param>
        public static void Configure(GameObject prefab)
        {
            _prefab = prefab;
        }

        /// <summary>从池取出并摆放到指定位置。</summary>
        /// <param name="position">世界坐标。</param>
        /// <param name="rotation">世界旋转。</param>
        /// <param name="instanceName">实例名称。</param>
        /// <returns>池化视图。</returns>
        public static BattleMonsterView SpawnAt(Vector3 position, Quaternion rotation, string instanceName)
        {
            var view = Spawn();
            var go = view.View;
            go.name = instanceName;
            go.transform.SetPositionAndRotation(position, rotation);
            if (!go.activeSelf)
            {
                go.SetActive(true);
            }

            return view;
        }

        static void EnsurePool()
        {
            if (_prefab == null)
            {
                return;
            }

            SetupPool(
                factory: () => CreateInstance<BattleMonsterView>("Monster", Object.Instantiate(_prefab)),
                allowMultiSpawn: false,
                capacity: Capacity);
        }

        /// <inheritdoc />
        protected override void Release(bool isShutdown)
        {
            if (Target is GameObject go)
            {
                Object.Destroy(go);
            }
        }
    }
}
