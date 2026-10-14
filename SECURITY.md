# 安全策略

## 支持的版本

项目处于 0.x 阶段，只对 `main` 分支的最新代码修复安全问题。

## 报告漏洞

**请不要在公开 Issue 里报告安全漏洞。**

请使用 GitHub 的 [Private vulnerability reporting](https://github.com/jokerseed/UnityFramework/security/advisories/new) 提交，或私信维护者 [@jokerseed](https://github.com/jokerseed)。报告里请尽量包含：

- 受影响的模块和版本（提交哈希）
- 复现步骤或最小复现工程
- 你判断的影响范围

我们会在 7 天内确认收到，并在修复发布后公开致谢（如果你愿意）。

## 范围

特别关注：

- 网络层（`Framework.Network`、KCP、WebSocket、RPC）对不可信输入的解析
- 帧同步输入校验与反作弊相关逻辑
- 资源下载与补丁流程（`Framework.Res`）的校验
- 编辑器工具执行外部进程（Luban 打表等）时的命令注入风险
