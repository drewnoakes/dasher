#region License
//
// Dasher
//
// Copyright 2015-2016 Drew Noakes
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.
//
// More information about this project is available at:
//
//    https://github.com/drewnoakes/dasher
//
#endregion

using System;
using System.Reflection;
using System.Reflection.Emit;
using Dasher.TypeProviders;

namespace Dasher
{
    internal static class DeserialiserEmitter
    {
        public static Func<Unpacker, DasherContext, object> Build(Type type, UnexpectedFieldBehaviour unexpectedFieldBehaviour, DasherContext context)
        {
            // Verify and prepare for target type

            if (type.IsPrimitive)
                throw new DeserialisationException($"Cannot deserialise primitive type \"{type.Name}\". The root type must contain properties and values to support future versioning.", type);

            if (type.GetConstructors(BindingFlags.Public | BindingFlags.DeclaredOnly | BindingFlags.Instance).Length != 1)
                throw new DeserialisationException($"Type \"{type.Name}\" must have a single public constructor.", type);

            // Initialise code gen
            var method = new DynamicMethod(
                $"Generated{type.Name}Deserialiser",
                returnType: typeof(object),
                parameterTypes: new[] {typeof(Unpacker), typeof(DasherContext)},
                restrictedSkipVisibility: true);

            var ilg = method.GetILGenerator();

            // Convert args to locals, so we can pass them around
            var unpacker = ilg.DeclareLocal(typeof(Unpacker));
            ilg.Emit(OpCodes.Ldarg_0);
            ilg.Emit(OpCodes.Stloc, unpacker);

            var contextLocal = ilg.DeclareLocal(typeof(DasherContext));
            ilg.Emit(OpCodes.Ldarg_1);
            ilg.Emit(OpCodes.Stloc, contextLocal);

            var valueLocal = ilg.DeclareLocal(type);

            if (!TryEmitDeserialiseCode(ilg, "<root>", type, valueLocal, unpacker, context, contextLocal, unexpectedFieldBehaviour, isRoot: true))
                throw new Exception($"Cannot serialise type {type}.");

            ilg.Emit(OpCodes.Ldloc, valueLocal);

            if (type.IsValueType)
                ilg.Emit(OpCodes.Box, type);

            // Return the newly constructed object!
            ilg.Emit(OpCodes.Ret);

            // Return a delegate that performs the above operations
            return (Func<Unpacker, DasherContext, object>)method.CreateDelegate(typeof(Func<Unpacker, DasherContext, object>));
        }

        public static bool TryEmitDeserialiseCode(ILGenerator ilg, string name, Type targetType, LocalBuilder value, LocalBuilder unpacker, DasherContext context, LocalBuilder contextLocal, UnexpectedFieldBehaviour unexpectedFieldBehaviour, bool isRoot = false)
        {
            ITypeProvider provider;
            if (!context.TryGetTypeProvider(value.LocalType, out provider))
                return false;

            if (!isRoot && provider is ComplexTypeProvider)
            {
                ilg.Emit(OpCodes.Ldloc, contextLocal);
                ilg.LoadType(value.LocalType);
                ilg.Emit(OpCodes.Ldc_I4, (int)unexpectedFieldBehaviour);
                ilg.Emit(OpCodes.Call, typeof(DasherContext).GetMethod(nameof(DasherContext.GetOrCreateDeserialiseFunc), BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(Type), typeof(UnexpectedFieldBehaviour) }, null));
                ilg.Emit(OpCodes.Ldloc, unpacker);
                ilg.Emit(OpCodes.Ldloc, contextLocal);
                ilg.Emit(OpCodes.Call, typeof(Func<Unpacker, DasherContext, object>).GetMethod(nameof(Func<Unpacker, DasherContext, object>.Invoke), new[] { typeof(Unpacker), typeof(DasherContext) }));
                ilg.Emit(value.LocalType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, value.LocalType);
                ilg.Emit(OpCodes.Stloc, value);
            }
            else
            {
                var end = ilg.DefineLabel();

                if (!value.LocalType.IsValueType)
                {
                    // check for null
                    var nonNullLabel = ilg.DefineLabel();
                    ilg.Emit(OpCodes.Ldloc, unpacker);
                    ilg.Emit(OpCodes.Call, typeof(Unpacker).GetMethod(nameof(Unpacker.TryReadNull)));
                    ilg.Emit(OpCodes.Brfalse, nonNullLabel);
                    {
                        ilg.Emit(OpCodes.Ldnull);
                        ilg.Emit(OpCodes.Stloc, value);
                        ilg.Emit(OpCodes.Br, end);
                    }
                    ilg.MarkLabel(nonNullLabel);
                }

                provider.EmitDeserialiseCode(ilg, name, targetType, value, unpacker, contextLocal, context, unexpectedFieldBehaviour);

                ilg.MarkLabel(end);
            }
            return true;
        }
    }
}