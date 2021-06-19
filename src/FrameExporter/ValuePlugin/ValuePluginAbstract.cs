using FrameExporter.Utils;
using Microsoft.Samples.Debugging.MdbgEngine;

namespace FrameExporter.ValuePlugin
{
    public abstract class ValuePluginAbstract
    {
        public const string handledType = "object";

        public static object GetValue(MDbgValue value)
        {
            return ValueInfoUtils.GetValue(value);
        }
    }
}
