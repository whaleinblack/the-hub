using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

internal sealed class QueueResult
{
    internal bool Accepted;
    internal bool Uncertain;
    internal string Error;
}

internal static class QueueDelivery
{
    // Windows argv escaping, not shell escaping. ProcessStartInfo.UseShellExecute is always false.
    internal static string Quote(string value) {
        var result = new StringBuilder("\""); int slashes = 0;
        foreach (char c in value) {
            if (c == '\\') { slashes++; continue; }
            if (c == '"') { result.Append('\\', slashes * 2 + 1); result.Append('"'); }
            else { result.Append('\\', slashes); result.Append(c); }
            slashes = 0;
        }
        result.Append('\\', slashes * 2); return result.Append('"').ToString();
    }
    internal static string FindCli() {
        string overrideFile = Path.Combine(Program.Home, "codex-path.txt");
        if (File.Exists(overrideFile)) {
            string custom = File.ReadAllText(overrideFile).Trim();
            if (File.Exists(custom) && String.Equals(Path.GetFileName(custom), "codex.exe", StringComparison.OrdinalIgnoreCase)) return custom;
            throw new InvalidOperationException("Configured CLI is unavailable.");
        }
        string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpenAI", "Codex", "bin");
        if (Directory.Exists(root)) {
            var found = Directory.GetDirectories(root).Select(d => Path.Combine(d, "codex.exe")).Where(File.Exists)
                .OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
            if (found != null) return found;
        }
        throw new FileNotFoundException("Codex CLI unavailable.");
    }
    internal static bool IsAcknowledged(string output, string thread) {
        return output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Any(line => line.StartsWith("Queued message ", StringComparison.Ordinal) && line.EndsWith(" for thread " + thread + ".", StringComparison.Ordinal));
    }
    internal static QueueResult Send(string query) {
        bool started = false;
        try {
            CodexLauncher.ValidateQuery(query);
            string uri = CodexLauncher.ThreadUri();
            string thread = uri.Substring("codex://threads/".Length);
            var info = new ProcessStartInfo(FindCli(), "queue --thread " + Quote(thread) + " --message " + Quote(query.Trim())) {
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8, WorkingDirectory = Program.Home
            };
            using (var process = Process.Start(info)) {
                started = true;
                var output = process.StandardOutput.ReadToEndAsync();
                var errors = process.StandardError.ReadToEndAsync();
                if (!process.WaitForExit(10000)) {
                    try { process.Kill(); } catch { }
                    return new QueueResult { Uncertain = true, Error = "发送状态未确认，请先检查助手对话。" };
                }
                if (!Task.WaitAll(new Task[] { output, errors }, 1000)) return new QueueResult { Uncertain = true, Error = "发送状态未确认，请先检查助手对话。" };
                if (process.ExitCode == 0 && IsAcknowledged(output.Result, thread)) return new QueueResult { Accepted = true };
                return new QueueResult { Uncertain = true, Error = "队列未确认接收，请先检查助手对话。" };
            }
        } catch (Exception ex) {
            Program.Log(ex.GetType().Name);
            return new QueueResult { Uncertain = started, Error = started ? "发送状态未确认，请先检查助手对话。" : "无法自动发送，可使用复制转入。" };
        }
    }
}
