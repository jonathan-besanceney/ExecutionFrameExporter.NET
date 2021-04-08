using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FrameExporter.OutputPlugin
{
    public interface IOutputPlugin: IDisposable
    {
        Task SendFrameAsync(ExecutionFrame frame);
    }
}
