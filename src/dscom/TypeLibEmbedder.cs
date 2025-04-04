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

using System.Runtime.InteropServices;

namespace dSPACE.Runtime.InteropServices;

/// <summary>
/// Embeds a type library into an assembly.
/// </summary>
public static class TypeLibEmbedder
{
    // HANDLE BeginUpdateResourceW(
    //   [in] LPCWSTR pFileName,
    //   [in] BOOL    bDeleteExistingResources
    // );
    [DllImport("Kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    private static extern IntPtr BeginUpdateResourceW(
        [In, MarshalAs(UnmanagedType.LPWStr)] string pFileName,
        [In] bool bDeleteExistingResources);

    // BOOL UpdateResourceW(
    //   [in]           HANDLE  hUpdate,
    //   [in]           LPCWSTR lpType,
    //   [in]           LPCWSTR lpName,
    //   [in]           WORD    wLanguage,
    //   [in, optional] LPVOID  lpData,
    //   [in]           DWORD   cb
    // );
    [DllImport("Kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    private static extern bool UpdateResourceW(
        [In] IntPtr hUpdate,
        IntPtr lpType,
        IntPtr lpName,
        [In] short wLanguage,
        [In, Optional] byte[] lpData,
        [In] long cb);

    // BOOL EndUpdateResourceW(
    //   [in] HANDLE hUpdate,
    //   [in] BOOL   fDiscard
    // );
    [DllImport("Kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    private static extern bool EndUpdateResourceW(
        [In] IntPtr hUpdate,
        [In] bool fDiscard);

    /// <summary>
    /// Adds the supplied type library as a TYPELIB resource type into the assembly. 
    /// For the purpose of embedding, the assembly is treated as if it was a PE file.
    /// The resource will be always assigned index #1. 
    /// </summary>
    /// <param name="settings">Settings to indicate the source type library and target
    /// assembly.</param>
    /// <returns><code>true</code> is returned to indicate success.</returns>
    /// <exception cref="ApplicationException">Reports an issue with applying changes to the resources.</exception>
    public static bool EmbedTypeLib(TypeLibEmbedderSettings settings)
    {
        if (settings.Index < 1)
        {
            throw new ApplicationException("The index must be set to a number between 1 to 65535.");
        }

        if (settings.RetryCount <= 0)
        {
            throw new ApplicationException("The retry count must be set to a number greater than 0.");
        }

        if (settings.RetryDelay <= 0)
        {
            throw new ApplicationException("The retry delay must be set to a number greater than 0.");
        }

        IntPtr strPtr = IntPtr.Zero;
        try
        {
            strPtr = Marshal.StringToHGlobalUni("TYPELIB");
            var bytes = File.ReadAllBytes(settings.SourceTypeLibrary);

            for (int attempt = 0; attempt < settings.RetryCount; attempt++)
            {
                IntPtr assemblyFileHandle = BeginUpdateResourceW(settings.TargetAssembly, false);
                if (assemblyFileHandle == IntPtr.Zero)
                {
                    HandleRetryOrThrow(attempt, settings.RetryCount, settings.RetryDelay, $"Unable to lock the assembly file {settings.TargetAssembly} for resource updating");
                    continue;
                }

                if (!UpdateResourceW(assemblyFileHandle, strPtr, new IntPtr(settings.Index), 0, bytes, bytes.Length))
                {
                    HandleRetryOrThrow(attempt, settings.RetryCount, settings.RetryDelay, $"Error: Unable to update assembly file '{settings.TargetAssembly}' using TLB file '{settings.SourceTypeLibrary}'");
                    continue;
                }

                if (!EndUpdateResourceW(assemblyFileHandle, false))
                {
                    HandleRetryOrThrow(attempt, settings.RetryCount, settings.RetryDelay, $"Error: Unable to write changes to assembly file '{settings.TargetAssembly}' using TLB file '{settings.SourceTypeLibrary}'");
                    continue;
                }

                return true;
            }
        }
        finally
        {
            if (strPtr != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(strPtr);
            }
        }

        return false;
    }
    private static void HandleRetryOrThrow(int attempt, int retryCount, int retryDelay, string errorMessage)
    {
        if (attempt < retryCount - 1)
        {
            Thread.Sleep(retryDelay);
        }
        else
        {
            int win32ErrorCode = Marshal.GetLastWin32Error();
            throw new ApplicationException($"{errorMessage}; error code {win32ErrorCode}.");
        }
    }
}
