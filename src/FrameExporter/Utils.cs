using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using FrameExporter.ValuePlugin;
using Microsoft.Samples.Debugging.CorDebug;
using Microsoft.Samples.Debugging.CorDebug.NativeApi;
using Microsoft.Samples.Debugging.MdbgEngine;

namespace FrameExporter
{
    public static class MethodDescUtils
    {
        private static char[] nameSeparator = new char[] { '.' };

        private static void SplitMethodFullName(string methodFullName, out string AssemblyName, out string Namespace, out string ClassName, out string MethodName)
        {
            AssemblyName = null;
            Namespace = null;
            ClassName = null;
            MethodName = null;

            string[] elements = methodFullName.Split(nameSeparator);

            int max = elements.Length;
            for (int i = 0; i < max; i++)
            {
                if (i <= max - 3)
                {
                    AssemblyName += AssemblyName == null ? elements[i] : nameSeparator + elements[i];
                    Namespace = AssemblyName;
                }
                else if (i < max - 2) Namespace = Namespace + nameSeparator.ToString() + elements[i];
                else if (i == max - 1) MethodName = elements[i];
                else ClassName = elements[i];
            }
        }

        private static MethodInfo GetMethodInfoFromAssemblies(List<Assembly> assemblies, string AssemblyName,  string Namespace, string ClassName, string MethodName, Dictionary<string, string> ParametersInfo)
        {
            foreach (Assembly assembly in assemblies)
            {
                if (assembly.FullName.StartsWith(AssemblyName))
                {
                    foreach(TypeInfo type in assembly.DefinedTypes)
                    {
                        if (type.Namespace.Equals(Namespace) && type.Name.Equals(ClassName))
                        {
                            foreach(MethodInfo info in type.DeclaredMethods)
                            {
                                if (info.Name.Equals(MethodName) && (info.IsSpecialName || IsParameterInfoArrayEquivalent(info.GetParameters(), ParametersInfo)))
                                {
                                    return info;
                                }
                            }
                            /*
                            foreach (MethodInfo info in type.DeclaredConstructors)
                            {
                                if (info.Name.Equals(MethodName) && (info.IsSpecialName || IsParameterInfoArrayEquivalent(info.GetParameters(), ParametersInfo)))
                                {
                                    return info;
                                }
                            }
                            */
                        }
                    }
                }
            }

            return null;
        }

        private static Dictionary<string, string> MDbgValueArrayToParameterNameTypeNameDictionary(MDbgValue[] mDbgValues)
        {
            Dictionary<string, string> parameters = new Dictionary<string, string>();
            foreach (MDbgValue mDbgValue in mDbgValues)
            {
                parameters.Add(mDbgValue.Name, mDbgValue.TypeName);
            }
            return parameters;
        }

        private static bool IsParameterInfoArrayEquivalent(ParameterInfo[] parameters, Dictionary<string, string> NameTypeDic)
        {
            if (parameters.Length != NameTypeDic.Count) return false;

            // comparing in reverse order
            bool same = true;
            for (int i = parameters.Length - 1; i >= 0 && same; i--)
                same = NameTypeDic.ContainsKey(parameters[i].Name) && parameters[i].ParameterType.FullName.Equals(NameTypeDic[parameters[i].Name]);

            return same;
        }

        private static void CompleteMethodDesc(List<Assembly> assemblies, string fullname, MDbgValue[] arguments, MethodDesc method)
        {
            SplitMethodFullName(fullname, out string AssemblyName, out string Namespace, out string ClassName, out string MethodName);
            method.AssemblyName = AssemblyName;
            method.NameSpace = Namespace;
            method.ClassName = ClassName;
            method.Name = MethodName;
            Dictionary<string, string> ParametersInfo = MDbgValueArrayToParameterNameTypeNameDictionary(arguments);
            MethodInfo methodInfo = GetMethodInfoFromAssemblies(assemblies, AssemblyName, Namespace, ClassName, MethodName, ParametersInfo);
            method.Signature = methodInfo.ToString();
            method.Attributes = methodInfo.Attributes.ToString();
            method.ReturnTypeName = methodInfo.ReturnType.FullName;
        }

        private static async Task CompleteMethodDescAsync(List<Assembly> assemblies, string fullname, MDbgValue[] arguments, MethodDesc method)
        {
            await Task.Run(() => CompleteMethodDesc(assemblies, fullname, arguments, method));
        }

        public static MethodDesc GetMethodDescInstance(MDbgFrame frame, List<Assembly> assemblies)
        {
            MethodDesc method = new MethodDesc();
            method.Token = frame.Function.CorFunction.Token;
            method.FullName = frame.Function.FullName;

            MDbgValue[] arguments = frame.Function.GetArguments(frame);

            // Parameters Values
            Task<ValueInfo[]> argumentTasks = ValueInfoUtils.MDbgValuesToValueInfoArrayAsync(arguments);

            // MethodInfo
            Task completeTask = CompleteMethodDescAsync(assemblies, method.FullName, arguments, method);

            Task.WaitAll(new Task[] { completeTask, argumentTasks });

            method.Arguments = argumentTasks.Result;

            return method;
        }


    }

    public static class ValueInfoUtils
    {
        static void PopulateValueInfoStdProperties(ValueInfo valueInfo, MDbgValue mDbgValue)
        {
            valueInfo.IsArrayType = mDbgValue.IsArrayType;
            valueInfo.IsComplexType = mDbgValue.IsComplexType;
            valueInfo.IsNull = mDbgValue.IsNull;
            valueInfo.Name = mDbgValue.Name;
            valueInfo.TypeName = mDbgValue.TypeName;
            valueInfo.Address = mDbgValue.CorValue.Address;
        }

        static async Task PopulateValueInfoStdPropertiesAsync(ValueInfo valueInfo, MDbgValue mDbgValue)
        {
            await Task.Run(() => PopulateValueInfoStdProperties(valueInfo, mDbgValue));
        }

        public static ValueInfo MDbgValueExceptionToValueInfo(MDbgValue mDbgValue)
        {
            if (mDbgValue.IsNull) return null;

            ValueInfo value = new ValueInfo();
            Task populateTask = PopulateValueInfoStdPropertiesAsync(value, mDbgValue);
            Task<Dictionary<string, object>> valueTask = ExceptionValues.GetValueAsync(mDbgValue);
            Task.WaitAll(new Task[] { populateTask, valueTask });
            value.Value = valueTask.Result;
            return value;
        }

        public static async Task<ValueInfo> MDbgValueExceptionToValueInfoAsync(MDbgValue mDbgValue)
        {
            return await Task.Run(() => MDbgValueExceptionToValueInfo(mDbgValue));
        }

        public static ValueInfo MDbgValueToValueInfo(MDbgValue mDbgValue)
        {
            ValueInfo value = new ValueInfo();
            Task populateTask = PopulateValueInfoStdPropertiesAsync(value, mDbgValue);
            Task<object> valueTask = GetValueAsync(mDbgValue);
            Task.WaitAll(new Task[] { populateTask, valueTask });
            value.Value = valueTask.Result;
            return value;
        }

        public static async Task<ValueInfo> MDbgValueToValueInfoAsync(MDbgValue mDbgValue)
        {  
            return await Task.Run(() => MDbgValueToValueInfo(mDbgValue));
        }

        public static ValueInfo[] MDbgValuesToValueInfoArray(MDbgValue[] mDbgValues)
        {
            ValueInfo[] values = new ValueInfo[mDbgValues.Length];
            Task<ValueInfo>[] tasks = new Task<ValueInfo>[mDbgValues.Length];

            for (int i = 0; i < mDbgValues.Length; i++)
                tasks[i] = MDbgValueToValueInfoAsync(mDbgValues[i]);

            Task.WaitAll(tasks);

            for (int i = 0; i < mDbgValues.Length; i++)
                values[i] = tasks[i].Result;
            
            return values;
        }

        public static async Task<ValueInfo[]> MDbgValuesToValueInfoArrayAsync(MDbgValue[] mDbgValues)
        {
            return await Task.Run(() => MDbgValuesToValueInfoArray(mDbgValues));
        }

        public static bool IsCircularReference(MDbgValue value)
        {
            if (value.Parent == null) return false;
            MDbgValue parent = value.Parent;
            bool circular = false;
            do
            {
                if (parent.CorValue.Address == value.CorValue.Address)
                {
                    circular = true;
                }
                else parent = parent.Parent;
            }
            while (parent != null && !circular);
            return circular;
        }

        public static bool IsCircularReference(MDbgValue value, out string name)
        {
            name = null;
            if (value.Parent == null) return false;
            MDbgValue parent = value.Parent;
            bool circular = false;
            do
            {
                if (parent.CorValue.Address == value.CorValue.Address)
                {
                    circular = true;
                    name = parent.Name;
                }
                else parent = parent.Parent;
            }
            while (parent != null && !circular);
            return circular;
        }

        public static object[] GetArrayValue(MDbgValue value)
        {
            object[] retArray = null;
            CorReferenceValue referenceValue = value.CorValue.CastToReferenceValue();
            if (referenceValue != null)
            {
                CorArrayValue arrayValue = ((CorValue)referenceValue.Dereference()).CastToArrayValue();
                if (arrayValue != null)
                {
                    retArray = new object[arrayValue.Count];
                    for (int i = 0; i < arrayValue.Count; i++)
                    {
                        MDbgValue elementValue = new MDbgValue(value.Process, value.Name, arrayValue.GetElementAtPosition(i), value.Parent);
                        ((object[])retArray)[i] = GetValue(elementValue);
                    }
                    return retArray;
                }
            }
            return retArray;
        }

        public static object GetGenericValue(CorValue value)
        {
            object retObject = null;
            CorGenericValue lsValGen = value.CastToGenericValue();
            if (lsValGen != null)
            {
                retObject = lsValGen.GetValue();
                return retObject;
            }
            return retObject;
        }

        public static async Task<object> GetGenericValueAsync(CorValue corValue)
        {
            return await Task.Run(() => GetGenericValue(corValue));
        }

        public static Dictionary<string, object> GetClassValue(MDbgValue dbgValue)
        {
            Dictionary<string, object> values = new Dictionary<string, object>();

            MDbgValue[] dbgValues = dbgValue.GetFields();
            for (int i = 0; i < dbgValues.Length; i++)
            {
                // we skip "this"
                if (dbgValue.Name != "this")
                {
                    string name = dbgValues[i].Name;
                    // rework property field name
                    if (name.EndsWith("BackingField"))
                    {
                        int indexlt = name.IndexOf('<') + 1;
                        int indexgt = name.IndexOf('>');
                        name = name.Substring(indexlt, indexgt - indexlt);
                    }
                    values.Add(name, null);
                    try
                    {
                        if (!dbgValues[i].IsNull)
                        {
                            values[name] = GetValue(dbgValues[i]);
                        }
                        else values[name] = null;
                    }
                    catch (Exception e)
                    {
                        values[name] = e;
                    }
                }
            }
            return values;
        }

        public static string GetStringValue(CorValue corValue)
        {
            string retStr = null;
            try
            {
                CorReferenceValue lsValRef = corValue.CastToReferenceValue();
                if (lsValRef != null)
                {
                    CorValue value = lsValRef.Dereference();
                    CorStringValue corStringValue = value.CastToStringValue();
                    retStr = corStringValue.String;
                }
            }
            catch
            {
                try
                {
                    CorStringValue sv = corValue.CastToStringValue();
                    if (sv != null)
                    {
                        retStr = sv.String;
                    }
                }
                catch { }
            }
            return retStr;
        }

        public static async Task<string> GetStringValueAsync(CorValue corValue)
        {
            return await Task.Run(() => GetStringValue(corValue));
        }

        /// <summary>
        /// Tries to convert CorValue to the right object.
        /// </summary>
        /// <param name="value">CorValue to convert</param>
        /// <returns>Converted object</returns>
        static object CorValueToObject(MDbgValue value)
        {
            switch (value.CorValue.Type)
            {
                case CorElementType.ELEMENT_TYPE_PINNED:
                case CorElementType.ELEMENT_TYPE_SENTINEL:
                case CorElementType.ELEMENT_TYPE_MODIFIER:
                    break;

                case CorElementType.ELEMENT_TYPE_MAX:
                    break;

                case CorElementType.ELEMENT_TYPE_INTERNAL:
                    break;

                case CorElementType.ELEMENT_TYPE_CMOD_OPT:
                case CorElementType.ELEMENT_TYPE_CMOD_REQD:
                    break;

                case CorElementType.ELEMENT_TYPE_MVAR:
                    break;
                case CorElementType.ELEMENT_TYPE_SZARRAY:
                    return GetArrayValue(value);
                case CorElementType.ELEMENT_TYPE_OBJECT:
                    try
                    {
                        CorReferenceValue corReferenceValue = value.CorValue.CastToReferenceValue();
                        if (corReferenceValue != null)
                        {
                            if (!corReferenceValue.IsNull)
                            {
                                CorObjectValue ov = value.CorValue.CastToObjectValue();
                                if (ov != null)
                                {
                                    return GetValue(new MDbgValue(value.Process, value.Name, ov, value.Parent));
                                }
                            }
                        }
                        return null;
                    }
                    catch { }
                    break;
                case CorElementType.ELEMENT_TYPE_FNPTR:
                case CorElementType.ELEMENT_TYPE_U:
                case CorElementType.ELEMENT_TYPE_I:
                    break;

                case CorElementType.ELEMENT_TYPE_TYPEDBYREF:
                case CorElementType.ELEMENT_TYPE_GENERICINST:
                case CorElementType.ELEMENT_TYPE_ARRAY:
                case CorElementType.ELEMENT_TYPE_VAR:
                case CorElementType.ELEMENT_TYPE_CLASS:
                    // do we know a fast way to get data ?
                    if (value.TypeName.StartsWith("System.Collections.Generic.LinkedList`1"))
                    {
                        return LinkedListValues.GetValue(value);
                    }
                    if (value.TypeName.StartsWith("System.Collections.Generic.List`1"))
                    {
                        return ListValues.GetValue(value);
                    }
                    return GetClassValue(value);

                case CorElementType.ELEMENT_TYPE_VALUETYPE:
                    break;

                case CorElementType.ELEMENT_TYPE_BYREF:
                case CorElementType.ELEMENT_TYPE_PTR:
                    break;

                case CorElementType.ELEMENT_TYPE_STRING:
                    return GetStringValue(value.CorValue);
                case CorElementType.ELEMENT_TYPE_R8:
                case CorElementType.ELEMENT_TYPE_R4:
                case CorElementType.ELEMENT_TYPE_U8:
                case CorElementType.ELEMENT_TYPE_I8:
                case CorElementType.ELEMENT_TYPE_U4:
                case CorElementType.ELEMENT_TYPE_I4:
                case CorElementType.ELEMENT_TYPE_U2:
                case CorElementType.ELEMENT_TYPE_I2:
                case CorElementType.ELEMENT_TYPE_U1:
                case CorElementType.ELEMENT_TYPE_I1:
                case CorElementType.ELEMENT_TYPE_CHAR:
                case CorElementType.ELEMENT_TYPE_BOOLEAN:
                    return GetGenericValue(value.CorValue);

                case CorElementType.ELEMENT_TYPE_VOID:
                case CorElementType.ELEMENT_TYPE_END:
                    return null;
            }
            return null;
        }

        public static object GetValue(MDbgValue value)
        {
            object retObject = null;
            try
            {
                if (value == null) throw new ArgumentNullException("value is null");
                if (value.IsNull) return null;
                if (IsCircularReference(value, out string name)) return $"Is the same value as {name}";

                retObject = CorValueToObject(value);
                if (retObject != null) return retObject;
                else
                { 
                    //TODO remove this part after more experimentation
                    CorValue corValue = value.CorValue;
                    CorArrayValue av = corValue.CastToArrayValue();
                    if (av != null)
                    {
                        retObject = GetArrayValue(value);
                        return retObject;
                    }

                    try
                    {
                        CorGenericValue lsValGen = corValue.CastToGenericValue();
                        if (lsValGen != null)
                        {
                            retObject = lsValGen.GetValue();
                            return retObject;
                        }
                    }
                    catch { }
                    /*
                    CorHeapValue hv = corValue.CastToHeapValue();
                    if (hv != null)
                    {
                        //return;
                    }

                    CorBoxValue bv = corValue.CastToBoxValue();
                    if (bv != null)
                    {
                        //return;
                    }


                    */

                    /*
                    CorHandleValue handv = corValue.CastToHandleValue();
                    if (handv != null)
                    {
                        return;
                    }*/

                    CorReferenceValue lsValRef = corValue.CastToReferenceValue();
                    if (lsValRef != null)
                    {
                        MDbgValue referenceValue = new MDbgValue(value.Process, value.Name, lsValRef.Dereference(), value.Parent);
                        return GetValue(referenceValue);
                    }

                }
            }
            catch (ArgumentNullException e)
            {
                retObject = e;
            }
            catch (MDbgValueWrongTypeException e)
            {
                retObject = e;
            }
            return retObject;
        }

        static async Task<object> GetValueAsync(MDbgValue value)
        {
            return await Task.Run(() => GetValue(value));
        }
    }
}
