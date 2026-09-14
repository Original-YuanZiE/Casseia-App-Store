using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace App_Store.Core
{
    public class WinGetCli
    {
        
        private static readonly SemaphoreSlim _lock = new(1, 1);

        public async Task<string> RunAsync(string arguments, int timeoutMs = 60000)
        {
            await _lock.WaitAsync();
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "winget",
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = false,
                    UseShellExecute = false,
                    StandardOutputEncoding = Encoding.UTF8,
                    WindowStyle = ProcessWindowStyle.Hidden,
                };

                using var process = Process.Start(psi);
                using var cts = new CancellationTokenSource(timeoutMs);
                var output = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync(cts.Token);
                return output;
            }
            finally
            {
                _lock.Release();
            }
        }

        private async Task<int> RunWithExitCodeAsync(string arguments, int timeoutMs = 300000)
        {
            await _lock.WaitAsync();
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "winget",
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    StandardOutputEncoding = Encoding.UTF8
                };

                using var process = Process.Start(psi);
                using var cts = new CancellationTokenSource(timeoutMs);

                // 逐字符读取 stdout，处理进度指示（CR = 进度更新，LF = 新行）
                var outputBuilder = new StringBuilder();
                var buffer = new char[1];
                while (!process.StandardOutput.EndOfStream)
                {
                    var read = await process.StandardOutput.ReadAsync(buffer, 0, 1);
                    if (read == 0) break;

                    if (buffer[0] == '\n')
                    {
                        OnOutputLine?.Invoke(outputBuilder.ToString());
                        outputBuilder.Clear();
                    }
                    else if (buffer[0] != '\r')
                    {
                        outputBuilder.Append(buffer[0]);
                    }
                    // \r 用于进度条动画，跳过即可
                }

                await process.WaitForExitAsync(cts.Token);
                return process.ExitCode;
            }
            finally
            {
                _lock.Release();
            }
        }

        // 可选：输出回调，用于 UI 显示进度
        public event Action<string> OnOutputLine;

        // 搜索
        public async Task<List<WinGetPackage>> SearchAsync(string query)
        {
            var output = await RunAsync(
                $"search \"{query}\" --accept-source-agreements --disable-interactivity");
            return ParseTable(output);
        }

        // 已安装
        public async Task<List<WinGetPackage>> GetInstalledAsync()
        {
            var output = await RunAsync(
                "list --accept-source-agreements --disable-interactivity");
            return ParseTable(output);
        }

        // 可用更新
        public async Task<List<WinGetPackage>> GetUpdatesAsync()
        {
            var output = await RunAsync(
                "upgrade --include-unknown --accept-source-agreements --disable-interactivity");
            return ParseTable(output);
        }

        // 可用版本
        public async Task<List<string>> GetVersionsAsync(string id)
        {
            var output = await RunAsync(
                $"show --id {id} --exact --versions --accept-source-agreements");
            // 输出格式：每行一个版本号，在 "Versions:" 行之后
            var lines = output.Split('\n');
            var versions = new List<string>();
            bool found = false;
            foreach (var line in lines)
            {
                if (line.Trim().StartsWith("Version"))
                {
                    found = true;
                    continue;
                }
                if (found && !string.IsNullOrWhiteSpace(line))
                    versions.Add(line.Trim());
            }
            return versions;
        }

        // 安装
        public async Task<OperationResult> InstallAsync(string id, string source = null,
    string version = null, bool silent = true)
        {
            var args = $"install --id {id} --exact --accept-source-agreements --disable-interactivity";
            if (!string.IsNullOrEmpty(source)) args += $" --source {source}";
            if (!string.IsNullOrEmpty(version)) args += $" --version {version}";
            if (silent) args += " --silent";

            var exitCode = await RunWithExitCodeAsync(args, timeoutMs: 300000);
            return ExitCodeHelper.Interpret(exitCode);
        }

        // 更新
        public async Task<int> UpdateAsync(string id)
        {
            return await RunWithExitCodeAsync(
                $"upgrade --id {id} --exact --accept-source-agreements --disable-interactivity --silent");
        }

        // 卸载
        public async Task<int> UninstallAsync(string id)
        {
            return await RunWithExitCodeAsync(
                $"uninstall --id {id} --exact --accept-source-agreements --disable-interactivity --silent");
        }

        public class WinGetPackage
        {
            public string Name { get; set; }
            public string Id { get; set; }
            public string Version { get; set; }
            public string AvailableVersion { get; set; } // upgrade 时有
            public string Source { get; set; }
        }

        public static List<WinGetPackage> ParseTable(string output)
        {
            var lines = output.Split('\n');
            var packages = new List<WinGetPackage>();

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i].TrimEnd('\r');
                if (!IsSeparatorLine(line)) continue;

                // 分隔线的上一行是表头
                if (i == 0) continue;
                var headerLine = lines[i - 1].TrimEnd('\r');

                // 解析列起始位置
                var columns = ParseColumnStarts(headerLine);
                if (columns.Count < 3) continue;

                // 读取数据行
                for (int j = i + 1; j < lines.Length; j++)
                {
                    var dataLine = lines[j].TrimEnd('\r');
                    if (string.IsNullOrWhiteSpace(dataLine)) break;
                    if (IsSeparatorLine(dataLine)) break;

                    var name = GetCell(dataLine, columns, 0).Trim();
                    var id = GetCell(dataLine, columns, 1).Trim();
                    var version = GetCell(dataLine, columns, 2).Trim();

                    // 有第4列且不是 Available/Match 才是 Source
                    string source = "";
                    if (columns.Count >= 4)
                    {
                        var col4 = GetCell(dataLine, columns, 3).Trim();
                        if (col4 != "" && !col4.Contains('.'))
                            source = col4;
                    }

                    if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(id)) continue;
                    if (name.Equals("Name", StringComparison.OrdinalIgnoreCase)) continue;
                    if (name.Equals("名称", StringComparison.OrdinalIgnoreCase)) continue;

                    packages.Add(new WinGetPackage
                    {
                        Name = name,
                        Id = id,
                        Version = version,
                        Source = source
                    });
                }
            }
            return packages;
        }

        private static bool IsSeparatorLine(string line)
        {
            int dashes = 0;
            foreach (char c in line)
            {
                if (c == '-') dashes++;
                else if (c != ' ') return false;
            }
            return dashes >= 3;
        }

        private static List<int> ParseColumnStarts(string headerLine)
        {
            var starts = new List<int>();
            bool prevWasSpace = true;
            int displayCol = 0;
            int index = 0;

            while (index < headerLine.Length)
            {
                int codePoint = FirstCodePoint(headerLine, index);
                bool isSpace = codePoint == ' ';

                if (!isSpace && prevWasSpace)
                    starts.Add(displayCol);

                prevWasSpace = isSpace;
                displayCol += GetDisplayWidth(codePoint);
                index += TextElementLength(headerLine, index);
            }
            return starts;
        }

        private static string GetCell(string line, List<int> columns, int colIndex)
        {
            if (colIndex >= columns.Count) return "";

            int start = CharIndexOfColumn(line, columns[colIndex]);
            if (start >= line.Length) return "";

            int end = colIndex + 1 < columns.Count
                ? CharIndexOfColumn(line, columns[colIndex + 1])
                : line.Length;

            if (end > line.Length) end = line.Length;
            return end <= start ? "" : line[start..end].Trim();
        }

        private static int CharIndexOfColumn(string line, int displayColumn)
        {
            int index = 0;
            int width = 0;

            while (index < line.Length && width < displayColumn)
            {
                width += GetDisplayWidth(FirstCodePoint(line, index));
                index += TextElementLength(line, index);
            }

            // 回退到词边界，避免截断中文字
            while (index > 0 && index < line.Length
                && line[index] != ' ' && line[index - 1] != ' ')
            {
                index--;
            }
            return index;
        }

        private static int FirstCodePoint(string text, int index) =>
            char.IsHighSurrogate(text[index]) && index + 1 < text.Length && char.IsLowSurrogate(text[index + 1])
                ? char.ConvertToUtf32(text[index], text[index + 1])
                : text[index];

        private static int TextElementLength(string text, int index) =>
            Math.Max(1, StringInfo.GetNextTextElementLength(text.AsSpan(index)));

        private static int GetDisplayWidth(int codePoint) => IsFullWidth(codePoint) ? 2 : 1;

        // CJK / 全角字符范围（简化版，覆盖常见情况）
        private static bool IsFullWidth(int codePoint) =>
            (codePoint >= 0x1100 && codePoint <= 0x115F) ||
            (codePoint >= 0x2E80 && codePoint <= 0x303E) ||
            (codePoint >= 0x3040 && codePoint <= 0x33BF) ||
            (codePoint >= 0x3400 && codePoint <= 0x4DBF) ||
            (codePoint >= 0x4E00 && codePoint <= 0x9FFF) ||
            (codePoint >= 0xA000 && codePoint <= 0xA4CF) ||
            (codePoint >= 0xAC00 && codePoint <= 0xD7AF) ||
            (codePoint >= 0xF900 && codePoint <= 0xFAFF) ||
            (codePoint >= 0xFE10 && codePoint <= 0xFE6F) ||
            (codePoint >= 0xFF01 && codePoint <= 0xFF60) ||
            (codePoint >= 0xFFE0 && codePoint <= 0xFFE6) ||
            (codePoint >= 0x20000 && codePoint <= 0x2FA1F);

        public enum OperationResult
        {
            Success,
            Failure,
            Canceled,
            NeedElevation
        }

        public static class ExitCodeHelper
        {
            public static OperationResult Interpret(int code)
            {
                uint u = (uint)code;
                return u switch
                {
                    0x00000000 => OperationResult.Success,
                    0x8A150109 => OperationResult.Success,       // 需要重启
                    0x8A15010D => OperationResult.Success,       // 已安装
                    0x8A15004F => OperationResult.Success,       // 已安装（另一种）
                    0x8A150077 => OperationResult.Canceled,      // 用户取消
                    0x8A15010C => OperationResult.Canceled,
                    0x8A150011 => OperationResult.Failure,       // 哈希不匹配
                    0x8A150019 => OperationResult.NeedElevation, // 需要管理员
                    0x80073D28 => OperationResult.NeedElevation,
                    _ => OperationResult.Failure
                };
            }
        }

        public static OperationResult InterpretExitCode(int code)
        {
            uint u = (uint)code;
            return u switch
            {
                0x00000000 => OperationResult.Success,
                0x8A150109 => OperationResult.Success,      // 需要重启
                0x8A15010D => OperationResult.Success,      // 已安装
                0x8A15004F => OperationResult.Success,      // 已安装（另一种）
                0x8A150077 => OperationResult.Canceled,     // 用户取消
                0x8A15010C => OperationResult.Canceled,
                0x8A150011 => OperationResult.Failure,      // 哈希不匹配
                0x8A150019 => OperationResult.NeedElevation,// 需要管理员权限
                0x80073D28 => OperationResult.NeedElevation,
                _ => OperationResult.Failure
            };
        }
    }
}
