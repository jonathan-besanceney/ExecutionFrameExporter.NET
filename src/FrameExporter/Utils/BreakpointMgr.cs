using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Samples.Debugging.CorDebug;
using Microsoft.Samples.Debugging.MdbgEngine;
using Microsoft.Samples.Tools.Mdbg;


namespace FrameExporter.Utils
{
    /// <summary>
    /// Responsible for setting breakpoints.
    /// Depending 
    /// </summary>
    public class BreakpointMgr
    {
        public void InitBreakpointMgr(MDbgProcess mdbgProcess, FunctionTokensPerModule functionTokensPerModule)
        {
            _functionTokensPerModule = functionTokensPerModule;
            _mdbgProcess = mdbgProcess;

            // When attached to a running process, modules are already loaded. Create breakpoints on it
            CreateFunctionBreakpointsForAll(mdbgProcess);

            // When a new module is loaded, create breakpoints on it
            _mdbgProcess.CorProcess.OnModuleLoad += CorProcess_OnModuleLoad;

            // Manage user created breakpoints
            // TODO : move that in User breakpoint section to avoid unnecessary event firering
            _mdbgProcess.CorProcess.OnBreakpoint += CorProcess_OnBreakpoint;
        }

        private void CreateFunctionBreakpointsForAll(MDbgProcess mdbgProcess)
        {
            foreach(MDbgModule mDbgModule in mdbgProcess.Modules)
            {
                CreateFunctionBreakpoints(mDbgModule);
            }
        }

        /// <summary>
        /// Called on breakpoint hit.
        /// 
        /// If manager is set on USER_DEFINED, default function breakpoints will be activated when *all* UserBreakpoint.breakpointLineStart is hit
        /// and default function breakpoints will be deactivated when *all* UserBreakpoint.breakpointLineEnd is hit
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CorProcess_OnBreakpoint(object sender, CorBreakpointEventArgs e)
        {
            if(_mgrMode == BreakpointMgrMode.USER_DEFINED)
            {
                MDbgBreakpoint breakpoint = _mdbgProcess.Breakpoints.Lookup(e.Breakpoint);
                // TODO finalize USER_DEFINED impl
            }
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
                    bp.Enabled = (_mgrMode == BreakpointMgrMode.DEFAULT);
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

        public BreakpointMgrMode breakpointMgrMode
        {
            get;
            set;
        }

        public void EnableDefaultBreakpoints()
        {
            foreach(MDbgBreakpoint breakpoint in _defaultFunctionBreakpoints)
            {
                breakpoint.Enabled = true;
            }
        }

        public void DisableDefaultBreakpoints()
        {
            foreach (MDbgBreakpoint breakpoint in _defaultFunctionBreakpoints)
            {
                breakpoint.Enabled = false;
            }
        }

        private BreakpointMgrMode _mgrMode = BreakpointMgrMode.DEFAULT;
        private MDbgProcess _mdbgProcess;
        private FunctionTokensPerModule _functionTokensPerModule;
        private List<string> knownModules = new List<string>();
        private List<MDbgBreakpoint> _defaultFunctionBreakpoints = new List<MDbgBreakpoint>();
        private int _userBreakpointsStartHit = 0;
        private List<UserBreakpoint> _userBreakpoints = new List<UserBreakpoint>();
    }

    public class UserBreakpoint
    {
        public UserBreakpoint(MDbgBreakpoint breakpointLineStart, MDbgBreakpoint breakpointLineEnd = null)
        {
            _breakpointLineStart = breakpointLineStart;
            _breakpointLineEnd = breakpointLineEnd;
        }

        private MDbgBreakpoint _breakpointLineStart;
        private MDbgBreakpoint _breakpointLineEnd;
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
}
