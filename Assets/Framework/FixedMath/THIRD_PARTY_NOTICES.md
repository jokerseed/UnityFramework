# Third-Party Notices — Framework.FixedMath

本模块自项目主干 `D:\Client\Assets\Script\Core\TrueSync\Engine\Math` 迁移，数学核心源自：

## FixedMath.Net / Fix64

- https://github.com/asik/FixedMath.Net
- Apache License 2.0（André Slupik 等）
- 上游亦包含 libfixmath（MIT）、log2fix（MIT）相关算法

## TrueSync / Jitter Physics 向量与矩阵

- Photon TrueSync 数学封装（`TSVector` / `TSMatrix` / `TSQuaternion` 等）
- Jitter Physics 线性代数部分为 zlib 风格许可（见各文件头版权声明）

## Framework 改动

- 命名空间：`TrueSync` → `Framework.FixedMath`
- 移除 `IWGames`、`Obscured*` 隐式转换（Client 反作弊相关）
- 补充 `FPConversions` 表现层桥接

完整许可证文本以各上游仓库为准。
