// SPDX-License-Identifier: MIT
// AgentEval × MAF — a tiny, dependency-free console toolkit.

using System.Text;
using AgentEval.Evals;

namespace AgentEvalMafEvals.Infrastructure;

/// <summary>Banners, key/value rows, section rules, and an EvalResult tree printer. Console-only.</summary>
public static class Output
{
    private const int Width = 78;

    public static void Init()
    {
        try { Console.OutputEncoding = Encoding.UTF8; } catch { /* headless */ }
    }

    public static void Banner(string title, string subtitle)
    {
        Console.WriteLine();
        Line("╭" + new string('─', Width - 2) + "╮", ConsoleColor.Cyan);
        BannerLine("  " + title, ConsoleColor.White);
        if (!string.IsNullOrWhiteSpace(subtitle)) BannerLine("  " + subtitle, ConsoleColor.DarkGray);
        Line("╰" + new string('─', Width - 2) + "╯", ConsoleColor.Cyan);
        Console.WriteLine();
    }

    public static void Section(string label)
    {
        Console.WriteLine();
        var text = $"── {label} ";
        Line(text + new string('─', Math.Max(0, Width - text.Length)), ConsoleColor.Cyan);
    }

    public static void Kv(string key, string value)
    {
        Write($"  {key,-12}", ConsoleColor.DarkGray);
        Line(value, ConsoleColor.White);
    }

    /// <summary>A menu line: a white label (wide column) followed by a dim description.</summary>
    public static void Option(string label, string desc)
    {
        Write($"  {label,-28}", ConsoleColor.White);
        Line(desc, ConsoleColor.DarkGray);
    }

    /// <summary>A dim, indented status line.</summary>
    public static void Dim(string text) => Line(text, ConsoleColor.DarkGray);

    public static void SkipBox()
    {
        Line("+---------------------------------------------------------------------------+", ConsoleColor.Yellow);
        Line("|  SKIPPING — no model backend configured. Set one of:                      |", ConsoleColor.Yellow);
        Line("|    AZURE_OPENAI_ENDPOINT + AZURE_OPENAI_API_KEY + AZURE_OPENAI_DEPLOYMENT  |", ConsoleColor.Yellow);
        Line("|    or OPENAI_API_KEY                                                       |", ConsoleColor.Yellow);
        Line("+---------------------------------------------------------------------------+", ConsoleColor.Yellow);
    }

    /// <summary>Prints an EvalResult tree, one indented line per node with a pass/fail icon and score.</summary>
    public static void Tree(EvalResult node, int indent = 0)
    {
        var pad = new string(' ', indent * 2);
        var skipped = string.Equals(node.Score.Label, "skipped", StringComparison.OrdinalIgnoreCase);
        var (icon, color) = skipped
            ? ("○", ConsoleColor.DarkGray)               // not tested — neutral, not a failure
            : node.Score.Passed ? ("✅", ConsoleColor.Green) : ("❌", ConsoleColor.Red);
        Write($"  {pad}{icon} ", color);
        Write($"{node.Metric.Name,-40}", ConsoleColor.Gray);
        Line($" {node.Score.Value * 100,6:F1}%  [{node.Score.Label}]", ConsoleColor.DarkGray);
        foreach (var child in node.Details.SubResults ?? [])
            Tree(child, indent + 1);
    }

    private static void BannerLine(string content, ConsoleColor color)
    {
        var inner = Width - 2;
        if (content.Length > inner) content = content[..inner];
        Write("│", ConsoleColor.Cyan);
        Write(content.PadRight(inner), color);
        Line("│", ConsoleColor.Cyan);
    }

    private static void Write(string text, ConsoleColor color)
    {
        var prev = Console.ForegroundColor;
        Console.ForegroundColor = color;
        Console.Write(text);
        Console.ForegroundColor = prev;
    }

    private static void Line(string text, ConsoleColor color) => Write(text + Environment.NewLine, color);
}
