using System;

namespace FrameExporter.OutputPlugin
{
    public interface IOutputPlugin: IDisposable
    {
        void SendFrameAsync(ExecutionFrame frame);
    }
}
