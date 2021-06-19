using System;
using System.Collections.Generic;
using Microsoft.Samples.Debugging.MdbgEngine;

namespace FrameExporter.ValuePlugin
{
    public static class RegisteredValuePlugin
    {
        private static Dictionary<string, Func<MDbgValue, object>> plugins = new Dictionary<string, Func<MDbgValue, object>>();

        static RegisteredValuePlugin()
        {
            plugins.Add(ListValues.handledType, ListValues.GetValue);
            plugins.Add(LinkedListValues.handledType, LinkedListValues.GetValue);
        }

        public static Func<MDbgValue, object> GetPlugin(MDbgValue value)
        {
            string typeName = value.TypeName;
            foreach (string handledType in plugins.Keys)
                if (typeName.StartsWith(handledType)) return plugins[handledType];

            return null;
        }
    }
}
