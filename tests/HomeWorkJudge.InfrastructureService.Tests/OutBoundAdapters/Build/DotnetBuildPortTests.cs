using InfrastructureService.OutBoundAdapters.Build;
using Ports.DTO.Submission;

namespace HomeWorkJudge.InfrastructureService.Tests.OutBoundAdapters.Build;

public class DotnetBuildPortTests
{
    [Fact]
    public async Task BuildAsync_WhenNoProjectFile_ShouldReturnFailureWithMessage()
    {
        var sut = new DotnetBuildPort();

        var files = new[]
        {
            new SourceFileDto("Program.cs", "Console.WriteLine(\"Hello\");")
        };

        var result = await sut.BuildAsync(files, Guid.NewGuid().ToString());

        Assert.False(result.Success);
        Assert.Contains("Không tìm thấy file .sln, .slnx hoặc .csproj", result.BuildLog);
    }

    [Fact]
    public async Task BuildAsync_WhenProjectBuilds_ShouldReturnSuccess()
    {
        var sut = new DotnetBuildPort();

        var files = new[]
        {
            new SourceFileDto("StudentApp/StudentApp.csproj", """
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net9.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
</Project>
"""),
            new SourceFileDto("StudentApp/Program.cs", """
using System;

Console.WriteLine("OK");
""")
        };

        var result = await sut.BuildAsync(files, Guid.NewGuid().ToString());

        Assert.True(result.Success);
        Assert.Contains("Build succeeded", result.BuildLog, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task BuildAsync_WhenCompileError_ShouldReturnFailureAndErrorOutput()
    {
        var sut = new DotnetBuildPort();

        var files = new[]
        {
            new SourceFileDto("Broken/Broken.csproj", """
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net9.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
</Project>
"""),
            new SourceFileDto("Broken/Program.cs", """
using System;

Console.WriteLine("Hello")
""")
        };

        var result = await sut.BuildAsync(files, Guid.NewGuid().ToString());

        Assert.False(result.Success);
        Assert.Contains("error", result.BuildLog, StringComparison.OrdinalIgnoreCase);
    }
}
