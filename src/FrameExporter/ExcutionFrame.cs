using Newtonsoft.Json;


namespace FrameExporter
{
    public class ValueInfo
    {
        public long Address { get; set; }
        public string Name { get; set; }
        public string TypeName { get; set; }
        public bool IsArrayType { get; set; }
        public bool IsComplexType { get; set; }
        public bool IsNull { get; set; }
        public object Value { get; set; }
    }

    public class MethodDesc
    {
        public int Token { get; set; }
        public string AssemblyName { get; set; }
        public string NameSpace { get; set; }
        public string ClassName { get; set; }
        public string FullName { get; set; }
        public string Attributes { get; internal set; }
        public string Name { get; set; }
        public string ReturnTypeName { get; set; }
        public string Signature { get; set; }
        public ValueInfo[] Arguments { get; set; }
    }

    public class ExecutionFrame
    {
        //public FrameInfo PreviousFrameInfo { get; set; }

        public int ThreadNumber { get; set; }

        // replace with a bean containing indexes in source file
        public string SourceFile { get; set; }
        public int SourceFileLineNumber { get; set; }
        public int SourceFileColumnNumber { get; set; }
        public string SourceFileLine { get; set; } 

        public MethodDesc Method { get; set; }
        public ValueInfo[] LocalVariables { get; set; }

        public ValueInfo[] GlobalVariables { get; set; }
        public ValueInfo CurrentException { get; set; }

        public string executionTime { get; set; }

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this);// Formatting.Indented);
        }
    }

    
}
