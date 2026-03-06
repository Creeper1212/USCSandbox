using System;
using System.Text;

namespace USCSandbox
{
    internal static class ConsoleUi
    {
        private const int AccentR = 170; // aa
        private const int AccentG = 57;  // 39
        private const int AccentB = 255; // ff
        private const int DarkBlueR = 14;
        private const int DarkBlueG = 38;
        private const int DarkBlueB = 94;

        private static readonly string Accent = Ansi(AccentR, AccentG, AccentB, bold: true);
        private static readonly string InfoColor = Ansi(120, 190, 255);
        private static readonly string SuccessColor = Ansi(90, 210, 130);
        private static readonly string WarningColor = Ansi(255, 196, 85);
        private static readonly string ErrorColor = Ansi(255, 110, 110, bold: true);
        private static readonly string MutedColor = Ansi(140, 140, 160);
        private static readonly string Reset = "\u001b[0m";

        public static void Initialize()
        {
            Console.OutputEncoding = Encoding.UTF8;
        }

        public static void Banner()
        {
            Console.WriteLine(Gradient("USCSandbox"));
            Console.WriteLine(Gradient("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"));
            Console.WriteLine($"{MutedColor}Dark blue → #aa39ff gradient theme{Reset}");
            Console.WriteLine($"{MutedColor}Unity Shader Decompiler{Reset}");
            Console.WriteLine();
        }

        public static void Usage()
        {
            Section("Usage");
            Console.WriteLine($"{MutedColor}USCSandbox [bundle path] [assets path] [shader path id] [--platform d3d11|Switch] [--version unityVersion] [--all]{Reset}");
            Console.WriteLine();
            Console.WriteLine($"{Accent}Examples{Reset}");
            Console.WriteLine($"  {MutedColor}List bundle files:{Reset}");
            Console.WriteLine($"    USCSandbox \"C:\\path\\data.unity3d\"");
            Console.WriteLine($"  {MutedColor}List shaders in one assets file:{Reset}");
            Console.WriteLine($"    USCSandbox \"C:\\path\\data.unity3d\" \"sharedassets0.assets\"");
            Console.WriteLine($"  {MutedColor}Decompile all shaders in one assets file:{Reset}");
            Console.WriteLine($"    USCSandbox \"C:\\path\\data.unity3d\" \"sharedassets0.assets\" 0 --all");
            Console.WriteLine();
        }

        public static void Section(string title)
        {
            Console.WriteLine($"{Accent}{title}{Reset}");
        }

        public static void Info(string text)
        {
            Console.WriteLine($"{InfoColor}[i]{Reset} {text}");
        }

        public static void Success(string text)
        {
            Console.WriteLine($"{SuccessColor}[ok]{Reset} {text}");
        }

        public static void Warning(string text)
        {
            Console.WriteLine($"{WarningColor}[!]{Reset} {text}");
        }

        public static void Error(string text)
        {
            Console.WriteLine($"{ErrorColor}[x]{Reset} {text}");
        }

        public static void ListItem(string text)
        {
            Console.WriteLine($"  {Accent}•{Reset} {text}");
        }

        public static void Progress(int index, int total, string shaderName)
        {
            Console.WriteLine($"{MutedColor}[{index}/{total}]{Reset} {shaderName}");
        }

        public static void Summary(int total, int succeeded, int failed, string outputDirectory, TimeSpan elapsed)
        {
            Section("Summary");
            Console.WriteLine($"  {MutedColor}Total:{Reset} {total}");
            Console.WriteLine($"  {MutedColor}Succeeded:{Reset} {SuccessColor}{succeeded}{Reset}");
            Console.WriteLine($"  {MutedColor}Failed:{Reset} {(failed == 0 ? SuccessColor : ErrorColor)}{failed}{Reset}");
            Console.WriteLine($"  {MutedColor}Output:{Reset} {outputDirectory}");
            Console.WriteLine($"  {MutedColor}Elapsed:{Reset} {elapsed:hh\\:mm\\:ss}");
            Console.WriteLine();
        }

        private static string Ansi(int r, int g, int b, bool bold = false)
        {
            return bold
                ? $"\u001b[1;38;2;{r};{g};{b}m"
                : $"\u001b[38;2;{r};{g};{b}m";
        }

        private static string Gradient(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            int last = Math.Max(1, text.Length - 1);
            StringBuilder sb = new StringBuilder(text.Length * 16);
            for (int i = 0; i < text.Length; i++)
            {
                float t = i / (float)last;
                int r = Lerp(DarkBlueR, AccentR, t);
                int g = Lerp(DarkBlueG, AccentG, t);
                int b = Lerp(DarkBlueB, AccentB, t);
                sb.Append(Ansi(r, g, b, bold: true));
                sb.Append(text[i]);
            }
            sb.Append(Reset);
            return sb.ToString();
        }

        private static int Lerp(int a, int b, float t)
        {
            return (int)Math.Round(a + (b - a) * t);
        }
    }
}
