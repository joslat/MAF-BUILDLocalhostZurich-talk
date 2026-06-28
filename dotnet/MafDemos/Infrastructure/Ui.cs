// SPDX-License-Identifier: MIT
// Microsoft Agent Framework — Build & Run the Demo Samples (.NET)

using System.Text;

namespace MafDemos.Infrastructure;

/// <summary>
/// A tiny, dependency-free console UI toolkit: themed colors, rounded banners,
/// per-agent colors, a live "thinking" spinner, a streaming printer, and pass/fail badges.
/// Everything degrades gracefully if the terminal can't render Unicode.
/// </summary>
public static class Ui
{
    // ── Theme ──────────────────────────────────────────────────────────────
    public const ConsoleColor Accent = ConsoleColor.Cyan;
    public const ConsoleColor Muted = ConsoleColor.DarkGray;
    public const ConsoleColor Good = ConsoleColor.Green;
    public const ConsoleColor Bad = ConsoleColor.Red;
    public const ConsoleColor Warn = ConsoleColor.Yellow;

    private const int Width = 74;

    public static void Init()
    {
        try { Console.OutputEncoding = Encoding.UTF8; } catch { /* headless */ }
    }

    // ── Coloring helpers ───────────────────────────────────────────────────
    public static void Write(string text, ConsoleColor color)
    {
        var prev = Console.ForegroundColor;
        Console.ForegroundColor = color;
        Console.Write(text);
        Console.ForegroundColor = prev;
    }

    public static void Line(string text, ConsoleColor color)
    {
        Write(text + Environment.NewLine, color);
    }

    public static void Info(string text) => Line("  " + text, ConsoleColor.Gray);
    public static void Dim(string text) => Line("  " + text, Muted);
    public static void Hint(string text) => Line("  💡 " + text, Warn);
    public static void Success(string text) => Line("  ✅ " + text, Good);
    public static void Failure(string text) => Line("  ❌ " + text, Bad);
    public static void Step(string n, string text) => Line($"  ▸ {n}  {text}", Accent);

    // ── Banner (rounded box, ASCII-safe alignment) ─────────────────────────
    public static void Banner(string title, string subtitle)
    {
        Console.WriteLine();
        var top = "╭" + new string('─', Width - 2) + "╮";
        var bottom = "╰" + new string('─', Width - 2) + "╯";
        Line(top, Accent);
        BannerLine("");
        BannerLine("  " + title, ConsoleColor.White);
        if (!string.IsNullOrWhiteSpace(subtitle))
            BannerLine("  " + subtitle, Muted);
        BannerLine("");
        Line(bottom, Accent);
        Console.WriteLine();
    }

    private static void BannerLine(string content, ConsoleColor? contentColor = null)
    {
        int inner = Width - 2;
        if (content.Length > inner) content = content[..inner];
        Write("│", Accent);
        Write(content.PadRight(inner), contentColor ?? Muted);
        Line("│", Accent);
    }

    public static void Rule(string? label = null)
    {
        if (string.IsNullOrEmpty(label))
        {
            Line(new string('─', Width), Muted);
        }
        else
        {
            var line = $"── {label} ";
            Line(line + new string('─', Math.Max(0, Width - line.Length)), Muted);
        }
    }

    // ── Per-agent colors ───────────────────────────────────────────────────
    private static readonly Dictionary<string, ConsoleColor> KnownAgents = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Triage"] = ConsoleColor.Cyan,
        ["Orders"] = ConsoleColor.Green,
        ["Returns"] = ConsoleColor.Yellow,
        ["Fraud"] = ConsoleColor.Red,
        ["Ops"] = ConsoleColor.Magenta,
        ["ShopBot"] = ConsoleColor.Cyan,
    };

    private static readonly ConsoleColor[] Palette =
        [ConsoleColor.Cyan, ConsoleColor.Green, ConsoleColor.Yellow, ConsoleColor.Magenta, ConsoleColor.Blue, ConsoleColor.Red];

    public static ConsoleColor ColorFor(string? agentName)
    {
        if (string.IsNullOrEmpty(agentName)) return ConsoleColor.Gray;
        if (KnownAgents.TryGetValue(agentName, out var c)) return c;
        int h = Math.Abs(agentName.GetHashCode());
        return Palette[h % Palette.Length];
    }

    // ── Badges & key/value ─────────────────────────────────────────────────
    public static void Badge(bool pass, string label)
    {
        var prev = Console.ForegroundColor;
        Console.ForegroundColor = pass ? Good : Bad;
        Console.Write(pass ? "  ✔ PASS " : "  ✘ FAIL ");
        Console.ForegroundColor = ConsoleColor.Gray;
        Console.WriteLine(label);
        Console.ForegroundColor = prev;
    }

    public static void KeyValue(string key, string value, ConsoleColor valueColor = ConsoleColor.White)
    {
        Write($"  {key,-18}", Muted);
        Line(value, valueColor);
    }

    // ── Input ──────────────────────────────────────────────────────────────
    /// <summary>Prompts for a line of input. Returns null on end-of-input (Ctrl-Z / piped EOF)
    /// so callers can treat it as "quit" instead of spinning forever.</summary>
    public static string? Prompt(string label)
    {
        Write($"\n  {label} ", Accent);
        Write("› ", ConsoleColor.White);
        var prev = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.White;
        var input = Console.ReadLine();
        Console.ForegroundColor = prev;
        return input;
    }

    public static bool Confirm(string question)
    {
        Write($"\n  {question} ", Warn);
        Write("(y/n) › ", ConsoleColor.White);
        var ans = (Console.ReadLine() ?? "n").Trim();
        return ans.StartsWith("y", StringComparison.OrdinalIgnoreCase);
    }

    public static void PressAnyKey()
    {
        Line("\n  Press any key to return to the menu…", Muted);
        try { Console.ReadKey(intercept: true); } catch { Console.ReadLine(); }
    }

    // ── A live "thinking…" spinner for non-streaming awaits ────────────────
    public sealed class Spinner : IDisposable
    {
        private static readonly string[] Frames = ["⠋", "⠙", "⠹", "⠸", "⠼", "⠴", "⠦", "⠧", "⠇", "⠏"];
        private readonly CancellationTokenSource _cts = new();
        private readonly Task _loop;
        private readonly string _label;

        public Spinner(string label)
        {
            _label = label;

            // When output is redirected (piped/CI) there's no \r repaint — print one static
            // line instead of spamming frames.
            if (Console.IsOutputRedirected)
            {
                Line($"  · {label}", Accent);
                _loop = Task.CompletedTask;
                return;
            }

            _loop = Task.Run(async () =>
            {
                int i = 0;
                try
                {
                    while (!_cts.IsCancellationRequested)
                    {
                        var prev = Console.ForegroundColor;
                        Console.ForegroundColor = Accent;
                        Console.Write($"\r  {Frames[i++ % Frames.Length]} {_label}   ");
                        Console.ForegroundColor = prev;
                        await Task.Delay(80, _cts.Token);
                    }
                }
                catch (TaskCanceledException) { }
            });
        }

        public void Dispose()
        {
            _cts.Cancel();
            try { _loop.Wait(500); } catch { }
            if (!Console.IsOutputRedirected)
                Console.Write("\r" + new string(' ', _label.Length + 8) + "\r"); // clear the line
            _cts.Dispose();
        }
    }

    /// <summary>
    /// Streams text to the console grouped by the speaking agent: when the speaker
    /// changes, a colored "[Agent]" label is printed once, then its text flows after it.
    /// </summary>
    public sealed class AgentStream
    {
        private string? _current;

        public void Chunk(string? agentName, string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            if (!string.Equals(_current, agentName, StringComparison.Ordinal))
            {
                _current = agentName;
                Console.WriteLine();
                Write($"  [{agentName}] ", ColorFor(agentName));
            }
            Console.Write(text);
        }

        public void End() => Console.WriteLine();

        /// <summary>Forget the current speaker so the next chunk reprints its label
        /// (use after interjecting a tool-call / handoff line).</summary>
        public void Reset() => _current = null;
    }
}
