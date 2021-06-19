using System;
using System.Collections.Generic;
using System.IO;

namespace FrameExporter
{
    public class Parameters
    {
        private string _sourcePath = string.Empty;
        public string SourcePath {
            get {
                if (string.IsNullOrEmpty(_sourcePath))
                {
                    throw new ArgumentException("Source Path is not set !");
                }
                return _sourcePath;
            }
            set
            {
                if (string.IsNullOrEmpty(value))
                {
                    throw new ArgumentException("Source Path cannot be null !");
                }
                if (!Directory.Exists(value))
                {
                    throw new ArgumentException("Source Path doesn't exist ! " + value);
                }
                _sourcePath = value;
            }
        }

        private string _symbolPath = string.Empty;
        public string SymbolPath
        {
            get
            {
                if (string.IsNullOrEmpty(_symbolPath))
                {
                    throw new ArgumentException("Symbol Path is not set !");
                }
                return _symbolPath;
            }
            set
            {
                if (string.IsNullOrEmpty(value))
                {
                    throw new ArgumentException("Symbol Path cannot be null !");
                }
                if (!Directory.Exists(value))
                {
                    throw new ArgumentException("Symbol Path doesn't exist ! " + value);
                }
                _symbolPath = value;
            }
        }

        private List<UserBreakpointParameter> _userBreakpointParameters = new List<UserBreakpointParameter>();
        public List<UserBreakpointParameter> UserBreakpointParameters { get => _userBreakpointParameters; }
        public void UserBreakpointParametersAdd(string value)
        {
            UserBreakpointParameter bp = UserBreakpointParameter.GetInstance(value);
            _userBreakpointParameters.Add(bp);
        }


        private const int C_PID_NOT_SET = -1;
        private int _pid = C_PID_NOT_SET;
        public int PID
        {
            get
            {
                if (_pid == C_PID_NOT_SET)
                {
                    throw new ArgumentException("PID is not set !");
                }
                return _pid;
            }
            set
            {
                if (_pid == C_PID_NOT_SET)
                {
                    throw new ArgumentException("PID can't be " + C_PID_NOT_SET);
                }
                _pid = value;
            }
        }

        public bool IsPIDSet()
        {
            return _pid != C_PID_NOT_SET;
        }

        public string SessionName { get; set; } = "Undefined";

        private string _executablePath = string.Empty;
        public string ExecutablePath
        {
            get
            {
                if (_executablePath == string.Empty)
                {
                    throw new ArgumentException("Executable Path is not set !");
                }
                return _executablePath;
            }
            set
            {
                if (value == null || value == string.Empty)
                {
                    throw new ArgumentException("Executable Path can't be null !");
                }
                if (!File.Exists(value))
                {
                    throw new ArgumentException("Executable Path doesn't exist ! " + value);
                }
                _executablePath = value;
            }
        }

        private string _executableArgs = string.Empty;
        public string ExecutableArgs
        {
            get => string.IsNullOrEmpty(_executableArgs) ? _executablePath  : $"{_executablePath} {_executableArgs}";
            set => _executableArgs = value;
        }

        public string ExecutableWorkingDir { get; set; } = ".";

        public bool StopOnNewThread { get; set; } = false;

        public void Check()
        {
            string err = string.Empty;
            if (string.IsNullOrEmpty(_sourcePath))
            {
                err = "* Source Path should be set\n";
            }
            if (string.IsNullOrEmpty(_symbolPath))
            {
                err += "* Symbol Path should be set\n";
            }
            if (string.IsNullOrEmpty(_executablePath))
            {
                err += "* Executable Path should be set";
            }
            if (!string.IsNullOrEmpty(err))
            {
                throw new ArgumentException(err);
            }
        }
    }

    public class UserBreakpointParameter
    {
        private UserBreakpointParameter() { }

        public static UserBreakpointParameter GetInstance(string bps)
        {
            if (string.IsNullOrEmpty(bps))
            {
                throw new ArgumentException("Can't parse empty breakpoint!");
            }
            try
            {
                string start = string.Empty;
                string end = string.Empty;
                string[] rawbp = bps.Split(_breakpointSeparator);

                if (rawbp.Length > 2)
                    throw new ArgumentException($"Unexpected breakpoint string '{bps}'. Expected format 'sourceFilePath:startIntLineNumber[,sourceFilePath:endIntLineNumber]'.");

                start = rawbp[0];
                if (rawbp.Length == 2) end = rawbp[1];

                return GetInstance(start, end);
            }
            catch (ArgumentException) { throw; }
            catch (Exception e)
            {
                throw new ArgumentException("Can't parse breakpoint: " + e.Message);
            }
        }

        public static UserBreakpointParameter GetInstance(string start, string end)
        {
            if (string.IsNullOrEmpty(start))
            {
                throw new ArgumentException("Start breakpoint cannot be null !");
            }
            try
            {
                UserBreakpointParameter bp = new UserBreakpointParameter();
                string[] rawbp = start.Split(_sourceLineSeparator);
                if (rawbp.Length != 2)
                    throw new ArgumentException($"Unexpected breakpoint string '{start}'. Expected format sourceFilePath:intLineNumber.");

                bp.StartSourceFile = rawbp[0];
                bp.StartLine = int.Parse(rawbp[1]);

                if (!string.IsNullOrEmpty(end))
                {
                    rawbp = end.Split(_sourceLineSeparator);
                    if (rawbp.Length != 2)
                        throw new ArgumentException($"Unexpected breakpoint string '{end}'. Expected format sourceFilePath:intLineNumber.");

                    bp.EndSourceFile = rawbp[0];
                    bp.EndLine = int.Parse(rawbp[1]);
                }

                return bp;
            }
            catch (ArgumentException) { throw; }
            catch (Exception e)
            {
                throw new ArgumentException("Failed to parse breakpoint! : " + e.Message);
            }
        }

        private string _startSourcefile = string.Empty;
        public string StartSourceFile
        {
            get => _startSourcefile;
            set
            {
                if (string.IsNullOrEmpty(value))
                {
                    throw new ArgumentException("Source file can't be null !");
                }
                if (!File.Exists(value))
                {
                    throw new ArgumentException("Source file doesn't exist ! " + value);
                }
                _startSourcefile = value;
            }
        }
        public int StartLine = 0;

        private string _endSourceFile = string.Empty;
        public string EndSourceFile
        {
            get => _endSourceFile;
            set
            {
                if (string.IsNullOrEmpty(value))
                {
                    throw new ArgumentException("Source file can't be null !");
                }
                if (!File.Exists(value))
                {
                    throw new ArgumentException("Source file doesn't exist ! " + value);
                }
                _endSourceFile = value;
            }
        }
        public int EndLine = 0;
        public bool IsEndDefined { get => EndLine != 0; }

        private static readonly char[] _breakpointSeparator = new char[] { ',', ';' };
        private static readonly char[] _sourceLineSeparator = new char[] { '#' };
    }
}
