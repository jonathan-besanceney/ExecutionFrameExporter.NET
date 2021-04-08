using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using FrameExporter.OutputPlugin;
using FrameExporter.Utils;
using Microsoft.Samples.Debugging.CorDebug;
using Microsoft.Samples.Debugging.MdbgEngine;
using Microsoft.Samples.Tools.Mdbg;

namespace FrameExporter
{
    public class DebuggerDriver : CommandBase, IDisposable
    {
        private ProcessToDebug m_ProcessToDebug = null;
        private MDbgShell shell = null;
        private MDbgProcess dbgProcess = null;
        private MDbgEngine debugger = null;

        private static MDbgSourceFileMgr sourceFileMgr = new MDbgSourceFileMgr();
        private readonly Dictionary<int, MethodDesc> methods = null;
        //private List<Assembly> assemblies = new List<Assembly>();
        private static AssemblyMgr assemblyMgr = new AssemblyMgr();

        private DebuggerDriver()
        {
            methods = new Dictionary<int, MethodDesc>();
        }

        public DebuggerDriver(ProcessToDebug processToDebug): this()
        {
            m_ProcessToDebug = processToDebug;
        }

        public void Initialize()
        {
            shell = new MDbgShell();
            debugger = new MDbgEngine();
            shell.Debugger = debugger;

            // set source and pdb folders
            debugger.Options.SymbolPath = m_ProcessToDebug.SymbolPath;
            Console.WriteLine($"SymbolPath set to {m_ProcessToDebug.SymbolPath}");
            shell.FileLocator.Path = m_ProcessToDebug.SourcePath;
            Console.WriteLine($"SourcePath set to {m_ProcessToDebug.SourcePath}");

            // Load source files
            Console.WriteLine("Building source cache");
            sourceFileMgr.BuildSourceCache(m_ProcessToDebug.SourcePath);

            // Load assemblies 
            assemblyMgr.BuildAssemblyCache(m_ProcessToDebug.ExecutablePath);

            Console.Write($"Get CLR Version of {m_ProcessToDebug.ExecutablePath}: ");
            string version = MdbgVersionPolicy.GetDefaultLaunchVersion(m_ProcessToDebug.ExecutablePath);
            Console.WriteLine(version);
            Console.Write("Create debugable process...");
            dbgProcess = debugger.Processes.CreateLocalProcess(new CorDebugger(version));
            Console.WriteLine("done");
            if (dbgProcess == null)
            {
                throw new MDbgShellException("Could not create debugging interface for runtime version " + version);
            }
            dbgProcess.DebugMode = DebugModeFlag.Debug;
            if (m_ProcessToDebug.IsPIDSet())
            {
                Console.Write($"Attaching PID {m_ProcessToDebug.PID}...");
                dbgProcess.Attach(m_ProcessToDebug.PID);
            }
            else
            {
                // TODO Deal with arguments !
                Console.Write($"Create process {m_ProcessToDebug.ExecutablePath}...");
                dbgProcess.CreateProcess(m_ProcessToDebug.ExecutablePath, m_ProcessToDebug.ExecutablePath);
            }
            Console.WriteLine("done");
        }

        public void Run()
        {
            try
            {
                // entry point
                using (WaitHandle waitHandle = dbgProcess.Go())
                {
                    using (IOutputPlugin output = new FileOutput("debug.log"))
                    {
                        while (waitHandle.WaitOne() && dbgProcess.Threads.HaveActive)
                        {
                            ReadFrame(dbgProcess, dbgProcess.Threads.Active, output);
                        }
                    }
                }
            }
            catch (MDbgNoActiveInstanceException e)
            {
                Console.WriteLine(e.Message);
            }
            catch (System.Runtime.InteropServices.COMException e)
            {
                Console.WriteLine(e.Message);
            }
        }
       
        private static void GetMethodInfoAndArgValues(ExecutionFrame frame, MDbgFrame mdbgFrame)
        {
            frame.Method = assemblyMgr.GetMethodDesc(mdbgFrame);
            MDbgValue[] mDbgValues = frame.Method.GetMethodArguments();
            if (mDbgValues == null) return;
            frame.LocalVariables = ValueInfoUtils.MDbgValuesToValueInfoArray(mDbgValues);

            // resolve constants
            ValueInfoUtils.GetValueForNonLitteralValueInfos(assemblyMgr.GetConstants($"{frame.Method.NameSpace}.{frame.Method.ClassName}"), mdbgFrame);
        }

        private static async Task GetMethodInfoAndArgValuesAsync(ExecutionFrame frame, MDbgFrame mdbgFrame)
        {
            await Task.Run(() => GetMethodInfoAndArgValues(frame, mdbgFrame));
        }

        private static void ReadFrame(MDbgProcess process, MDbgThread thread, IOutputPlugin output)
        {
            ExecutionFrame frame = null;

            if (thread.CurrentSourcePosition != null)
            {
                DateTime startTime = DateTime.Now;

                frame = new ExecutionFrame();
                int token = thread.CurrentFrame.Function.CorFunction.Token;
                frame.ThreadNumber = thread.Number;
                frame.SourceFile = thread.CurrentSourcePosition.Path;
                frame.SourceFileLineNumber = thread.CurrentSourcePosition.Line;
                frame.SourceFileColumnNumber = thread.CurrentSourcePosition.StartColumn;

                Task methodDescTask = GetMethodInfoAndArgValuesAsync(frame, thread.CurrentFrame);
                Task<string> sourceLineTask = sourceFileMgr.GetSourceLineAsync(frame.SourceFile, frame.SourceFileLineNumber);
                Task<ValueInfo[]> valueInfosTask = ValueInfoUtils.MDbgValuesToValueInfoArrayAsync(thread.CurrentFrame.Function.GetActiveLocalVars(thread.CurrentFrame));
                Task<ValueInfo> exceptionTask = ValueInfoUtils.MDbgValueExceptionToValueInfoAsync(thread.CurrentException);

                Task.WaitAll(new Task[] { sourceLineTask, valueInfosTask, methodDescTask, exceptionTask });

                frame.SourceFileLine = sourceLineTask.Result;
                   
                ValueInfo[] valueInfos = new ValueInfo[frame.LocalVariables.Length + valueInfosTask.Result.Length];
                frame.LocalVariables.CopyTo(valueInfos, 0);
                valueInfosTask.Result.CopyTo(valueInfos, frame.LocalVariables.Length);

                frame.LocalVariables = valueInfos;
                frame.CurrentException = exceptionTask.Result;
                frame.executionTime = (DateTime.Now - startTime).ToString();

                output.SendFrameAsync(frame);

                process.StepInto(false);
            }
            else
            {
                process.StepOut().WaitOne();
            }
        }

        public void Dispose()
        {
            if (dbgProcess != null && dbgProcess.IsAlive)
            {
                dbgProcess.Detach();
                dbgProcess = null;
            }
            shell = null;
            debugger = null;
        }
    }
}
