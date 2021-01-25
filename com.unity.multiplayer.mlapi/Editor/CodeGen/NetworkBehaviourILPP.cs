using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using MLAPI.Messaging;
using MLAPI.Serialization;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using Unity.CompilationPipeline.Common.Diagnostics;
using Unity.CompilationPipeline.Common.ILPostProcessing;
using UnityEngine;
using MethodAttributes = Mono.Cecil.MethodAttributes;
using ParameterAttributes = Mono.Cecil.ParameterAttributes;

#if UNITY_2020_2_OR_NEWER
using ILPPInterface = Unity.CompilationPipeline.Common.ILPostProcessing.ILPostProcessor;
#else
using ILPPInterface = MLAPI.Editor.CodeGen.ILPostProcessor;
#endif

namespace MLAPI.Editor.CodeGen
{
    internal sealed class NetworkBehaviourILPP : ILPPInterface
    {
        public override ILPPInterface GetInstance() => this;

        public override bool WillProcess(ICompiledAssembly compiledAssembly) => compiledAssembly.References.Any(filePath => Path.GetFileNameWithoutExtension(filePath) == CodeGenHelpers.RuntimeAssemblyName);

        private readonly List<DiagnosticMessage> m_Diagnostics = new List<DiagnosticMessage>();

        public override ILPostProcessResult Process(ICompiledAssembly compiledAssembly)
        {
            if (!WillProcess(compiledAssembly)) return null;
            m_Diagnostics.Clear();

            // read
            var assemblyDefinition = CodeGenHelpers.AssemblyDefinitionFor(compiledAssembly);
            if (assemblyDefinition == null)
            {
                m_Diagnostics.AddError($"Cannot read assembly definition: {compiledAssembly.Name}");
                return null;
            }

            // process
            var mainModule = assemblyDefinition.MainModule;
            if (mainModule != null)
            {
                if (ImportReferences(mainModule))
                {
                    // process `NetworkBehaviour` types
                    mainModule.Types
                        .Where(t => t.IsSubclassOf(CodeGenHelpers.NetworkBehaviour_FullName))
                        .ToList()
                        .ForEach(ProcessNetworkBehaviour);
                }
                else m_Diagnostics.AddError($"Cannot import references into main module: {mainModule.Name}");
            }
            else m_Diagnostics.AddError($"Cannot get main module from assembly definition: {compiledAssembly.Name}");

            // write
            var pe = new MemoryStream();
            var pdb = new MemoryStream();

            var writerParameters = new WriterParameters
            {
                SymbolWriterProvider = new PortablePdbWriterProvider(),
                SymbolStream = pdb,
                WriteSymbols = true
            };

            assemblyDefinition.Write(pe, writerParameters);

            return new ILPostProcessResult(new InMemoryAssembly(pe.ToArray(), pdb.ToArray()), m_Diagnostics);
        }

        private TypeReference NetworkManager_TypeRef;
        private FieldReference NetworkManager_ntable_FieldRef;
        private MethodReference NetworkManager_ntable_Add_MethodRef;
        private MethodReference NetworkManager_getSingleton_MethodRef;
        private MethodReference NetworkManager_getIsListening_MethodRef;
        private MethodReference NetworkManager_getIsHost_MethodRef;
        private MethodReference NetworkManager_getIsServer_MethodRef;
        private MethodReference NetworkManager_getIsClient_MethodRef;
        private TypeReference NetworkBehaviour_TypeRef;
        private MethodReference NetworkBehaviour_BeginSendServerRpc_MethodRef;
        private MethodReference NetworkBehaviour_EndSendServerRpc_MethodRef;
        private MethodReference NetworkBehaviour_BeginSendClientRpc_MethodRef;
        private MethodReference NetworkBehaviour_EndSendClientRpc_MethodRef;
        private FieldReference NetworkBehaviour_nexec_FieldRef;
        private MethodReference NetworkHandlerDelegateCtor_MethodRef;
        private TypeReference ServerRpcParams_TypeRef;
        private FieldReference ServerRpcParams_Send_FieldRef;
        private FieldReference ServerRpcParams_Receive_FieldRef;
        private TypeReference ServerRpcSendParams_TypeRef;
        private TypeReference ServerRpcReceiveParams_TypeRef;
        private FieldReference ServerRpcReceiveParams_SenderClientId_FieldRef;
        private TypeReference ClientRpcParams_TypeRef;
        private FieldReference ClientRpcParams_Send_FieldRef;
        private FieldReference ClientRpcParams_Receive_FieldRef;
        private TypeReference ClientRpcSendParams_TypeRef;
        private TypeReference ClientRpcReceiveParams_TypeRef;
        private TypeReference BitSerializer_TypeRef;
        private MethodReference BitSerializer_SerializeBool_MethodRef;
        private MethodReference BitSerializer_SerializeChar_MethodRef;
        private MethodReference BitSerializer_SerializeSbyte_MethodRef;
        private MethodReference BitSerializer_SerializeByte_MethodRef;
        private MethodReference BitSerializer_SerializeShort_MethodRef;
        private MethodReference BitSerializer_SerializeUshort_MethodRef;
        private MethodReference BitSerializer_SerializeInt_MethodRef;
        private MethodReference BitSerializer_SerializeUint_MethodRef;
        private MethodReference BitSerializer_SerializeLong_MethodRef;
        private MethodReference BitSerializer_SerializeUlong_MethodRef;
        private MethodReference BitSerializer_SerializeFloat_MethodRef;
        private MethodReference BitSerializer_SerializeDouble_MethodRef;
        private MethodReference BitSerializer_SerializeString_MethodRef;
        private MethodReference BitSerializer_SerializeColor_MethodRef;
        private MethodReference BitSerializer_SerializeColor32_MethodRef;
        private MethodReference BitSerializer_SerializeVector2_MethodRef;
        private MethodReference BitSerializer_SerializeVector3_MethodRef;
        private MethodReference BitSerializer_SerializeVector4_MethodRef;
        private MethodReference BitSerializer_SerializeQuaternion_MethodRef;
        private MethodReference BitSerializer_SerializeRay_MethodRef;
        private MethodReference BitSerializer_SerializeRay2D_MethodRef;
        private MethodReference BitSerializer_SerializeNetObject_MethodRef;
        private MethodReference BitSerializer_SerializeNetBehaviour_MethodRef;
        private MethodReference BitSerializer_SerializeBoolArray_MethodRef;
        private MethodReference BitSerializer_SerializeCharArray_MethodRef;
        private MethodReference BitSerializer_SerializeSbyteArray_MethodRef;
        private MethodReference BitSerializer_SerializeByteArray_MethodRef;
        private MethodReference BitSerializer_SerializeShortArray_MethodRef;
        private MethodReference BitSerializer_SerializeUshortArray_MethodRef;
        private MethodReference BitSerializer_SerializeIntArray_MethodRef;
        private MethodReference BitSerializer_SerializeUintArray_MethodRef;
        private MethodReference BitSerializer_SerializeLongArray_MethodRef;
        private MethodReference BitSerializer_SerializeUlongArray_MethodRef;
        private MethodReference BitSerializer_SerializeFloatArray_MethodRef;
        private MethodReference BitSerializer_SerializeDoubleArray_MethodRef;
        private MethodReference BitSerializer_SerializeStringArray_MethodRef;
        private MethodReference BitSerializer_SerializeColorArray_MethodRef;
        private MethodReference BitSerializer_SerializeColor32Array_MethodRef;
        private MethodReference BitSerializer_SerializeVector2Array_MethodRef;
        private MethodReference BitSerializer_SerializeVector3Array_MethodRef;
        private MethodReference BitSerializer_SerializeVector4Array_MethodRef;
        private MethodReference BitSerializer_SerializeQuaternionArray_MethodRef;
        private MethodReference BitSerializer_SerializeRayArray_MethodRef;
        private MethodReference BitSerializer_SerializeRay2DArray_MethodRef;
        private MethodReference BitSerializer_SerializeNetObjectArray_MethodRef;
        private MethodReference BitSerializer_SerializeNetBehaviourArray_MethodRef;

        private const string k_NetworkingManager_Singleton = nameof(NetworkingManager.Singleton);
        private const string k_NetworkingManager_IsListening = nameof(NetworkingManager.IsListening);
        private const string k_NetworkingManager_IsHost = nameof(NetworkingManager.IsHost);
        private const string k_NetworkingManager_IsServer = nameof(NetworkingManager.IsServer);
        private const string k_NetworkingManager_IsClient = nameof(NetworkingManager.IsClient);
#pragma warning disable 618
        private const string k_NetworkingManager_ntable = nameof(NetworkingManager.__ntable);

        private const string k_NetworkedBehaviour_BeginSendServerRpc = nameof(NetworkedBehaviour.__beginSendServerRpc);
        private const string k_NetworkedBehaviour_EndSendServerRpc = nameof(NetworkedBehaviour.__endSendServerRpc);
        private const string k_NetworkedBehaviour_BeginSendClientRpc = nameof(NetworkedBehaviour.__beginSendClientRpc);
        private const string k_NetworkedBehaviour_EndSendClientRpc = nameof(NetworkedBehaviour.__endSendClientRpc);
        private const string k_NetworkedBehaviour_nexec = nameof(NetworkedBehaviour.__nexec);
#pragma warning restore 618

        private bool ImportReferences(ModuleDefinition moduleDefinition)
        {
            var networkManagerType = typeof(NetworkingManager);
            NetworkManager_TypeRef = moduleDefinition.ImportReference(networkManagerType);
            foreach (var propertyInfo in networkManagerType.GetProperties())
            {
                switch (propertyInfo.Name)
                {
                    case k_NetworkingManager_Singleton:
                        NetworkManager_getSingleton_MethodRef = moduleDefinition.ImportReference(propertyInfo.GetMethod);
                        break;
                    case k_NetworkingManager_IsListening:
                        NetworkManager_getIsListening_MethodRef = moduleDefinition.ImportReference(propertyInfo.GetMethod);
                        break;
                    case k_NetworkingManager_IsHost:
                        NetworkManager_getIsHost_MethodRef = moduleDefinition.ImportReference(propertyInfo.GetMethod);
                        break;
                    case k_NetworkingManager_IsServer:
                        NetworkManager_getIsServer_MethodRef = moduleDefinition.ImportReference(propertyInfo.GetMethod);
                        break;
                    case k_NetworkingManager_IsClient:
                        NetworkManager_getIsClient_MethodRef = moduleDefinition.ImportReference(propertyInfo.GetMethod);
                        break;
                }
            }

            foreach (var fieldInfo in networkManagerType.GetFields(BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
            {
                switch (fieldInfo.Name)
                {
                    case k_NetworkingManager_ntable:
                        NetworkManager_ntable_FieldRef = moduleDefinition.ImportReference(fieldInfo);
                        NetworkManager_ntable_Add_MethodRef = moduleDefinition.ImportReference(fieldInfo.FieldType.GetMethod("Add"));
                        break;
                }
            }

            var networkBehaviourType = typeof(NetworkedBehaviour);
            NetworkBehaviour_TypeRef = moduleDefinition.ImportReference(networkBehaviourType);
            foreach (var methodInfo in networkBehaviourType.GetMethods(BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
            {
                switch (methodInfo.Name)
                {
                    case k_NetworkedBehaviour_BeginSendServerRpc:
                        NetworkBehaviour_BeginSendServerRpc_MethodRef = moduleDefinition.ImportReference(methodInfo);
                        break;
                    case k_NetworkedBehaviour_EndSendServerRpc:
                        NetworkBehaviour_EndSendServerRpc_MethodRef = moduleDefinition.ImportReference(methodInfo);
                        break;
                    case k_NetworkedBehaviour_BeginSendClientRpc:
                        NetworkBehaviour_BeginSendClientRpc_MethodRef = moduleDefinition.ImportReference(methodInfo);
                        break;
                    case k_NetworkedBehaviour_EndSendClientRpc:
                        NetworkBehaviour_EndSendClientRpc_MethodRef = moduleDefinition.ImportReference(methodInfo);
                        break;
                }
            }

            foreach (var fieldInfo in networkBehaviourType.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
            {
                switch (fieldInfo.Name)
                {
                    case k_NetworkedBehaviour_nexec:
                        NetworkBehaviour_nexec_FieldRef = moduleDefinition.ImportReference(fieldInfo);
                        break;
                }
            }

            var networkHandlerDelegateType = typeof(Action<NetworkedBehaviour, BitSerializer, ulong>);
            NetworkHandlerDelegateCtor_MethodRef = moduleDefinition.ImportReference(networkHandlerDelegateType.GetConstructor(new[] { typeof(object), typeof(IntPtr) }));

            var serverRpcParamsType = typeof(ServerRpcParams);
            ServerRpcParams_TypeRef = moduleDefinition.ImportReference(serverRpcParamsType);
            foreach (var fieldInfo in serverRpcParamsType.GetFields())
            {
                switch (fieldInfo.Name)
                {
                    case nameof(ServerRpcParams.Send):
                        ServerRpcParams_Send_FieldRef = moduleDefinition.ImportReference(fieldInfo);
                        break;
                    case nameof(ServerRpcParams.Receive):
                        ServerRpcParams_Receive_FieldRef = moduleDefinition.ImportReference(fieldInfo);
                        break;
                }
            }

            var serverRpcSendParamsType = typeof(ServerRpcSendParams);
            ServerRpcSendParams_TypeRef = moduleDefinition.ImportReference(serverRpcSendParamsType);

            var serverRpcReceiveParamsType = typeof(ServerRpcReceiveParams);
            ServerRpcReceiveParams_TypeRef = moduleDefinition.ImportReference(serverRpcReceiveParamsType);
            foreach (var fieldInfo in serverRpcReceiveParamsType.GetFields())
            {
                switch (fieldInfo.Name)
                {
                    case nameof(ServerRpcReceiveParams.SenderClientId):
                        ServerRpcReceiveParams_SenderClientId_FieldRef = moduleDefinition.ImportReference(fieldInfo);
                        break;
                }
            }

            var clientRpcParamsType = typeof(ClientRpcParams);
            ClientRpcParams_TypeRef = moduleDefinition.ImportReference(clientRpcParamsType);
            foreach (var fieldInfo in clientRpcParamsType.GetFields())
            {
                switch (fieldInfo.Name)
                {
                    case nameof(ClientRpcParams.Send):
                        ClientRpcParams_Send_FieldRef = moduleDefinition.ImportReference(fieldInfo);
                        break;
                    case nameof(ClientRpcParams.Receive):
                        ClientRpcParams_Receive_FieldRef = moduleDefinition.ImportReference(fieldInfo);
                        break;
                }
            }

            var clientRpcSendParamsType = typeof(ClientRpcSendParams);
            ClientRpcSendParams_TypeRef = moduleDefinition.ImportReference(clientRpcSendParamsType);

            var clientRpcReceiveParamsType = typeof(ClientRpcReceiveParams);
            ClientRpcReceiveParams_TypeRef = moduleDefinition.ImportReference(clientRpcReceiveParamsType);

            var bitSerializerType = typeof(BitSerializer);
            BitSerializer_TypeRef = moduleDefinition.ImportReference(bitSerializerType);
            foreach (var methodInfo in bitSerializerType.GetMethods())
            {
                if (methodInfo.Name != nameof(BitSerializer.Serialize)) continue;
                var methodParams = methodInfo.GetParameters();
                if (methodParams.Length != 1) continue;
                var paramType = methodParams[0].ParameterType;
                if (paramType.IsByRef == false) continue;
                var paramTypeName = paramType.Name;

                if (paramTypeName == typeof(bool).MakeByRefType().Name) BitSerializer_SerializeBool_MethodRef = moduleDefinition.ImportReference(methodInfo);
                else if (paramTypeName == typeof(char).MakeByRefType().Name) BitSerializer_SerializeChar_MethodRef = moduleDefinition.ImportReference(methodInfo);
                else if (paramTypeName == typeof(sbyte).MakeByRefType().Name) BitSerializer_SerializeSbyte_MethodRef = moduleDefinition.ImportReference(methodInfo);
                else if (paramTypeName == typeof(byte).MakeByRefType().Name) BitSerializer_SerializeByte_MethodRef = moduleDefinition.ImportReference(methodInfo);
                else if (paramTypeName == typeof(short).MakeByRefType().Name) BitSerializer_SerializeShort_MethodRef = moduleDefinition.ImportReference(methodInfo);
                else if (paramTypeName == typeof(ushort).MakeByRefType().Name) BitSerializer_SerializeUshort_MethodRef = moduleDefinition.ImportReference(methodInfo);
                else if (paramTypeName == typeof(int).MakeByRefType().Name) BitSerializer_SerializeInt_MethodRef = moduleDefinition.ImportReference(methodInfo);
                else if (paramTypeName == typeof(uint).MakeByRefType().Name) BitSerializer_SerializeUint_MethodRef = moduleDefinition.ImportReference(methodInfo);
                else if (paramTypeName == typeof(long).MakeByRefType().Name) BitSerializer_SerializeLong_MethodRef = moduleDefinition.ImportReference(methodInfo);
                else if (paramTypeName == typeof(ulong).MakeByRefType().Name) BitSerializer_SerializeUlong_MethodRef = moduleDefinition.ImportReference(methodInfo);
                else if (paramTypeName == typeof(float).MakeByRefType().Name) BitSerializer_SerializeFloat_MethodRef = moduleDefinition.ImportReference(methodInfo);
                else if (paramTypeName == typeof(double).MakeByRefType().Name) BitSerializer_SerializeDouble_MethodRef = moduleDefinition.ImportReference(methodInfo);
                else if (paramTypeName == typeof(string).MakeByRefType().Name) BitSerializer_SerializeString_MethodRef = moduleDefinition.ImportReference(methodInfo);
                else if (paramTypeName == typeof(Color).MakeByRefType().Name) BitSerializer_SerializeColor_MethodRef = moduleDefinition.ImportReference(methodInfo);
                else if (paramTypeName == typeof(Color32).MakeByRefType().Name) BitSerializer_SerializeColor32_MethodRef = moduleDefinition.ImportReference(methodInfo);
                else if (paramTypeName == typeof(Vector2).MakeByRefType().Name) BitSerializer_SerializeVector2_MethodRef = moduleDefinition.ImportReference(methodInfo);
                else if (paramTypeName == typeof(Vector3).MakeByRefType().Name) BitSerializer_SerializeVector3_MethodRef = moduleDefinition.ImportReference(methodInfo);
                else if (paramTypeName == typeof(Vector4).MakeByRefType().Name) BitSerializer_SerializeVector4_MethodRef = moduleDefinition.ImportReference(methodInfo);
                else if (paramTypeName == typeof(Quaternion).MakeByRefType().Name) BitSerializer_SerializeQuaternion_MethodRef = moduleDefinition.ImportReference(methodInfo);
                else if (paramTypeName == typeof(Ray).MakeByRefType().Name) BitSerializer_SerializeRay_MethodRef = moduleDefinition.ImportReference(methodInfo);
                else if (paramTypeName == typeof(Ray2D).MakeByRefType().Name) BitSerializer_SerializeRay2D_MethodRef = moduleDefinition.ImportReference(methodInfo);
                else if (paramTypeName == typeof(NetworkedObject).MakeByRefType().Name) BitSerializer_SerializeNetObject_MethodRef = moduleDefinition.ImportReference(methodInfo);
                else if (paramTypeName == typeof(NetworkedBehaviour).MakeByRefType().Name) BitSerializer_SerializeNetBehaviour_MethodRef = moduleDefinition.ImportReference(methodInfo);
                else if (paramTypeName == typeof(bool[]).MakeByRefType().Name) BitSerializer_SerializeBoolArray_MethodRef = moduleDefinition.ImportReference(methodInfo);
                else if (paramTypeName == typeof(char[]).MakeByRefType().Name) BitSerializer_SerializeCharArray_MethodRef = moduleDefinition.ImportReference(methodInfo);
                else if (paramTypeName == typeof(sbyte[]).MakeByRefType().Name) BitSerializer_SerializeSbyteArray_MethodRef = moduleDefinition.ImportReference(methodInfo);
                else if (paramTypeName == typeof(byte[]).MakeByRefType().Name) BitSerializer_SerializeByteArray_MethodRef = moduleDefinition.ImportReference(methodInfo);
                else if (paramTypeName == typeof(short[]).MakeByRefType().Name) BitSerializer_SerializeShortArray_MethodRef = moduleDefinition.ImportReference(methodInfo);
                else if (paramTypeName == typeof(ushort[]).MakeByRefType().Name) BitSerializer_SerializeUshortArray_MethodRef = moduleDefinition.ImportReference(methodInfo);
                else if (paramTypeName == typeof(int[]).MakeByRefType().Name) BitSerializer_SerializeIntArray_MethodRef = moduleDefinition.ImportReference(methodInfo);
                else if (paramTypeName == typeof(uint[]).MakeByRefType().Name) BitSerializer_SerializeUintArray_MethodRef = moduleDefinition.ImportReference(methodInfo);
                else if (paramTypeName == typeof(long[]).MakeByRefType().Name) BitSerializer_SerializeLongArray_MethodRef = moduleDefinition.ImportReference(methodInfo);
                else if (paramTypeName == typeof(ulong[]).MakeByRefType().Name) BitSerializer_SerializeUlongArray_MethodRef = moduleDefinition.ImportReference(methodInfo);
                else if (paramTypeName == typeof(float[]).MakeByRefType().Name) BitSerializer_SerializeFloatArray_MethodRef = moduleDefinition.ImportReference(methodInfo);
                else if (paramTypeName == typeof(double[]).MakeByRefType().Name) BitSerializer_SerializeDoubleArray_MethodRef = moduleDefinition.ImportReference(methodInfo);
                else if (paramTypeName == typeof(string[]).MakeByRefType().Name) BitSerializer_SerializeStringArray_MethodRef = moduleDefinition.ImportReference(methodInfo);
                else if (paramTypeName == typeof(Color[]).MakeByRefType().Name) BitSerializer_SerializeColorArray_MethodRef = moduleDefinition.ImportReference(methodInfo);
                else if (paramTypeName == typeof(Color32[]).MakeByRefType().Name) BitSerializer_SerializeColor32Array_MethodRef = moduleDefinition.ImportReference(methodInfo);
                else if (paramTypeName == typeof(Vector2[]).MakeByRefType().Name) BitSerializer_SerializeVector2Array_MethodRef = moduleDefinition.ImportReference(methodInfo);
                else if (paramTypeName == typeof(Vector3[]).MakeByRefType().Name) BitSerializer_SerializeVector3Array_MethodRef = moduleDefinition.ImportReference(methodInfo);
                else if (paramTypeName == typeof(Vector4[]).MakeByRefType().Name) BitSerializer_SerializeVector4Array_MethodRef = moduleDefinition.ImportReference(methodInfo);
                else if (paramTypeName == typeof(Quaternion[]).MakeByRefType().Name) BitSerializer_SerializeQuaternionArray_MethodRef = moduleDefinition.ImportReference(methodInfo);
                else if (paramTypeName == typeof(Ray[]).MakeByRefType().Name) BitSerializer_SerializeRayArray_MethodRef = moduleDefinition.ImportReference(methodInfo);
                else if (paramTypeName == typeof(Ray2D[]).MakeByRefType().Name) BitSerializer_SerializeRay2DArray_MethodRef = moduleDefinition.ImportReference(methodInfo);
                else if (paramTypeName == typeof(NetworkedObject[]).MakeByRefType().Name) BitSerializer_SerializeNetObjectArray_MethodRef = moduleDefinition.ImportReference(methodInfo);
                else if (paramTypeName == typeof(NetworkedBehaviour[]).MakeByRefType().Name) BitSerializer_SerializeNetBehaviourArray_MethodRef = moduleDefinition.ImportReference(methodInfo);
            }

            return true;
        }

        private void ProcessNetworkBehaviour(TypeDefinition typeDefinition)
        {
            var staticHandlers = new List<(uint Hash, MethodDefinition Method)>();
            foreach (var methodDefinition in typeDefinition.Methods)
            {
                var rpcAttribute = CheckAndGetRPCAttribute(methodDefinition);
                if (rpcAttribute == null) continue;

                var methodDefHash = methodDefinition.Hash();
                if (methodDefHash == 0) continue;

                InjectWriteAndCallBlocks(methodDefinition, rpcAttribute, methodDefHash);
                staticHandlers.Add((methodDefHash, GenerateStaticHandler(methodDefinition, rpcAttribute)));
            }

            if (staticHandlers.Count > 0)
            {
                var staticCtorMethodDef = typeDefinition.GetStaticConstructor();
                if (staticCtorMethodDef == null)
                {
                    staticCtorMethodDef = new MethodDefinition(
                        ".cctor", // Static Constructor (constant-constructor)
                        MethodAttributes.HideBySig |
                        MethodAttributes.SpecialName |
                        MethodAttributes.RTSpecialName |
                        MethodAttributes.Static,
                        typeDefinition.Module.TypeSystem.Void);
                    staticCtorMethodDef.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
                    typeDefinition.Methods.Add(staticCtorMethodDef);
                }

                var instructions = new List<Instruction>();
                var processor = staticCtorMethodDef.Body.GetILProcessor();
                foreach (var (hash, method) in staticHandlers)
                {
                    if (hash == 0 || method == null) continue;

                    typeDefinition.Methods.Add(method);

                    // NetworkManager.__ntable.Add(HandlerHash, HandlerMethod);
                    instructions.Add(processor.Create(OpCodes.Ldsfld, NetworkManager_ntable_FieldRef));
                    instructions.Add(processor.Create(OpCodes.Ldc_I4, unchecked((int)hash)));
                    instructions.Add(processor.Create(OpCodes.Ldnull));
                    instructions.Add(processor.Create(OpCodes.Ldftn, method));
                    instructions.Add(processor.Create(OpCodes.Newobj, NetworkHandlerDelegateCtor_MethodRef));
                    instructions.Add(processor.Create(OpCodes.Call, NetworkManager_ntable_Add_MethodRef));
                }

                instructions.Reverse();
                instructions.ForEach(instruction => processor.Body.Instructions.Insert(0, instruction));
            }

            // process nested `NetworkBehaviour` types
            typeDefinition.NestedTypes
                .Where(t => t.IsSubclassOf(CodeGenHelpers.NetworkBehaviour_FullName))
                .ToList()
                .ForEach(ProcessNetworkBehaviour);
        }

        private CustomAttribute CheckAndGetRPCAttribute(MethodDefinition methodDefinition)
        {
            CustomAttribute rpcAttribute = null;
            bool isServerRpc = false;
            foreach (var customAttribute in methodDefinition.CustomAttributes)
            {
                var customAttributeType_FullName = customAttribute.AttributeType.FullName;

                if (customAttributeType_FullName == CodeGenHelpers.ServerRpcAttribute_FullName ||
                    customAttributeType_FullName == CodeGenHelpers.ClientRpcAttribute_FullName)
                {
                    bool isValid = true;

                    if (methodDefinition.IsStatic)
                    {
                        m_Diagnostics.AddError(methodDefinition, "RPC method must not be static!");
                        isValid = false;
                    }

                    if (methodDefinition.IsAbstract)
                    {
                        m_Diagnostics.AddError(methodDefinition, "RPC method must not be abstract!");
                        isValid = false;
                    }

                    if (methodDefinition.ReturnType != methodDefinition.Module.TypeSystem.Void)
                    {
                        m_Diagnostics.AddError(methodDefinition, "RPC method must return `void`!");
                        isValid = false;
                    }

                    if (customAttributeType_FullName == CodeGenHelpers.ServerRpcAttribute_FullName &&
                        !methodDefinition.Name.EndsWith("ServerRpc", StringComparison.OrdinalIgnoreCase))
                    {
                        m_Diagnostics.AddError(methodDefinition, "ServerRpc method must end with 'ServerRpc' suffix!");
                        isValid = false;
                    }

                    if (customAttributeType_FullName == CodeGenHelpers.ClientRpcAttribute_FullName &&
                        !methodDefinition.Name.EndsWith("ClientRpc", StringComparison.OrdinalIgnoreCase))
                    {
                        m_Diagnostics.AddError(methodDefinition, "ClientRpc method must end with 'ClientRpc' suffix!");
                        isValid = false;
                    }

                    if (isValid)
                    {
                        isServerRpc = customAttributeType_FullName == CodeGenHelpers.ServerRpcAttribute_FullName;
                        rpcAttribute = customAttribute;
                    }
                }
            }

            if (rpcAttribute == null)
            {
                if (methodDefinition.Name.EndsWith("ServerRpc", StringComparison.OrdinalIgnoreCase))
                {
                    m_Diagnostics.AddError(methodDefinition, "ServerRpc method must be marked with 'ServerRpc' attribute!");
                }
                else if (methodDefinition.Name.EndsWith("ClientRpc", StringComparison.OrdinalIgnoreCase))
                {
                    m_Diagnostics.AddError(methodDefinition, "ClientRpc method must be marked with 'ClientRpc' attribute!");
                }

                return null;
            }

            int paramCount = methodDefinition.Parameters.Count;
            for (int paramIndex = 0; paramIndex < paramCount; ++paramIndex)
            {
                var paramDef = methodDefinition.Parameters[paramIndex];
                var paramType = paramDef.ParameterType;

                // Serializable
                if (paramType.IsSerializable()) continue;
                // ServerRpcParams
                if (paramType.FullName == CodeGenHelpers.ServerRpcParams_FullName && isServerRpc && paramIndex == paramCount - 1) continue;
                // ClientRpcParams
                if (paramType.FullName == CodeGenHelpers.ClientRpcParams_FullName && !isServerRpc && paramIndex == paramCount - 1) continue;

                m_Diagnostics.AddError(methodDefinition, $"RPC method parameter does not support serialization: {paramType.FullName}");
                rpcAttribute = null;
            }

            return rpcAttribute;
        }

        private void InjectWriteAndCallBlocks(MethodDefinition methodDefinition, CustomAttribute rpcAttribute, uint methodDefHash)
        {
            var typeSystem = methodDefinition.Module.TypeSystem;
            var instructions = new List<Instruction>();
            var processor = methodDefinition.Body.GetILProcessor();
            var isServerRpc = rpcAttribute.AttributeType.FullName == CodeGenHelpers.ServerRpcAttribute_FullName;
            var isReliableRpc = true;
            foreach (var attrField in rpcAttribute.Fields)
            {
                switch (attrField.Name)
                {
                    case nameof(RpcAttribute.IsReliable):
                        isReliableRpc = attrField.Argument.Type == typeSystem.Boolean && (bool)attrField.Argument.Value;
                        break;
                }
            }

            var paramCount = methodDefinition.Parameters.Count;
            var hasRpcParams =
                paramCount > 0 &&
                ((isServerRpc && methodDefinition.Parameters[paramCount - 1].ParameterType.FullName == CodeGenHelpers.ServerRpcParams_FullName) ||
                 (!isServerRpc && methodDefinition.Parameters[paramCount - 1].ParameterType.FullName == CodeGenHelpers.ClientRpcParams_FullName));

            methodDefinition.Body.InitLocals = true;
            // NetworkManager networkManager;
            methodDefinition.Body.Variables.Add(new VariableDefinition(NetworkManager_TypeRef));
            int netManLocIdx = methodDefinition.Body.Variables.Count - 1;
            // BitSerializer serializer;
            methodDefinition.Body.Variables.Add(new VariableDefinition(BitSerializer_TypeRef));
            int serializerLocIdx = methodDefinition.Body.Variables.Count - 1;
            // uint methodHash;
            methodDefinition.Body.Variables.Add(new VariableDefinition(typeSystem.UInt32));
            int methodHashLocIdx = methodDefinition.Body.Variables.Count - 1;
            // XXXRpcSendParams
            if (!hasRpcParams) methodDefinition.Body.Variables.Add(new VariableDefinition(isServerRpc ? ServerRpcSendParams_TypeRef : ClientRpcSendParams_TypeRef));
            int sendParamsIdx = !hasRpcParams ? methodDefinition.Body.Variables.Count - 1 : -1;

            {
                var returnInstr = processor.Create(OpCodes.Ret);
                var lastInstr = processor.Create(OpCodes.Nop);

                // networkManager = NetworkManager.Singleton;
                instructions.Add(processor.Create(OpCodes.Call, NetworkManager_getSingleton_MethodRef));
                instructions.Add(processor.Create(OpCodes.Stloc, netManLocIdx));

                // if (networkManager == null || !networkManager.IsListening) return;
                instructions.Add(processor.Create(OpCodes.Ldloc, netManLocIdx));
                instructions.Add(processor.Create(OpCodes.Brfalse, returnInstr));
                instructions.Add(processor.Create(OpCodes.Ldloc, netManLocIdx));
                instructions.Add(processor.Create(OpCodes.Callvirt, NetworkManager_getIsListening_MethodRef));
                instructions.Add(processor.Create(OpCodes.Brtrue, lastInstr));

                instructions.Add(returnInstr);
                instructions.Add(lastInstr);
            }

            {
                var beginInstr = processor.Create(OpCodes.Nop);
                var endInstr = processor.Create(OpCodes.Nop);
                var lastInstr = processor.Create(OpCodes.Nop);

                // if (__nexec != NExec.Server) -> ServerRpc
                // if (__nexec != NExec.Client) -> ClientRpc
                instructions.Add(processor.Create(OpCodes.Ldarg_0));
                instructions.Add(processor.Create(OpCodes.Ldfld, NetworkBehaviour_nexec_FieldRef));
#pragma warning disable 618
                instructions.Add(processor.Create(OpCodes.Ldc_I4, (int)(isServerRpc ? NetworkedBehaviour.__NExec.Server : NetworkedBehaviour.__NExec.Client)));
#pragma warning restore 618
                instructions.Add(processor.Create(OpCodes.Ceq));
                instructions.Add(processor.Create(OpCodes.Ldc_I4, 0));
                instructions.Add(processor.Create(OpCodes.Ceq));
                instructions.Add(processor.Create(OpCodes.Brfalse, lastInstr));

                // if (networkManager.IsClient || networkManager.IsHost) { ... } -> ServerRpc
                // if (networkManager.IsServer || networkManager.IsHost) { ... } -> ClientRpc
                instructions.Add(processor.Create(OpCodes.Ldloc, netManLocIdx));
                instructions.Add(processor.Create(OpCodes.Callvirt, isServerRpc ? NetworkManager_getIsClient_MethodRef : NetworkManager_getIsServer_MethodRef));
                instructions.Add(processor.Create(OpCodes.Brtrue, beginInstr));
                instructions.Add(processor.Create(OpCodes.Ldloc, netManLocIdx));
                instructions.Add(processor.Create(OpCodes.Callvirt, NetworkManager_getIsHost_MethodRef));
                instructions.Add(processor.Create(OpCodes.Brfalse, lastInstr));

                instructions.Add(beginInstr);

                // var serializer = BeginSendServerRpc(sendParams, isReliable) -> ServerRpc
                // var serializer = BeginSendClientRpc(sendParams, isReliable) -> ClientRpc
                if (isServerRpc)
                {
                    // ServerRpc
                    // var serializer = BeginSendServerRpc(sendParams, isReliable);
                    instructions.Add(processor.Create(OpCodes.Ldarg_0));

                    if (hasRpcParams)
                    {
                        // rpcParams.Send
                        instructions.Add(processor.Create(OpCodes.Ldarg, paramCount));
                        instructions.Add(processor.Create(OpCodes.Ldfld, ServerRpcParams_Send_FieldRef));
                    }
                    else
                    {
                        // default
                        instructions.Add(processor.Create(OpCodes.Ldloca, sendParamsIdx));
                        instructions.Add(processor.Create(OpCodes.Initobj, ServerRpcSendParams_TypeRef));
                        instructions.Add(processor.Create(OpCodes.Ldloc, sendParamsIdx));
                    }

                    // isReliable
                    instructions.Add(processor.Create(isReliableRpc ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0));

                    // BeginSendServerRpc
                    instructions.Add(processor.Create(OpCodes.Call, NetworkBehaviour_BeginSendServerRpc_MethodRef));
                    instructions.Add(processor.Create(OpCodes.Stloc, serializerLocIdx));
                }
                else
                {
                    // ClientRpc
                    // var serializer = BeginSendClientRpc(sendParams, isReliable);
                    instructions.Add(processor.Create(OpCodes.Ldarg_0));

                    if (hasRpcParams)
                    {
                        // rpcParams.Send
                        instructions.Add(processor.Create(OpCodes.Ldarg, paramCount));
                        instructions.Add(processor.Create(OpCodes.Ldfld, ClientRpcParams_Send_FieldRef));
                    }
                    else
                    {
                        // default
                        instructions.Add(processor.Create(OpCodes.Ldloca, sendParamsIdx));
                        instructions.Add(processor.Create(OpCodes.Initobj, ClientRpcSendParams_TypeRef));
                        instructions.Add(processor.Create(OpCodes.Ldloc, sendParamsIdx));
                    }

                    // isReliable
                    instructions.Add(processor.Create(isReliableRpc ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0));

                    // BeginSendClientRpc
                    instructions.Add(processor.Create(OpCodes.Call, NetworkBehaviour_BeginSendClientRpc_MethodRef));
                    instructions.Add(processor.Create(OpCodes.Stloc, serializerLocIdx));
                }

                // if (serializer != null)
                instructions.Add(processor.Create(OpCodes.Ldloc, serializerLocIdx));
                instructions.Add(processor.Create(OpCodes.Brfalse, endInstr));

                // methodHash = methodDefHash
                instructions.Add(processor.Create(OpCodes.Ldc_I4, unchecked((int)methodDefHash)));
                instructions.Add(processor.Create(OpCodes.Stloc, methodHashLocIdx));
                // serializer.Serialize(ref methodHash); // NetworkMethodId
                instructions.Add(processor.Create(OpCodes.Ldloc, serializerLocIdx));
                instructions.Add(processor.Create(OpCodes.Ldloca, methodHashLocIdx));
                instructions.Add(processor.Create(OpCodes.Callvirt, BitSerializer_SerializeUint_MethodRef));

                // write method parameters into stream
                for (int paramIndex = 0; paramIndex < paramCount; ++paramIndex)
                {
                    var paramDef = methodDefinition.Parameters[paramIndex];
                    var paramType = paramDef.ParameterType;

                    // C# primitives (+arrays)

                    if (paramType == typeSystem.Boolean)
                    {
                        instructions.Add(processor.Create(OpCodes.Ldloc, serializerLocIdx));
                        instructions.Add(processor.Create(OpCodes.Ldarga, paramIndex + 1));
                        instructions.Add(processor.Create(OpCodes.Callvirt, BitSerializer_SerializeBool_MethodRef));
                        continue;
                    }

                    if (paramType.IsArray && paramType.GetElementType() == typeSystem.Boolean)
                    {
                        instructions.Add(processor.Create(OpCodes.Ldloc, serializerLocIdx));
                        instructions.Add(processor.Create(OpCodes.Ldarga, paramIndex + 1));
                        instructions.Add(processor.Create(OpCodes.Callvirt, BitSerializer_SerializeBoolArray_MethodRef));
                        continue;
                    }

                    if (paramType == typeSystem.Char)
                    {
                        instructions.Add(processor.Create(OpCodes.Ldloc, serializerLocIdx));
                        instructions.Add(processor.Create(OpCodes.Ldarga, paramIndex + 1));
                        instructions.Add(processor.Create(OpCodes.Callvirt, BitSerializer_SerializeChar_MethodRef));
                        continue;
                    }

                    if (paramType.IsArray && paramType.GetElementType() == typeSystem.Char)
                    {
                        instructions.Add(processor.Create(OpCodes.Ldloc, serializerLocIdx));
                        instructions.Add(processor.Create(OpCodes.Ldarga, paramIndex + 1));
                        instructions.Add(processor.Create(OpCodes.Callvirt, BitSerializer_SerializeCharArray_MethodRef));
                        continue;
                    }

                    if (paramType == typeSystem.SByte)
                    {
                        instructions.Add(processor.Create(OpCodes.Ldloc, serializerLocIdx));
                        instructions.Add(processor.Create(OpCodes.Ldarga, paramIndex + 1));
                        instructions.Add(processor.Create(OpCodes.Callvirt, BitSerializer_SerializeSbyte_MethodRef));
                        continue;
                    }

                    if (paramType.IsArray && paramType.GetElementType() == typeSystem.SByte)
                    {
                        instructions.Add(processor.Create(OpCodes.Ldloc, serializerLocIdx));
                        instructions.Add(processor.Create(OpCodes.Ldarga, paramIndex + 1));
                        instructions.Add(processor.Create(OpCodes.Callvirt, BitSerializer_SerializeSbyteArray_MethodRef));
                        continue;
                    }

                    if (paramType == typeSystem.Byte)
                    {
                        instructions.Add(processor.Create(OpCodes.Ldloc, serializerLocIdx));
                        instructions.Add(processor.Create(OpCodes.Ldarga, paramIndex + 1));
                        instructions.Add(processor.Create(OpCodes.Callvirt, BitSerializer_SerializeByte_MethodRef));
                        continue;
                    }

                    if (paramType.IsArray && paramType.GetElementType() == typeSystem.Byte)
                    {
                        instructions.Add(processor.Create(OpCodes.Ldloc, serializerLocIdx));
                        instructions.Add(processor.Create(OpCodes.Ldarga, paramIndex + 1));
                        instructions.Add(processor.Create(OpCodes.Callvirt, BitSerializer_SerializeByteArray_MethodRef));
                        continue;
                    }

                    if (paramType == typeSystem.Int16)
                    {
                        instructions.Add(processor.Create(OpCodes.Ldloc, serializerLocIdx));
                        instructions.Add(processor.Create(OpCodes.Ldarga, paramIndex + 1));
                        instructions.Add(processor.Create(OpCodes.Callvirt, BitSerializer_SerializeShort_MethodRef));
                        continue;
                    }

                    if (paramType.IsArray && paramType.GetElementType() == typeSystem.Int16)
                    {
                        instructions.Add(processor.Create(OpCodes.Ldloc, serializerLocIdx));
                        instructions.Add(processor.Create(OpCodes.Ldarga, paramIndex + 1));
                        instructions.Add(processor.Create(OpCodes.Callvirt, BitSerializer_SerializeShortArray_MethodRef));
                        continue;
                    }

                    if (paramType == typeSystem.UInt16)
                    {
                        instructions.Add(processor.Create(OpCodes.Ldloc, serializerLocIdx));
                        instructions.Add(processor.Create(OpCodes.Ldarga, paramIndex + 1));
                        instructions.Add(processor.Create(OpCodes.Callvirt, BitSerializer_SerializeUshort_MethodRef));
                        continue;
                    }

                    if (paramType.IsArray && paramType.GetElementType() == typeSystem.UInt16)
                    {
                        instructions.Add(processor.Create(OpCodes.Ldloc, serializerLocIdx));
                        instructions.Add(processor.Create(OpCodes.Ldarga, paramIndex + 1));
                        instructions.Add(processor.Create(OpCodes.Callvirt, BitSerializer_SerializeUshortArray_MethodRef));
                        continue;
                    }

                    if (paramType == typeSystem.Int32)
                    {
                        instructions.Add(processor.Create(OpCodes.Ldloc, serializerLocIdx));
                        instructions.Add(processor.Create(OpCodes.Ldarga, paramIndex + 1));
                        instructions.Add(processor.Create(OpCodes.Callvirt, BitSerializer_SerializeInt_MethodRef));
                        continue;
                    }

                    if (paramType.IsArray && paramType.GetElementType() == typeSystem.Int32)
                    {
                        instructions.Add(processor.Create(OpCodes.Ldloc, serializerLocIdx));
                        instructions.Add(processor.Create(OpCodes.Ldarga, paramIndex + 1));
                        instructions.Add(processor.Create(OpCodes.Callvirt, BitSerializer_SerializeIntArray_MethodRef));
                        continue;
                    }

                    if (paramType == typeSystem.UInt32)
                    {
                        instructions.Add(processor.Create(OpCodes.Ldloc, serializerLocIdx));
                        instructions.Add(processor.Create(OpCodes.Ldarga, paramIndex + 1));
                        instructions.Add(processor.Create(OpCodes.Callvirt, BitSerializer_SerializeUint_MethodRef));
                        continue;
                    }

                    if (paramType.IsArray && paramType.GetElementType() == typeSystem.UInt32)
                    {
                        instructions.Add(processor.Create(OpCodes.Ldloc, serializerLocIdx));
                        instructions.Add(processor.Create(OpCodes.Ldarga, paramIndex + 1));
                        instructions.Add(processor.Create(OpCodes.Callvirt, BitSerializer_SerializeUintArray_MethodRef));
                        continue;
                    }

                    if (paramType == typeSystem.Int64)
                    {
                        instructions.Add(processor.Create(OpCodes.Ldloc, serializerLocIdx));
                        instructions.Add(processor.Create(OpCodes.Ldarga, paramIndex + 1));
                        instructions.Add(processor.Create(OpCodes.Callvirt, BitSerializer_SerializeLong_MethodRef));
                        continue;
                    }

                    if (paramType.IsArray && paramType.GetElementType() == typeSystem.Int64)
                    {
                        instructions.Add(processor.Create(OpCodes.Ldloc, serializerLocIdx));
                        instructions.Add(processor.Create(OpCodes.Ldarga, paramIndex + 1));
                        instructions.Add(processor.Create(OpCodes.Callvirt, BitSerializer_SerializeLongArray_MethodRef));
                        continue;
                    }

                    if (paramType == typeSystem.UInt64)
                    {
                        instructions.Add(processor.Create(OpCodes.Ldloc, serializerLocIdx));
                        instructions.Add(processor.Create(OpCodes.Ldarga, paramIndex + 1));
                        instructions.Add(processor.Create(OpCodes.Callvirt, BitSerializer_SerializeUlong_MethodRef));
                        continue;
                    }

                    if (paramType.IsArray && paramType.GetElementType() == typeSystem.UInt64)
                    {
                        instructions.Add(processor.Create(OpCodes.Ldloc, serializerLocIdx));
                        instructions.Add(processor.Create(OpCodes.Ldarga, paramIndex + 1));
                        instructions.Add(processor.Create(OpCodes.Callvirt, BitSerializer_SerializeUlongArray_MethodRef));
                        continue;
                    }

                    if (paramType == typeSystem.Single)
                    {
                        instructions.Add(processor.Create(OpCodes.Ldloc, serializerLocIdx));
                        instructions.Add(processor.Create(OpCodes.Ldarga, paramIndex + 1));
                        instructions.Add(processor.Create(OpCodes.Callvirt, BitSerializer_SerializeFloat_MethodRef));
                        continue;
                    }

                    if (paramType.IsArray && paramType.GetElementType() == typeSystem.Single)
                    {
                        instructions.Add(processor.Create(OpCodes.Ldloc, serializerLocIdx));
                        instructions.Add(processor.Create(OpCodes.Ldarga, paramIndex + 1));
                        instructions.Add(processor.Create(OpCodes.Callvirt, BitSerializer_SerializeFloatArray_MethodRef));
                        continue;
                    }

                    if (paramType == typeSystem.Double)
                    {
                        instructions.Add(processor.Create(OpCodes.Ldloc, serializerLocIdx));
                        instructions.Add(processor.Create(OpCodes.Ldarga, paramIndex + 1));
                        instructions.Add(processor.Create(OpCodes.Callvirt, BitSerializer_SerializeDouble_MethodRef));
                        continue;
                    }

                    if (paramType.IsArray && paramType.GetElementType() == typeSystem.Double)
                    {
                        instructions.Add(processor.Create(OpCodes.Ldloc, serializerLocIdx));
                        instructions.Add(processor.Create(OpCodes.Ldarga, paramIndex + 1));
                        instructions.Add(processor.Create(OpCodes.Callvirt, BitSerializer_SerializeDoubleArray_MethodRef));
                        continue;
                    }

                    if (paramType == typeSystem.String)
                    {
                        instructions.Add(processor.Create(OpCodes.Ldloc, serializerLocIdx));
                        instructions.Add(processor.Create(OpCodes.Ldarga, paramIndex + 1));
                        instructions.Add(processor.Create(OpCodes.Callvirt, BitSerializer_SerializeString_MethodRef));
                        continue;
                    }

                    if (paramType.IsArray && paramType.GetElementType() == typeSystem.String)
                    {
                        instructions.Add(processor.Create(OpCodes.Ldloc, serializerLocIdx));
                        instructions.Add(processor.Create(OpCodes.Ldarga, paramIndex + 1));
                        instructions.Add(processor.Create(OpCodes.Callvirt, BitSerializer_SerializeStringArray_MethodRef));
                        continue;
                    }

                    // Unity primitives (+arrays)

                    if (paramType.FullName == CodeGenHelpers.UnityColor_FullName)
                    {
                        instructions.Add(processor.Create(OpCodes.Ldloc, serializerLocIdx));
                        instructions.Add(processor.Create(OpCodes.Ldarga, paramIndex + 1));
                        instructions.Add(processor.Create(OpCodes.Callvirt, BitSerializer_SerializeColor_MethodRef));
                        continue;
                    }

                    if (paramType.IsArray && paramType.GetElementType().FullName == CodeGenHelpers.UnityColor_FullName)
                    {
                        instructions.Add(processor.Create(OpCodes.Ldloc, serializerLocIdx));
                        instructions.Add(processor.Create(OpCodes.Ldarga, paramIndex + 1));
                        instructions.Add(processor.Create(OpCodes.Callvirt, BitSerializer_SerializeColorArray_MethodRef));
                        continue;
                    }

                    if (paramType.FullName == CodeGenHelpers.UnityColor32_FullName)
                    {
                        instructions.Add(processor.Create(OpCodes.Ldloc, serializerLocIdx));
                        instructions.Add(processor.Create(OpCodes.Ldarga, paramIndex + 1));
                        instructions.Add(processor.Create(OpCodes.Callvirt, BitSerializer_SerializeColor32_MethodRef));
                        continue;
                    }

                    if (paramType.IsArray && paramType.GetElementType().FullName == CodeGenHelpers.UnityColor32_FullName)
                    {
                        instructions.Add(processor.Create(OpCodes.Ldloc, serializerLocIdx));
                        instructions.Add(processor.Create(OpCodes.Ldarga, paramIndex + 1));
                        instructions.Add(processor.Create(OpCodes.Callvirt, BitSerializer_SerializeColor32Array_MethodRef));
                        continue;
                    }

                    if (paramType.FullName == CodeGenHelpers.UnityVector2_FullName)
                    {
                        instructions.Add(processor.Create(OpCodes.Ldloc, serializerLocIdx));
                        instructions.Add(processor.Create(OpCodes.Ldarga, paramIndex + 1));
                        instructions.Add(processor.Create(OpCodes.Callvirt, BitSerializer_SerializeVector2_MethodRef));
                        continue;
                    }

                    if (paramType.IsArray && paramType.GetElementType().FullName == CodeGenHelpers.UnityVector2_FullName)
                    {
                        instructions.Add(processor.Create(OpCodes.Ldloc, serializerLocIdx));
                        instructions.Add(processor.Create(OpCodes.Ldarga, paramIndex + 1));
                        instructions.Add(processor.Create(OpCodes.Callvirt, BitSerializer_SerializeVector2Array_MethodRef));
                        continue;
                    }

                    if (paramType.FullName == CodeGenHelpers.UnityVector3_FullName)
                    {
                        instructions.Add(processor.Create(OpCodes.Ldloc, serializerLocIdx));
                        instructions.Add(processor.Create(OpCodes.Ldarga, paramIndex + 1));
                        instructions.Add(processor.Create(OpCodes.Callvirt, BitSerializer_SerializeVector3_MethodRef));
                        continue;
                    }

                    if (paramType.IsArray && paramType.GetElementType().FullName == CodeGenHelpers.UnityVector3_FullName)
                    {
                        instructions.Add(processor.Create(OpCodes.Ldloc, serializerLocIdx));
                        instructions.Add(processor.Create(OpCodes.Ldarga, paramIndex + 1));
                        instructions.Add(processor.Create(OpCodes.Callvirt, BitSerializer_SerializeVector3Array_MethodRef));
                        continue;
                    }

                    if (paramType.FullName == CodeGenHelpers.UnityVector4_FullName)
                    {
                        instructions.Add(processor.Create(OpCodes.Ldloc, serializerLocIdx));
                        instructions.Add(processor.Create(OpCodes.Ldarga, paramIndex + 1));
                        instructions.Add(processor.Create(OpCodes.Callvirt, BitSerializer_SerializeVector4_MethodRef));
                        continue;
                    }

                    if (paramType.IsArray && paramType.GetElementType().FullName == CodeGenHelpers.UnityVector4_FullName)
                    {
                        instructions.Add(processor.Create(OpCodes.Ldloc, serializerLocIdx));
                        instructions.Add(processor.Create(OpCodes.Ldarga, paramIndex + 1));
                        instructions.Add(processor.Create(OpCodes.Callvirt, BitSerializer_SerializeVector4Array_MethodRef));
                        continue;
                    }

                    if (paramType.FullName == CodeGenHelpers.UnityQuaternion_FullName)
                    {
                        instructions.Add(processor.Create(OpCodes.Ldloc, serializerLocIdx));
                        instructions.Add(processor.Create(OpCodes.Ldarga, paramIndex + 1));
                        instructions.Add(processor.Create(OpCodes.Callvirt, BitSerializer_SerializeQuaternion_MethodRef));
                        continue;
                    }

                    if (paramType.IsArray && paramType.GetElementType().FullName == CodeGenHelpers.UnityQuaternion_FullName)
                    {
                        instructions.Add(processor.Create(OpCodes.Ldloc, serializerLocIdx));
                        instructions.Add(processor.Create(OpCodes.Ldarga, paramIndex + 1));
                        instructions.Add(processor.Create(OpCodes.Callvirt, BitSerializer_SerializeQuaternionArray_MethodRef));
                        continue;
                    }

                    if (paramType.FullName == CodeGenHelpers.UnityRay_FullName)
                    {
                        instructions.Add(processor.Create(OpCodes.Ldloc, serializerLocIdx));
                        instructions.Add(processor.Create(OpCodes.Ldarga, paramIndex + 1));
                        instructions.Add(processor.Create(OpCodes.Callvirt, BitSerializer_SerializeRay_MethodRef));
                        continue;
                    }

                    if (paramType.IsArray && paramType.GetElementType().FullName == CodeGenHelpers.UnityRay_FullName)
                    {
                        instructions.Add(processor.Create(OpCodes.Ldloc, serializerLocIdx));
                        instructions.Add(processor.Create(OpCodes.Ldarga, paramIndex + 1));
                        instructions.Add(processor.Create(OpCodes.Callvirt, BitSerializer_SerializeRayArray_MethodRef));
                        continue;
                    }

                    if (paramType.FullName == CodeGenHelpers.UnityRay2D_FullName)
                    {
                        instructions.Add(processor.Create(OpCodes.Ldloc, serializerLocIdx));
                        instructions.Add(processor.Create(OpCodes.Ldarga, paramIndex + 1));
                        instructions.Add(processor.Create(OpCodes.Callvirt, BitSerializer_SerializeRay2D_MethodRef));
                        continue;
                    }

                    if (paramType.IsArray && paramType.GetElementType().FullName == CodeGenHelpers.UnityRay2D_FullName)
                    {
                        instructions.Add(processor.Create(OpCodes.Ldloc, serializerLocIdx));
                        instructions.Add(processor.Create(OpCodes.Ldarga, paramIndex + 1));
                        instructions.Add(processor.Create(OpCodes.Callvirt, BitSerializer_SerializeRay2DArray_MethodRef));
                        continue;
                    }

                    // Enum // todo: (+arrays)

                    {
                        var paramEnumIntType = paramType.GetEnumAsInt();
                        if (paramEnumIntType != null)
                        {
                            instructions.Add(processor.Create(OpCodes.Ldloc, serializerLocIdx));
                            instructions.Add(processor.Create(OpCodes.Ldarga, paramIndex + 1));

                            if (paramEnumIntType == typeSystem.Int32)
                            {
                                instructions.Add(processor.Create(OpCodes.Callvirt, BitSerializer_SerializeInt_MethodRef));
                                continue;
                            }

                            if (paramEnumIntType == typeSystem.UInt32)
                            {
                                instructions.Add(processor.Create(OpCodes.Callvirt, BitSerializer_SerializeUint_MethodRef));
                                continue;
                            }

                            if (paramEnumIntType == typeSystem.Byte)
                            {
                                instructions.Add(processor.Create(OpCodes.Callvirt, BitSerializer_SerializeByte_MethodRef));
                                continue;
                            }

                            if (paramEnumIntType == typeSystem.SByte)
                            {
                                instructions.Add(processor.Create(OpCodes.Callvirt, BitSerializer_SerializeSbyte_MethodRef));
                                continue;
                            }

                            if (paramEnumIntType == typeSystem.Int16)
                            {
                                instructions.Add(processor.Create(OpCodes.Callvirt, BitSerializer_SerializeShort_MethodRef));
                                continue;
                            }

                            if (paramEnumIntType == typeSystem.UInt16)
                            {
                                instructions.Add(processor.Create(OpCodes.Callvirt, BitSerializer_SerializeUshort_MethodRef));
                                continue;
                            }

                            if (paramEnumIntType == typeSystem.Int64)
                            {
                                instructions.Add(processor.Create(OpCodes.Callvirt, BitSerializer_SerializeLong_MethodRef));
                                continue;
                            }

                            if (paramEnumIntType == typeSystem.UInt64)
                            {
                                instructions.Add(processor.Create(OpCodes.Callvirt, BitSerializer_SerializeUlong_MethodRef));
                                continue;
                            }
                        }
                    }

                    // NetworkObject & NetworkBehaviour (+arrays)

                    if (paramType.FullName == CodeGenHelpers.NetworkObject_FullName)
                    {
                        instructions.Add(processor.Create(OpCodes.Ldloc, serializerLocIdx));
                        instructions.Add(processor.Create(OpCodes.Ldarga, paramIndex + 1));
                        instructions.Add(processor.Create(OpCodes.Callvirt, BitSerializer_SerializeNetObject_MethodRef));
                        continue;
                    }

                    if (paramType.IsArray && paramType.GetElementType().FullName == CodeGenHelpers.NetworkObject_FullName)
                    {
                        instructions.Add(processor.Create(OpCodes.Ldloc, serializerLocIdx));
                        instructions.Add(processor.Create(OpCodes.Ldarga, paramIndex + 1));
                        instructions.Add(processor.Create(OpCodes.Callvirt, BitSerializer_SerializeNetObjectArray_MethodRef));
                        continue;
                    }

                    if (paramType.FullName == CodeGenHelpers.NetworkBehaviour_FullName)
                    {
                        instructions.Add(processor.Create(OpCodes.Ldloc, serializerLocIdx));
                        instructions.Add(processor.Create(OpCodes.Ldarga, paramIndex + 1));
                        instructions.Add(processor.Create(OpCodes.Callvirt, BitSerializer_SerializeNetBehaviour_MethodRef));
                        continue;
                    }

                    if (paramType.IsArray && paramType.GetElementType().FullName == CodeGenHelpers.NetworkBehaviour_FullName)
                    {
                        instructions.Add(processor.Create(OpCodes.Ldloc, serializerLocIdx));
                        instructions.Add(processor.Create(OpCodes.Ldarga, paramIndex + 1));
                        instructions.Add(processor.Create(OpCodes.Callvirt, BitSerializer_SerializeNetBehaviourArray_MethodRef));
                        continue;
                    }

                    // INetworkSerializable // todo: (+arrays)

                    if (paramType.HasInterface(CodeGenHelpers.INetworkSerializable_FullName))
                    {
                        var paramTypeDef = paramType.Resolve();
                        var paramTypeNetworkSerialize_MethodDef = paramTypeDef.Methods.FirstOrDefault(m => m.Name == CodeGenHelpers.INetworkSerializable_NetworkSerialize_Name);
                        if (paramTypeNetworkSerialize_MethodDef != null)
                        {
                            if (paramType.IsValueType)
                            {
                                // struct (pass by value)
                                instructions.Add(processor.Create(OpCodes.Ldarga, paramIndex + 1));
                                instructions.Add(processor.Create(OpCodes.Ldloc, serializerLocIdx));
                                instructions.Add(processor.Create(OpCodes.Call, paramTypeNetworkSerialize_MethodDef));
                            }
                            else
                            {
                                // class (pass by reference)
                                instructions.Add(processor.Create(OpCodes.Ldarg, paramIndex + 1));
                                instructions.Add(processor.Create(OpCodes.Ldloc, serializerLocIdx));
                                instructions.Add(processor.Create(OpCodes.Callvirt, paramTypeNetworkSerialize_MethodDef));
                            }

                            continue;
                        }
                    }
                }

                instructions.Add(endInstr);

                // EndSendServerRpc(serializer, sendParams, isReliable) -> ServerRpc
                // EndSendClientRpc(serializer, sendParams, isReliable) -> ClientRpc
                if (isServerRpc)
                {
                    // ServerRpc
                    // EndSendServerRpc(serializer, sendParams, isReliable);
                    instructions.Add(processor.Create(OpCodes.Ldarg_0));

                    // serializer
                    instructions.Add(processor.Create(OpCodes.Ldloc, serializerLocIdx));

                    if (hasRpcParams)
                    {
                        // rpcParams.Send
                        instructions.Add(processor.Create(OpCodes.Ldarg, paramCount));
                        instructions.Add(processor.Create(OpCodes.Ldfld, ServerRpcParams_Send_FieldRef));
                    }
                    else
                    {
                        // default
                        instructions.Add(processor.Create(OpCodes.Ldloc, sendParamsIdx));
                    }

                    // isReliable
                    instructions.Add(processor.Create(isReliableRpc ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0));

                    // EndSendServerRpc
                    instructions.Add(processor.Create(OpCodes.Call, NetworkBehaviour_EndSendServerRpc_MethodRef));
                }
                else
                {
                    // ClientRpc
                    // EndSendClientRpc(serializer, sendParams, isReliable);
                    instructions.Add(processor.Create(OpCodes.Ldarg_0));

                    // serializer
                    instructions.Add(processor.Create(OpCodes.Ldloc, serializerLocIdx));

                    if (hasRpcParams)
                    {
                        // rpcParams.Send
                        instructions.Add(processor.Create(OpCodes.Ldarg, paramCount));
                        instructions.Add(processor.Create(OpCodes.Ldfld, ClientRpcParams_Send_FieldRef));
                    }
                    else
                    {
                        // default
                        instructions.Add(processor.Create(OpCodes.Ldloc, sendParamsIdx));
                    }

                    // isReliable
                    instructions.Add(processor.Create(isReliableRpc ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0));

                    // EndSendClientRpc
                    instructions.Add(processor.Create(OpCodes.Call, NetworkBehaviour_EndSendClientRpc_MethodRef));
                }

                instructions.Add(lastInstr);
            }

            {
                var returnInstr = processor.Create(OpCodes.Ret);
                var lastInstr = processor.Create(OpCodes.Nop);

                // if (__nexec == NExec.Server) -> ServerRpc
                // if (__nexec == NExec.Client) -> ClientRpc
                instructions.Add(processor.Create(OpCodes.Ldarg_0));
                instructions.Add(processor.Create(OpCodes.Ldfld, NetworkBehaviour_nexec_FieldRef));
#pragma warning disable 618
                instructions.Add(processor.Create(OpCodes.Ldc_I4, (int)(isServerRpc ? NetworkedBehaviour.__NExec.Server : NetworkedBehaviour.__NExec.Client)));
#pragma warning restore 618
                instructions.Add(processor.Create(OpCodes.Ceq));
                instructions.Add(processor.Create(OpCodes.Brfalse, returnInstr));

                // if (networkManager.IsServer || networkManager.IsHost) -> ServerRpc
                // if (networkManager.IsClient || networkManager.IsHost) -> ClientRpc
                instructions.Add(processor.Create(OpCodes.Ldloc, netManLocIdx));
                instructions.Add(processor.Create(OpCodes.Callvirt, isServerRpc ? NetworkManager_getIsServer_MethodRef : NetworkManager_getIsClient_MethodRef));
                instructions.Add(processor.Create(OpCodes.Brtrue, lastInstr));
                instructions.Add(processor.Create(OpCodes.Ldloc, netManLocIdx));
                instructions.Add(processor.Create(OpCodes.Callvirt, NetworkManager_getIsHost_MethodRef));
                instructions.Add(processor.Create(OpCodes.Brtrue, lastInstr));

                instructions.Add(returnInstr);
                instructions.Add(lastInstr);
            }

            instructions.Reverse();
            instructions.ForEach(instruction => processor.Body.Instructions.Insert(0, instruction));
        }

        private MethodDefinition GenerateStaticHandler(MethodDefinition methodDefinition, CustomAttribute rpcAttribute)
        {
            var typeSystem = methodDefinition.Module.TypeSystem;
            var nhandler = new MethodDefinition(
                $"{methodDefinition.Name}__nhandler",
                MethodAttributes.Private | MethodAttributes.Static | MethodAttributes.HideBySig,
                methodDefinition.Module.TypeSystem.Void);
            nhandler.Parameters.Add(new ParameterDefinition("target", ParameterAttributes.None, NetworkBehaviour_TypeRef));
            nhandler.Parameters.Add(new ParameterDefinition("serializer", ParameterAttributes.None, BitSerializer_TypeRef));
            nhandler.Parameters.Add(new ParameterDefinition("sender", ParameterAttributes.None, typeSystem.UInt64));

            var processor = nhandler.Body.GetILProcessor();
            var isServerRpc = rpcAttribute.AttributeType.FullName == CodeGenHelpers.ServerRpcAttribute_FullName;

            nhandler.Body.InitLocals = true;
            // read method parameters from stream
            int paramCount = methodDefinition.Parameters.Count;
            for (int paramIndex = 0; paramIndex < paramCount; ++paramIndex)
            {
                var paramDef = methodDefinition.Parameters[paramIndex];
                var paramType = paramDef.ParameterType;

                // local variable to storage argument
                nhandler.Body.Variables.Add(new VariableDefinition(paramType));

                // C# primitives (+arrays)

                if (paramType == typeSystem.Boolean)
                {
                    processor.Emit(OpCodes.Ldarg_1);
                    processor.Emit(OpCodes.Ldloca, paramIndex);
                    processor.Emit(OpCodes.Callvirt, BitSerializer_SerializeBool_MethodRef);
                    continue;
                }

                if (paramType.IsArray && paramType.GetElementType() == typeSystem.Boolean)
                {
                    processor.Emit(OpCodes.Ldarg_1);
                    processor.Emit(OpCodes.Ldloca, paramIndex);
                    processor.Emit(OpCodes.Callvirt, BitSerializer_SerializeBoolArray_MethodRef);
                    continue;
                }

                if (paramType == typeSystem.Char)
                {
                    processor.Emit(OpCodes.Ldarg_1);
                    processor.Emit(OpCodes.Ldloca, paramIndex);
                    processor.Emit(OpCodes.Callvirt, BitSerializer_SerializeChar_MethodRef);
                    continue;
                }

                if (paramType.IsArray && paramType.GetElementType() == typeSystem.Char)
                {
                    processor.Emit(OpCodes.Ldarg_1);
                    processor.Emit(OpCodes.Ldloca, paramIndex);
                    processor.Emit(OpCodes.Callvirt, BitSerializer_SerializeCharArray_MethodRef);
                    continue;
                }

                if (paramType == typeSystem.SByte)
                {
                    processor.Emit(OpCodes.Ldarg_1);
                    processor.Emit(OpCodes.Ldloca, paramIndex);
                    processor.Emit(OpCodes.Callvirt, BitSerializer_SerializeSbyte_MethodRef);
                    continue;
                }

                if (paramType.IsArray && paramType.GetElementType() == typeSystem.SByte)
                {
                    processor.Emit(OpCodes.Ldarg_1);
                    processor.Emit(OpCodes.Ldloca, paramIndex);
                    processor.Emit(OpCodes.Callvirt, BitSerializer_SerializeSbyteArray_MethodRef);
                    continue;
                }

                if (paramType == typeSystem.Byte)
                {
                    processor.Emit(OpCodes.Ldarg_1);
                    processor.Emit(OpCodes.Ldloca, paramIndex);
                    processor.Emit(OpCodes.Callvirt, BitSerializer_SerializeByte_MethodRef);
                    continue;
                }

                if (paramType.IsArray && paramType.GetElementType() == typeSystem.Byte)
                {
                    processor.Emit(OpCodes.Ldarg_1);
                    processor.Emit(OpCodes.Ldloca, paramIndex);
                    processor.Emit(OpCodes.Callvirt, BitSerializer_SerializeByteArray_MethodRef);
                    continue;
                }

                if (paramType == typeSystem.Int16)
                {
                    processor.Emit(OpCodes.Ldarg_1);
                    processor.Emit(OpCodes.Ldloca, paramIndex);
                    processor.Emit(OpCodes.Callvirt, BitSerializer_SerializeShort_MethodRef);
                    continue;
                }

                if (paramType.IsArray && paramType.GetElementType() == typeSystem.Int16)
                {
                    processor.Emit(OpCodes.Ldarg_1);
                    processor.Emit(OpCodes.Ldloca, paramIndex);
                    processor.Emit(OpCodes.Callvirt, BitSerializer_SerializeShortArray_MethodRef);
                    continue;
                }

                if (paramType == typeSystem.UInt16)
                {
                    processor.Emit(OpCodes.Ldarg_1);
                    processor.Emit(OpCodes.Ldloca, paramIndex);
                    processor.Emit(OpCodes.Callvirt, BitSerializer_SerializeUshort_MethodRef);
                    continue;
                }

                if (paramType.IsArray && paramType.GetElementType() == typeSystem.UInt16)
                {
                    processor.Emit(OpCodes.Ldarg_1);
                    processor.Emit(OpCodes.Ldloca, paramIndex);
                    processor.Emit(OpCodes.Callvirt, BitSerializer_SerializeUshortArray_MethodRef);
                    continue;
                }

                if (paramType == typeSystem.Int32)
                {
                    processor.Emit(OpCodes.Ldarg_1);
                    processor.Emit(OpCodes.Ldloca, paramIndex);
                    processor.Emit(OpCodes.Callvirt, BitSerializer_SerializeInt_MethodRef);
                    continue;
                }

                if (paramType.IsArray && paramType.GetElementType() == typeSystem.Int32)
                {
                    processor.Emit(OpCodes.Ldarg_1);
                    processor.Emit(OpCodes.Ldloca, paramIndex);
                    processor.Emit(OpCodes.Callvirt, BitSerializer_SerializeIntArray_MethodRef);
                    continue;
                }

                if (paramType == typeSystem.UInt32)
                {
                    processor.Emit(OpCodes.Ldarg_1);
                    processor.Emit(OpCodes.Ldloca, paramIndex);
                    processor.Emit(OpCodes.Callvirt, BitSerializer_SerializeUint_MethodRef);
                    continue;
                }

                if (paramType.IsArray && paramType.GetElementType() == typeSystem.UInt32)
                {
                    processor.Emit(OpCodes.Ldarg_1);
                    processor.Emit(OpCodes.Ldloca, paramIndex);
                    processor.Emit(OpCodes.Callvirt, BitSerializer_SerializeUintArray_MethodRef);
                    continue;
                }

                if (paramType == typeSystem.Int64)
                {
                    processor.Emit(OpCodes.Ldarg_1);
                    processor.Emit(OpCodes.Ldloca, paramIndex);
                    processor.Emit(OpCodes.Callvirt, BitSerializer_SerializeLong_MethodRef);
                    continue;
                }

                if (paramType.IsArray && paramType.GetElementType() == typeSystem.Int64)
                {
                    processor.Emit(OpCodes.Ldarg_1);
                    processor.Emit(OpCodes.Ldloca, paramIndex);
                    processor.Emit(OpCodes.Callvirt, BitSerializer_SerializeLongArray_MethodRef);
                    continue;
                }

                if (paramType == typeSystem.UInt64)
                {
                    processor.Emit(OpCodes.Ldarg_1);
                    processor.Emit(OpCodes.Ldloca, paramIndex);
                    processor.Emit(OpCodes.Callvirt, BitSerializer_SerializeUlong_MethodRef);
                    continue;
                }

                if (paramType.IsArray && paramType.GetElementType() == typeSystem.UInt64)
                {
                    processor.Emit(OpCodes.Ldarg_1);
                    processor.Emit(OpCodes.Ldloca, paramIndex);
                    processor.Emit(OpCodes.Callvirt, BitSerializer_SerializeUlongArray_MethodRef);
                    continue;
                }

                if (paramType == typeSystem.Single)
                {
                    processor.Emit(OpCodes.Ldarg_1);
                    processor.Emit(OpCodes.Ldloca, paramIndex);
                    processor.Emit(OpCodes.Callvirt, BitSerializer_SerializeFloat_MethodRef);
                    continue;
                }

                if (paramType.IsArray && paramType.GetElementType() == typeSystem.Single)
                {
                    processor.Emit(OpCodes.Ldarg_1);
                    processor.Emit(OpCodes.Ldloca, paramIndex);
                    processor.Emit(OpCodes.Callvirt, BitSerializer_SerializeFloatArray_MethodRef);
                    continue;
                }

                if (paramType == typeSystem.Double)
                {
                    processor.Emit(OpCodes.Ldarg_1);
                    processor.Emit(OpCodes.Ldloca, paramIndex);
                    processor.Emit(OpCodes.Callvirt, BitSerializer_SerializeDouble_MethodRef);
                    continue;
                }

                if (paramType.IsArray && paramType.GetElementType() == typeSystem.Double)
                {
                    processor.Emit(OpCodes.Ldarg_1);
                    processor.Emit(OpCodes.Ldloca, paramIndex);
                    processor.Emit(OpCodes.Callvirt, BitSerializer_SerializeDoubleArray_MethodRef);
                    continue;
                }

                if (paramType == typeSystem.String)
                {
                    processor.Emit(OpCodes.Ldarg_1);
                    processor.Emit(OpCodes.Ldloca, paramIndex);
                    processor.Emit(OpCodes.Callvirt, BitSerializer_SerializeString_MethodRef);
                    continue;
                }

                if (paramType.IsArray && paramType.GetElementType() == typeSystem.String)
                {
                    processor.Emit(OpCodes.Ldarg_1);
                    processor.Emit(OpCodes.Ldloca, paramIndex);
                    processor.Emit(OpCodes.Callvirt, BitSerializer_SerializeStringArray_MethodRef);
                    continue;
                }

                // Unity primitives (+arrays)

                if (paramType.FullName == CodeGenHelpers.UnityColor_FullName)
                {
                    processor.Emit(OpCodes.Ldarg_1);
                    processor.Emit(OpCodes.Ldloca, paramIndex);
                    processor.Emit(OpCodes.Callvirt, BitSerializer_SerializeColor_MethodRef);
                    continue;
                }

                if (paramType.IsArray && paramType.GetElementType().FullName == CodeGenHelpers.UnityColor_FullName)
                {
                    processor.Emit(OpCodes.Ldarg_1);
                    processor.Emit(OpCodes.Ldloca, paramIndex);
                    processor.Emit(OpCodes.Callvirt, BitSerializer_SerializeColorArray_MethodRef);
                    continue;
                }

                if (paramType.FullName == CodeGenHelpers.UnityColor32_FullName)
                {
                    processor.Emit(OpCodes.Ldarg_1);
                    processor.Emit(OpCodes.Ldloca, paramIndex);
                    processor.Emit(OpCodes.Callvirt, BitSerializer_SerializeColor32_MethodRef);
                    continue;
                }

                if (paramType.IsArray && paramType.GetElementType().FullName == CodeGenHelpers.UnityColor32_FullName)
                {
                    processor.Emit(OpCodes.Ldarg_1);
                    processor.Emit(OpCodes.Ldloca, paramIndex);
                    processor.Emit(OpCodes.Callvirt, BitSerializer_SerializeColor32Array_MethodRef);
                    continue;
                }

                if (paramType.FullName == CodeGenHelpers.UnityVector2_FullName)
                {
                    processor.Emit(OpCodes.Ldarg_1);
                    processor.Emit(OpCodes.Ldloca, paramIndex);
                    processor.Emit(OpCodes.Callvirt, BitSerializer_SerializeVector2_MethodRef);
                    continue;
                }

                if (paramType.IsArray && paramType.GetElementType().FullName == CodeGenHelpers.UnityVector2_FullName)
                {
                    processor.Emit(OpCodes.Ldarg_1);
                    processor.Emit(OpCodes.Ldloca, paramIndex);
                    processor.Emit(OpCodes.Callvirt, BitSerializer_SerializeVector2Array_MethodRef);
                    continue;
                }

                if (paramType.FullName == CodeGenHelpers.UnityVector3_FullName)
                {
                    processor.Emit(OpCodes.Ldarg_1);
                    processor.Emit(OpCodes.Ldloca, paramIndex);
                    processor.Emit(OpCodes.Callvirt, BitSerializer_SerializeVector3_MethodRef);
                    continue;
                }

                if (paramType.IsArray && paramType.GetElementType().FullName == CodeGenHelpers.UnityVector3_FullName)
                {
                    processor.Emit(OpCodes.Ldarg_1);
                    processor.Emit(OpCodes.Ldloca, paramIndex);
                    processor.Emit(OpCodes.Callvirt, BitSerializer_SerializeVector3Array_MethodRef);
                    continue;
                }

                if (paramType.FullName == CodeGenHelpers.UnityVector4_FullName)
                {
                    processor.Emit(OpCodes.Ldarg_1);
                    processor.Emit(OpCodes.Ldloca, paramIndex);
                    processor.Emit(OpCodes.Callvirt, BitSerializer_SerializeVector4_MethodRef);
                    continue;
                }

                if (paramType.IsArray && paramType.GetElementType().FullName == CodeGenHelpers.UnityVector4_FullName)
                {
                    processor.Emit(OpCodes.Ldarg_1);
                    processor.Emit(OpCodes.Ldloca, paramIndex);
                    processor.Emit(OpCodes.Callvirt, BitSerializer_SerializeVector4Array_MethodRef);
                    continue;
                }

                if (paramType.FullName == CodeGenHelpers.UnityQuaternion_FullName)
                {
                    processor.Emit(OpCodes.Ldarg_1);
                    processor.Emit(OpCodes.Ldloca, paramIndex);
                    processor.Emit(OpCodes.Callvirt, BitSerializer_SerializeQuaternion_MethodRef);
                    continue;
                }

                if (paramType.IsArray && paramType.GetElementType().FullName == CodeGenHelpers.UnityQuaternion_FullName)
                {
                    processor.Emit(OpCodes.Ldarg_1);
                    processor.Emit(OpCodes.Ldloca, paramIndex);
                    processor.Emit(OpCodes.Callvirt, BitSerializer_SerializeQuaternionArray_MethodRef);
                    continue;
                }

                if (paramType.FullName == CodeGenHelpers.UnityRay_FullName)
                {
                    processor.Emit(OpCodes.Ldarg_1);
                    processor.Emit(OpCodes.Ldloca, paramIndex);
                    processor.Emit(OpCodes.Callvirt, BitSerializer_SerializeRay_MethodRef);
                    continue;
                }

                if (paramType.IsArray && paramType.GetElementType().FullName == CodeGenHelpers.UnityRay_FullName)
                {
                    processor.Emit(OpCodes.Ldarg_1);
                    processor.Emit(OpCodes.Ldloca, paramIndex);
                    processor.Emit(OpCodes.Callvirt, BitSerializer_SerializeRayArray_MethodRef);
                    continue;
                }

                if (paramType.FullName == CodeGenHelpers.UnityRay2D_FullName)
                {
                    processor.Emit(OpCodes.Ldarg_1);
                    processor.Emit(OpCodes.Ldloca, paramIndex);
                    processor.Emit(OpCodes.Callvirt, BitSerializer_SerializeRay2D_MethodRef);
                    continue;
                }

                if (paramType.IsArray && paramType.GetElementType().FullName == CodeGenHelpers.UnityRay2D_FullName)
                {
                    processor.Emit(OpCodes.Ldarg_1);
                    processor.Emit(OpCodes.Ldloca, paramIndex);
                    processor.Emit(OpCodes.Callvirt, BitSerializer_SerializeRay2DArray_MethodRef);
                    continue;
                }

                // Enum // todo: (+arrays)

                {
                    var paramEnumIntType = paramType.GetEnumAsInt();
                    if (paramEnumIntType != null)
                    {
                        processor.Emit(OpCodes.Ldarg_1);
                        processor.Emit(OpCodes.Ldloca, paramIndex);

                        if (paramEnumIntType == typeSystem.Int32)
                        {
                            processor.Emit(OpCodes.Callvirt, BitSerializer_SerializeInt_MethodRef);
                            continue;
                        }

                        if (paramEnumIntType == typeSystem.UInt32)
                        {
                            processor.Emit(OpCodes.Callvirt, BitSerializer_SerializeUint_MethodRef);
                            continue;
                        }

                        if (paramEnumIntType == typeSystem.Byte)
                        {
                            processor.Emit(OpCodes.Callvirt, BitSerializer_SerializeByte_MethodRef);
                            continue;
                        }

                        if (paramEnumIntType == typeSystem.SByte)
                        {
                            processor.Emit(OpCodes.Callvirt, BitSerializer_SerializeSbyte_MethodRef);
                            continue;
                        }

                        if (paramEnumIntType == typeSystem.Int16)
                        {
                            processor.Emit(OpCodes.Callvirt, BitSerializer_SerializeShort_MethodRef);
                            continue;
                        }

                        if (paramEnumIntType == typeSystem.UInt16)
                        {
                            processor.Emit(OpCodes.Callvirt, BitSerializer_SerializeUshort_MethodRef);
                            continue;
                        }

                        if (paramEnumIntType == typeSystem.Int64)
                        {
                            processor.Emit(OpCodes.Callvirt, BitSerializer_SerializeLong_MethodRef);
                            continue;
                        }

                        if (paramEnumIntType == typeSystem.UInt64)
                        {
                            processor.Emit(OpCodes.Callvirt, BitSerializer_SerializeUlong_MethodRef);
                            continue;
                        }
                    }
                }

                // NetworkObject & NetworkBehaviour (+arrays)

                if (paramType.FullName == CodeGenHelpers.NetworkObject_FullName)
                {
                    processor.Emit(OpCodes.Ldarg_1);
                    processor.Emit(OpCodes.Ldloca, paramIndex);
                    processor.Emit(OpCodes.Callvirt, BitSerializer_SerializeNetObject_MethodRef);
                    continue;
                }

                if (paramType.IsArray && paramType.GetElementType().FullName == CodeGenHelpers.NetworkObject_FullName)
                {
                    processor.Emit(OpCodes.Ldarg_1);
                    processor.Emit(OpCodes.Ldloca, paramIndex);
                    processor.Emit(OpCodes.Callvirt, BitSerializer_SerializeNetObjectArray_MethodRef);
                    continue;
                }

                if (paramType.FullName == CodeGenHelpers.NetworkBehaviour_FullName)
                {
                    processor.Emit(OpCodes.Ldarg_1);
                    processor.Emit(OpCodes.Ldloca, paramIndex);
                    processor.Emit(OpCodes.Callvirt, BitSerializer_SerializeNetBehaviour_MethodRef);
                    continue;
                }

                if (paramType.IsArray && paramType.GetElementType().FullName == CodeGenHelpers.NetworkBehaviour_FullName)
                {
                    processor.Emit(OpCodes.Ldarg_1);
                    processor.Emit(OpCodes.Ldloca, paramIndex);
                    processor.Emit(OpCodes.Callvirt, BitSerializer_SerializeNetBehaviourArray_MethodRef);
                    continue;
                }

                // INetworkSerializable // todo: (+arrays)

                if (paramType.HasInterface(CodeGenHelpers.INetworkSerializable_FullName))
                {
                    var paramTypeDef = paramType.Resolve();
                    var paramTypeNetworkSerialize_MethodDef = paramTypeDef.Methods.FirstOrDefault(m => m.Name == CodeGenHelpers.INetworkSerializable_NetworkSerialize_Name);
                    if (paramTypeNetworkSerialize_MethodDef != null)
                    {
                        if (paramType.IsValueType)
                        {
                            // struct (pass by value)
                            processor.Emit(OpCodes.Ldloca, paramIndex);
                            processor.Emit(OpCodes.Ldarg_1);
                            processor.Emit(OpCodes.Call, paramTypeNetworkSerialize_MethodDef);
                        }
                        else
                        {
                            // class (pass by reference)
                            var paramTypeDefCtor = paramTypeDef.GetConstructors().FirstOrDefault(m => m.Parameters.Count == 0);
                            if (paramTypeDefCtor != null)
                            {
                                // new INetworkSerializable()
                                processor.Emit(OpCodes.Newobj, paramTypeDefCtor);
                                processor.Emit(OpCodes.Stloc, paramIndex);

                                // INetworkSerializable.NetworkSerialize(serializer)
                                processor.Emit(OpCodes.Ldloc, paramIndex);
                                processor.Emit(OpCodes.Ldarg_1);
                                processor.Emit(OpCodes.Callvirt, paramTypeNetworkSerialize_MethodDef);
                            }
                        }

                        continue;
                    }
                }

                // ServerRpcParams, ClientRpcParams
                {
                    // ServerRpcParams
                    if (paramType.FullName == CodeGenHelpers.ServerRpcParams_FullName)
                    {
                        processor.Emit(OpCodes.Ldloca, paramIndex);
                        processor.Emit(OpCodes.Ldflda, ServerRpcParams_Receive_FieldRef);
                        processor.Emit(OpCodes.Ldarg_2);
                        processor.Emit(OpCodes.Stfld, ServerRpcReceiveParams_SenderClientId_FieldRef);
                        continue;
                    }

                    // ClientRpcParams
                    if (paramType.FullName == CodeGenHelpers.ClientRpcParams_FullName)
                    {
                        continue;
                    }
                }
            }

            // NetworkBehaviour.__nexec = NExec.Server; -> ServerRpc
            // NetworkBehaviour.__nexec = NExec.Client; -> ClientRpc
            processor.Emit(OpCodes.Ldarg_0);
#pragma warning disable 618
            processor.Emit(OpCodes.Ldc_I4, (int)(isServerRpc ? NetworkedBehaviour.__NExec.Server : NetworkedBehaviour.__NExec.Client));
#pragma warning restore 618
            processor.Emit(OpCodes.Stfld, NetworkBehaviour_nexec_FieldRef);

            // NetworkBehaviour.XXXRpc(...);
            processor.Emit(OpCodes.Ldarg_0);
            processor.Emit(OpCodes.Castclass, methodDefinition.DeclaringType);
            Enumerable.Range(0, paramCount).ToList().ForEach(paramIndex => processor.Emit(OpCodes.Ldloc, paramIndex));
            processor.Emit(OpCodes.Callvirt, methodDefinition);

            // NetworkBehaviour.__nexec = NExec.None;
            processor.Emit(OpCodes.Ldarg_0);
#pragma warning disable 618
            processor.Emit(OpCodes.Ldc_I4, (int)NetworkedBehaviour.__NExec.None);
#pragma warning restore 618
            processor.Emit(OpCodes.Stfld, NetworkBehaviour_nexec_FieldRef);

            processor.Emit(OpCodes.Ret);
            return nhandler;
        }
    }
}