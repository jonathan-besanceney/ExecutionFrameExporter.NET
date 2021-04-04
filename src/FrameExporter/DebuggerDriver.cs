using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
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

        private readonly SourceFileMgr sourceFileMgr = null;
        private readonly Dictionary<int, MethodDesc> methods = null;
        private List<Assembly> assemblies = new List<Assembly>();
        private AssemblyMgr assemblyMgr = new AssemblyMgr();

        private DebuggerDriver()
        {
            sourceFileMgr = new SourceFileMgr();

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

            // TODO Create AssemblyMgr
            // Load assemblies 
            assemblies.Add(Assembly.LoadFile(m_ProcessToDebug.ExecutablePath));
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
                    using (StreamWriter file = new StreamWriter("debug.log"))
                    {
                        while (waitHandle.WaitOne() && dbgProcess.Threads.HaveActive)
                        {
                            ExecutionFrame frame = ReadFrame(dbgProcess.Threads.Active);
                            if (frame != null) file.WriteLineAsync(frame.ToString());
                            dbgProcess.StepInto(false);
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
       
        private void GetMethodInfoAndArgValues(ExecutionFrame frame, MDbgFrame mdbgFrame)
        {
            frame.Method = assemblyMgr.GetMethodDesc(mdbgFrame);
            MDbgValue[] mDbgValues = frame.Method.GetMethodArguments();
            if (mDbgValues == null) return;
            frame.LocalVariables = ValueInfoUtils.MDbgValuesToValueInfoArray(mDbgValues);

            // resolve constants
            ValueInfoUtils.GetValueForNonLitteralValueInfos(assemblyMgr.GetConstants($"{frame.Method.NameSpace}.{frame.Method.ClassName}"), mdbgFrame);
        }

        private async Task GetMethodInfoAndArgValuesAsync(ExecutionFrame frame, MDbgFrame mdbgFrame)
        {
            await Task.Run(() => GetMethodInfoAndArgValues(frame, mdbgFrame));
        }

        private ExecutionFrame ReadFrame(MDbgThread thread)
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

                // task 4
                //if (!methods.ContainsKey(token))
                //    methods.Add(token, MethodDescUtils.GetMethodDescInstance(thread.CurrentFrame, assemblies));
                //Task loadMethodCacheTask = LoadMethodsCacheAsync(token, thread.CurrentFrame, assemblies);
                Task methodDescTask = GetMethodInfoAndArgValuesAsync(frame, thread.CurrentFrame);

                // task 1
                //frame.SourceFileLine = GetSourceLine(frame.SourceFile, frame.SourceFileLineNumber);
                Task<string> sourceLineTask = sourceFileMgr.GetSourceLineAsync(frame.SourceFile, frame.SourceFileLineNumber);
                
                // task 2
                //frame.LocalVariables = ValueInfoUtils.MDbgValuesToValueInfoArray(thread.CurrentFrame.Function.GetActiveLocalVars(thread.CurrentFrame));
                Task<ValueInfo[]> valueInfosTask = ValueInfoUtils.MDbgValuesToValueInfoArrayAsync(thread.CurrentFrame.Function.GetActiveLocalVars(thread.CurrentFrame));

                // task 3
                Task<ValueInfo> exceptionTask = ValueInfoUtils.MDbgValueExceptionToValueInfoAsync(dbgProcess.Threads.Active.CurrentException);

                Task.WaitAll(new Task[] { sourceLineTask, valueInfosTask, methodDescTask, exceptionTask });

                frame.SourceFileLine = sourceLineTask.Result;
                   
                ValueInfo[] valueInfos = new ValueInfo[frame.LocalVariables.Length + valueInfosTask.Result.Length];
                frame.LocalVariables.CopyTo(valueInfos, 0);
                valueInfosTask.Result.CopyTo(valueInfos, frame.LocalVariables.Length);

                frame.LocalVariables = valueInfos;

                frame.CurrentException = exceptionTask.Result;

                frame.executionTime = (DateTime.Now - startTime).ToString();
            }
            else
            {
                dbgProcess.StepOut().WaitOne();
            }
            return frame;
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
