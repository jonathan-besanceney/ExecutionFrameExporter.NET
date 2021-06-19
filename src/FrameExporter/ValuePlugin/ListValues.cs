using FrameExporter.Utils;
using Microsoft.Samples.Debugging.MdbgEngine;

namespace FrameExporter.ValuePlugin
{
    public class ListValues : ValuePluginAbstract
    {
        public new const string handledType = "System.Collections.Generic.List`1";

        public new static object[] GetValue(MDbgValue value)
        {
            if (value.IsNull) return null;
            // we need only to get '_items'
            MDbgValue items = value.GetFields("_items")[0];
            return ValueInfoUtils.GetArrayValue(items);
        }
    }
}
