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

using System.Diagnostics;
using System.Text;

namespace dSPACE.Runtime.InteropServices.Tests;

/// <summary>
/// Provides the base implementation for running CLI tests.
/// </summary>
/// <remarks>The CLI tests are not available for .NET Framework</remarks>
public abstract class CLITestBase : IClassFixture<CompileReleaseFixture>
{
    protected const string ErrorNoCommandOrOptions = "Required command was not provided.";

    internal record struct ProcessOutput(string StdOut, string StdErr, int ExitCode);

    internal string DSComPath { get; set; } = string.Empty;

    internal string TestAssemblyPath { get; }

    internal string TestAssemblyDependencyPath { get; }

    public CLITestBase(CompileReleaseFixture compileFixture)
    {
        DSComPath = compileFixture.DSComPath;
        TestAssemblyPath = compileFixture.TestAssemblyPath;
        TestAssemblyDependencyPath = compileFixture.TestAssemblyDependencyPath;

        RetryHandler.Retry(() =>
        {
            foreach (var file in Directory.EnumerateFiles(Environment.CurrentDirectory, "*.tlb"))
            {
                File.Delete(file);
            };
        }, new[] { typeof(UnauthorizedAccessException) });

        RetryHandler.Retry(() =>
        {
            foreach (var file in Directory.EnumerateFiles(Environment.CurrentDirectory, "*.yaml"))
            {
                File.Delete(file);
            };
        }, new[] { typeof(UnauthorizedAccessException) });
    }

    internal static ProcessOutput Execute(string filename, params string[] args)
    {
        var processOutput = new ProcessOutput();
        using var process = new Process();
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.RedirectStandardError = true;
        var sb = new StringBuilder();
        process.ErrorDataReceived += new DataReceivedEventHandler((sender, e) => { sb.Append(e.Data); });
        process.StartInfo.FileName = filename;
        process.StartInfo.Arguments = string.Join(" ", args);
        process.Start();

        process.BeginErrorReadLine();
        process.WaitForExit();
        processOutput.StdOut = process.StandardOutput.ReadToEnd();
        processOutput.StdErr = sb.ToString();
        processOutput.ExitCode = process.ExitCode;

        return processOutput;
    }

    /// <summary>
    /// When running tests that involves the <c>tlbembed</c> command or <c>tlbexport</c> 
    /// command with the <c>--embed</c> switch enabled, the test should call the function 
    /// to handle the difference between .NET 4.8 which normally would expect COM objects 
    /// to be defined in the generated assembly versus .NET 5+ where COM objects will be 
    /// present in a <c>*.comhost.dll</c> instead.
    /// </summary>
    /// <param name="sourceAssemblyFile">The path to the source assembly from where the COM objects are defined.</param>
    internal static string GetEmbeddedPath(string sourceAssemblyFile)
    {
        var embedFile = Path.Combine(Path.GetDirectoryName(sourceAssemblyFile) ?? string.Empty, Path.GetFileNameWithoutExtension(sourceAssemblyFile) + ".comhost" + Path.GetExtension(sourceAssemblyFile));
        File.Exists(embedFile).Should().BeTrue($"File {embedFile} must exist prior to running the test.");
        return embedFile;
    }
}

/// <summary>
/// The basic CLI tests to run.
/// </summary>
[Collection("CLI Tests")]
public class CLITest : CLITestBase
{
    public CLITest(CompileReleaseFixture compileFixture) : base(compileFixture) { }

    [StaFact]
    public void CallWithoutCommandOrOption_ExitCodeIs1AndStdOutIsHelpStringAndStdErrIsUsed()
    {
        var result = Execute(DSComPath);

        result.ExitCode.Should().Be(1);
        result.StdErr.Trim().Should().Be(ErrorNoCommandOrOptions);
        result.StdOut.Trim().Should().Contain("Description");
    }

    [StaFact]
    public void CallWithoutCommandABC_ExitCodeIs1AndStdOutIsHelpStringAndStdErrIsUsed()
    {
        var result = Execute(DSComPath, "ABC");

        result.ExitCode.Should().Be(1);
        result.StdErr.Trim().Should().Contain(ErrorNoCommandOrOptions);
        result.StdErr.Trim().Should().Contain("Unrecognized command or argument 'ABC'");
    }

    [StaFact]
    public void CallWithVersionOption_VersionIsAssemblyInformationalVersionAttributeValue()
    {
        var assemblyInformationalVersion = typeof(TypeLibConverter).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
        assemblyInformationalVersion.Should().NotBeNull("AssemblyInformationalVersionAttribute is not set");
        var versionFromLib = assemblyInformationalVersion!.InformationalVersion;

        var result = Execute(DSComPath, "--version");
        result.ExitCode.Should().Be(0);
        versionFromLib.Should().StartWith(result.StdOut.Trim());
    }

    [StaFact]
    public void CallWithHelpOption_StdOutIsHelpStringAndExitCodeIsZero()
    {
        var result = Execute(DSComPath, "--help");
        result.ExitCode.Should().Be(0);
        result.StdOut.Trim().Should().Contain("Description");
    }

    [StaFact]
    public void TlbExportAndHelpOption_StdOutIsHelpStringAndExitCodeIsZero()
    {
        var result = Execute(DSComPath, "tlbexport", "--help");
        result.ExitCode.Should().Be(0);
        result.StdOut.Trim().Should().Contain("Description");
    }

    [StaFact]
    public void TlbDumpAndHelpOption_StdOutIsHelpStringAndExitCodeIsZero()
    {
        var result = Execute(DSComPath, "tlbdump", "--help");
        result.ExitCode.Should().Be(0);
        result.StdOut.Trim().Should().Contain("Description");
    }

    [StaFact]
    public void TlbRegisterAndHelpOption_StdOutIsHelpStringAndExitCodeIsZero()
    {
        var result = Execute(DSComPath, "tlbregister", "--help");
        result.ExitCode.Should().Be(0);
        result.StdOut.Trim().Should().Contain("Description");
    }

    [StaFact]
    public void TlbUnRegisterAndHelpOption_StdOutIsHelpStringAndExitCodeIsZero()
    {
        var result = Execute(DSComPath, "tlbunregister", "--help");
        result.ExitCode.Should().Be(0);
        result.StdOut.Trim().Should().Contain("Description");
    }

    [StaFact]
    public void TlbUnRegisterAndFileNotExist_StdErrIsFileNotFoundAndExitCodeIs1()
    {
        var result = Execute(DSComPath, "tlbunregister", "abc");
        result.ExitCode.Should().Be(1);
        result.StdErr.Trim().Should().Contain("not found");
    }

    [StaFact]
    public void TlbRegisterAndFileNotExist_StdErrIsFileNotFoundAndExitCodeIs1()
    {
        var result = Execute(DSComPath, "tlbregister", "abc");
        result.ExitCode.Should().Be(1);
        result.StdErr.Trim().Should().Contain("not found");
    }

    [StaFact]
    public void TlbDumpAndFileNotExist_StdErrIsFileNotFoundAndExitCodeIs1()
    {
        var result = Execute(DSComPath, "tlbdump", "abc");
        result.ExitCode.Should().Be(1);
        result.StdErr.Trim().Should().Contain("not found");
    }

    [StaFact]
    public void TlbExportAndFileNotExist_StdErrIsFileNotFoundAndExitCodeIs1()
    {
        var result = Execute(DSComPath, "tlbexport", "abc");
        result.ExitCode.Should().Be(1);
        result.StdErr.Trim().Should().Contain("not found");
    }

    [StaFact]
    public void TlbExportAndDemoAssemblyAndCallWithTlbDump_ExitCodeIs0AndTlbIsAvailableAndValid()
    {
        var tlbFileName = $"{Path.GetFileNameWithoutExtension(TestAssemblyPath)}.tlb";
        var yamlFileName = $"{Path.GetFileNameWithoutExtension(TestAssemblyPath)}.yaml";

        var tlbFilePath = Path.Combine(Environment.CurrentDirectory, tlbFileName);
        var yamlFilePath = Path.Combine(Environment.CurrentDirectory, yamlFileName);
        var dependentTlbPath = $"{Path.GetFileNameWithoutExtension(TestAssemblyDependencyPath)}.tlb";

        var result = Execute(DSComPath, "tlbexport", TestAssemblyPath);
        result.ExitCode.Should().Be(0);

        File.Exists(tlbFilePath).Should().BeTrue($"File {tlbFilePath} should be available.");
        File.Exists(dependentTlbPath).Should().BeTrue($"File {dependentTlbPath} should be available.");

        var dumpResult = Execute(DSComPath, "tlbdump", tlbFilePath);
        dumpResult.ExitCode.Should().Be(0);

        File.Exists(yamlFilePath).Should().BeTrue($"File {yamlFilePath} should be available.");
    }

    [StaFact]
    public void TlbExportAndEmbedAssembly_ExitCodeIs0AndTlbIsEmbeddedAndValid()
    {
        var embedPath = GetEmbeddedPath(TestAssemblyPath);

        var tlbFileName = $"{Path.GetFileNameWithoutExtension(TestAssemblyPath)}.tlb";

        var tlbFilePath = Path.Combine(Environment.CurrentDirectory, tlbFileName);
        var dependentTlbPath = $"{Path.GetFileNameWithoutExtension(TestAssemblyDependencyPath)}.tlb";

        var result = Execute(DSComPath, "tlbexport", TestAssemblyPath, $"--embed {embedPath}");
        result.ExitCode.Should().Be(0, $"because it should succeed. Error: ${result.StdErr}. Output: ${result.StdOut}");

        File.Exists(tlbFilePath).Should().BeTrue($"File {tlbFilePath} should be available.");
        File.Exists(dependentTlbPath).Should().BeTrue($"File {dependentTlbPath} should be available.");

        OleAut32.LoadTypeLibEx(embedPath, REGKIND.NONE, out var embeddedTypeLib);
        OleAut32.LoadTypeLibEx(tlbFilePath, REGKIND.NONE, out var sourceTypeLib);

        embeddedTypeLib.GetDocumentation(-1, out var embeddedTypeLibName, out _, out _, out _);
        sourceTypeLib.GetDocumentation(-1, out var sourceTypeLibName, out _, out _, out _);

        Assert.Equal(sourceTypeLibName, embeddedTypeLibName);
    }

    [StaFact]
    public void TlbExportCreateMissingDependentTLBsFalseAndOverrideTlbId_ExitCodeIs0AndTlbIsAvailableAndDependentTlbIsNot()
    {
        var tlbFileName = $"{Path.GetFileNameWithoutExtension(TestAssemblyPath)}.tlb";
        var tlbFilePath = Path.Combine(Environment.CurrentDirectory, tlbFileName);

        var parameters = new[] { "tlbexport", TestAssemblyPath, "--createmissingdependenttlbs", "false", "--overridetlbid", "12345678-1234-1234-1234-123456789012" };

        var result = Execute(DSComPath, parameters);
        result.ExitCode.Should().Be(0);
        var fileName = Path.GetFileNameWithoutExtension(TestAssemblyPath);

        result.StdOut.Should().NotContain($"{fileName} does not have a type library");
        result.StdErr.Should().NotContain($"{fileName} does not have a type library");

        File.Exists(tlbFilePath).Should().BeTrue($"File {tlbFilePath} should be available.");
    }

    [StaFact]
    public void TlbExportCreateMissingDependentTLBsFalse_ExitCodeIs0AndTlbIsAvailableAndDependentTlbIsNot()
    {
        var tlbFileName = $"{Path.GetFileNameWithoutExtension(TestAssemblyPath)}.tlb";
        var dependentFileName = $"{Path.GetFileNameWithoutExtension(TestAssemblyDependencyPath)}.tlb";
        var tlbFilePath = Path.Combine(Environment.CurrentDirectory, tlbFileName);
        var dependentTlbPath = Path.Combine(Environment.CurrentDirectory, dependentFileName);

        var result = Execute(DSComPath, "tlbexport", TestAssemblyPath, "--createmissingdependenttlbs", "false");
        result.ExitCode.Should().Be(0);

        File.Exists(tlbFilePath).Should().BeTrue($"File {tlbFilePath} should be available.");
        File.Exists(dependentTlbPath).Should().BeFalse($"File {dependentTlbPath} should not be available.");

        result.StdErr.Should().Contain("auto generation of dependent type libs is disabled");
        result.StdErr.Should().Contain(Path.GetFileNameWithoutExtension(TestAssemblyDependencyPath));
    }

    [StaFact]
    public void TlbExportCreateMissingDependentTLBsTrue_ExitCodeIs0AndTlbIsAvailableAndDependentTlbIsNot()
    {
        var tlbFileName = $"{Path.GetFileNameWithoutExtension(TestAssemblyPath)}.tlb";
        var dependentFileName = $"{Path.GetFileNameWithoutExtension(TestAssemblyDependencyPath)}.tlb";
        var tlbFilePath = Path.Combine(Environment.CurrentDirectory, tlbFileName);
        var dependentTlbPath = Path.Combine(Environment.CurrentDirectory, dependentFileName);

        var result = Execute(DSComPath, "tlbexport", TestAssemblyPath, "--createmissingdependenttlbs", "true");
        result.ExitCode.Should().Be(0);

        File.Exists(tlbFilePath).Should().BeTrue($"File {tlbFilePath} should be available.");
        File.Exists(dependentTlbPath).Should().BeTrue($"File {dependentTlbPath} should be available.");
    }

    [StaFact]
    public void TlbExportCreateMissingDependentTLBsNoValue_ExitCodeIs0()
    {
        var tlbFileName = $"{Path.GetFileNameWithoutExtension(TestAssemblyPath)}.tlb";
        var dependentFileName = $"{Path.GetFileNameWithoutExtension(TestAssemblyDependencyPath)}.tlb";
        var tlbFilePath = Path.Combine(Environment.CurrentDirectory, tlbFileName);
        var dependentTlbPath = Path.Combine(Environment.CurrentDirectory, dependentFileName);

        var result = Execute(DSComPath, "tlbexport", TestAssemblyPath, "--createmissingdependenttlbs");
        result.ExitCode.Should().Be(0);

        File.Exists(tlbFilePath).Should().BeTrue($"File {tlbFilePath} should be available.");
        File.Exists(dependentTlbPath).Should().BeTrue($"File {dependentTlbPath} should be available.");
    }

    [StaFact]
    public void TlbExportAndOptionSilent_StdOutAndStdErrIsEmpty()
    {
        var tlbFileName = $"{Guid.NewGuid()}.tlb";
        var tlbFilePath = Path.Combine(Environment.CurrentDirectory, tlbFileName);

        var result = Execute(DSComPath, "tlbexport", TestAssemblyPath, "--out", tlbFileName, "--silent");
        result.ExitCode.Should().Be(0);

        File.Exists(tlbFilePath).Should().BeTrue($"File {tlbFilePath} should be available.");

        result.StdOut.Trim().Should().BeNullOrEmpty();
        result.StdErr.Trim().Should().BeNullOrEmpty();
    }

    [StaFact]
    public void TlbExportAndOptionSilenceTX801311A6andTX0013116F_StdOutAndStdErrIsEmpty()
    {
        var tlbFileName = $"{Guid.NewGuid()}.tlb";
        var tlbFilePath = Path.Combine(Environment.CurrentDirectory, tlbFileName);

        var result = Execute(DSComPath, "tlbexport", TestAssemblyPath, "--out", tlbFileName, "--silence", "TX801311A6", "--silence", "TX0013116F");
        result.ExitCode.Should().Be(0);

        File.Exists(tlbFilePath).Should().BeTrue($"File {tlbFilePath} should be available.");

        result.StdOut.Trim().Should().BeNullOrEmpty();
        result.StdErr.Trim().Should().BeNullOrEmpty();
    }

    [StaFact]
    public void TlbExportAndOptionOverrideTLBId_TLBIdIsCorrect()
    {
        var guid = Guid.NewGuid().ToString();

        var tlbFileName = $"{guid}.tlb";
        var yamlFileName = $"{guid}.yaml";

        var tlbFilePath = Path.Combine(Environment.CurrentDirectory, tlbFileName);
        var yamlFilePath = Path.Combine(Environment.CurrentDirectory, yamlFileName);

        var result = Execute(DSComPath, "tlbexport", TestAssemblyPath, "--out", tlbFilePath, "--overridetlbid", guid);

        result.ExitCode.Should().Be(0);
        File.Exists(tlbFilePath).Should().BeTrue($"File {tlbFilePath} should be available.");

        var dumpResult = Execute(DSComPath, "tlbdump", tlbFilePath, "/out", yamlFilePath);
        dumpResult.ExitCode.Should().Be(0);

        File.Exists(yamlFilePath).Should().BeTrue($"File {yamlFilePath} should be available.");

        var yamlContent = File.ReadAllText(yamlFilePath);
        yamlContent.Should().Contain($"guid: {guid}");
    }
}

/// <summary>
/// The tests for embeds are performed as a separate class due to the additional setup
/// required and the need for creating a TLB file as part of export prior to testing
/// the embed functionality itself. There are parallelization issues with running them,
/// even within the same class. Thus, the separate class has additional setup during the
/// constructor to perform the export once and ensure the process relating to export is
/// disposed with before attempting to test the embed functionality.
/// </summary>
[Collection("CLI Tests")]
public class CLITestEmbed : CLITestBase
{
    internal string TlbFilePath { get; }

    internal string DependentTlbPath { get; }

    public CLITestEmbed(CompileReleaseFixture compileFixture) : base(compileFixture)
    {
        var tlbFileName = $"{Path.GetFileNameWithoutExtension(TestAssemblyPath)}.tlb";

        var result = Execute(DSComPath, "tlbexport", TestAssemblyPath);
        result.ExitCode.Should().Be(0, $"because it should succeed. Error: ${result.StdErr}. Output: ${result.StdOut}");

        result = Execute(DSComPath, "tlbexport", TestAssemblyDependencyPath);
        result.ExitCode.Should().Be(0, $"because it should succeed. Error: ${result.StdErr}. Output: ${result.StdOut}");

        TlbFilePath = Path.Combine(Environment.CurrentDirectory, tlbFileName);
        DependentTlbPath = $"{Path.GetFileNameWithoutExtension(TestAssemblyDependencyPath)}.tlb";

        File.Exists(TlbFilePath).Should().BeTrue($"File {TlbFilePath} should be available.");
        File.Exists(DependentTlbPath).Should().BeTrue($"File {DependentTlbPath} should be available.");

        // This is necessary to ensure the process from previous Execute for the export command has completely disposed before running tests.
        GC.Collect();
        GC.WaitForPendingFinalizers();
    }

    [StaFact]
    public void TlbEmbedAssembly_ExitCodeIs0AndTlbIsEmbeddedAndValid()
    {
        var embedPath = GetEmbeddedPath(TestAssemblyPath);

        var result = Execute(DSComPath, "tlbembed", TlbFilePath, embedPath);
        result.ExitCode.Should().Be(0, $"because embedding should succeed. Output: {result.StdErr} ");

        OleAut32.LoadTypeLibEx(embedPath, REGKIND.NONE, out var embeddedTypeLib);
        OleAut32.LoadTypeLibEx(TlbFilePath, REGKIND.NONE, out var sourceTypeLib);

        embeddedTypeLib.GetDocumentation(-1, out var embeddedTypeLibName, out _, out _, out _);
        sourceTypeLib.GetDocumentation(-1, out var sourceTypeLibName, out _, out _, out _);

        Assert.Equal(sourceTypeLibName, embeddedTypeLibName);
    }

    [StaFact]
    public void TlbEmbedAssemblyWithArbitraryIndex_ExitCodeIs0AndTlbIsEmbeddedAndValid()
    {
        var embedPath = GetEmbeddedPath(TestAssemblyPath);
        var result = Execute(DSComPath, "tlbembed", TlbFilePath, embedPath, "--index 2");
        result.ExitCode.Should().Be(0);

        OleAut32.LoadTypeLibEx(embedPath + "\\2", REGKIND.NONE, out var embeddedTypeLib);
        OleAut32.LoadTypeLibEx(TlbFilePath, REGKIND.NONE, out var sourceTypeLib);

        embeddedTypeLib.GetDocumentation(-1, out var embeddedTypeLibName, out _, out _, out _);
        sourceTypeLib.GetDocumentation(-1, out var sourceTypeLibName, out _, out _, out _);

        Assert.Equal(sourceTypeLibName, embeddedTypeLibName);
    }

    [StaFact]
    public void TlbEmbedAssemblyWithArbitraryTlbAndArbitraryIndex_ExitCodeIs0AndTlbIsEmbeddedAndValid()
    {
        var embedPath = GetEmbeddedPath(TestAssemblyPath);
        var result = Execute(DSComPath, "tlbembed", TlbFilePath, embedPath, "--index 3");
        result.ExitCode.Should().Be(0);

        OleAut32.LoadTypeLibEx(embedPath + "\\3", REGKIND.NONE, out var embeddedTypeLib);
        OleAut32.LoadTypeLibEx(TlbFilePath, REGKIND.NONE, out var sourceTypeLib);

        embeddedTypeLib.GetDocumentation(-1, out var embeddedTypeLibName, out _, out _, out _);
        sourceTypeLib.GetDocumentation(-1, out var sourceTypeLibName, out _, out _, out _);

        Assert.Equal(sourceTypeLibName, embeddedTypeLibName);
    }

    [StaFact]
    public void TlbEmbedAssemblyWithMultipleTypeLibraries_ExitCodeAre0AndTlbsAreEmbeddedAndValid()
    {
        var embedPath = GetEmbeddedPath(TestAssemblyPath);
        var result = Execute(DSComPath, "tlbembed", TlbFilePath, embedPath);
        result.ExitCode.Should().Be(0);

        result = Execute(DSComPath, "tlbembed", DependentTlbPath, TestAssemblyPath, "--index 2");
        result.ExitCode.Should().Be(0);

        OleAut32.LoadTypeLibEx(embedPath, REGKIND.NONE, out var embeddedTypeLib1);
        OleAut32.LoadTypeLibEx(TlbFilePath, REGKIND.NONE, out var sourceTypeLib1);

        embeddedTypeLib1.GetDocumentation(-1, out var embeddedTypeLibName1, out _, out _, out _);
        sourceTypeLib1.GetDocumentation(-1, out var sourceTypeLibName1, out _, out _, out _);

        Assert.Equal(sourceTypeLibName1, embeddedTypeLibName1);

        OleAut32.LoadTypeLibEx(TestAssemblyPath + "\\2", REGKIND.NONE, out var embeddedTypeLib2);
        OleAut32.LoadTypeLibEx(DependentTlbPath, REGKIND.NONE, out var sourceTypeLib2);

        embeddedTypeLib2.GetDocumentation(-1, out var embeddedTypeLibName2, out _, out _, out _);
        sourceTypeLib2.GetDocumentation(-1, out var sourceTypeLibName2, out _, out _, out _);

        Assert.Equal(sourceTypeLibName2, embeddedTypeLibName2);
    }
}
