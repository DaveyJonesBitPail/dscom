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

namespace dSPACE.Runtime.InteropServices;

/// <summary>Provides a callback mechanism for the assembly converter to inform the caller of the status of the conversion, and involve the caller in the conversion process itself.</summary>
public interface ITypeLibExporterNotifySink
{
    /// <summary>Notifies the caller that an event occured during the conversion of an assembly.</summary>
    /// <param name="eventKind">An <see cref="T:System.Runtime.InteropServices.ExporterEventKind" /> value indicating the type of event.</param>
    /// <param name="eventCode">Indicates extra information about the event.</param>
    /// <param name="eventMsg">A message generated by the event.</param>
    void ReportEvent(ExporterEventKind eventKind, int eventCode, string eventMsg);

    /// <summary>Asks the user to resolve a reference to another assembly.</summary>
    /// <param name="assembly">The assembly to resolve.</param>
    /// <returns>The type library for <paramref name="assembly" />.</returns>
    object ResolveRef(Assembly assembly);
}
