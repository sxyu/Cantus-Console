using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Cantus.Core;
using System.Threading;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Cantus.CantusConsole
{
    class Program
    {
        #region "Win32"
        [DllImport("Kernel32")]
        public static extern bool SetConsoleCtrlHandler(HandlerRoutine Handler, bool Add);

        // A delegate type to be used as the handler routine
        // for SetConsoleCtrlHandler.
        public delegate bool HandlerRoutine(CtrlTypes CtrlType);

        // An enumerated type for the control messages
        // sent to the handler routine.
        public enum CtrlTypes
        {
            CTRL_C_EVENT = 0,
            CTRL_BREAK_EVENT,
            CTRL_CLOSE_EVENT,
            CTRL_LOGOFF_EVENT = 5,
            CTRL_SHUTDOWN_EVENT
        }

        #endregion

        private static string _prompt = string.Format("{0}@Cantus> ", Environment.UserName);
        private static CantusEvaluator _eval = new CantusEvaluator(reloadDefault: false);

        static void ClearLine()
        {
            Console.SetCursorPosition(0, Console.CursorTop);
            Console.Write("".PadRight(_prompt.Length, ' '));
            Console.SetCursorPosition(0, Console.CursorTop);
        }

        static bool ConsoleCtrl(CtrlTypes e)
        {
            string cantusPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + Path.DirectorySeparatorChar;
            try
            {
                File.WriteAllText(cantusPath + "init.can", _eval.ToScript());
            }
            catch
            {
                try {
                    Console.WriteLine("Error: Failed to save user data. Variable, function, and class definitions may be lost.");
                }
                catch { }
            }
            return true;
        }

        static void ExitRequested(object sender, EventArgs e)
        {
            ConsoleCtrl(CtrlTypes.CTRL_CLOSE_EVENT);
            Environment.Exit(0);
        }

        private static HandlerRoutine _closeHanler = new HandlerRoutine(ConsoleCtrl);
        static void InitConsole()
        {
            SetConsoleCtrlHandler(_closeHanler, true);
        }

        static void Main(string[] args)
        {
            try {
                bool runFiles = false;
                bool alwaysBlock = false;
                bool exitAfterComplete = false;

                string cantusPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + Path.DirectorySeparatorChar;

                StringBuilder extraLine = new StringBuilder();
                int extraCt = 0;

                _eval.ThreadController.MaxThreads = 5;

                _eval.ExitRequested += ExitRequested;

                // setup handlers for exit events so we can save user data on exit.
                InitConsole();

                    if (!args.Contains("--bare"))
                    {
                        _eval.ReloadDefault();
                        try {
                            _eval.ReInitialize();
                        }
                        catch(Exception ex)
                        {
                            Console.Error.WriteLine("Initialization Error:\n" + ex.Message);
                        }

                        // setup folders, etc.
                        string[] requiredFolders = {
                        cantusPath + "plugin",
                        cantusPath + "include",
                        cantusPath + "init"
                    };
                    foreach (string dir in requiredFolders)
                    {
                        if (!Directory.Exists(dir))
                        {
                            try
                            {
                                Directory.CreateDirectory(dir);
                            }
                            catch
                            {
                            }
                        }
                    }
                }

                foreach (string arg in args)
                {
                    if (arg == "-h" || arg == "--help") {
                        Console.WriteLine("Available commands:");
                        Console.WriteLine("[file1] [file2]...           \tRun scripts at the specified paths");
                        Console.WriteLine("-b --block                   \tRun in block mode (execute entire block, only print result on return)");
                        Console.WriteLine("-s --script                  \tRun in script mode (block mode + exit on first return)");
                        Console.WriteLine("--bare                       \tDo not load any plugins or initialization scripts");
                        Console.WriteLine();
                        Console.WriteLine("--sigfigs/--nosigfigs        \tSigFig mode on/off");
                        Console.WriteLine("--explicit/--implicit        \tExplicit mode on/off");
                        Console.WriteLine("--anglerepr=[deg/rad/grad]   \tSet angle representation");
                        Console.WriteLine("--output=[raw/math/sci]      \tSet output format");
                        Console.WriteLine();
                        Console.WriteLine("-h --help                    \tShow this help");
                        return;
                    } else if (arg == "--sigfigs") {
                        _eval.SignificantMode = true;
                    } else if (arg == "--nosigfigs") {
                        _eval.SignificantMode = false;
                    } else if (arg == "--explicit") {
                        _eval.ExplicitMode = true;
                    } else if (arg == "--implicit") {
                        _eval.ExplicitMode = false;
                    } else if (arg == "--anglerepr=rad") {
                        _eval.AngleMode = CantusEvaluator.AngleRepresentation.Radian;
                    } else if (arg == "--anglerepr=deg") {
                        _eval.AngleMode = CantusEvaluator.AngleRepresentation.Degree;
                    } else if (arg == "--anglerepr=grad") {
                        _eval.AngleMode = CantusEvaluator.AngleRepresentation.Gradian;
                    } else if (arg == "--output=raw") {
                        _eval.OutputMode = CantusEvaluator.OutputFormat.Raw;
                    } else if (arg == "--output=math") {
                        _eval.OutputMode = CantusEvaluator.OutputFormat.Math;
                    } else if (arg == "--output=sci") {
                        _eval.OutputMode = CantusEvaluator.OutputFormat.Scientific;
                    }
                    else if (arg == "-b" || arg == "--block")
                    {
                        alwaysBlock = true;
                        _eval.ThreadController.MaxThreads = int.MaxValue;
                    }
                    else if (arg == "-s" || arg == "--script")
                    {
                        alwaysBlock = true;
                        exitAfterComplete = true;
                        _eval.ThreadController.MaxThreads = int.MaxValue;
                    }
                    else if (arg == "--bare")
                    {
                        // do nothing, already dealt with
                    }
                    else if (File.Exists(arg))
                    {
                        runFiles = true;
                        try {
                            Console.WriteLine(_eval.Eval(File.ReadAllText(arg), returnedOnly: true));
                        }
                        catch (Exception ex)
                        {
                            Console.Error.WriteLine(ex.Message);
                        }
                    }
                    else if (File.Exists(arg + ".can"))
                    {
                            runFiles = true;
                        try
                        {
                            Console.WriteLine(_eval.Eval(File.ReadAllText(arg + ".can"), returnedOnly: true));
                        }
                        catch (Exception ex)
                        {
                            Console.Error.WriteLine(ex.Message);
                        }
                    }
                    else
                    {
                        runFiles = true;
                        if (extraCt > 0) extraLine.Append(" ");
                        extraLine.Append(arg);
                        extraCt++; 
                    }
                }
                if (runFiles)
                {
                    if (extraCt > 0)
                    {
                        try
                        {
                           if (extraLine.ToString().Contains(";"))
                               Console.Write( _eval.Eval(extraLine.ToString(), returnedOnly:true));
                           else
                               Console.Write( _eval.Eval(extraLine.ToString()));
                        }
                        catch (Exception ex)
                        {
                            Console.Error.Write(ex.Message);
                        }
                    }
                    Environment.Exit(0);
                }

                Console.WriteLine("Welcome to Cantus v." +
                    Assembly.GetAssembly(typeof(Cantus.Core.CantusEvaluator)).GetName().Version + " Beta");
                Console.WriteLine("By Alex Yu 2016");

                ScriptFeeder feeder = new ScriptFeeder(evaluator: _eval);
                Console.Title = "Cantus Console";

                bool first = true;
                bool block = alwaysBlock;
                bool lineTerm = true;
                bool prompted = false;
                int lastLeft = 0;
                StringBuilder buffer = new StringBuilder();
                ManualResetEventSlim ev = new ManualResetEventSlim(false);

                feeder.ResultReceived += (object sender, string result) =>
                {
                    ClearLine();
                    lineTerm = true;
                    block = false;
                    Console.WriteLine(result);
                    _prompt = string.Format("{0}@Cantus> ", Environment.UserName);
                    prompted = true;
                    if (exitAfterComplete) Environment.Exit(0);
                    Console.Write(_prompt);
                    feeder.BeginExecution();
                };

                _eval.WriteOutput += (object sender, CantusEvaluator.IOEventArgs e) =>
                {
                    ClearLine();
                    if (!lineTerm) Console.SetCursorPosition(lastLeft, Console.CursorTop - 1);
                    lineTerm = e.Content.EndsWith("\r") || e.Content.EndsWith("\n");
                    Console.Write(e.Content);
                    if (!lineTerm)
                    {
                        lastLeft = Console.CursorLeft;
                        Console.WriteLine();
                    }
                    if (block)
                    {
                        Console.Write(_prompt);
                        prompted = true;
                    }
                };

                _eval.ClearConsole += (object sender, EventArgs e) =>
                {
                    Console.Clear();
                };

                _eval.ReadInput += (object sender, CantusEvaluator.IOEventArgs e, out object @return) =>
                {
                    ClearLine();
                    @return = null;
                    while (buffer.Length > 0 && (int)buffer[0] <= (int)' ')
                    {
                        buffer.Remove(0, 1);
                    }
                    if (e.Message == CantusEvaluator.IOEventArgs.IOMessage.readLine)
                    {
                        if (buffer.Length == 0)
                        {
                            @return = Console.ReadLine();
                        }
                        else
                        {
                            if (buffer.ToString().Contains(Environment.NewLine))
                            {
                                int idx = buffer.ToString().IndexOf(Environment.NewLine);
                                @return = buffer.ToString().Remove(idx);
                                buffer = buffer.Remove(0, idx);
                            }
                            else
                            {
                                @return = buffer.ToString();
                                buffer.Clear();
                            }
                        }
                    }
                    else if (e.Message == CantusEvaluator.IOEventArgs.IOMessage.readChar)
                    {
                        if (buffer.Length == 0)
                        {
                            buffer.Append(Console.ReadLine());
                        }
                        @return = buffer[0];
                        buffer = buffer.Remove(0, 1);
                    }
                    else if (e.Message == CantusEvaluator.IOEventArgs.IOMessage.readWord ||
                             e.Message == CantusEvaluator.IOEventArgs.IOMessage.confirm)
                    {
                        while (true)
                        {
                            if (e.Message == CantusEvaluator.IOEventArgs.IOMessage.confirm)
                            {
                                if (e.Args["yes"].ToString() == "yes")
                                {
                                    Console.WriteLine("Please enter 'Y', 'N', 'yes', or 'no'");
                                }
                                else
                                {
                                    Console.WriteLine("Please enter 'Y', 'N', 'ok', or 'cancel'");
                                }
                            }
                            if (buffer.Length == 0)
                            {
                                buffer.Append(Console.ReadLine());
                            }
                            int idx = 0;
                            for (; idx < buffer.Length; idx++)
                            {
                                if ((int)buffer[idx] <= (int)' ') break;
                            }
                            if (idx == buffer.Length)
                            {
                                @return = buffer.ToString();
                                buffer.Clear();
                            }
                            else
                            {
                                @return = buffer.ToString().Remove(idx);
                                buffer = buffer.Remove(0, idx);
                            }

                            if (e.Message == CantusEvaluator.IOEventArgs.IOMessage.confirm)
                            {
                                string ret = @return.ToString();
                                if (e.Args["yes"].ToString() == "yes")
                                {
                                    ret = ret.ToLowerInvariant().Trim();
                                    if (ret == "yes" || ret == "y")
                                    {
                                        @return = true;
                                    }
                                    else if (ret == "no" || ret == "n")
                                    {
                                        @return = false;
                                    }
                                    else
                                    {
                                    // ask again
                                    continue;
                                    }
                                }
                                else
                                {
                                    ret = ret.ToLowerInvariant().Trim();
                                    if (ret == "ok" || ret == "y")
                                    {
                                        @return = true;
                                    }
                                    else if (ret == "cancel" || ret == "n")
                                    {
                                        @return = false;
                                    }
                                    else
                                    {
                                    // ask again
                                    continue;
                                    }
                                }
                            }
                            break;
                        }
                    }
                    prompted = false;
                };

                feeder.Waiting += (object sender, string lastExpr) =>
                {
                    if (first)
                    {
                        first = false;
                        return;
                    }
                    ev.Set();
                };
                feeder.BeginExecution();

                Console.Write(_prompt);
                while (true)
                {
                    prompted = false;
                    string line = Console.ReadLine();
                    if (line == null) break;
                    lineTerm = true;
                    if (line.EndsWith(":"))
                    {
                        line = line.Remove(line.Length - 1);
                        block = true;
                        _prompt = "".PadRight(_prompt.Length - 1, '.') + " ";
                    }

                    try {
                        if (block)
                        {
                            feeder.Append(line, true);
                            ev.Wait();
                            ev.Reset();
                        }
                        else
                        {
                            Console.WriteLine(_eval.Eval(line));
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine(ex.Message);
                    }

                    if (!alwaysBlock && string.IsNullOrWhiteSpace(line))
                    {
                        block = false;
                        //Console.WriteLine();
                        _prompt = string.Format("{0}@Cantus> ", Environment.UserName);
                    }

                    if (!prompted) Console.Write(_prompt);
                }
                feeder.EndAfterQueueDone();
            }
            finally {
                try
                {
                    _eval.Dispose();
                }
                catch { }
            }
        }
    }
}
