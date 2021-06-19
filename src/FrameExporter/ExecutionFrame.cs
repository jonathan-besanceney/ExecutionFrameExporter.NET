using System;
using System.Diagnostics;
using System.Threading.Tasks;

using FrameExporter.OutputPlugin;
using FrameExporter.Utils;

using Microsoft.Samples.Debugging.CorDebug;
using Microsoft.Samples.Debugging.CorDebug.NativeApi;
using Microsoft.Samples.Debugging.MdbgEngine;
using Microsoft.Samples.Tools.Mdbg;

using Newtonsoft.Json;


namespace FrameExporter
{
    public class ValueInfo
    {
        public long Address { get; set; }
        public int MetadataToken { get; set; } = 0;
        public string Name { get; set; }
        public string TypeName { get; set; }
        public bool IsArrayType { get; set; }
        public bool IsComplexType { get; set; }
        public bool IsNull { get; set; }
        public bool IsLitteral { get; set; } = false;
        public bool IsStatic { get; set; } = false;
        public bool IsMethodArgument { get; set; } = false;
        public object Value { get; set; }

        //public string executionTime { get; set; }
    }

    public class MethodDesc
    {
        public int Token { get; set; }
        public string AssemblyName { get; set; }
        public string NameSpace { get; set; }
        public string ClassName { get; set; }
        public string Attributes { get; internal set; }
        public string Name { get; set; }
        public string ReturnTypeName { get; set; }
        public string Signature { get; set; }

        //public string executionTime { get; set; }

        // Don't serialize these
        private MDbgValue[] args;
        public MDbgValue[] GetMethodArguments() { return args; }
        public void SetMethodArguments(MDbgValue[] value) { args = value; }
    }

    public class ExecutionFrame
    {
        [JsonProperty]
        public static string SessionName { get; set; }

        public long StepNo { get; set; }
        public int ThreadNumber { get; set; }

        public long Ticks { get; set; }

        public bool Suspended { get; set; }

        // replace with a bean containing indexes in source file
        public string SourceFile { get; set; }
        public int LineNumber { get; set; }
        public int ColumnNumber { get; set; }
        public string SourceFileLine { get; set; } 

        public MethodDesc Method { get; set; }
        public ValueInfo[] LocalVariables { get; set; }

        public ValueInfo[] Constants { get; set; }
        public ValueInfo[] StaticVariables { get; set; }
        public ValueInfo CurrentException { get; set; }

        public string executionTime { get; set; }

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this);// Formatting.Indented);
        }
    }

    public static class ExecutionFrameReader
    {
        public static MDbgSourceFileMgr SourcefileMgr { get; set; }
        public static AssemblyMgr AssemblyMgr { get; set; }

        private static void GetMethodInfoAndArgValues(ExecutionFrame frame, MDbgFrame mdbgFrame)
        {
            frame.Method = AssemblyMgr.GetMethodDesc(mdbgFrame);
            if (frame.Method == null)
                return;
            MDbgValue[] mDbgValues = frame.Method.GetMethodArguments();
            if (mDbgValues == null) return;
            frame.LocalVariables = ValueInfoUtils.MDbgValuesToValueInfoArray(mDbgValues, true);

            // Get constants and static variables
            string hash = $"{frame.Method.NameSpace}.{frame.Method.ClassName}";
            ValueInfo[] constants = AssemblyMgr.GetLitterals(hash);
            frame.Constants = constants;

            ValueInfo[] statics = AssemblyMgr.GetStaticVariables(hash);
            ValueInfoUtils.GetStaticValueInfos(statics, mdbgFrame);
            frame.StaticVariables = statics;
        }

        private static async Task GetMethodInfoAndArgValuesAsync(ExecutionFrame frame, MDbgFrame mdbgFrame)
        {
            await Task.Run(() => GetMethodInfoAndArgValues(frame, mdbgFrame));
        }

        public static FrameType ReadFrame(long stepNo, MDbgProcess process, MDbgThread thread, IOutputPlugin output)
        {
            FrameType frameType = FrameType.UNMANAGED;
            ExecutionFrame frame = null;

            //TraceThreadFrames(thread);
            try
            {
                if (
                    thread.HaveCurrentFrame
                    && thread.CurrentFrame.CorFrame.FrameType == CorFrameType.ILFrame 
                    && thread.CurrentSourcePosition != null 
                    && thread.CorThread.UserState != CorDebugUserState.USER_UNSAFE_POINT // Avoid COMException when retrieving dereferenced values. Also seems that CurrentFrame is out of context in this case
                    )
                {
                    DateTime startTime = DateTime.Now;

                    frame = new ExecutionFrame();
                    frame.StepNo = stepNo;
                    frame.Ticks = startTime.Ticks;
                    frame.ThreadNumber = thread.Number;
                    frame.SourceFile = thread.CurrentSourcePosition.Path;
                    frame.LineNumber = thread.CurrentSourcePosition.Line;
                    frame.ColumnNumber = thread.CurrentSourcePosition.StartColumn;
                    frame.Suspended = thread.Suspended;

                    Task methodDescTask = GetMethodInfoAndArgValuesAsync(frame, thread.CurrentFrame);
                    Task<string> sourceLineTask = SourcefileMgr.GetSourceLineAsync(frame.SourceFile, frame.LineNumber);
                    try
                    {
                        MDbgValue[] activeLocalVars = thread.CurrentFrame.Function.GetActiveLocalVars(thread.CurrentFrame);
                        Task<ValueInfo[]> valueInfosTask = ValueInfoUtils.MDbgValuesToValueInfoArrayAsync(activeLocalVars);


                        Task<ValueInfo> exceptionTask = ValueInfoUtils.MDbgValueExceptionToValueInfoAsync(thread.CurrentException);

                        Task.WaitAll(new Task[] { sourceLineTask, valueInfosTask, methodDescTask, exceptionTask });

                        frame.SourceFileLine = sourceLineTask.Result;
                        Trace.WriteLine($"thread[{thread.Number}]: {frame.SourceFileLine}");

                        ValueInfo[] valueInfos = new ValueInfo[frame.LocalVariables.Length + valueInfosTask.Result.Length];
                        frame.LocalVariables.CopyTo(valueInfos, 0);
                        valueInfosTask.Result.CopyTo(valueInfos, frame.LocalVariables.Length);

                        frame.LocalVariables = valueInfos;
                        frame.CurrentException = exceptionTask.Result;
                        frame.executionTime = (DateTime.Now - startTime).ToString();

                        output.SendFrameAsync(frame);
                        frameType = FrameType.IL;
                    }
                    catch
                    {
                        // TODO if crap happens. Should be dealt with
                        // Debug code by now
                        TraceThreadFrames(thread);
                    }
                }
                else if (thread.CorThread.UserState == CorDebugUserState.USER_UNSAFE_POINT)
                {
                    frameType = FrameType.UNSAFE;
                }
            }
            catch (Exception e)
            {
                Trace.WriteLine($"!! ReadFrame thread[{thread.Number}]: {e.Message} {e.StackTrace}");
            }
            return frameType;
        }

        static void TraceThreadFrames(MDbgThread thread)
        {
            try
            {
                Trace.WriteLine($"thread[{thread.Number}].CorThread.UserState: {thread.CorThread.UserState}");
                Trace.WriteLine($"thread[{thread.Number}].Id: {thread.Id}");
                Trace.WriteLine($"thread[{thread.Number}].Priority: {thread.Priority}");
                Trace.WriteLine($"thread[{thread.Number}].Suspended: {thread.Suspended}");

                int i = 0;
                foreach(MDbgFrame frame in thread.Frames)
                {
                    Trace.WriteLine($"thread[{thread.Number}].Frames[{i}]: {frame}");

                    Trace.WriteLine($"thread[{thread.Number}].Frames[{i}].Callee: {((MDbgILFrame)frame).Callee}");
                    Trace.WriteLine($"thread[{thread.Number}].Frames[{i}].Caller: {((MDbgILFrame)frame).Caller}");
                    i++;
                }
            }
            catch (Exception e)
            {
                Trace.WriteLine($"thread[{thread.Number}].BottomFrame: {e.Message}");
            }           
        }
    }

    public enum FrameType
    {
        /// <summary>
        /// All good, the frame is readable
        /// </summary>
        IL,
        /// <summary>
        /// UserState of the thread is CorDebugUserState.USER_UNSAFE_POINT
        /// </summary>
        UNSAFE,
        /// <summary>
        /// No current frame,
        /// Non-IL frame
        /// No current source position
        /// </summary>
        UNMANAGED
    }
}
