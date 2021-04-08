using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Samples.Debugging.MdbgEngine;
using Microsoft.Samples.Tools.Mdbg;

namespace FrameExporter.Utils
{
    public class AssemblyMgr
    {
        private const string assemblySeparator = ",";
        private const string dot = ".";
        private static char[] nameSeparator = new char[] { '.' };

        private readonly Dictionary<long, MethodDesc> methods = new Dictionary<long, MethodDesc>();
        
        private class TypeInfoCache
        {
            public Dictionary<string, MethodInfo> MethodInfoCache = new Dictionary<string, MethodInfo>();
            public Dictionary<string, MemberInfo> MemberInfoCache = new Dictionary<string, MemberInfo>();
            public Dictionary<string, List<ValueInfo>> FieldInfoCache = new Dictionary<string, List<ValueInfo>>();
            public Dictionary<string, EventInfo> EventInfoCache = new Dictionary<string, EventInfo>();
            public Dictionary<string, ConstructorInfo> ConstructorInfoCache = new Dictionary<string, ConstructorInfo>();
            public Dictionary<string, PropertyInfo> PropertyInfoCache = new Dictionary<string, PropertyInfo>();

            public void Concat(TypeInfoCache typeInfoCache)
            {
                MethodInfoCache = MethodInfoCache.Concat(typeInfoCache.MethodInfoCache).ToDictionary(x => x.Key, x=>x.Value);
                MemberInfoCache = MemberInfoCache.Concat(typeInfoCache.MemberInfoCache).ToDictionary(x => x.Key, x => x.Value);
                FieldInfoCache = FieldInfoCache.Concat(typeInfoCache.FieldInfoCache).ToDictionary(x => x.Key, x => x.Value);
                EventInfoCache = EventInfoCache.Concat(typeInfoCache.EventInfoCache).ToDictionary(x => x.Key, x => x.Value);
                ConstructorInfoCache = ConstructorInfoCache.Concat(typeInfoCache.ConstructorInfoCache).ToDictionary(x => x.Key, x => x.Value);
                PropertyInfoCache = PropertyInfoCache.Concat(typeInfoCache.PropertyInfoCache).ToDictionary(x => x.Key, x => x.Value);
            }
        }
        private readonly TypeInfoCache assemblyCache = new TypeInfoCache();

        private static string GetParameterInfoHash(ParameterInfo[] parameterInfos)
        {
            StringBuilder h_parameters = new StringBuilder();
            foreach (ParameterInfo parameterInfo in parameterInfos)
            {
                h_parameters.Append(dot);
                h_parameters.Append(parameterInfo.ParameterType.FullName);
            }
            return h_parameters.ToString();
        }

        private static string GetParameterInfoHash(MDbgFrame frame)
        {
            MDbgValue[] arguments = frame.Function.GetArguments(frame);
            StringBuilder hash = new StringBuilder();
            for (int i = 0; i < arguments.Length; i++)
            {
                if (arguments[i].Name == "this") continue;
                hash.Append(dot);
                hash.Append(arguments[i].TypeName);
            }
            return hash.ToString();
        }

        private static TypeInfoCache BuildTypeInfoCacheFromAssembly(string assemblyFile)
        {
            Assembly assembly = Assembly.LoadFile(assemblyFile);
            TypeInfoCache typeInfoCache = new TypeInfoCache();

            foreach (TypeInfo typeInfo in assembly.DefinedTypes)
            {
                string hash = string.Empty;
                string base_hash = $"{typeInfo.Namespace}{dot}{typeInfo.Name}";
                typeInfoCache.FieldInfoCache.Add(base_hash, new List<ValueInfo>());


                foreach (MethodInfo methodInfo in typeInfo.DeclaredMethods)
                {
                    string h_parameters = GetParameterInfoHash(methodInfo.GetParameters());
                    hash = $"{base_hash}{dot}{methodInfo.Name}{h_parameters}";
                    typeInfoCache.MethodInfoCache.Add(hash, methodInfo);
                }

                foreach (MemberInfo memberInfo in typeInfo.DeclaredMembers)
                {
                    hash = $"{base_hash}{dot}{memberInfo.Name}";
                    if (!typeInfoCache.MemberInfoCache.ContainsKey(hash))
                        typeInfoCache.MemberInfoCache.Add(hash, memberInfo);
                }

                foreach (FieldInfo fieldInfo in typeInfo.DeclaredFields)
                {
                    // value is set at compile time and not accessible via debugging api
                    if (fieldInfo.IsLiteral)
                    {
                        ValueInfo valueInfo = new ValueInfo();
                        valueInfo.IsLitteral = true;
                        valueInfo.MetadataToken = fieldInfo.MetadataToken;
                        valueInfo.Name = fieldInfo.Name;
                        valueInfo.TypeName = fieldInfo.FieldType.FullName;
                        valueInfo.IsArrayType = fieldInfo.FieldType.IsArray;
                        valueInfo.IsNull = valueInfo.Value != null ? false : true;
                        // See MDbgValue.IsComplexType property definition
                        valueInfo.IsComplexType = !valueInfo.IsNull && (fieldInfo.FieldType.IsClass || fieldInfo.FieldType.IsValueType);
                        valueInfo.Value = fieldInfo.GetRawConstantValue();
                        
                        typeInfoCache.FieldInfoCache[base_hash].Add(valueInfo);
                    }
                    // value is set at execution time and should be read via debugger
                    else if (fieldInfo.IsStatic) // exclude "<*>k__BackingField"
                    {
                        
                        ValueInfo valueInfo = new ValueInfo();
                        valueInfo.MetadataToken = fieldInfo.MetadataToken;
                        //valueInfo.Address will be set at debugging time
                        valueInfo.Name = fieldInfo.Name;
                        valueInfo.TypeName = fieldInfo.FieldType.FullName;
                        valueInfo.IsArrayType = fieldInfo.FieldType.IsArray;
                        // See MDbgValue.IsComplexType property definition
                        //valueInfo.IsComplexType = fieldInfo.FieldType.IsClass || fieldInfo.FieldType.IsValueType;
                        //valueInfo.Value
                        //valueInfo.IsNull = valueInfo.Value != null ? false : true;
                        typeInfoCache.FieldInfoCache[base_hash].Add(valueInfo);
                    }
                }

                foreach (EventInfo eventInfo in typeInfo.DeclaredEvents)
                {
                    hash = $"{base_hash}{dot}{eventInfo.Name}";
                    typeInfoCache.EventInfoCache.Add(hash, eventInfo);
                }

                foreach (ConstructorInfo constructorInfo in typeInfo.DeclaredConstructors)
                {
                    string h_parameters = GetParameterInfoHash(constructorInfo.GetParameters());
                    hash = $"{base_hash}{dot}{constructorInfo.Name}{h_parameters}";
                    typeInfoCache.ConstructorInfoCache.Add(hash, constructorInfo);
                }

                foreach (PropertyInfo propertyInfo in typeInfo.DeclaredProperties)
                {
                    hash = $"{base_hash}{dot}{propertyInfo.Name}";
                    typeInfoCache.PropertyInfoCache.Add(hash, propertyInfo);
                }
            }

            return typeInfoCache;
        }

        private async Task<TypeInfoCache> BuildTypeInfoCacheFromAssemblyAsync(string assemblyFile)
        {
            return await Task.Run(() => BuildTypeInfoCacheFromAssembly(assemblyFile));
        }

        private void LoadAssemblyTree(string assemblyPath, List<Task<TypeInfoCache>> assemblyTasks)
        {
            try
            {
                assemblyPath = Path.GetDirectoryName(assemblyPath);
                Console.WriteLine(assemblyPath);

                foreach (string f in Directory.GetFiles(assemblyPath))
                {
                    if (Common.EndsWithAny(f, extensions))
                    {
                        Console.WriteLine(f);
                        Task<TypeInfoCache> sourceFileTask = BuildTypeInfoCacheFromAssemblyAsync(f);
                        assemblyTasks.Add(sourceFileTask);
                    }
                }

                foreach (string d in Directory.GetDirectories(assemblyPath))
                    LoadAssemblyTree(d, assemblyTasks);
            }
            catch (Exception excpt)
            {
                Console.WriteLine(excpt.Message);
            }
        }

        public void BuildAssemblyCache(string assemblyPath)
        {
            List<Task<TypeInfoCache>> assemblyTasks = new List<Task<TypeInfoCache>>();
            LoadAssemblyTree(assemblyPath, assemblyTasks);
            Task<TypeInfoCache>[] tasks = new Task<TypeInfoCache>[assemblyTasks.Count];
            assemblyTasks.CopyTo(tasks, 0);
            Task.WaitAll(tasks);
            foreach (Task<TypeInfoCache> task in assemblyTasks)
            {
                TypeInfoCache typeInfoCache = task.Result;
                assemblyCache.Concat(typeInfoCache);
            }
        }

        private MethodDesc GetMethodDesc(MethodInfo methodInfo, MDbgValue[] args)
        {
            MethodDesc method = new MethodDesc();
            method.AssemblyName = methodInfo.DeclaringType.Assembly.FullName;
            method.NameSpace = methodInfo.DeclaringType.Namespace;
            method.ClassName = methodInfo.DeclaringType.Name;
            method.Name = methodInfo.Name;
            method.Signature = methodInfo.ToString();
            method.Attributes = methodInfo.Attributes.ToString();
            method.ReturnTypeName = methodInfo.ReturnType.FullName;
            // For later retrieval
            method.SetMethodArguments(args);
            return method;
        }

        private MethodDesc GetMethodDesc(ConstructorInfo methodInfo, MDbgValue[] args)
        {
            MethodDesc method = new MethodDesc();
            method.AssemblyName = methodInfo.DeclaringType.Assembly.FullName;
            method.NameSpace = methodInfo.DeclaringType.Namespace;
            method.ClassName = methodInfo.DeclaringType.Name;
            method.Name = methodInfo.Name;
            method.Signature = methodInfo.ToString();
            method.Attributes = methodInfo.Attributes.ToString();
            method.ReturnTypeName = "System.Void";
            // For later retrieval
            method.SetMethodArguments(args);
            return method;
        }

        public MethodDesc GetMethodDesc(MDbgFrame frame)
        {
            long methodHash = long.Parse($"{frame.Function.Module.Number}{frame.Function.CorFunction.Token}");
            if (!methods.ContainsKey(methodHash))
            {
                StringBuilder sbhash = new StringBuilder(frame.Function.FullName);
                sbhash.Append(GetParameterInfoHash(frame));
                string hash = sbhash.ToString();

                MethodDesc md = null;
                if (assemblyCache.MethodInfoCache.ContainsKey(hash))
                    md = GetMethodDesc(assemblyCache.MethodInfoCache[hash], frame.Function.GetArguments(frame));
                else if (assemblyCache.ConstructorInfoCache.ContainsKey(hash))
                    md = GetMethodDesc(assemblyCache.ConstructorInfoCache[hash], frame.Function.GetArguments(frame));

                methods.Add(methodHash, md);
            }
            return methods[methodHash];
        }

        public async Task<MethodDesc> GetMethodDescAsync(MDbgFrame frame)
        {
            return await Task.Run(() => GetMethodDesc(frame));
        }

        public ValueInfo[] GetConstants(string classNameWithNameSpace)
        {
            if (assemblyCache.FieldInfoCache.ContainsKey(classNameWithNameSpace))
                return assemblyCache.FieldInfoCache[classNameWithNameSpace].ToArray();

            return null;
        }

        private static readonly string[] extensions = new string[] { "exe", "dll" };
    }
}
