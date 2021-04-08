using System.IO;
using System.Threading.Tasks;

namespace FrameExporter.OutputPlugin
{
    class FileOutput : IOutputPlugin
    {
        public FileOutput(string filePath) => m_file = new StreamWriter(filePath);

        public void Dispose() => m_file.Dispose();

        public async Task SendFrameAsync(ExecutionFrame frame) => await Task.Run(() => m_file.WriteLine(frame.ToString()));

        private StreamWriter m_file;
    }
}
