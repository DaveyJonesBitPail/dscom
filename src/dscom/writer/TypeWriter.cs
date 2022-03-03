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
using dSPACE.Runtime.InteropServices.ComTypes;

namespace dSPACE.Runtime.InteropServices.Writer;

internal abstract class TypeWriter : BaseWriter
{
    private ICreateTypeInfo2? _createTypeInfo2;

    private string _name = string.Empty;

    protected TypeWriter(Type sourceType, LibraryWriter libraryWriter, WriterContext context) : base(context)
    {
        SourceType = sourceType;
        LibraryWriter = libraryWriter;
    }

    protected Type SourceType { get; set; }

    public TYPEFLAGS TypeFlags { get; set; }

    public TYPEKIND TypeKind { get; set; }

    protected virtual string Name => _name;

    public ICreateTypeInfo2 TypeInfo
    {
        get
        {
            if (Context.TargetTypeLib == null || _createTypeInfo2 == null)
            {
                throw new InvalidOperationException();
            }

            return _createTypeInfo2;
        }
    }

    public LibraryWriter LibraryWriter { get; }

    /// <summary>
    /// Adds a type description to those referenced by the type description being created
    /// </summary>
    public virtual void CreateTypeInheritance()
    {
    }

    /// <summary>
    /// Create the CreateTypeInfo instance.
    /// </summary>
    public virtual void CreateTypeInfo()
    {
        _name = LibraryWriter.GetUniqueTypeName(SourceType);

        var typeLib = Context.TargetTypeLib;

        if (typeLib != null)
        {
            typeLib.CreateTypeInfo(Context.NameResolver.GetMappedName(Name), TypeKind, out var createTypeInfo)
                    .ThrowIfFailed($"Failed to create type info for {Name}.");

            _createTypeInfo2 = (ICreateTypeInfo2)createTypeInfo;

            var guid = GetTypeGuid();

            TypeInfo.SetGuid(guid)
                .ThrowIfFailed($"Failed to set GUID for {Name}.");

            TypeInfo.SetVersion(MajorVersion, MinorVersion)
                .ThrowIfFailed($"Failed to set version for {Name}.");

            TypeInfo.SetCustData(new Guid(Guids.GUID_ManagedName), SourceType.ToString())
                .ThrowIfFailed($"Failed to set custom data for {Name}.");

            TypeInfo.SetTypeFlags((uint)TypeFlags)
                .ThrowIfFailed($"Failed to set type flags for {Name}.");

            var description = SourceType.GetCustomAttribute<System.ComponentModel.DescriptionAttribute>();
            if (description != null)
            {
                TypeInfo.SetDocString(description.Description)
                    .ThrowIfFailed($"Failed to set documentation string for {Name}.");
            }
        }

        Context.TypeInfoResolver.AddTypeToCache(TypeInfo as ITypeInfo);
    }

    protected virtual ushort MajorVersion => 1;

    protected virtual ushort MinorVersion => 0;

    protected virtual Guid GetTypeGuid()
    {
        return SourceType.GUID != Guid.Empty ? SourceType.GUID : System.Runtime.InteropServices.Marshal.GenerateGuidForType(SourceType);
    }
}
