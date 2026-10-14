using System;
using System.Collections.Generic;
using System.Text;

namespace Framework.DebugConsole
{
    /// <summary>
    /// 调试命令表：按名称注册处理函数，解析一行输入并执行。与界面无关，可单独测试或接到别的终端。
    /// 参数按空白分隔，双引号包住的部分算一个参数。
    /// </summary>
    public sealed class DebugCommandRegistry
    {
        sealed class Command
        {
            public string Help;
            public Func<string[], string> Handler;
        }

        readonly SortedDictionary<string, Command> _commands = new SortedDictionary<string, Command>(StringComparer.OrdinalIgnoreCase);

        /// <summary>已注册的命令名（按字母序）。</summary>
        public IEnumerable<string> Names => _commands.Keys;

        /// <summary>注册命令；同名命令会被替换。</summary>
        /// <param name="name">命令名，不含空白。</param>
        /// <param name="help">一行说明。</param>
        /// <param name="handler">处理函数，参数不含命令名；返回要输出的文本，可为 null。</param>
        /// <exception cref="ArgumentException"><paramref name="name"/> 为空或含空白。</exception>
        /// <exception cref="ArgumentNullException"><paramref name="handler"/> 为 null。</exception>
        public void Register(string name, string help, Func<string[], string> handler)
        {
            if (string.IsNullOrEmpty(name) || name.IndexOfAny(new[] { ' ', '\t', '"' }) >= 0)
            {
                throw new ArgumentException("Command name must be non-empty and contain no whitespace or quotes.", nameof(name));
            }

            _commands[name] = new Command
            {
                Help = help ?? string.Empty,
                Handler = handler ?? throw new ArgumentNullException(nameof(handler)),
            };
        }

        /// <summary>注销命令。</summary>
        /// <param name="name">命令名。</param>
        /// <returns>存在并注销返回 true。</returns>
        public bool Unregister(string name) => _commands.Remove(name);

        /// <summary>解析并执行一行输入。</summary>
        /// <param name="line">输入行。</param>
        /// <returns>命令输出；空行返回 null。</returns>
        /// <exception cref="KeyNotFoundException">命令不存在。</exception>
        public string Execute(string line)
        {
            var tokens = Tokenize(line);
            if (tokens.Count == 0)
            {
                return null;
            }

            if (!_commands.TryGetValue(tokens[0], out var command))
            {
                throw new KeyNotFoundException($"Unknown command: {tokens[0]}. Type 'help' to list commands.");
            }

            tokens.RemoveAt(0);
            return command.Handler(tokens.ToArray());
        }

        /// <summary>生成全部命令的说明文本。</summary>
        /// <returns>每行一个命令。</returns>
        public string BuildHelp()
        {
            var builder = new StringBuilder();
            foreach (var pair in _commands)
            {
                builder.Append(pair.Key);
                if (pair.Value.Help.Length > 0)
                {
                    builder.Append(" - ").Append(pair.Value.Help);
                }

                builder.Append('\n');
            }

            return builder.ToString().TrimEnd('\n');
        }

        /// <summary>按空白分词，双引号内视为一个参数。</summary>
        /// <param name="line">输入行。</param>
        /// <returns>分词结果。</returns>
        public static List<string> Tokenize(string line)
        {
            var tokens = new List<string>();
            if (string.IsNullOrEmpty(line))
            {
                return tokens;
            }

            var current = new StringBuilder();
            var quoted = false;
            var hasToken = false;
            for (var i = 0; i < line.Length; i++)
            {
                var c = line[i];
                if (c == '"')
                {
                    quoted = !quoted;
                    hasToken = true;
                    continue;
                }

                if (!quoted && char.IsWhiteSpace(c))
                {
                    if (hasToken)
                    {
                        tokens.Add(current.ToString());
                        current.Clear();
                        hasToken = false;
                    }

                    continue;
                }

                current.Append(c);
                hasToken = true;
            }

            if (hasToken)
            {
                tokens.Add(current.ToString());
            }

            return tokens;
        }
    }
}
