#undef TRACE
using System;

namespace FrameExporter
{
    class Program
    {
        static void Main(string[] args)
        {
            
            if (args.Length == 0)
            {
                Console.WriteLine(C_USAGE);
                Environment.Exit(1);
            }
            else
            {
                Parameters parameters = new Parameters();
                try
                {
                    foreach (string arg in args)
                    {
                        string[] result = arg.Split(C_KEY_VALUE_SEP, StringSplitOptions.None);
                        string key = result[0];
                        string value = string.Empty;
                        if (result.Length == 2)
                            value = result[1];

                        if (string.IsNullOrEmpty(value) && key != C_ARG_STOP_ON_NEW_THREAD)
                            throw new ArgumentException($"Parameter {key} expects a value !");

                        switch (key)
                        {
                            case C_SESSION_NAME:
                                parameters.SessionName = value;
                                break;
                            case C_ARG_SOURCE:
                                parameters.SourcePath = value;
                                break;
                            case C_ARG_SYMBOL:
                                parameters.SymbolPath = value;
                                break;
                            case C_ARG_ATTACH:
                                parameters.PID = int.Parse(value);
                                break;
                            case C_ARG_RUN:
                                parameters.ExecutablePath = value;
                                break;
                            case C_ARG_RUN_ARGS:
                                parameters.ExecutableArgs = value;
                                break;
                            case C_ARG_USER_BREAKPOINT:
                                parameters.UserBreakpointParametersAdd(value);
                                break;
                            case C_ARG_STOP_ON_NEW_THREAD:
                                parameters.StopOnNewThread = true;
                                break;
                            default:
                                throw new ArgumentException("Unknown parameter " + key);
                        }
                    }
                    parameters.Check();
                    using (DebuggerDriver debuggerDriver = new DebuggerDriver(parameters))
                    {
                        debuggerDriver.Initialize();
                        debuggerDriver.Run();
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                    Console.WriteLine(C_USAGE);
                    Environment.Exit(1);
                }
            }
        }

        private const string C_SESSION_NAME = "-sessionName";
        private const string C_ARG_SOURCE = "-sourcePath";
        private const string C_ARG_SYMBOL = "-symbolPath";
        private const string C_ARG_ATTACH = "-attach";
        private const string C_ARG_RUN = "-run";
        private const string C_ARG_RUN_ARGS = "-args";
        private const string C_ARG_USER_BREAKPOINT = "-bp";
        private const string C_ARG_STOP_ON_NEW_THREAD = "-stopOnNewThread";
        private static char[] C_KEY_VALUE_SEP = new char[] { '=' };
        private const string C_USAGE = @"FrameExporter -sourceDir=Path\to\source -symbolDir=Path\to\pdb [-attach=pid | -run=Path\to\bin [-args=""...""]] [-bp=""Path\to\sourcefile#startLine""[,""Path\to\sourcefile#endLine""]] [-stopOnNewThread] [-sessionName=MyAutomatedDebugSession]";

    }
}
