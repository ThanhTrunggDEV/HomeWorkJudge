using System.IO.Compression;
using InfrastructureService.Common.Errors;
using InfrastructureService.OutBoundAdapters.Storage;

namespace HomeWorkJudge.InfrastructureService.Tests.OutBoundAdapters.Storage;

public class ZipFileExtractorPortTests
{
    [Fact]
    public async Task ExtractAsync_WhenFileNotFound_ShouldThrowInfrastructureException()
    {
        var sut = new ZipFileExtractorPort();

        await Assert.ThrowsAsync<InfrastructureException>(() =>
            sut.ExtractAsync(Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.zip")));
    }

    [Fact]
    public async Task ExtractAsync_WhenUnsupportedFormat_ShouldThrowInfrastructureException()
    {
        var sut = new ZipFileExtractorPort();
        var tempFile = Path.Combine(Path.GetTempPath(), $"hwj-{Guid.NewGuid():N}.bin");
        await File.WriteAllTextAsync(tempFile, "dummy");

        try
        {
            await Assert.ThrowsAsync<InfrastructureException>(() => sut.ExtractAsync(tempFile));
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public async Task ExtractAsync_Zip_ShouldFilterIgnoredDirsInvalidExtensionsAndOversizedFiles()
    {
        var sut = new ZipFileExtractorPort();
        var zipPath = Path.Combine(Path.GetTempPath(), $"hwj-{Guid.NewGuid():N}.zip");

        try
        {
            using (var fs = new FileStream(zipPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
            using (var archive = new ZipArchive(fs, ZipArchiveMode.Create, leaveOpen: false))
            {
                AddEntry(archive, "src/Program.cs", "Console.WriteLine(1);");
                AddEntry(archive, "docs/README.md", "# Readme");
                AddEntry(archive, "solution.slnx", "<Solution />");
                AddEntry(archive, "bin/Debug/should-ignore.cs", "int x = 1;");
                AddEntry(archive, "obj/should-ignore.cs", "int y = 2;");
                AddEntry(archive, "assets/image.png", "not-source");
                AddEntry(archive, "large/Big.cs", new string('a', 1_048_577)); // > 1MB
            }

            var files = await sut.ExtractAsync(zipPath);
            var names = files.Select(f => f.FileName).OrderBy(x => x).ToArray();

            Assert.Equal(3, files.Count);
            Assert.Equal(new[] { "docs/README.md", "solution.slnx", "src/Program.cs" }, names);
        }
        finally
        {
            if (File.Exists(zipPath))
            {
                File.Delete(zipPath);
            }
        }
    }

    [Fact]
    public async Task ExtractAsync_Zip_ShouldAllowWpfAndAspNetExtensions()
    {
        var sut = new ZipFileExtractorPort();
        var zipPath = Path.Combine(Path.GetTempPath(), $"hwj-{Guid.NewGuid():N}.zip");

        try
        {
            using (var fs = new FileStream(zipPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
            using (var archive = new ZipArchive(fs, ZipArchiveMode.Create, leaveOpen: false))
            {
                // WPF
                AddEntry(archive, "App.xaml", "<Application />");
                AddEntry(archive, "Resources.resx", "<root />");
                // ASP.NET
                AddEntry(archive, "Views/Index.cshtml", "@model Foo");
                AddEntry(archive, "Components/Counter.razor", "@page \"/\"");
                // Config/data
                AddEntry(archive, "appsettings.json", "{}");
                AddEntry(archive, "App.config", "<config />");
                AddEntry(archive, "data.xml", "<data />");
                // Web
                AddEntry(archive, "index.html", "<html />");
                AddEntry(archive, "styles.css", "body {}");
                // Should STILL be excluded
                AddEntry(archive, "image.png", "binary");
                AddEntry(archive, "bin/Debug/app.dll", "binary");
            }

            var files = await sut.ExtractAsync(zipPath);
            var names = files.Select(f => f.FileName).OrderBy(x => x).ToArray();

            // All whitelisted files should be present
            Assert.Contains("App.xaml", names);
            Assert.Contains("Resources.resx", names);
            Assert.Contains("Views/Index.cshtml", names);
            Assert.Contains("Components/Counter.razor", names);
            Assert.Contains("appsettings.json", names);
            Assert.Contains("App.config", names);
            Assert.Contains("data.xml", names);
            Assert.Contains("index.html", names);
            Assert.Contains("styles.css", names);
            // Non-whitelisted / ignored should be excluded
            Assert.DoesNotContain("image.png", names);
        }
        finally
        {
            if (File.Exists(zipPath)) File.Delete(zipPath);
        }
    }

    private static void AddEntry(ZipArchive archive, string path, string content)
    {
        var entry = archive.CreateEntry(path);
        using var stream = entry.Open();
        using var writer = new StreamWriter(stream);
        writer.Write(content);
    }
}
