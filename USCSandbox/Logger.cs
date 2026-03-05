using System.Text;

namespace USCSandbox
{
    public static class Logger
    {
        private static readonly object _sync = new object();
        private static StreamWriter? _sessionWriter;
        private static StreamWriter? _shaderWriter;

        public static string? SessionLogPath { get; private set; }
        public static string? CurrentShaderLogPath { get; private set; }

        public static void Initialize(string logsRootDirectory)
        {
            lock (_sync)
            {
                Directory.CreateDirectory(logsRootDirectory);
                string timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
                SessionLogPath = Path.Combine(logsRootDirectory, $"session-{timestamp}.log");

                _sessionWriter?.Dispose();
                _sessionWriter = new StreamWriter(
                    new FileStream(SessionLogPath, FileMode.Create, FileAccess.Write, FileShare.Read),
                    Encoding.UTF8)
                {
                    AutoFlush = true
                };

                WriteLocked("INFO", $"Logger initialized. Session log: {SessionLogPath}");
            }
        }

        public static void StartShaderLog(string logsRootDirectory, string shaderName, long shaderPathId)
        {
            lock (_sync)
            {
                Directory.CreateDirectory(logsRootDirectory);
                string safeName = SanitizeFileName($"{shaderName}_{shaderPathId}");
                CurrentShaderLogPath = Path.Combine(logsRootDirectory, $"{safeName}.log");

                _shaderWriter?.Dispose();
                _shaderWriter = new StreamWriter(
                    new FileStream(CurrentShaderLogPath, FileMode.Create, FileAccess.Write, FileShare.Read),
                    Encoding.UTF8)
                {
                    AutoFlush = true
                };

                WriteLocked("INFO", $"Shader log started: {CurrentShaderLogPath}");
            }
        }

        public static void EndShaderLog()
        {
            lock (_sync)
            {
                if (_shaderWriter is not null)
                {
                    WriteLocked("INFO", "Shader log closed.");
                    _shaderWriter.Dispose();
                    _shaderWriter = null;
                }
                CurrentShaderLogPath = null;
            }
        }

        public static void Shutdown()
        {
            lock (_sync)
            {
                EndShaderLog();
                _sessionWriter?.Dispose();
                _sessionWriter = null;
            }
        }

        public static void Debug(string message) => Write("DEBUG", message);
        public static void Info(string message) => Write("INFO", message);
        public static void Warning(string message) => Write("WARNING", message);
        public static void Error(string message) => Write("ERROR", message);

        public static void Exception(string message, Exception ex)
        {
            Write("ERROR", $"{message}{Environment.NewLine}{ex}");
        }

        private static void Write(string level, string message)
        {
            lock (_sync)
            {
                WriteLocked(level, message);
            }
        }

        private static void WriteLocked(string level, string message)
        {
            string line = $"[{DateTime.UtcNow:O}] [{level}] {message}";
            Console.WriteLine(line);
            _sessionWriter?.WriteLine(line);
            _shaderWriter?.WriteLine(line);
        }

        private static string SanitizeFileName(string value)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var builder = new StringBuilder(value.Length);
            foreach (char c in value)
            {
                builder.Append(Array.IndexOf(invalid, c) >= 0 ? '_' : c);
            }
            return builder.ToString();
        }
    }
}
