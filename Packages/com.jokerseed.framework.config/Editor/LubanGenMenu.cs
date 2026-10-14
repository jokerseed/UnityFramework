using System;
using System.Diagnostics;
using System.IO;
using Framework.Logging;
using UnityEditor;
using UnityEngine;

namespace Framework.Editor
{
    /// <summary>Luban 打表 Editor 菜单，提供 <c>Tools/Luban/Generate Client Config</c> 快捷入口。</summary>
    public static class LubanGenMenu
    {
        const string MenuPath = "Tools/Luban/Generate Client Config";

        /// <summary>执行 Luban 客户端配置表生成（调用 <c>Config/Luban/gen_client.bat</c>）。</summary>
        [MenuItem(MenuPath)]
        public static void GenerateClientConfig()
        {
            var batPath = GetGenClientBatPath();
            if (!File.Exists(batPath))
            {
                EditorUtility.DisplayDialog(
                    "Luban",
                    $"找不到打表脚本：\n{batPath}",
                    "确定");
                return;
            }

            try
            {
                EditorUtility.DisplayProgressBar("Luban", "正在生成配置...", 0.5f);
                var output = RunBat(batPath);
                GameLog.Info(LogCategories.Luban, $"Generate {LogStyle.Ok("finished")}.\n{output}");
                AssetDatabase.Refresh();
                EditorUtility.DisplayDialog("Luban", "配置表生成完成。", "确定");
            }
            catch (Exception ex)
            {
                GameLog.Exception(LogCategories.Luban, ex, $"Generate {LogStyle.Fail("failed")}.");
                EditorUtility.DisplayDialog("Luban", $"打表失败：\n{ex.Message}", "确定");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        /// <summary>菜单可用性验证：编译中或播放模式下禁用该菜单项。</summary>
        /// <returns>可执行时返回 true。</returns>
        [MenuItem(MenuPath, true)]
        public static bool GenerateClientConfigValidate()
        {
            return !EditorApplication.isCompiling && !EditorApplication.isPlaying;
        }

        static string GetGenClientBatPath()
        {
            var projectRoot = Directory.GetParent(Application.dataPath).FullName;
            return Path.Combine(projectRoot, "Config", "Luban", "gen_client.bat");
        }

        static string RunBat(string batPath)
        {
            var workingDir = Path.GetDirectoryName(batPath);
            var startInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c \"{batPath}\"",
                WorkingDirectory = workingDir,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };

            using (var process = Process.Start(startInfo))
            {
                if (process == null)
                {
                    throw new InvalidOperationException("无法启动打表进程。");
                }

                var stdout = process.StandardOutput.ReadToEnd();
                var stderr = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    throw new InvalidOperationException(
                        $"打表进程退出码 {process.ExitCode}。\n{stdout}\n{stderr}");
                }

                if (!string.IsNullOrWhiteSpace(stderr))
                {
                    return $"{stdout}\n{stderr}";
                }

                return stdout;
            }
        }
    }
}
