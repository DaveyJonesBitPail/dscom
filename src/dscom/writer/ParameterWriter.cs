// Copyright 2022 dSPACE GmbH, Mark Lechtermann, Matthias Nissen and Contributors
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//     http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System.Reflection;
using System.Runtime.InteropServices;

namespace dSPACE.Runtime.InteropServices.Writer;

internal sealed class ParameterWriter : ElemDescBasedWriter
{
    public ParameterWriter(MethodWriter methodWriter, ParameterInfo parameterInfo, WriterContext context, bool isTransformedOutParameter)
    : base(parameterInfo.ParameterType, parameterInfo, methodWriter.MethodInfo.ReflectedType!, methodWriter.InterfaceWriter.TypeInfo, context)
    {
        _isTransformedOutParameter = isTransformedOutParameter;
        ParameterInfo = parameterInfo;
        TypeProvider = new TypeProvider(context, parameterInfo);
    }

    private readonly bool _isTransformedOutParameter;

    private bool ParameterTypeIsComVisible => ParameterInfo.ParameterType.IsComVisible();

    private PARAMFLAG _paramFlags = PARAMFLAG.PARAMFLAG_NONE;

    public ParameterInfo ParameterInfo { get; }

    /// <summary>
    /// Gets a value indicating whether this instance of ParameterWriter is valid to generate a PARAMDESC.
    /// If this instance is not valid, the owning method should no be created.
    /// </summary>
    /// <value>true if valid; otherwise false</value>
    public bool IsValid
    {
        get
        {
            var unrefedType = ParameterInfo.ParameterType.IsByRef ? ParameterInfo.ParameterType.GetElementType()! : ParameterInfo.ParameterType;

            // Marshal errors
            if ((GetVariantType(unrefedType) == VarEnum.VT_EMPTY)
                || (unrefedType.IsArray && GetVariantType(unrefedType.GetElementType()!, true) == VarEnum.VT_EMPTY))
            {
                return false;
            }

            var underlayingType = ParameterInfo.ParameterType.GetUnderlayingType();
            if (underlayingType.IsSpecialHandledClass() || underlayingType.IsSpecialHandledValueType())
            {
                return true;
            }
            else if (underlayingType.IsEnum)
            {
                if (GetVariantType(underlayingType) == VarEnum.VT_USERDEFINED)
                {
                    return ParameterTypeIsComVisible && ParameterIsResolvable;
                }
                else
                {
                    return true;
                }
            }
            else if (!underlayingType.IsGenericType && (ParameterIsResolvable || underlayingType.IsInterface || underlayingType.IsClass))
            {
                return true;
            }

            return false;
        }
    }

    public override void ReportEvent()
    {
        base.ReportEvent();
        if (!ParameterIsResolvable && Type.GetUnderlayingType().IsInterface)
        {
            Context.LogWarning($"Warning: Type library exporter could not find the type library for {Type.GetUnderlayingType()} while writing member {ParentType}.{ParameterInfo.Member.Name}. IUnknown was substituted for the interface.", HRESULT.TLBX_I_USEIUNKNOWN);
        }
    }

    private void CalculateFlags()
    {
        var hasInAttribute = ParameterInfo.Member != null && ParameterInfo.GetCustomAttribute<InAttribute>() != null;
        if (ParameterInfo.ParameterType.IsPointer || ParameterInfo.IsOut || (ParameterInfo.ParameterType.IsByRef && !hasInAttribute) || _isTransformedOutParameter)
        {
            IDLFlags |= IDLFLAG.IDLFLAG_FOUT;
            _paramFlags |= PARAMFLAG.PARAMFLAG_FOUT;
        }

        if (ParameterInfo.ParameterType.IsPointer || ParameterInfo.IsIn || hasInAttribute || (!ParameterInfo.IsRetval && !ParameterInfo.IsOut && !_isTransformedOutParameter))
        {
            IDLFlags |= IDLFLAG.IDLFLAG_FIN;
            _paramFlags |= PARAMFLAG.PARAMFLAG_FIN;
        }

        if (ParameterInfo.IsRetval || _isTransformedOutParameter)
        {
            IDLFlags |= IDLFLAG.IDLFLAG_FRETVAL;
            _paramFlags |= PARAMFLAG.PARAMFLAG_FRETVAL;
        }

        if (ParameterInfo.IsOptional)
        {
            _paramFlags |= PARAMFLAG.PARAMFLAG_FOPT;
        }

        if (ParameterInfo.HasDefaultValue)
        {
            if (ParameterInfo.IsOptional || ParameterInfo.DefaultValue != null)
            {
                _paramFlags |= PARAMFLAG.PARAMFLAG_FHASDEFAULT;
            }
            else if (ParameterInfo.Member != null
                     && (_paramFlags & PARAMFLAG.PARAMFLAG_FOUT) == 0
                     && !ParameterInfo.Member.Name.StartsWith("get_", StringComparison.Ordinal)
                     && !ParameterInfo.Member.Name.StartsWith("set_", StringComparison.Ordinal))
            {
                // The case of non-optional parameters with defaultValue "null" as described in #93 
                // Auto-generated Get and Set methods and out parameters will also have a default value for which there should not be a parameter flag 
                _paramFlags |= PARAMFLAG.PARAMFLAG_FHASDEFAULT;
            }
        }
    }

    public override void Create()
    {
        CalculateFlags();

        if (IsValid)
        {
            base.Create();
        }

        if (_isTransformedOutParameter || Type.IsByRef)
        {
            //encapsulate transformed out parameters and reference types
            _elementDescription.tdesc = new TYPEDESC()
            {
                vt = (short)VarEnum.VT_PTR,
                lpValue = StructureToPtr(_elementDescription.tdesc)
            };
        }
        else if (TypeProvider.MarshalAsAttribute != null && TypeProvider.MarshalAsAttribute.Value == UnmanagedType.LPStruct &&
            (Type == typeof(Guid)))
        {
            //add out parameter flag in this case, and only this case
            IDLFlags |= IDLFLAG.IDLFLAG_FOUT;
            _paramFlags |= PARAMFLAG.PARAMFLAG_FOUT;
        }

        _elementDescription.desc.idldesc.wIDLFlags = IDLFlags;
        _elementDescription.desc.paramdesc.wParamFlags = _paramFlags;
        if ((_paramFlags & PARAMFLAG.PARAMFLAG_FHASDEFAULT) != 0)
        {
            _elementDescription.desc.paramdesc.lpVarValue = GetDefaultValuePtr();
        }
    }

    private IntPtr GetDefaultValuePtr()
    {
        var ptrVariant = ObjectToVariantPtr(ParameterInfo.DefaultValue);

        var defValue = new PARAMDESCEX()
        {
            size = (ulong)Marshal.SizeOf<PARAMDESCEX>(),
            varValue = Marshal.PtrToStructure<VARIANT>(ptrVariant)
        };

        if (ParameterInfo.DefaultValue == null)
        {
            defValue.varValue.vt = VarEnum.VT_UNKNOWN;
        }
        return StructureToPtr(defValue);
    }

    private bool ParameterIsResolvable
    {
        get
        {
            try
            {
                if ((ParameterInfo.Attributes & ParameterAttributes.HasFieldMarshal) != 0)
                {
                    return true;
                }

                var type = ParameterInfo.ParameterType.GetUnderlayingType();
                var notSpecialHandledValueType = !type.IsSpecialHandledValueType();

                // If type is interface, enum, class, or struct...
                if (type.IsInterface ||
                    type.IsEnum ||
                    (type.IsClass && notSpecialHandledValueType) ||
                    (type.IsValueType && !type.IsPrimitive && TypeProvider.MarshalAsAttribute == null && notSpecialHandledValueType))
                {
                    return Context.TypeInfoResolver.ResolveTypeInfo(type) != null;
                }

                return true;
            }
            catch (FileNotFoundException)
            {
                // Assembly not found, or TypeLib not found...
                return false;
            }
        }
    }

    public IDLFLAG IDLFlags { get; set; } = IDLFLAG.IDLFLAG_NONE;
}
