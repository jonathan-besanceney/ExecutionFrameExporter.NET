using System.Threading.Tasks;
using FrameExporter.Utils;
using Microsoft.Samples.Debugging.MdbgEngine;

namespace FrameExporter.ValuePlugin
{
    public class LinkedListValues : ValuePluginAbstract
    {
        public new const string handledType = "System.Collections.Generic.LinkedList`1";

        private static async Task<object> GetValueAsync(MDbgValue item)
        {
            return await Task.Run(() => ValueInfoUtils.GetValue(item));
        }

        public new static object[] GetValue(MDbgValue value)
        {
            if (value.IsNull) return null;

            // we need only to get 'item' value and resolve 'head' and 'next' until circular reference
            value.GetFields("count, head");
            int count = (int)ValueInfoUtils.GetGenericValue(value.GetField("count").CorValue);
            if (count == 0) return new object[0];
            object[] listValues = new object[count];
            Task<object>[] tasks = new Task<object>[count];

            MDbgValue next = value.GetField("head");
            int i = 0;
            while (i < count)
            {
                next.GetFields("item, next");
                tasks[i] = GetValueAsync(next.GetField("item"));
                if (++i == count) break;
                next = next.GetField("next");
            }

            Task.WaitAll(tasks);
            for (int j = 0; j < count; j++) listValues[j] = tasks[j].Result;

            return listValues;
        }
    }
}
