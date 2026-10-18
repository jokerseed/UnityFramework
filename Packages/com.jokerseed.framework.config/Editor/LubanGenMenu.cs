using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using Framework.Logging;
using UnityEditor;
using UnityEngine;

namespace Framework.Editor
{
    /// <summary>
    /// Luban 打表：直接调用 <c>dotnet Luban.dll</c>，不依赖 cmd / bash / Python，Windows 与 macOS 行为一致。
    /// 路径在 Project Settings → Framework → Luban 中配置。菜单 <c>Tools/Framework/Config/Generate Luban Config</c>。
    /// 子进程运行期间进度条可以取消；超过 <see cref="ProcessTimeout"/> 仍未结束就终止进程，编辑器不会一直卡住。
    /// </summary>
    public static class LubanGenMenu
    {
        const string MenuPath = "Tools/Framework/Config/Generate Luban Config";
        const string ProgressTitle = "Luban";

        /// <summary>单个子进程（首次编译 Luban 或一次打表）最多等多久。</summary>
        static readonly TimeSpan ProcessTimeout = TimeSpan.FromMinutes(10);

        /// <summary>生成 C# 代码与二进制数据，校验生成代码前缀，再导出调试 JSON。</summary>
        [MenuItem(MenuPath, priority = 100)]
        public static void GenerateClientConfig()
        {
            var settings = FrameworkSettings.instance.Luban;
            try
            {
                EditorUtility.DisplayProgressBar(ProgressTitle, "准备 Luban CLI...", 0.1f);
                var lubanDll = EnsureLubanDll(settings);

                var log = new StringBuilder();
                log.AppendLine(RunLuban(settings, lubanDll, "-c cs-bin -d bin",
                    FrameworkSettings.ToAbsolute(settings.OutputCodeDir),
                    FrameworkSettings.ToAbsolute(settings.OutputDataDir),
                    "生成代码与二进制数据...", 0.4f));

                EditorUtility.DisplayProgressBar(ProgressTitle, "校验生成代码命名...", 0.7f);
                log.AppendLine(LubanCodegenValidator.Validate(
                    FrameworkSettings.ToAbsolute(settings.CodegenRules),
                    FrameworkSettings.ToAbsolute(settings.OutputCodeDir)));

                if (!string.IsNullOrEmpty(settings.OutputJsonDir))
                {
                    var ignoredCode = Path.Combine(Path.GetTempPath(), "luban_json_code_ignored");
                    log.AppendLine(RunLuban(settings, lubanDll, "-d json", ignoredCode,
                        FrameworkSettings.ToAbsolute(settings.OutputJsonDir),
                        "导出调试 JSON...", 0.85f));
                }

                GameLog.Info(LogCategories.Luban, $"Generate {LogStyle.Ok("finished")}.\n{log}");
                AssetDatabase.Refresh();
                EditorUtility.DisplayDialog("Luban", "配置表生成完成。", "确定");
            }
            catch (OperationCanceledException)
            {
                GameLog.Warning(LogCategories.Luban, "Generate cancelled; generated files may be incomplete.");
                EditorUtility.DisplayDialog("Luban", "已取消打表，生成目录里的文件可能不完整，请重新打表。", "确定");
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

        static string EnsureLubanDll(LubanSettings settings)
        {
            var dll = FrameworkSettings.ToAbsolute(settings.LubanDll);
            if (File.Exists(dll))
            {
                return dll;
            }

            var project = FrameworkSettings.ToAbsolute(settings.LubanProject);
            if (!File.Exists(project))
            {
                throw new FileNotFoundException($"找不到 Luban.dll，也找不到 Luban 源码工程：\n{dll}\n{project}");
            }

            Run(settings.DotnetExecutable,
                $"build \"{project}\" -c Release -o \"{Path.GetDirectoryName(dll)}\"",
                FrameworkSettings.ProjectRoot,
                "首次编译 Luban CLI...", 0.2f);
            if (!File.Exists(dll))
            {
                throw new FileNotFoundException($"Luban 构建完成但未找到 {dll}，请检查 LubanDll 设置。");
            }

            return dll;
        }

        static string RunLuban(
            LubanSettings settings,
            string lubanDll,
            string modeArgs,
            string codeDir,
            string dataDir,
            string progressInfo,
            float progress)
        {
            var conf = FrameworkSettings.ToAbsolute(settings.ConfFile);
            if (!File.Exists(conf))
            {
                throw new FileNotFoundException($"找不到 luban.conf：{conf}");
            }

            var args = $"\"{lubanDll}\" -t client {modeArgs} --conf \"{conf}\" " +
                       $"-x \"outputCodeDir={codeDir}\" -x \"outputDataDir={dataDir}\"";
            return Run(settings.DotnetExecutable, args, Path.GetDirectoryName(conf), progressInfo, progress);
        }

        static string Run(string fileName, string arguments, string workingDirectory, string progressInfo, float progress)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };

            Process process;
            try
            {
                process = Process.Start(startInfo);
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                throw new InvalidOperationException(
                    $"无法启动 {fileName}（{ex.Message}）。请安装 .NET SDK，或在 Project Settings → Framework 中填写 dotnet 的绝对路径。", ex);
            }

            using (process)
            {
                if (process == null)
                {
                    throw new InvalidOperationException($"无法启动 {fileName}。");
                }

                // 两路输出都异步读，主线程每 200ms 轮询一次：既能刷新可取消的进度条，又不会因为管道写满而互相等死。
                var stdoutTask = process.StandardOutput.ReadToEndAsync();
                var stderrTask = process.StandardError.ReadToEndAsync();
                var watch = Stopwatch.StartNew();
                while (!process.WaitForExit(200))
                {
                    var elapsed = watch.Elapsed;
                    if (EditorUtility.DisplayCancelableProgressBar(
                            ProgressTitle,
                            $"{progressInfo}（{(int)elapsed.TotalSeconds}s，可取消）",
                            progress))
                    {
                        KillQuietly(process);
                        throw new OperationCanceledException($"已取消：{fileName} {arguments}");
                    }

                    if (elapsed > ProcessTimeout)
                    {
                        KillQuietly(process);
                        throw new TimeoutException($"{fileName} 运行超过 {ProcessTimeout.TotalMinutes} 分钟仍未结束，已终止。\n{arguments}");
                    }
                }

                // 带超时的 WaitForExit 返回时输出可能还没读完，再等一次无参版本让异步读取收尾。
                process.WaitForExit();
                var stdout = stdoutTask.Result;
                var stderr = stderrTask.Result;
                if (process.ExitCode != 0)
                {
                    throw new InvalidOperationException($"{fileName} 退出码 {process.ExitCode}。\n{stdout}\n{stderr}");
                }

                return string.IsNullOrWhiteSpace(stderr) ? stdout : $"{stdout}\n{stderr}";
            }
        }

        static void KillQuietly(Process process)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill();
                    process.WaitForExit(5000);
                }
            }
            catch (InvalidOperationException)
            {
                // 进程已经退出。
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                GameLog.Warning(LogCategories.Luban, $"Failed to kill {process.StartInfo.FileName}: {ex.Message}");
            }
        }
    }

    /// <summary>按 codegen.json 校验 Luban 生成的 C# 文件名前缀（原 validate_codegen_prefix.py）。</summary>
    public static class LubanCodegenValidator
    {
        [Serializable]
        sealed class Rules
        {
            public string codeTypePrefix;
            public string[] exemptTypeNames;
            public bool enforceOnGenerate;
        }

        /// <summary>校验生成代码目录下的 .cs 文件名。</summary>
        /// <param name="rulesPath">codegen.json 绝对路径。</param>
        /// <param name="codeDir">生成代码目录绝对路径。</param>
        /// <returns>校验报告。</returns>
        /// <exception cref="InvalidOperationException">规则无效、目录不存在，或 <c>enforceOnGenerate</c> 为 true 且有文件缺少前缀。</exception>
        public static string Validate(string rulesPath, string codeDir)
        {
            if (!File.Exists(rulesPath))
            {
                throw new InvalidOperationException($"缺少 {rulesPath}");
            }

            var rules = JsonUtility.FromJson<Rules>(File.ReadAllText(rulesPath, Encoding.UTF8));
            var prefix = rules?.codeTypePrefix?.Trim();
            if (string.IsNullOrEmpty(prefix))
            {
                throw new InvalidOperationException("codegen.json 的 codeTypePrefix 为空。");
            }

            if (!Directory.Exists(codeDir))
            {
                throw new InvalidOperationException($"生成代码目录不存在：{codeDir}");
            }

            var exempt = new HashSet<string>(rules.exemptTypeNames ?? Array.Empty<string>());
            var offenders = FindOffenders(codeDir, prefix, exempt);
            if (offenders.Count == 0)
            {
                return $"[Luban] codeTypePrefix='{prefix}' 校验通过";
            }

            var message = $"[Luban] {offenders.Count} 个生成文件缺少前缀 '{prefix}'：{string.Join(", ", offenders)}。" +
                          "请在 Config/Luban/Defines 中改类型名后重新生成。";
            if (rules.enforceOnGenerate)
            {
                throw new InvalidOperationException(message);
            }

            return message;
        }

        /// <summary>列出缺少前缀的生成文件名。</summary>
        /// <param name="codeDir">生成代码目录。</param>
        /// <param name="prefix">要求的前缀。</param>
        /// <param name="exempt">豁免的类型名（不含扩展名）。</param>
        /// <returns>缺少前缀的文件名。</returns>
        public static List<string> FindOffenders(string codeDir, string prefix, ICollection<string> exempt)
        {
            var offenders = new List<string>();
            foreach (var path in Directory.GetFiles(codeDir, "*.cs", SearchOption.TopDirectoryOnly))
            {
                var stem = Path.GetFileNameWithoutExtension(path);
                if (!exempt.Contains(stem) && !stem.StartsWith(prefix, StringComparison.Ordinal))
                {
                    offenders.Add(stem + ".cs");
                }
            }

            offenders.Sort(StringComparer.Ordinal);
            return offenders;
        }
    }
}
