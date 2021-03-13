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
                ProcessToDebug processToDebug = new ProcessToDebug();
                try
                {
                    foreach (string arg in args)
                    {
                        string[] result = arg.Split(C_KEY_VALUE_SEP, StringSplitOptions.None);
                        string key = result[0];
                        string value = result[1];

                        switch (key)
                        {
                            case C_ARG_SOURCE:
                                processToDebug.SourcePath = value;
                                break;
                            case C_ARG_SYMBOL:
                                processToDebug.SymbolPath = value;
                                break;
                            case C_ARG_ATTACH:
                                processToDebug.PID = int.Parse(value);
                                break;
                            case C_ARG_RUN:
                                processToDebug.ExecutablePath = value;
                                break;
                            default:
                                Console.WriteLine("Unknown parameter " + key);
                                Console.WriteLine(C_USAGE);
                                Environment.Exit(1);
                                break;
                        }
                    }
                    processToDebug.Check();
                    using (DebuggerDriver debuggerDriver = new DebuggerDriver(processToDebug))
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

        const string C_ARG_SOURCE = "-sourcePath";
        const string C_ARG_SYMBOL = "-symbolPath";
        const string C_ARG_ATTACH = "-attach";
        const string C_ARG_RUN = "-run";
        static char[] C_KEY_VALUE_SEP = new char[] { '=' };
        const string C_USAGE = @"connector -sourceDir=Path\to\source -symbolDir=Path\to\pdb [-attach=pid | -run=Path\to\bin]";

    }
}
