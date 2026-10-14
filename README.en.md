# UnityFramework

[中文](README.md) · [Changelog](CHANGELOG.md) · [Contributing](CONTRIBUTING.md) · [MIT License](LICENSE)

A client framework for **Unity 2021.3 LTS**. It covers modular startup, resources and hot update, data tables, UI, networking and audio, plus an optional **deterministic battle kit** (fixed-point math, lockstep with rollback, GAS, ECS, behaviour trees). All async APIs use **UniTask** and run on mobile, desktop and WebGL / mini-game platforms.

The framework ships as 7 embedded UPM packages (`Packages/com.jokerseed.framework.*`), one asmdef per feature, so you reference only what you need. `Assets/Scripts` is a demo game layer; the framework never references it.

## Quick start

Requirements: Unity **2021.3** (currently `2021.3.21f1`); .NET 8 SDK for table generation and `dotnet test`.

1. Open the repository root in Unity.
2. Open `Assets/Bundles/Scenes/Launch.unity` and press Play.
3. The `launch` module group becomes Ready and the main menu appears. The Battle scene shows GAS casting with ECS projectiles.

In the Editor, `ResourceManager` uses `EditorSimulateMode`, so no bundles need to be built first.

To use the framework in your own project, copy the packages you need into `Packages/` (or reference them by Git URL) and add UniTask, YooAsset and luban_unity to your manifest. See `Assets/Scripts/Launch.cs` for a complete startup example.

## Packages

| Package | Contents |
|---------|----------|
| **core** | Bootstrap (dependency-ordered async module init with rollback), logging, zero-GC events, coroutine lifetime, memory/object pools, FSM and procedures, timers, save/settings storage, localization, runtime debug console |
| **res** | YooAsset wrapper with frame-budgeted loading, scenes, scopes, `ResourcePatcher` for patch download, optional HybridCLR hot-update assembly loading |
| **config** | Luban table loading and caching, async preloading for WebGL |
| **ui** | Layered window stack, release policies, async opening, back-key routing, input blocking, safe area, localized text |
| **audio** | BGM / SFX channels |
| **network** | TCP, WebSocket (including WebGL) and KCP channels, heartbeats, main-thread dispatch, `NetworkRpcClient` with timeouts; separate HTTP requests |
| **battle** | Deterministic battle kit (optional) |

Each module has a README next to its code (in Chinese). The architecture overview is in [docs/architecture.md](docs/architecture.md).

## Battle kit status

- Fixed-point math is bit-identical across platforms; CI checks the same golden replay on Linux and Windows.
- `LockstepHost` waits for all inputs and rolls back to checkpoints covering GAS, ECS, RNG, physics and behaviour trees. Inputs carry `BattleSimulationVersion`; a mismatch stops the match immediately.
- Replays and verification: `BattleReplayRecorder` / `BattleReplayVerifier`.
- No real network transport is bundled: the framework defines `ICommunicator`, and tests use an in-memory loopback.
- No memory anti-cheat is built in.

Any change that alters per-tick results must bump `BattleSimulationVersion.Current` and update the golden replay, or CI fails.

## Tooling

All editor tools live under **Tools → Framework**, and their paths are configured in **Project Settings → Framework**. Table generation calls `dotnet Luban.dll` directly (no Python, same on Windows and macOS). Command-line scripts: `Config/Luban/gen_client.bat` / `gen_client.sh`. See [docs/editor-tools.md](docs/editor-tools.md).

## Tests and CI

- `dotnet test tests/Framework.Sim.Tests` covers the engine-independent layers and the golden replay.
- Unity EditMode tests are in `Assets/Tests/EditMode`.
- `.github/workflows/ci.yml` runs the dotnet tests and the determinism scan on Linux and Windows for every push and PR. `editmode-tests.yml` is manual and needs Unity license secrets.

## License

The framework's own code is under the [MIT License](LICENSE). Third-party components keep their own licenses; see [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).

**Note:** `Assets/ThirdParty/` contains commercial Asset Store plugins (Animancer, Odin Inspector, DOTween Pro, Spine, Behavior Designer, and others). They are used only for the author's local development and demo. They are **not** covered by the MIT license and you may not redistribute them. The framework packages do not depend on them.
