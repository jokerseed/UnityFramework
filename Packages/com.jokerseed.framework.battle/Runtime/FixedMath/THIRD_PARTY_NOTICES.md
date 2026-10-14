# Third-Party Notices — Framework.FixedMath

本模块迁移自一个基于 Photon TrueSync 的既有客户端工程，数学核心源自下列项目。仓库级完整清单见根目录 `THIRD_PARTY_NOTICES.md`。

## FixedMath.Net / Fix64

- https://github.com/asik/FixedMath.Net
- Apache License 2.0（André Slupik 等）
- 上游亦包含 libfixmath（MIT）、log2fix（MIT）相关算法

## Jitter Physics 向量与矩阵

- `TSVector` / `TSMatrix` / `TSMatrix4x4` / `TSQuaternion` / `TSVector4` / `TSMath`
- zlib 许可（Thorben Linneweber，2009-2011），见各文件头；文件头声明不得删除，改动版本须标明已修改

## Mono.Xna

- `TSVector2`，MIT 许可（The Mono.Xna Team），见文件头

## Photon TrueSync 数学封装

- 部分封装来自 Photon TrueSync（Exit Games）；使用方须自行确认该部分授权

## Framework 改动

- 命名空间：`TrueSync` → `Framework.FixedMath`
- 移除 `IWGames`、`Obscured*` 隐式转换（原工程反作弊相关）
- 补充 `FPConversions` 表现层桥接（`Framework.FixedMath.Unity`）

完整许可证文本以各上游仓库为准。
