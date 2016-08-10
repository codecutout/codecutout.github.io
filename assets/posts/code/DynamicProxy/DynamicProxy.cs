using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;

namespace Proxy
{
    public static class DynamicProxy
    {
        private const string AssemblyName = "ProxyAssembly";
        private const string ModuleName = "ProxyModule";
        private const string TypeSuffix = "Proxy";

        private static ConcurrentDictionary<Type, Type> _dynamicTypeCache = new ConcurrentDictionary<Type, Type>();

        /// <summary>
        /// Creates a new proxy
        /// </summary>
        /// <typeparam name="I">Interface to implement</typeparam>
        /// <param name="implementor">function to get called when interface methods are called</param>
        /// <returns>Proxy object that impelments the interface</returns>
        public static I New<I>(Func<MethodInfo, object[], object> implementor)
        {
            return (I)New(typeof(I), implementor);
        }


        public static object New(Type interfaceType, Func<MethodInfo, object[], object> implementor)
        {
            Type proxyType = _dynamicTypeCache.GetOrAdd(interfaceType, (t) => CreateProxy(interfaceType));
             return Activator.CreateInstance(proxyType, implementor);
        }

        /// <summary>
        /// Gets all the methods, including inherited methods
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        private static IEnumerable<MethodInfo> GetAllInterfaceMethods(this Type type)
        {
            foreach (MethodInfo info in type.GetMethods())
                yield return info;

            foreach (Type interfaceType in type.GetInterfaces())
                foreach (MethodInfo info in interfaceType.GetMethods())
                    yield return info;
        }

        private static Type CreateProxy(Type interfaceType)
        {
           

            //create the dynamic assembly
            ModuleBuilder moduleBuilder = CreateAssembly(AssemblyName, ModuleName);

            //create our new type that implements the proxy interface
            TypeBuilder typeBuilder;
            IEnumerable<MethodInfo> methodsToImplement;
            if (interfaceType.IsInterface)
            {
                typeBuilder = moduleBuilder.CreateType(interfaceType.Name + TypeSuffix, interfaceType, typeof(object));
                methodsToImplement = interfaceType.GetAllInterfaceMethods();
            }
            else if (interfaceType.IsAbstract)
            {
                typeBuilder = moduleBuilder.CreateType(interfaceType.Name + TypeSuffix, null, interfaceType);
                methodsToImplement = interfaceType.GetMethods().Where(m => m.IsAbstract);
            }
            else
            {
                throw new ArgumentException("Type specified must be an interface or abstract type");
            }

             //create two fields, one to store the proxied object, and one to store the Interceptor delegate
            //this will also create a constructor to set both these values
            FieldInfo[] fieldInfo = typeBuilder.CreateFieldsAndConstrucutor(typeof(Func<MethodInfo, object[], object>), "Implementor");



            //Create an implementation of each method in the interface.
            foreach (MethodInfo interfaceMethodInfo in methodsToImplement)
            {
                //get a list of parameter types
                ParameterInfo[] parameterInfos = interfaceMethodInfo.GetParameters();
                Type[] parameterTypes = new Type[parameterInfos.Length];
                for (int i = 0; i < parameterInfos.Length; i++)
                    parameterTypes[i] = parameterInfos[i].ParameterType;


                typeBuilder.CreateProxyMethod(interfaceMethodInfo, fieldInfo[0]);

            }

            return typeBuilder.CreateType();
        }

        /// <summary>
        /// Creates a dynamic assembly and module with the given assembly name and module name
        /// </summary>
        /// <param name="assemblyName"></param>
        /// <param name="moduleName"></param>
        /// <returns></returns>
        private static ModuleBuilder CreateAssembly(string assemblyName, string moduleName)
        {
            AppDomain domain = Thread.GetDomain();
            AssemblyName assembly = new AssemblyName();
            assembly.Name = assemblyName;
            assembly.Version = new Version(0, 0, 0, 0);

            // create a new assembly
            AssemblyBuilder assemblyBuilder = domain.DefineDynamicAssembly(assembly, AssemblyBuilderAccess.Run);

            // create a new module for this proxy
            return assemblyBuilder.DefineDynamicModule(moduleName);
        }


        /// <summary>
        /// Creates a type
        /// </summary>
        /// <typeparam name="I">interface to implement</typeparam>
        /// <typeparam name="B">base type for new class</typeparam>
        /// <param name="moduleBuilder">module builder to use to construct type</param>
        /// <param name="typeName">name of type</param>
        /// <returns></returns>
        private static TypeBuilder CreateType(this ModuleBuilder moduleBuilder, string typeName, Type interfaceToImplement, Type baseType)
        {
            TypeAttributes typeAttributes = TypeAttributes.Class | TypeAttributes.Public | TypeAttributes.Sealed;

            TypeBuilder typeBuilder = moduleBuilder.DefineType(typeName, typeAttributes, baseType, interfaceToImplement == null ? Type.EmptyTypes : new Type[] { interfaceToImplement });
            return typeBuilder;
        }


        /// <summary>
        /// Creates the fields and constructor to set the fields
        /// </summary>
        /// <param name="typeBuilder">type builder to use to construct fields and constructor</param>
        /// <param name="fieldTypeAndName">alternating list of type, name of fields</param>
        private static FieldInfo[] CreateFieldsAndConstrucutor(this TypeBuilder typeBuilder, params object[] fieldTypeAndName)
        {
            Type[] types = new Type[fieldTypeAndName.Length / 2];
            string[] names = new string[fieldTypeAndName.Length / 2];

            //Define the types, even items are types odd items are fields
            for (int i = 0; i < types.Length; i++)
            {
                types[i] = (Type)fieldTypeAndName[i * 2];
                names[i] = (string)fieldTypeAndName[i * 2 + 1];
            }

            //create all the fields. We create them as public, as they will have to reflect
            //to see them anyway, and if they are going through that much effort i dont see
            //any reason to make it harder to access them
            FieldInfo[] fieldInfos = new FieldInfo[types.Length];
            for (int i = 0; i < types.Length; i++)
                fieldInfos[i] = typeBuilder.DefineField(names[i], types[i], FieldAttributes.Public);

            ConstructorInfo superConstructor = typeof(object).GetConstructor(Type.EmptyTypes);
            ConstructorBuilder fieldPopulateConstructor = typeBuilder.DefineConstructor(
                    MethodAttributes.Public, CallingConventions.Standard, types);

            #region( "Constructor IL Code" )
            ILGenerator constructorIL = fieldPopulateConstructor.GetILGenerator();

            //loop through all the fields adding them to the correct field
            for (int i = 0; i < fieldInfos.Length; i++)
            {
                // Load "this"
                constructorIL.Emit(OpCodes.Ldarg_0);

                // Load parameter
                constructorIL.Emit(OpCodes.Ldarg, i + 1);

                //Set the parameter into the field
                constructorIL.Emit(OpCodes.Stfld, fieldInfos[i]);
            }

            // Load "this"
            constructorIL.Emit(OpCodes.Ldarg_0);

            //call super
            constructorIL.Emit(OpCodes.Call, superConstructor);

            // Constructor return
            constructorIL.Emit(OpCodes.Ret);
            #endregion

            return fieldInfos;

        }

       /// <summary>
        /// Creates a proxy method. THe proxy method will call the EmitProxyInterceptor when invoked
        /// </summary>
        /// <typeparam name="I"></typeparam>
        /// <param name="typeBuilder"></param>
        /// <param name="interfaceMethod"></param>
        /// <param name="executeMethod"></param>
        /// <param name="proxiedObjectField"></param>
        /// <param name="interceptorField"></param>
        private static MethodBuilder CreateProxyMethod(this TypeBuilder typeBuilder, MethodInfo interfaceMethod, FieldInfo implementorDelegate)
        {
            //get a list of parameter types
            ParameterInfo[] parameterInfos = interfaceMethod.GetParameters();
            Type[] parameterTypes = new Type[parameterInfos.Length];
            for (int i = 0; i < parameterInfos.Length; i++)
            {
                parameterTypes[i] = parameterInfos[i].ParameterType;
            }
            
            MethodBuilder methodBuilder = typeBuilder.DefineMethod(
                interfaceMethod.Name,
                MethodAttributes.Public | MethodAttributes.Virtual,
                interfaceMethod.CallingConvention,
                interfaceMethod.ReturnType,
                parameterTypes);


            #region ("Proxy IL Code")
            ILGenerator il = methodBuilder.GetILGenerator();
            List<int> refIndices = new List<int>();
            
            //define local array to store parameters to
            var paramArray = il.DeclareLocal(typeof(object[])); 


            #region ("Create Parameters Array in loc_0")
            il.Emit(OpCodes.Ldc_I4, parameterTypes.Length);
            il.Emit(OpCodes.Newarr, typeof(object)); //Create new array
            il.Emit(OpCodes.Stloc, paramArray);               //Store new array at loc_0

            for (int i = 0; i < parameterTypes.Length; i++)
            {
                var isRef = parameterTypes[i].IsByRef;
                var isOut = parameterInfos[i].IsOut;
                var dereferencedParameterType = parameterTypes[i].IsByRef ? parameterTypes[i].GetElementType() : parameterTypes[i];
                

                il.Emit(OpCodes.Ldloc, paramArray);           //load array
                il.Emit(OpCodes.Ldc_I4, i);         //load index
                il.Emit(OpCodes.Ldarg, i + 1);        //load argument

                //keep track of the ref variables so we can copy them back after the delegate is called
                if (isRef)
                    refIndices.Add(i);

                //out parameter should come through as null
                if (isOut)
                {
                    il.Emit(OpCodes.Pop);
                    il.Emit(OpCodes.Ldnull);
                }
                else if(isRef)
                {
                    //ref parameters need to be dereferenced
                    if (dereferencedParameterType.IsValueType)
                        il.Emit(OpCodes.Ldobj, dereferencedParameterType);
                    else
                        il.Emit(OpCodes.Ldind_Ref);
                }

                //box the value types (except if its an out because then it is null)
                if (!isOut && dereferencedParameterType.IsValueType)
                    il.Emit(OpCodes.Box, dereferencedParameterType); //box if needed

                il.Emit(OpCodes.Stelem_Ref);        //store arguemtn at index for array
            }
            #endregion

            il.Emit(OpCodes.Ldarg_0);                    //load this
            il.Emit(OpCodes.Ldfld, implementorDelegate); //load the this.Func<> onto the stack


            il.Emit(OpCodes.Ldtoken, interfaceMethod);  //load Member Info
            il.Emit(OpCodes.Call, typeof(MethodBase).GetMethod("GetMethodFromHandle", new Type[] { typeof(RuntimeMethodHandle) }));
            
            il.Emit(OpCodes.Ldloc_0);                    //load paramter array

            il.EmitCall(OpCodes.Callvirt, typeof(Func<MethodInfo, object[], object>).GetMethod("Invoke"), null); //call interceptor delegate

            //reinsert our out and ref parameters back into the original parameter array
            foreach (int refIndex in refIndices)
            {
                var paramType = parameterTypes[refIndex].GetElementType();

                il.Emit(OpCodes.Ldarg, refIndex + 1);       //load the real method parameter

                il.Emit(OpCodes.Ldloc, paramArray);         //load paramter array
                il.Emit(OpCodes.Ldc_I4, refIndex);          //load index
                il.Emit(OpCodes.Ldelem_Ref);                //get value at index

                if (paramType.IsValueType)
                {
                    il.Emit(OpCodes.Unbox_Any, paramType);  //unbox if needed
                    il.Emit(OpCodes.Stobj, paramType); //store object at reference
                }
                else
                {
                    il.Emit(OpCodes.Castclass, paramType);  //cast if needed
                    il.Emit(OpCodes.Stind_Ref);             //load the result into our parameter
                }
            }
            

            //go and handle the result. Note at this point we still have the result on the stack
            if (interfaceMethod.ReturnType == typeof(void))
            {
                //we dont have a reutrn result, get our result off the stack and return
                il.Emit(OpCodes.Pop);
                il.Emit(OpCodes.Ret);
            }
            else if (interfaceMethod.ReturnType.IsValueType)
            {
                //values types are more complicated, if the result is null we will convert it to the default value type.
                //to do this we need an additional label and local variable
                Label unbox = il.DefineLabel();
                il.DeclareLocal(interfaceMethod.ReturnType);
               

                il.Emit(OpCodes.Dup);                   //duplicate the result on the stack
                il.Emit(OpCodes.Ldnull);                //load null onto the stack
                il.Emit(OpCodes.Ceq);                   //compare our result to null
                il.Emit(OpCodes.Brfalse_S, unbox);      //if result is not null go unbox the result and return
                il.Emit(OpCodes.Pop);                   //our result is null, pop our result off the stack, it is no use to us
                il.Emit(OpCodes.Ldloca, 1);             //load our local variable address to the stack
                il.Emit(OpCodes.Initobj, interfaceMethod.ReturnType);   //assign our local variable to be default(valueType)
                il.Emit(OpCodes.Ldloc_1);               //push our local variable on the stack
                il.Emit(OpCodes.Ret);                   //return our local variable

                il.MarkLabel(unbox);
                il.Emit(OpCodes.Unbox_Any, interfaceMethod.ReturnType); //unbox to make our return type the value type the interface demands
                il.Emit(OpCodes.Ret);
            }
            else
            {
                //Normal object was return, cast it and return it
                il.Emit(OpCodes.Castclass, interfaceMethod.ReturnType); //cast our result to the appropriate value
                il.Emit(OpCodes.Ret);
            }

            #endregion


            typeBuilder.DefineMethodOverride(methodBuilder, interfaceMethod);
            return methodBuilder;

        }

        #region ("MethodInfo Helpers")

        private static ConcurrentDictionary<MethodInfo, PropertyInfo> _propertyInfoCache = new ConcurrentDictionary<MethodInfo, PropertyInfo>();

        public static bool IsSetProperty(this MethodInfo method)
        {
            PropertyInfo propertyInfo = method.GetProperty();
            return propertyInfo != null && propertyInfo.GetSetMethod() == method;
        }

        public static bool IsGetProperty(this MethodInfo method)
        {
            PropertyInfo propertyInfo = method.GetProperty();
            return propertyInfo != null && propertyInfo.GetGetMethod() == method;
        }

        public static PropertyInfo GetProperty(this MethodInfo method)
        {
            return _propertyInfoCache.GetOrAdd(method, (m) =>
            {
                bool takesArg = m.GetParameters().Length == 1;
                bool hasReturn = m.ReturnType != typeof(void);
                if (takesArg == hasReturn) return null;
                if (takesArg)
                {
                    return m.DeclaringType.GetProperties()
                        .Where(prop => prop.GetSetMethod() == m).FirstOrDefault();
                }
                else
                {
                    return m.DeclaringType.GetProperties()
                        .Where(prop => prop.GetGetMethod() == m).FirstOrDefault();
                }
            });
        }

        #endregion

    }
}
