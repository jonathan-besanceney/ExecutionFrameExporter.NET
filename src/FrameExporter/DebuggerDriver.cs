using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

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
        private MDbgProcess mdbgProcess = null;
        private MDbgEngine debugger = null;
        private List<MDbgThread> mDbgThreads = new List<MDbgThread>();
        private static CorDebugger corDebugger;
        private static NextStepMgr nextStepMgr = new NextStepMgr();

        private static MDbgSourceFileMgr sourceFileMgr = new MDbgSourceFileMgr();
        private static AssemblyMgr assemblyMgr = new AssemblyMgr();
        private static BreakpointMgr breakpointMgr = new BreakpointMgr();

        private DebuggerDriver() { }

        public DebuggerDriver(ProcessToDebug processToDebug): this()
        {
            m_ProcessToDebug = processToDebug;
        }

        public void Initialize()
        {
            shell = new MDbgShell();
            debugger = new MDbgEngine();
            shell.Debugger = debugger;

            // Do we want the debugger to stop all new threads ?
            // TODO Make it parameter in app.config
            //debugger.Options.StopOnNewThread = true;

            // set source and pdb folders
            debugger.Options.SymbolPath = m_ProcessToDebug.SymbolPath;
            Console.WriteLine($"SymbolPath set to {m_ProcessToDebug.SymbolPath}");
            shell.FileLocator.Path = m_ProcessToDebug.SourcePath;
            Console.WriteLine($"SourcePath set to {m_ProcessToDebug.SourcePath}");

            // Load source files
            Console.WriteLine("Building source cache");
            sourceFileMgr.BuildSourceCache(m_ProcessToDebug.SourcePath);
            ExecutionFrameReader.SourcefileMgr = sourceFileMgr;

            // Load assemblies 
            assemblyMgr.BuildAssemblyCache(m_ProcessToDebug.ExecutablePath);
            ExecutionFrameReader.AssemblyMgr = assemblyMgr;
            
            Console.Write($"Get CLR Version of {m_ProcessToDebug.ExecutablePath}: ");
            string version = MdbgVersionPolicy.GetDefaultLaunchVersion(m_ProcessToDebug.ExecutablePath);
            Console.WriteLine(version);
            Console.Write("Create debugable process...");
            corDebugger = new CorDebugger(version);
            mdbgProcess = debugger.Processes.CreateLocalProcess(corDebugger);
            Console.WriteLine("done");
            if (mdbgProcess == null)
            {
                throw new MDbgShellException("Could not create debugging interface for runtime version " + version);
            }
            mdbgProcess.DebugMode = DebugModeFlag.Debug;
            
            
            if (m_ProcessToDebug.IsPIDSet())
            {
                Console.Write($"Attaching PID {m_ProcessToDebug.PID}...");
                mdbgProcess.Attach(m_ProcessToDebug.PID);
            }
            else
            {
                // TODO Deal with arguments !
                Console.Write($"Create process {m_ProcessToDebug.ExecutablePath}...");
                mdbgProcess.CreateProcess(m_ProcessToDebug.ExecutablePath, m_ProcessToDebug.ExecutablePath);
            }

            // Init breakpoint manager
            breakpointMgr.InitBreakpointMgr(mdbgProcess, assemblyMgr.FunctionTokensPerModule);

            Console.WriteLine("done");
        }

        public void Run()
        {
            try
            {
                using (IOutputPlugin output = new FileOutput("debug.log"))
                {
                    // entry point
                    using (WaitHandle waitHandle = mdbgProcess.Go())
                    {
                        long stepNo = 1;
                        while (mdbgProcess.ThreadCreatedStopEvent.WaitOne() && waitHandle.WaitOne() && mdbgProcess.Threads.HaveActive)
                        {
                            mdbgProcess.ProcessStopEvent.Reset();
                            
                            int no = 0;
                            while(no < mdbgProcess.Threads.Count)
                            {
                                if (mdbgProcess.Threads[no] != null && !mdbgProcess.Threads[no].Suspended)
                                {
                                    Trace.WriteLine($"Read thread[{no}] step no {stepNo}");
                                    nextStepMgr.Add(ExecutionFrameReader.ReadFrame(stepNo, mdbgProcess, mdbgProcess.Threads[no], output));
                                }
                                no++;
                            }
                            nextStepMgr.Run();
                            mdbgProcess.ProcessStopEvent.Set();
                            mdbgProcess.Go();
                            stepNo++;
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
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }

        public void Dispose()
        {
            if (mdbgProcess != null && mdbgProcess.IsAlive)
            {
                mdbgProcess.Detach();
                mdbgProcess = null;
            }
            shell = null;
            debugger = null;
        }
    }
}
