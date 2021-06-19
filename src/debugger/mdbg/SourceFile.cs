//---------------------------------------------------------------------
//  This file is part of the CLR Managed Debugger (mdbg) Sample.
// 
//  Copyright (C) Microsoft Corporation.  All rights reserved.
//---------------------------------------------------------------------
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Microsoft.Samples.Tools.Mdbg
{
    public class MDbgSourceFileMgr : IMDbgSourceFileMgr
    {
        public MDbgSourceFileMgr()
        {
            m_sourceCache = new Dictionary<string, MDbgSourceFile>();
        }

        public MDbgSourceFileMgr(string folder) : this()
        {
            BuildSourceCache(folder);
        }

        public void BuildSourceCache(string folder)
        {
            Dictionary<string, Task<MDbgSourceFile>> sourceTasks = new Dictionary<string, Task<MDbgSourceFile>>();
            LoadSourceTree(folder, sourceTasks);
            Task<MDbgSourceFile>[] tasks = new Task<MDbgSourceFile>[sourceTasks.Count];
            sourceTasks.Values.CopyTo(tasks, 0);
            Task.WaitAll(tasks);
            foreach (string key in sourceTasks.Keys)
            {
                m_sourceCache.Add(key, sourceTasks[key].Result);
            }
        }

        private void LoadSourceTree(string folder, Dictionary<string, Task<MDbgSourceFile>> sourceTasks)
        {
            try
            {
                Console.WriteLine(folder);

                foreach (string f in Directory.GetFiles(folder))
                {
                    if (Common.EndsWithAny(f, extensions))
                    {
                        Console.WriteLine(f);
                        Task<MDbgSourceFile> sourceFileTask = GetMDbsSourceFileInstanceAsync(f);
                        sourceTasks.Add(f, sourceFileTask);
                    }
                }

                foreach (string d in Directory.GetDirectories(folder))
                {
                    if (!Common.EndsWithAny(d, excludedFolders))
                        LoadSourceTree(d, sourceTasks);
                }
            }
            catch (Exception excpt)
            {
                Console.WriteLine(excpt.Message);
            }
        }

        private async Task<MDbgSourceFile> GetMDbsSourceFileInstanceAsync(string source)
        {
            return await Task.Run(() => new MDbgSourceFile(source));
        }

        public string GetSourceLine(string path, int lineNumber)
        {
            return GetSourceFile(path)[lineNumber];
        }

        public async Task<string> GetSourceLineAsync(string path, int lineNumber)
        {
            return await Task.Run(() => GetSourceLine(path, lineNumber));
        }

        public IMDbgSourceFile GetSourceFile(string path)
        {
            string s = string.Intern(path);

            if (!m_sourceCache.ContainsKey(s))
                m_sourceCache.Add(s, new MDbgSourceFile(s));

            return m_sourceCache[s];
        }

        public void ClearDocumentCache()
        {
            m_sourceCache.Clear();
        }

        //https://github.com/dotnet/platform-compat/blob/master/docs/DE0006.md
        //private Hashtable m_sourceCache = new Hashtable();
        private Dictionary<string, MDbgSourceFile> m_sourceCache;

        // https://www.techrepublic.com/article/make-sense-out-of-the-confusing-world-of-net-file-types/
        // .NET source file extensions
        private static readonly string[] extensions = new string[] { "vb", "cs", "aspx", "asax", "ashx", "ascx" };
        private static readonly string[] excludedFolders = new string[] { "bin", "obj" };
    }

    class MDbgSourceFile : IMDbgSourceFile
    {
        public MDbgSourceFile(string path)
        {
            m_path = path;
            try
            {
                Initialize();
            }
            catch (FileNotFoundException)
            {
                throw new MDbgShellException("Could not find source: " + m_path);
            }
        }

        public string Path
        {
            get
            {
                return m_path;
            }
        }

        public string this[int lineNumber]
        {
            get
            {
                if (m_lines == null)
                {
                    Initialize();
                }
                if ((lineNumber < 1) || (lineNumber > m_lines.Count))
                    throw new MDbgShellException(string.Format("Could not retrieve line {0} from file {1}.",
                                                               lineNumber, this.Path));

                return (string)m_lines[lineNumber - 1];
            }
        }

        public int Count
        {
            get
            {
                if (m_lines == null)
                {
                    Initialize();
                }
                return m_lines.Count;
            }
        }

        protected void Initialize()
        {
            StreamReader sr = null;
            try
            {
                // Encoding.Default doesn’t port between machines, but it's used just in case the source isn’t Unicode
                sr = new StreamReader(m_path, System.Text.Encoding.Default, true);
                m_lines = new ArrayList();

                string s = sr.ReadLine();
                while (s != null)
                {
                    m_lines.Add(s);
                    s = sr.ReadLine();
                }
            }
            finally
            {
                if (sr != null)
                    sr.Close(); // free resources in advance
            }
        }

        private ArrayList m_lines;
        private string m_path;

    }
}
