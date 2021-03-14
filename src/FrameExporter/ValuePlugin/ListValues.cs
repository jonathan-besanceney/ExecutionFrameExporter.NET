using Microsoft.Samples.Debugging.MdbgEngine;

namespace FrameExporter.ValuePlugin
{
    class ListValues
    {
        public static object[] GetValue(MDbgValue value)
        {
            if (value.IsNull) return null;
            // we need only to get '_items'
            MDbgValue items = value.GetFields("_items")[0];
            return ValueInfoUtils.GetArrayValue(items);
        }
    }
}
