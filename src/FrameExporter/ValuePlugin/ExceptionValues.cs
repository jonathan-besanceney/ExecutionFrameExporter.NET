using System.Collections.Generic;
using System.Threading.Tasks;

using Microsoft.Samples.Debugging.MdbgEngine;

namespace FrameExporter.ValuePlugin
{
    class ExceptionValues
    {
        /**
         * Exception standard properties :
         * 
         * s_EDILock
         * _className
         * _exceptionMethod
         * _exceptionMethodString
         * _message
         * _data
         * _innerException
         * _helpURL
         * _stackTrace
         * _watsonBuckets : Uncaught exception are distinguished with _watsonBuckets property https://stackoverflow.com/questions/51361370/what-is-a-watson-information-bucket
         * _stackTraceString
         * _remoteStackTraceString
         * _remoteStackIndex
         * _dynamicMethods
         * _HResult
         * _source
         * _xptrs
         * _xcode
         * _ipForWatsonBuckets
         * _safeSerializationManager
         * 
         * As we already have the stack tree, only few properties will be retrived from the exception object:
         * _message
         * _watsonBuckets to set uncaught exception flag
         * _HResult
         * 
         **/

        public static Dictionary<string, object> GetValue(MDbgValue exception)
        {
            if (exception.IsNull) return null;

            Dictionary<string, object> exceptionValues = new Dictionary<string, object>();
            
            exception.GetFields("_message, _watsonBuckets, _HResult");

            Task<string> messageTask = ValueInfoUtils.GetStringValueAsync(exception.GetField("_message").CorValue);
            Task<object> HResultTask = ValueInfoUtils.GetGenericValueAsync(exception.GetField("_HResult").CorValue);

            exceptionValues.Add("Uncaught Exception", !exception.GetField("_watsonBuckets").IsNull);

            Task.WaitAll(new Task[] { messageTask, HResultTask });

            exceptionValues.Add("Message", messageTask.Result);
            exceptionValues.Add("HResult", HResultTask.Result);

            return exceptionValues;
        }

        public static async Task<Dictionary<string, object>> GetValueAsync(MDbgValue exception)
        {
            return await Task.Run(() => GetValue(exception));
        }
    }
}
