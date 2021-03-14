using System;
using System.IO;

namespace FrameExporter
{
    public class ProcessToDebug
    {
        private string m_SourcePath = string.Empty;
        public string SourcePath {
            get {
                if (m_SourcePath == string.Empty)
                {
                    throw new ArgumentException("Source Path is not set !");
                }
                return m_SourcePath;
            }
            set {
                if (value == null || value == String.Empty)
                {
                    throw new ArgumentException("Source Path can't be null !");
                }
                if (!Directory.Exists(value))
                {
                    throw new ArgumentException("Source Path doesn't exist ! " + value);
                }
                m_SourcePath = value;
            }
        }

        private string m_SymbolPath = string.Empty;
        public string SymbolPath
        {
            get
            {
                if (m_SymbolPath == string.Empty)
                {
                    throw new ArgumentException("Symbol Path is not set !");
                }
                return m_SymbolPath;
            }
            set
            {
                if (value == null || value == String.Empty)
                {
                    throw new ArgumentException("Symbol Path can't be null !");
                }
                if (!Directory.Exists(value))
                {
                    throw new ArgumentException("Symbol Path doesn't exist ! " + value);
                }
                m_SymbolPath = value;
            }
        }

        private const int C_PID_NOT_SET = -1;
        private int m_PID = C_PID_NOT_SET;
        public int PID
        {
            get
            {
                if (m_PID == C_PID_NOT_SET)
                {
                    throw new ArgumentException("PID is not set !");
                }
                return m_PID;
            }
            set
            {
                if (m_PID == C_PID_NOT_SET)
                {
                    throw new ArgumentException("PID can't be " + C_PID_NOT_SET);
                }
                m_PID = value;
            }
        }

        public bool IsPIDSet()
        {
            return m_PID != C_PID_NOT_SET;
        }

        private string m_ExecutablePath = string.Empty;
        public string ExecutablePath
        {
            get
            {
                if (m_ExecutablePath == string.Empty)
                {
                    throw new ArgumentException("Executable Path is not set !");
                }
                return m_ExecutablePath;
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
                m_ExecutablePath = value;
            }
        }

        public void Check()
        {
            string err = string.Empty;
            if (m_SourcePath == string.Empty)
            {
                err = "* Source Path should be set\n";
            }
            if (m_SymbolPath == string.Empty)
            {
                err += "* Symbol Path should be set\n";
            }
            if (m_ExecutablePath == string.Empty)
            {
                err += "* Executable Path should be set";
            }
            if ( err != string.Empty )
            {
                throw new ArgumentException(err);
            }
        }
    }
}
