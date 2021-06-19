using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

using Microsoft.Samples.Debugging.MdbgEngine;
using Microsoft.Samples.Tools.Mdbg;

namespace FrameExporter.Utils
{
    public class AssemblyMgr
    {
        private static TypeInfoCache BuildTypeInfoCacheFromAssembly(string assemblyFile)
        {
            Assembly assembly = Assembly.LoadFile(assemblyFile);
            TypeInfoCache typeInfoCache = new TypeInfoCache();

            foreach (TypeInfo typeInfo in assembly.DefinedTypes)
            {
                string hash = string.Empty;
                string base_hash = $"{typeInfo.Namespace}{DOT}{typeInfo.Name}";

                typeInfoCache.LitteralCache.Add(base_hash, new List<ValueInfo>());
                typeInfoCache.StaticCache.Add(base_hash, new List<ValueInfo>());


                foreach (MethodInfo methodInfo in typeInfo.DeclaredMethods)
                {
                    typeInfoCache.MethodInfoCacheToken.Add($"{methodInfo.DeclaringType.FullName}{methodInfo.MetadataToken}", methodInfo);
                    typeInfoCache.functionTokensPerModule.AddToken(methodInfo.Module.FullyQualifiedName, methodInfo.MetadataToken);
                }

                foreach (MemberInfo memberInfo in typeInfo.DeclaredMembers)
                {
                    hash = $"{base_hash}{DOT}{memberInfo.Name}";
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

                        typeInfoCache.LitteralCache[base_hash].Add(valueInfo);
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
                        valueInfo.IsStatic = true;

                        typeInfoCache.StaticCache[base_hash].Add(valueInfo);
                    }
                }

                foreach (EventInfo eventInfo in typeInfo.DeclaredEvents)
                {
                    hash = $"{base_hash}{DOT}{eventInfo.Name}";
                    typeInfoCache.EventInfoCache.Add(hash, eventInfo);
                }

                foreach (ConstructorInfo constructorInfo in typeInfo.DeclaredConstructors)
                {
                    typeInfoCache.ConstructorInfoCacheToken.Add($"{constructorInfo.DeclaringType.FullName}{constructorInfo.MetadataToken}", constructorInfo);
                    typeInfoCache.functionTokensPerModule.AddToken(constructorInfo.Module.FullyQualifiedName, constructorInfo.MetadataToken);
                }

                foreach (PropertyInfo propertyInfo in typeInfo.DeclaredProperties)
                {
                    hash = $"{base_hash}{DOT}{propertyInfo.Name}";
                    typeInfoCache.PropertyInfoCache.Add(hash, propertyInfo);
                }
            }

            return typeInfoCache;
        }

        private static async Task<TypeInfoCache> BuildTypeInfoCacheFromAssemblyAsync(string assemblyFile)
        {
            return await Task.Run(() => BuildTypeInfoCacheFromAssembly(assemblyFile));
        }

        private static void LoadAssemblyTree(string assemblyPath, List<Task<TypeInfoCache>> assemblyTasks)
        {
            Console.WriteLine("Build assembly cache");
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
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }

        /// <summary>
        /// Build assembly cache.
        /// This method should be called before all other methods of AssemblyMgr
        /// </summary>
        /// <param name="assemblyPath"></param>
        public void BuildAssemblyCache(string assemblyPath)
        {
            List<Task<TypeInfoCache>> assemblyTasks = new List<Task<TypeInfoCache>>();
            LoadAssemblyTree(assemblyPath, assemblyTasks);
            Task<TypeInfoCache>[] tasks = assemblyTasks.ToArray();//new Task<TypeInfoCache>[assemblyTasks.Count];
            //assemblyTasks.CopyTo(tasks, 0);
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
            method.Token = methodInfo.MetadataToken;

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

        private MethodDesc GetConstructorDesc(ConstructorInfo methodInfo, MDbgValue[] args)
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

        /// <summary>
        /// Extract MethodDesc from current frame
        /// </summary>
        /// <param name="frame"></param>
        /// <returns></returns>
        public MethodDesc GetMethodDesc(MDbgFrame frame)
        {
            // Method Token is not unique across modules
            long methodHash = long.Parse($"{frame.Function.Module.Number}{frame.Function.CorFunction.Token}");
            if (!methodsCache.ContainsKey(methodHash))
            {
                string hash = $"{frame.Function.MethodInfo.DeclaringType.FullName}{frame.Function.MethodInfo.MetadataToken}";

                MethodDesc md = null;
                if (assemblyCache.MethodInfoCacheToken.ContainsKey(hash))
                    md = GetMethodDesc(assemblyCache.MethodInfoCacheToken[hash], frame.Function.GetArguments(frame));
                else if (assemblyCache.ConstructorInfoCacheToken.ContainsKey(hash))
                    md = GetConstructorDesc(assemblyCache.ConstructorInfoCacheToken[hash], frame.Function.GetArguments(frame));

                methodsCache.Add(methodHash, md);
            }
            return methodsCache[methodHash];
        }

        /// <summary>
        /// Async version of GetMethodDesc
        /// </summary>
        /// <param name="frame"></param>
        /// <returns></returns>
        public async Task<MethodDesc> GetMethodDescAsync(MDbgFrame frame)
        {
            return await Task.Run(() => GetMethodDesc(frame));
        }

        /// <summary>
        /// Returns ValueInfo array of static variables. Value field is populated
        /// </summary>
        /// <param name="classNameWithNameSpace">Where to search constants</param>
        /// <returns></returns>
        public ValueInfo[] GetLitterals(string classNameWithNameSpace)
        {
            if (assemblyCache.LitteralCache.ContainsKey(classNameWithNameSpace))
                return assemblyCache.LitteralCache[classNameWithNameSpace].ToArray();

            return null;
        }

        /// <summary>
        /// Returns ValueInfo array of static variables. Value field needs to be populated
        /// </summary>
        /// <param name="classNameWithNameSpace">Where to search static variables</param>
        /// <returns></returns>
        public ValueInfo[] GetStaticVariables(string classNameWithNameSpace)
        {
            if (assemblyCache.StaticCache.ContainsKey(classNameWithNameSpace))
                return assemblyCache.StaticCache[classNameWithNameSpace].ToArray();

            return null;
        }

        public FunctionTokensPerModule FunctionTokensPerModule
        {
            get {
                return assemblyCache.functionTokensPerModule;
            }
        }

        /// <summary>
        /// Stores MethodDesc in a dictionary. Key is made with Module ID concatenated with methode Token
        /// Used as TypeInfoCache shortcut in GetMethodDesc()
        /// </summary>
        private readonly Dictionary<long, MethodDesc> methodsCache = new Dictionary<long, MethodDesc>();

        /// <summary>
        /// AssemblyMgr main cache definition. Stores all assembly objects in Dictionary<string, object>
        /// </summary>
        private class TypeInfoCache
        {
            public Dictionary<string, MethodInfo> MethodInfoCacheToken = new Dictionary<string, MethodInfo>();
            public Dictionary<string, MemberInfo> MemberInfoCache = new Dictionary<string, MemberInfo>();
            public Dictionary<string, List<ValueInfo>> LitteralCache = new Dictionary<string, List<ValueInfo>>();
            public Dictionary<string, List<ValueInfo>> StaticCache = new Dictionary<string, List<ValueInfo>>();
            public Dictionary<string, EventInfo> EventInfoCache = new Dictionary<string, EventInfo>();
            public Dictionary<string, ConstructorInfo> ConstructorInfoCacheToken = new Dictionary<string, ConstructorInfo>();
            public Dictionary<string, PropertyInfo> PropertyInfoCache = new Dictionary<string, PropertyInfo>();
            public FunctionTokensPerModule functionTokensPerModule = new FunctionTokensPerModule();

            public void Concat(TypeInfoCache typeInfoCache)
            {
                MethodInfoCacheToken = MethodInfoCacheToken.Concat(typeInfoCache.MethodInfoCacheToken).ToDictionary(x => x.Key, x => x.Value);
                MemberInfoCache = MemberInfoCache.Concat(typeInfoCache.MemberInfoCache).ToDictionary(x => x.Key, x => x.Value);
                LitteralCache = LitteralCache.Concat(typeInfoCache.LitteralCache).ToDictionary(x => x.Key, x => x.Value);
                StaticCache = StaticCache.Concat(typeInfoCache.StaticCache).ToDictionary(x => x.Key, x => x.Value);
                EventInfoCache = EventInfoCache.Concat(typeInfoCache.EventInfoCache).ToDictionary(x => x.Key, x => x.Value);
                ConstructorInfoCacheToken = ConstructorInfoCacheToken.Concat(typeInfoCache.ConstructorInfoCacheToken).ToDictionary(x => x.Key, x => x.Value);
                PropertyInfoCache = PropertyInfoCache.Concat(typeInfoCache.PropertyInfoCache).ToDictionary(x => x.Key, x => x.Value);
                functionTokensPerModule = functionTokensPerModule.Concat(typeInfoCache.functionTokensPerModule);
            }
        }
        private readonly TypeInfoCache assemblyCache = new TypeInfoCache();

        /// <summary>
        /// Assembly file extensions
        /// </summary>
        private static readonly string[] extensions = new string[] { "exe", "dll" };

        private const string DOT = ".";
    }

    public class FunctionTokensPerModule
    {
        private Dictionary<string, List<int>> _functionTokensPerModule = new Dictionary<string, List<int>>();

        public void AddToken(string moduleFullyQualifiedName, int functionToken)
        {
            if (!_functionTokensPerModule.ContainsKey(moduleFullyQualifiedName))
                _functionTokensPerModule.Add(moduleFullyQualifiedName, new List<int>());
            _functionTokensPerModule[moduleFullyQualifiedName].Add(functionToken);
        }

        public List<int> GetModuleFunctionTokens(string moduleFullyQualifiedName)
        {
            if (!_functionTokensPerModule.ContainsKey(moduleFullyQualifiedName))
                return new List<int>();
            return _functionTokensPerModule[moduleFullyQualifiedName];
        }

        public FunctionTokensPerModule Concat(FunctionTokensPerModule functionTokensPerModule)
        {
            _functionTokensPerModule = _functionTokensPerModule.Concat(functionTokensPerModule._functionTokensPerModule).ToDictionary(x => x.Key, x => x.Value);
            return this;
        }

        public bool ContainsModule(string moduleFullyQualifiedName)
        {
            return _functionTokensPerModule.ContainsKey(moduleFullyQualifiedName);
        }
    }

}
