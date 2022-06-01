using System;
using System.Text.RegularExpressions;
namespace kpem_flux;
public static class ConsoleHelper
{
    public static void WriteLine<T>(T content, ConsoleColor? color = null)
    {
        if (content != null)
        {
            var oldColor = Console.ForegroundColor;
            if (color != null)
            {
                Console.ForegroundColor = (ConsoleColor)color;
            }
            Console.WriteLine(content.ToString());
            Console.ForegroundColor = oldColor;
        }
    }
    public static string GetInput(string prompt = "Enter data", ObscurityType obscurity = ObscurityType.AllVisible)
    {
        Console.Write(String.Format("{0} >> ", prompt));
        string input = "";
        while (true)
        {
            var k = Console.ReadKey(true);
            if (k.Key == ConsoleKey.Enter)
            {
                Console.Write("\r\n");
                return input;
            }
            else if (k.Key == ConsoleKey.Backspace)
            {
                if (input.Length > 0)
                {
                    input = input.Substring(0, input.Length - 1);
                    Console.CursorLeft -= 1;
                    Console.Write("\x1B[1P");
                }
            }
            else if (Regex.IsMatch(k.KeyChar.ToString(), "[ -~]+"))
            {
                if (obscurity == ObscurityType.AllVisible)
                {
                    Console.Write(k.KeyChar);
                }
                else if (obscurity == ObscurityType.LengthOnly)
                {
                    Console.Write("*");
                }
                input += k.KeyChar;
            }
        }
    }
    public static bool GetBinaryChoice(string prompt = "Enter data")
    {
        Console.Write(String.Format("{0} (Y/N) >> ", prompt));
        while (true)
        {
            var k = Console.ReadKey(true);
            if (k.Key == ConsoleKey.Y || k.Key == ConsoleKey.Enter)
            {
                Console.Write("Yes\n");
                return true;
            }
            else if (k.Key == ConsoleKey.N || k.Key == ConsoleKey.Escape)
            {
                Console.Write("No\n");
                return false;
            }
        }
    }
    public static async Task PanDownAndClearAsync(int effectDuration)
    {
        var height = Console.WindowHeight + 1;
        var perLineDelay = effectDuration / height;
        for (int i = 0; i < height; i++)
        {
            Console.Write("\n");
            await Task.Delay(perLineDelay);
        }
        Console.Clear();
        //If necessary, clear the scrollback buffer
#pragma warning disable CS8602 // Dereference of a possibly null reference.
        if (Environment.GetEnvironmentVariable("TERM").StartsWith("xterm"))
        {
            Console.Write("\x1b[3J");
        }
#pragma warning restore CS8602 // Dereference of a possibly null reference.
    }
    public enum ObscurityType
    {
        AllVisible,
        LastCharOnly,
        LengthOnly,
        NoFeedback
    }
}

