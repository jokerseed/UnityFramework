# Luban 生成代码（cs-bin）

由 `Config/Luban/gen_client.bat` 或 **Tools → Luban → Generate Client Config** 自动生成，**请勿手动编辑**。

## 关联文件

| 用途 | 路径 |
|------|------|
| 表结构 | `Config/Luban/Defines/battle.xml` |
| Excel 数据 | `Config/Luban/Datas/battle/*.xlsx` |
| 运行时二进制 | `Assets/Bundles/Configs/*.bytes` |
| 程序集定义 | `Assets/Generated/Generated.Luban.asmdef` |

> `Generated.Luban.asmdef` 放在 `Assets/Generated/` 下（不在 `Luban/` 子目录），避免打表时被 Luban 清理掉。

## 命名空间

生成代码命名空间为 `cfg`，入口类 `cfg.Tables`。

## 加载示例

```csharp
using cfg;
using Framework.Config;
using Framework.Res;

Tables tables = BattleConfigBootstrap.LoadTables(ResourceManager.Instance);
var fireball = tables.TbAbility.Get("Fireball");
```
