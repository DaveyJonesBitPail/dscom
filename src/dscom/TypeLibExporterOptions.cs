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

namespace dSPACE.Runtime.InteropServices;

public class TypeLibExporterOptions
{
    public string TypeLibrary { get; set; } = string.Empty;

    public string Out { get; set; } = string.Empty;

    public string OutPutType { get; set; } = "yaml";

    public string[] TLBReference { get; set; } = Array.Empty<string>();

    public string[] TLBRefpath { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Gets or sets the filter
    /// Example: .Types\[\].Functions\[\].Parameters\[\].Name=puArgErr
    /// Example: \.file\=   
    /// </summary>
    public string[] FilterRegex { get; set; } = Array.Empty<string>();
}
