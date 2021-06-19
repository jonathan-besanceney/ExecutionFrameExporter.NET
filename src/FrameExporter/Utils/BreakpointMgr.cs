using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Microsoft.Samples.Debugging.CorDebug;
using Microsoft.Samples.Debugging.MdbgEngine;


namespace FrameExporter.Utils
{
    /// <summary>
    /// Manage automated and user breakpoints.
    /// Automated function breakpoints allow debugger to issue StepOver instead of Stepping In blindly, resulting in less steps.
    /// User breakpoints allow users to choose where to start and end reading frame, reducing number of steps to perform to get wanted frame information.
    /// When user breakpoints are set, the automated function breakpoints are created deactivated. They will be activated on first user start breakpoint is hit,
    /// and deactivated when the last end breakpoint is hit.
    /// </summary>
    public class BreakpointMgr
    {
        private static int breakpointHitEventCounter = 0;

        public void InitBreakpointMgr(MDbgProcess mdbgProcess, FunctionTokensPerModule functionTokensPerModule, List<UserBreakpointParameter> userBreakpointParameter)
        {
            _functionTokensPerModule = functionTokensPerModule;
            _mdbgProcess = mdbgProcess;

            // Manage user created breakpoints
            if (userBreakpointParameter.Count > 0)
            {
                breakpointMgrMode = BreakpointMgrMode.USER_DEFINED;
                _defaultBreakpointActivated = false;
                CreateUserBreakpoints(userBreakpointParameter);
                _mdbgProcess.CorProcess.OnBreakpoint += CorProcess_OnBreakpoint;
            }

            // When attached to a running process, modules are already loaded. Create breakpoints on it
            CreateFunctionBreakpointsForAll();

            // When a new module is loaded, create breakpoints on it
            _mdbgProcess.CorProcess.OnModuleLoad += CorProcess_OnModuleLoad;
        }

        private void CreateUserBreakpoints(List<UserBreakpointParameter> userBreakpointParameter)
        {
            foreach(UserBreakpointParameter bps in userBreakpointParameter)
            {
                MDbgBreakpoint start = _mdbgProcess.Breakpoints.CreateBreakpoint(bps.StartSourceFile, bps.StartLine);
                UserBreakpoint userBreakpoint = new UserBreakpoint(start);
                if (bps.IsEndDefined)
                {
                    MDbgBreakpoint end = _mdbgProcess.Breakpoints.CreateBreakpoint(bps.EndSourceFile, bps.EndLine);
                    userBreakpoint.BreakpointLineEnd = end;
                }
                _userBreakpoints.Add(userBreakpoint);
            }
        }

        private void CreateFunctionBreakpointsForAll()
        {
            foreach(MDbgModule mDbgModule in _mdbgProcess.Modules)
            {
                CreateFunctionBreakpoints(mDbgModule);
            }
        }

        private static Mutex _counterMutex = new Mutex();
        /// <summary>
        /// Called on breakpoint hit.
        /// 
        /// If manager is set on USER_DEFINED, automatic function breakpoints will be activated when 1st UserBreakpoint.breakpointLineStart is hit allowing proper frame reading.
        /// Deactivated when *all* UserBreakpoint.breakpointLineEnd are hit ( _userBreakpointsCounter == 0 )
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CorProcess_OnBreakpoint(object sender, CorBreakpointEventArgs e)
        {
            if (breakpointHitEventCounter == 0) _breakpointHit.Reset();
            breakpointHitEventCounter++;
            _counterMutex.WaitOne();
            MDbgBreakpoint breakpoint = _mdbgProcess.Breakpoints.Lookup(e.Breakpoint);
            switch (_userBreakpoints.GetBreakpointType(breakpoint))
            {
                case BreakpointType.USER_START:
                    _userBreakpointsCounter++;
                    if (_userBreakpointsCounter == 1 && !_defaultBreakpointActivated) EnableDefaultBreakpoints();
                    break;
                case BreakpointType.USER_END:
                    _userBreakpointsCounter--;
                    if (_userBreakpointsCounter == 0 && _defaultBreakpointActivated) DisableDefaultBreakpoints();
                    break;
                case BreakpointType.DEBUGGER:
                    break;
            }           
            _counterMutex.ReleaseMutex();
            breakpointHitEventCounter--;
            if (breakpointHitEventCounter == 0) _breakpointHit.Set();
        }

        /// <summary>
        /// Define breakpoints on loaded module.
        /// If manager is set on USER_DEFINED, default function breakpoints will be initialized deactivated.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CorProcess_OnModuleLoad(object sender, CorModuleEventArgs e)
        {
            CreateFunctionBreakpoints(e.Module.Name);
        }

        private void CreateFunctionBreakpoints(string moduleFullyQualifiedName, MDbgModule mDbgModule = null)
        {
            if (!knownModules.Contains(moduleFullyQualifiedName) && _functionTokensPerModule.ContainsModule(moduleFullyQualifiedName))
            {
                if (mDbgModule == null) mDbgModule = _mdbgProcess.Modules.Lookup(moduleFullyQualifiedName);
                foreach (int functionToken in _functionTokensPerModule.GetModuleFunctionTokens(moduleFullyQualifiedName))
                {
                    MDbgFunction mDbgFunction = mDbgModule.GetFunction(functionToken);
                    MDbgBreakpoint bp = _mdbgProcess.Breakpoints.CreateBreakpoint(mDbgFunction, 0);
                    bp.Enabled = (breakpointMgrMode == BreakpointMgrMode.DEFAULT);
                    _defaultFunctionBreakpoints.Add(bp);
                }
                knownModules.Add(moduleFullyQualifiedName);
            }
        }

        private void CreateFunctionBreakpoints(MDbgModule mDbgModule)
        {
            string moduleFullyQualifiedName = mDbgModule.CorModule.Name;
            CreateFunctionBreakpoints(moduleFullyQualifiedName, mDbgModule);
        }

        public BreakpointMgrMode breakpointMgrMode { get; set; } = BreakpointMgrMode.DEFAULT;

        public void EnableDefaultBreakpoints()
        {
            foreach(MDbgBreakpoint breakpoint in _defaultFunctionBreakpoints)
            {
                breakpoint.Enabled = true;
            }
            _defaultBreakpointActivated = true;
        }

        public void DisableDefaultBreakpoints()
        {
            foreach (MDbgBreakpoint breakpoint in _defaultFunctionBreakpoints)
            {
                breakpoint.Enabled = false;
            }
            _defaultBreakpointActivated = false;
        }

        public bool HasBreakpoints
        {
            get
            {
                _breakpointHit.WaitOne();
                try
                {
                    return _mdbgProcess.StopReason is BreakpointHitStopReason || _defaultBreakpointActivated;
                } catch
                {
                    return false;
                }
            }
        }

        private ManualResetEvent _breakpointHit = new ManualResetEvent(true); // this will prevent concurrency issue when calling HasBreakpoints
        private bool _defaultBreakpointActivated = true;
        private MDbgProcess _mdbgProcess;
        private FunctionTokensPerModule _functionTokensPerModule;
        private List<string> knownModules = new List<string>();
        private List<MDbgBreakpoint> _defaultFunctionBreakpoints = new List<MDbgBreakpoint>();
        private int _userBreakpointsCounter = 0;
        private UserBreakPoints _userBreakpoints = new UserBreakPoints();
    }

    /// <summary>
    /// User breakpoint list
    /// </summary>
    public class UserBreakPoints : List<UserBreakpoint>
    {
        public BreakpointType GetBreakpointType(MDbgBreakpoint breakpoint)
        {
            if (breakpoint == null) return BreakpointType.DEBUGGER;
            Trace.WriteLine($"Breakpoint #{breakpoint.Number}");
            foreach (UserBreakpoint bp in this)
            {
                if (breakpoint.Number == bp.BreakpointLineStart.Number)
                    return BreakpointType.USER_START;
                if (breakpoint.Number == bp.BreakpointLineEnd.Number)
                    return BreakpointType.USER_END;
            }
            return BreakpointType.DEBUGGER;
        }
    }

    /// <summary>
    /// User start / end breakpoint.
    /// End breakpoint may not be defined
    /// </summary>
    public class UserBreakpoint
    {
        public UserBreakpoint(MDbgBreakpoint breakpointLineStart)
        {
            _breakpointLineStart = breakpointLineStart;
        }

        public MDbgBreakpoint BreakpointLineStart { get => _breakpointLineStart; }

        public MDbgBreakpoint BreakpointLineEnd
        {
            get => _breakpointLineEnd;
            set => _breakpointLineEnd = value;
        }

        private MDbgBreakpoint _breakpointLineStart;
        private MDbgBreakpoint _breakpointLineEnd = null;
    }

    /// <summary>
    /// Behaviour of the Breakpoint Manager
    /// </summary>
    public enum BreakpointMgrMode
    {
        /// <summary>
        /// Debuger will issue breakpoints on all methods
        /// </summary>
        DEFAULT,
        /// <summary>
        /// Debuger will issue user defined start end stop breakpoints. Between start/stop breakpoint, debugger switches on DEFAULT.
        /// </summary>
        USER_DEFINED
    }

    /// <summary>
    /// Breakpoint Type
    /// </summary>
    public enum BreakpointType
    {
        /// <summary>
        /// Breakpoint is a user start breakpoint
        /// </summary>
        USER_START,
        /// <summary>
        /// Breakpoint is a user end breakpoint
        /// </summary>
        USER_END,
        /// <summary>
        /// debugger managed breakpoint (automatic function breakpoint)
        /// </summary>
        DEBUGGER
    }
}
