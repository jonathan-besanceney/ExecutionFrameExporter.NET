using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace FrameExporter.OutputPlugin
{
    class FileOutput : IOutputPlugin
    {
        public FileOutput(string filePath)
        {
            m_file = new StreamWriter(filePath);
            m_running = true;
            m_terminate = false;
            m_queue = new ConcurrentQueue<string>();
            m_first = true;
            m_file.WriteLine("[");
            Task.Run(() => WriteQueue());
        }

        private void WriteQueue()
        {
            while(m_running)
            {
                // wait until we have something to write
                while (m_queue.IsEmpty)
                {
                    Thread.Sleep(100);
                    // ok stop waiting and quit
                    if (m_queue.IsEmpty && m_terminate)
                    {
                        m_running = false;
                        break;
                    }
                }
                if (!m_queue.IsEmpty)
                {
                    try
                    {
                        m_queue.TryDequeue(out string frame);
                        if (m_first)
                        {
                            m_first = false;
                            m_file.Write(frame);
                        }
                        else
                        {
                            m_file.Write($",{Environment.NewLine}{frame}");
                        }
                    }
                    catch (Exception e)
                    {
                        Trace.WriteLine($"WriteQueue: {e.Message}");
                    }
                }
            }
        }

        public void Dispose() {
            m_terminate = true;
            while (m_running) Thread.Sleep(100);
            m_file.WriteLine($"{Environment.NewLine}]");
            m_file.Dispose();
        }

        public void SendFrameAsync(ExecutionFrame frame) => m_queue.Enqueue(frame.ToString());

        private StreamWriter m_file;
        private ConcurrentQueue<string> m_queue;
        private bool m_running;
        private bool m_terminate;
        private bool m_first;
    }
}
