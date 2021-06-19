using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Threading;

using FrameExporter.OutputPlugin;
using FrameExporter.Utils;

using Microsoft.Samples.Debugging.CorDebug;
using Microsoft.Samples.Debugging.CorDebug.NativeApi;
using Microsoft.Samples.Debugging.MdbgEngine;
using Microsoft.Samples.Tools.Mdbg;

namespace FrameExporter
{
    public class DebuggerDriver : CommandBase, IDisposable
    {
        private Parameters _parameters = null;
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

        public DebuggerDriver(Parameters parameters): this()
        {
            _parameters = parameters;
        }

        public void Initialize()
        {
            shell = new MDbgShell();
            debugger = new MDbgEngine();
            shell.Debugger = debugger;

            // Assign SessionName in the exported execution frames
            ExecutionFrame.SessionName = _parameters.SessionName;

            // Do we want the debugger to stop on all new threads ?
            debugger.Options.StopOnNewThread = _parameters.StopOnNewThread;

            // set source and pdb folders
            debugger.Options.SymbolPath = _parameters.SymbolPath;
            Console.WriteLine($"SymbolPath set to {_parameters.SymbolPath}");
            shell.FileLocator.Path = _parameters.SourcePath;
            Console.WriteLine($"SourcePath set to {_parameters.SourcePath}");

            // Load source files
            Console.WriteLine("Building source cache");
            sourceFileMgr.BuildSourceCache(_parameters.SourcePath);
            ExecutionFrameReader.SourcefileMgr = sourceFileMgr;

            // Load assemblies 
            assemblyMgr.BuildAssemblyCache(_parameters.ExecutablePath);
            ExecutionFrameReader.AssemblyMgr = assemblyMgr;
            
            Console.Write($"Get CLR Version of {_parameters.ExecutablePath}: ");
            string version = MdbgVersionPolicy.GetDefaultLaunchVersion(_parameters.ExecutablePath);
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
            
            
            if (_parameters.IsPIDSet())
            {
                Console.Write($"Attaching PID {_parameters.PID}...");
                mdbgProcess.Attach(_parameters.PID);
            }
            else
            {
                Console.Write($"Create process {_parameters.ExecutablePath}...");
                mdbgProcess.CreateProcess(_parameters.ExecutablePath, _parameters.ExecutableArgs, _parameters.ExecutableWorkingDir);
            }

            // Init breakpoint manager
            breakpointMgr.InitBreakpointMgr(mdbgProcess, assemblyMgr.FunctionTokensPerModule, _parameters.UserBreakpointParameters);

            Console.WriteLine("done");
        }

        public void Run()
        {
            try
            {
                using (IOutputPlugin output = new FileOutput("debug.log"))
                {
                    // Debugee entry point
                    using (WaitHandle waitHandle = mdbgProcess.Go())
                    {
                        bool entryPoint = true;
                        long stepNo = 1;
                        int incrementStepNo = 0; // Will be set to 1 if there is something read

                        while (mdbgProcess.ThreadCreatedStopEvent.WaitOne() && waitHandle.WaitOne() && mdbgProcess.Threads.HaveActive)
                        {
                            mdbgProcess.ProcessStopEvent.Reset();

                            // If user has set breakpoints, step out the entrypoint
                            if (entryPoint && breakpointMgr.breakpointMgrMode == BreakpointMgrMode.USER_DEFINED)
                            {
                                nextStepMgr.Add(new NextStep(mdbgProcess, mdbgProcess.Threads[0], StepperType.Out, false));
                                entryPoint = false;
                            }
                            else if (breakpointMgr.HasBreakpoints) // we want to read frame here
                            {
                                int threadCounter = 0;
                                while (threadCounter < mdbgProcess.Threads.Count)
                                {
                                    if (mdbgProcess.Threads[threadCounter] != null && !mdbgProcess.Threads[threadCounter].Suspended)
                                    {
                                        Trace.WriteLine($"Read thread[{threadCounter}] step no {stepNo}");
                                        NextStep nextStep = null;
                                        FrameType frameType = ExecutionFrameReader.ReadFrame(stepNo, mdbgProcess, mdbgProcess.Threads[threadCounter], output);

                                        switch (frameType)
                                        {
                                            case FrameType.UNSAFE:
                                            case FrameType.UNMANAGED:
                                                nextStep = new NextStep(mdbgProcess, mdbgProcess.Threads[threadCounter], StepperType.Out, false);
                                                break;
                                            case FrameType.IL:
                                                nextStep = new NextStep(mdbgProcess, mdbgProcess.Threads[threadCounter], StepperType.Over, false);
                                                incrementStepNo = 1;
                                                break;
                                        }
                                        nextStepMgr.Add(nextStep);
                                    }
                                    threadCounter++;
                                }
                            }
                            else // nothing left to read
                            {
                                nextStepMgr.Add(new NextStep(mdbgProcess, mdbgProcess.Threads[0], StepperType.Out, false));
                            }
                            nextStepMgr.Run();

                            mdbgProcess.ProcessStopEvent.Set();
                            mdbgProcess.Go();

                            stepNo += incrementStepNo;
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
