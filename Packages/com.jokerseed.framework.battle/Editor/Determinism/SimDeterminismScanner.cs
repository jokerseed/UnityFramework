using System.Collections.Generic;
using System.IO;
using Framework.Logging;
using UnityEditor;
using UnityEngine;

namespace Framework.Editor.Determinism
{
    /// <summary>
    /// 战斗逻辑确定性扫描的 Unity 入口（菜单与批处理）。规则本体见 <see cref="DeterminismRules"/>，
    /// 纯 .NET CI（<c>tests/Framework.Sim.Tests</c>）编译的也是同一份规则。
    /// 这些程序集都开了 <c>noEngineReferences</c>，Unity API 已经由编译器挡住；扫描器负责编译器挡不住的部分。
    /// </summary>
    public static class SimDeterminismScanner
    {
        /// <summary>战斗包运行时代码的根目录（相对工程根）。</summary>
        public const string BattleRuntimeRoot = DeterminismRules.BattleRuntimeRoot;

        /// <summary>相对工程根目录的扫描根，见 <see cref="DeterminismRules.ScanRoots"/>。</summary>
        public static readonly string[] ScanRoots = DeterminismRules.ScanRoots;

        /// <summary>扫描工程里的战斗逻辑目录。</summary>
        /// <param name="projectRoot">工程根目录（<c>Assets</c> 的上一级）；为空时用当前编辑器工程。</param>
        /// <returns>违规说明；空列表表示通过。</returns>
        public static List<string> Scan(string projectRoot = null)
        {
            var root = string.IsNullOrEmpty(projectRoot)
                ? Path.GetDirectoryName(Application.dataPath)
                : projectRoot;
            return DeterminismRules.Scan(root);
        }

        /// <summary>对一段源码收集违规，供编辑器菜单和测试复用。</summary>
        /// <param name="relativePath">显示用路径。</param>
        /// <param name="source">C# 源码。</param>
        /// <param name="violations">追加违规说明。</param>
        public static void CollectViolations(string relativePath, string source, List<string> violations)
        {
            DeterminismRules.CollectViolations(relativePath, source, violations);
        }

        /// <summary>
        /// 批处理入口：<c>Unity -batchmode -executeMethod Framework.Editor.Determinism.SimDeterminismScanner.ScanBatch</c>。
        /// 有违规时退出码为 1。
        /// </summary>
        public static void ScanBatch()
        {
            var violations = Scan();
            if (violations.Count == 0)
            {
                Debug.Log("Determinism scan passed.");
            }
            else
            {
                for (var i = 0; i < violations.Count; i++)
                {
                    Debug.LogError(violations[i]);
                }
            }

            if (Application.isBatchMode)
            {
                EditorApplication.Exit(violations.Count == 0 ? 0 : 1);
            }
        }

        /// <summary>菜单：Tools/Framework/Battle/Scan Determinism。</summary>
        [MenuItem("Tools/Framework/Battle/Scan Determinism")]
        public static void ScanFromMenu()
        {
            var violations = Scan();
            if (violations.Count == 0)
            {
                GameLog.Info(LogCategories.GamePlay, "Determinism scan passed.");
                return;
            }

            for (var i = 0; i < violations.Count; i++)
            {
                GameLog.Error(LogCategories.GamePlay, violations[i]);
            }
        }
    }
}
