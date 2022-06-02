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
    public static void ClearLine(int row)
    {
        var cursorDown = Console.CursorTop;
        Console.SetCursorPosition(0, row);
        Console.Write(new string(' ', Console.BufferWidth));
        Console.SetCursorPosition(0, cursorDown);
    }
    public static void ClearLines(int fromRow, int toRow)
    {
        for (int i = fromRow; i < toRow; i++)
        {
            ClearLine(i);
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
    public static bool GetBinaryChoice(string prompt = "Yes or no")
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
    public static int GetNumericChoice(string prompt = "Make a choice", params string[] options)
    {
        for (int i = 0; i < options.Length; i++)
        {
            //Write each option. Except for the last option, add a comma to the end
            Console.WriteLine(String.Format("{0}. {1}" + (i < options.Length - 1 ? "," : ""), i + 1, options[i]));
        }
        Console.Write(String.Format("{0} (1-{1}) >> ", prompt, options.Length));
        while (true)
        {
            var k = Console.ReadKey(true);
            var s = int.TryParse(k.KeyChar.ToString(), out int n);
            if (s && n <= options.Length)
            {
                Console.Write(options[n - 1] + "\n");
                return n;
            }
        }
    }
    public static void WaitForKeypressToContinue()
    {
        Console.Write("Press any key to continue");
        _ = Console.ReadKey(true);
    }
    public static async Task PanDownAndClearAsync(int perLineWait)
    {
        var cDown = Console.CursorTop;
        var cHeight = Console.WindowHeight;
        //If we haven't used a whole page, we don't need to scroll as far
        int scrollLength = cDown < cHeight ? cDown : cHeight;
        //Make sure each line we write is scrolling the window
        Console.SetCursorPosition(0, cHeight);
        for (int i = 0; i < scrollLength; i++)
        {
            Console.Write("\n");
            await Task.Delay(perLineWait);
        }
        Console.Clear();
        //If necessary, clear the scrollback buffer
        if (Environment.GetEnvironmentVariable("TERM")!.StartsWith("xterm"))
        {
            Console.Write("\x1b[3J");
        }
    }
    public enum ObscurityType
    {
        AllVisible,
        LastCharOnly,
        LengthOnly,
        NoFeedback
    }
}
public class MenuLevel
{
    public MenuItem[]? items;
    public MenuLevel? parentLevel;
    public string name;
    private bool allowBack;
    public MenuLevel(bool allowBack, MenuLevel? parentLevel = null, string menuName = "Menu")
    {
        this.parentLevel = parentLevel;
        this.allowBack = allowBack;
        this.name = menuName;
    }
    private void Init()
    {

    }
    public void EnterMenu()
    {
        int cursorDownStart = Console.CursorTop;
        if (items != null)
        {
            //Create a new temporary array that we can modify
            MenuItem[] tempItems = items;
            if (allowBack)
            {
                tempItems = tempItems.Append(new MenuItem("Return", parentLevel!)).ToArray();
            }
            Console.WriteLine(String.Format("{0}:", name));
            string[] choicesNames = new string[tempItems.Length];
            for (int i = 0; i < tempItems.Length; i++)
            {
                choicesNames[i] = tempItems[i].name;
            }
            var c = ConsoleHelper.GetNumericChoice("Make a choice", choicesNames) - 1;
            if (tempItems[c].Mode == MenuItem.MenuItemMode.Action)
            {
                tempItems[c].action!.Invoke();
            }
            else if (tempItems[c].Mode == MenuItem.MenuItemMode.MenuLevelLink)
            {
                ConsoleHelper.ClearLines(cursorDownStart, Console.CursorTop);
                Console.SetCursorPosition(0, cursorDownStart);
                tempItems[c].linkedLevel!.EnterMenu();
            }
        }
        else
        {
            throw new Exception("Menu level not ready: missing items");
        }
    }
    public class MenuItem
    {
        public string name;
        public Action? action;
        public MenuLevel? linkedLevel;
        public MenuItemMode Mode { get; private set; }
        public MenuItem(string name, Action? action)
        {
            this.name = name;
            this.linkedLevel = null;
            this.action = action;
            Mode = MenuItemMode.Action;
        }
        public MenuItem(string name, MenuLevel? linkedLevel)
        {
            this.name = name;
            this.linkedLevel = linkedLevel;
            this.action = null;
            Mode = MenuItemMode.MenuLevelLink;
        }
        public enum MenuItemMode
        {
            Action,
            MenuLevelLink
        }
    }
}