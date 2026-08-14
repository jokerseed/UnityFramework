# Framework.ObjectPool

对象池模块，参考 [TEngine ObjectPoolModule](https://github.com/Alex-Rachel/TEngine)。管理带生命周期的 `ObjectBase`（可挂 GameObject 等 Target），支持容量、过期与自动释放。

## 程序集

| 项 | 值 |
|---|---|
| 程序集 | `Framework.ObjectPool` |
| 命名空间 | `Framework.ObjectPool` |
| 依赖 | `Framework.Core`、`Logging`、`MemoryPool` |

## 核心类型

| 类型 | 职责 |
|------|------|
| `ObjectBase` | 池化对象基类；`CreateInstance` 取壳初始化 |
| `PooledObject<T>` | **推荐**：自持池，对外 `Spawn()` / `Unspawn(obj)` |
| `IObjectPool<T>` | 底层池 API（由 `PooledObject` 内部使用） |
| `ObjectPoolManager` | 创建/销毁命名池，`Update` 自动释放 |
| `ObjectPoolModule` | Bootstrap 模块 |

## 推荐用法（外部零参数）

prefab 地址、容量、过期时间全部写在子类里；业务侧**不传参**：

```csharp
var bullet = BulletObject.Spawn();
BulletObject.Unspawn(bullet);
```

```csharp
public sealed class BulletObject : PooledObject<BulletObject>
{
    const string PrefabAddress = "bundles/prefabs/bullet";
    const int Capacity = 64;

    static GameObject _prefab;

    static BulletObject()
    {
        // 注册无参自动建池；首次 Spawn 时执行
        UseAutoSetup(EnsurePool);
    }

    static void EnsurePool()
    {
        if (_prefab == null)
        {
            // 用项目自己的加载方式（ResourceManager / 预加载缓存等）
            using var handle = ResourceManager.Instance.LoadAssetSync<GameObject>(PrefabAddress);
            _prefab = handle.GetAsset<GameObject>();
            // 若句柄 Dispose 会卸资源，请改为持有 handle 或预加载到静态缓存
        }

        SetupPool(
            factory: () => CreateInstance<BulletObject>("Bullet", Object.Instantiate(_prefab)),
            allowMultiSpawn: true,
            capacity: Capacity,
            expireTime: 60f);
    }

    protected override void Release(bool isShutdown)
    {
        if (Target is GameObject go)
        {
            Object.Destroy(go);
        }
    }
}
```

可选：战斗开始时预热（同样无参）——`BulletObject.Setup();`  
不调用也可以，第一次 `Spawn()` 会自动 `EnsurePool`。

### 约定

| 层级 | 职责 |
|------|------|
| 子类静态构造 + `EnsurePool` | prefab 加载、容量、`SetupPool` |
| `PooledObject<T>` | 懒初始化、`Spawn` / `Unspawn` / `TearDown` |
| 业务代码 | 只调 `T.Spawn()` / `T.Unspawn(obj)`，**不传 pool/prefab** |

## Bootstrap

```csharp
new MemoryPoolModule(),
new ObjectPoolModule(),
```

须先完成 `ObjectPoolModule`（以及若用 `ResourceManager` 则 `ResourceModule`）初始化，再 `Spawn`。

## 池类型（底层）

| 方法 | 语义 |
|------|------|
| `CreateSingleSpawnObjectPool` | 未 Unspawn 前不可再次 Spawn 同一实例 |
| `CreateMultiSpawnObjectPool` | 允许多次 Spawn（引用计数） |
