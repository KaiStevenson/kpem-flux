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
    public static string GetInput(string prompt = "", ObscurityType obscurity = ObscurityType.AllVisible)
    {
        Console.Write(String.Format("{0} >> ", prompt));
        string input = "";
        while (true)
        {
            var k = Console.ReadKey(true);
            if (k.Key == ConsoleKey.Enter)
            {
                if (obscurity == ObscurityType.LastCharOnly)
                {
                    Console.CursorLeft -= 1;
                    Console.Write("*");
                }
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
                else if (obscurity == ObscurityType.LastCharOnly)
                {
                    if (input.Length > 0)
                    {
                        Console.CursorLeft -= 1;
                        Console.Write("*");
                    }
                    Console.Write(k.KeyChar);
                }
                input += k.KeyChar;
            }
        }
    }
    //Be careful not to write anything while this is running
    //TODO: This method probably sucks a lot. Fix it.
    public static async Task<string> GetInputAsync(AsyncReaderInterruptToken interruptToken, string prompt = "")
    {
        Console.Write(String.Format("{0} >> ", prompt));
        string input = "";
        //Per keypress loop
        while (true)
        {
            //Pass control to calling thread until a key is available
            var cancellationSource = new CancellationTokenSource();
            var readTask = ReadKeyAsync(true, cancellationSource.Token);
            var interruptTask = interruptToken.GetInterrupt();
            int i = await Task.Run(() => Task.WaitAny(readTask, interruptTask));
            if (i == 0)
            {
                //This blocks, so make sure the task is completed when we get here.
                var k = readTask.Result;
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
                else if (k.Key == ConsoleKey.Escape)
                {
                    throw new UserCancelledOperationException();
                }
                else if (Regex.IsMatch(k.KeyChar.ToString(), "[ -~]+"))
                {
                    Console.Write(k.KeyChar);
                    input += k.KeyChar;
                }
            }
            //Interrupted
            else if (i == 1)
            {
                var interruptLine = interruptTask!.Result;
                ClearLine(Console.CursorTop);
                Console.WriteLine(interruptLine);
                Console.Write(String.Format("{0} >> {1}", prompt, input));
                //Cancel the read attempt to prepare for the loop
                cancellationSource.Cancel();
            }
        }
    }
    //Be very careful with this method
    public static async Task<ConsoleKeyInfo> ReadKeyAsync(bool intercept, CancellationToken cancellation)
    {
        while (!Console.KeyAvailable && !cancellation.IsCancellationRequested)
        {
            await Task.Delay(25);
        }
        cancellation.ThrowIfCancellationRequested();
        return Console.ReadKey(intercept);
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
    //Effect duration is the time to scroll all the lines off of a full console
    //If there are less lines, it'll execute quicker
    public static void Clear()
    {
        Console.Clear();
        //If necessary, clear the scrollback buffer
        var termVar = Environment.GetEnvironmentVariable("TERM");
        if (termVar != null && termVar.StartsWith("xterm"))
        {
            Console.Write("\x1b[3J");
        }
    }
    public static async Task PanDownAndClearAsync(int effectDuration)
    {
        var cDown = Console.CursorTop;
        var cHeight = Console.WindowHeight;
        //If we haven't used a whole page, we don't need to scroll as far
        int scrollLength = cDown < cHeight ? cDown : cHeight;
        var perLineWait = effectDuration / cHeight;
        //Make sure each line we write is scrolling the window
        Console.SetCursorPosition(0, cHeight);
        for (int i = 0; i < scrollLength; i++)
        {
            Console.Write("\n");
            await Task.Delay(perLineWait);
        }
        Clear();
    }
    //Instantiate this object to define a point to which the UI should be refreshed, as in a loop
    public class ConsoleClearPoint
    {
        private int clearFrom;
        public ConsoleClearPoint()
        {
            clearFrom = Console.CursorTop;
        }
        public void Back()
        {
            var clearTo = Console.CursorTop;
            ClearLines(clearFrom, clearTo + 1);
            Console.SetCursorPosition(0, clearFrom);
        }
    }
    public class AsyncReaderInterruptToken
    {
        private TaskCompletionSource<string> tcs = new TaskCompletionSource<string>();
        public Task<string> GetInterrupt()
        {
            return tcs.Task;
        }
        public void InterruptWithContent(string content)
        {
            tcs.SetResult(content);
            tcs = new TaskCompletionSource<string>();
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
    public void EnterMenu()
    {
        var clearPoint = new ConsoleHelper.ConsoleClearPoint();
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
            if (tempItems[c].askForConfirmation ? ConsoleHelper.GetBinaryChoice("Are you sure?") : true)
            {
                if (tempItems[c].Mode == MenuItem.MenuItemMode.Action)
                {
                    tempItems[c].action!.Invoke();
                }
                else if (tempItems[c].Mode == MenuItem.MenuItemMode.MenuLevelLink)
                {
                    clearPoint.Back();
                    tempItems[c].linkedLevel!.EnterMenu();
                }
            }
            //Confirmation failed, reload the same menu level
            else
            {
                clearPoint.Back();
                EnterMenu();
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
        public bool askForConfirmation;
        public MenuItemMode Mode { get; private set; }
        public MenuItem(string name, Action action, bool askForConfirmation = false)
        {
            this.name = name;
            this.linkedLevel = null;
            this.action = action;
            this.askForConfirmation = askForConfirmation;
            Mode = MenuItemMode.Action;
        }
        public MenuItem(string name, MenuLevel linkedLevel, bool askForConfirmation = false)
        {
            this.name = name;
            this.linkedLevel = linkedLevel;
            this.action = null;
            this.askForConfirmation = askForConfirmation;
            Mode = MenuItemMode.MenuLevelLink;
        }
        public enum MenuItemMode
        {
            Action,
            MenuLevelLink
        }
    }
}
[Serializable]
public class UserCancelledOperationException : Exception
{
    public UserCancelledOperationException()
    {

    }
    public UserCancelledOperationException(string message) : base(message)
    {

    }
    public UserCancelledOperationException(string message, Exception inner) : base(message, inner)
    {

    }
}