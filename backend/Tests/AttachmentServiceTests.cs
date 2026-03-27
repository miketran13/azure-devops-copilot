using DevOpsCopilot.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace DevOpsCopilot.Tests;

public class AttachmentServiceTests
{
    private static AttachmentService CreateService()
    {
        // ValidateFile, IsReadableFileType, and ExtractTextContent don't use AzureDevOpsService
        return new AttachmentService(null!, NullLogger<AttachmentService>.Instance);
    }

    // ─── ValidateFile ──────────────────────────────────────────────────

    [Fact]
    public void ValidateFile_ValidTextFile_ReturnsSuccess()
    {
        var svc = CreateService();
        var result = svc.ValidateFile("readme.md", 1024, "text/markdown");

        result.IsValid.Should().BeTrue();
        result.IsReadable.Should().BeTrue();
    }

    [Fact]
    public void ValidateFile_ValidImageFile_ReturnsSuccessNotReadable()
    {
        var svc = CreateService();
        var result = svc.ValidateFile("screenshot.png", 5000, "image/png");

        result.IsValid.Should().BeTrue();
        result.IsReadable.Should().BeFalse();
    }

    [Fact]
    public void ValidateFile_BlockedExtension_ReturnsFail()
    {
        var svc = CreateService();
        var result = svc.ValidateFile("malware.exe", 1024, null);

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not allowed");
    }

    [Fact]
    public void ValidateFile_EmptyFile_ReturnsFail()
    {
        var svc = CreateService();
        var result = svc.ValidateFile("empty.txt", 0, null);

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("empty");
    }

    [Fact]
    public void ValidateFile_TooLarge_ReturnsFail()
    {
        var svc = CreateService();
        var result = svc.ValidateFile("huge.zip", 11 * 1024 * 1024, null);

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("exceeds");
    }

    [Fact]
    public void ValidateFile_EmptyFileName_ReturnsFail()
    {
        var svc = CreateService();
        var result = svc.ValidateFile("", 1024, null);

        result.IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData(".bat")]
    [InlineData(".cmd")]
    [InlineData(".dll")]
    [InlineData(".msi")]
    [InlineData(".scr")]
    [InlineData(".vbs")]
    public void ValidateFile_AllBlockedExtensions_ReturnsFail(string ext)
    {
        var svc = CreateService();
        var result = svc.ValidateFile($"file{ext}", 100, null);

        result.IsValid.Should().BeFalse();
    }

    // ─── IsReadableFileType ────────────────────────────────────────────

    [Theory]
    [InlineData(".md", true)]
    [InlineData(".txt", true)]
    [InlineData(".cs", true)]
    [InlineData(".ts", true)]
    [InlineData(".json", true)]
    [InlineData(".yaml", true)]
    [InlineData(".csv", true)]
    [InlineData(".sql", true)]
    [InlineData(".png", false)]
    [InlineData(".pdf", false)]
    [InlineData(".zip", false)]
    [InlineData(".docx", false)]
    public void IsReadableFileType_ReturnsCorrectResult(string ext, bool expected)
    {
        AttachmentService.IsReadableFileType(ext).Should().Be(expected);
    }

    [Fact]
    public void IsReadableFileType_WithFileName_Works()
    {
        AttachmentService.IsReadableFileType("readme.md").Should().BeTrue();
        AttachmentService.IsReadableFileType("image.png").Should().BeFalse();
    }

    // ─── ExtractTextContent ────────────────────────────────────────────

    [Fact]
    public void ExtractTextContent_ReadableFile_ReturnsContent()
    {
        var svc = CreateService();
        var content = System.Text.Encoding.UTF8.GetBytes("Hello World");
        var result = svc.ExtractTextContent("test.md", content);

        result.Should().Be("Hello World");
    }

    [Fact]
    public void ExtractTextContent_NonReadableFile_ReturnsNull()
    {
        var svc = CreateService();
        var content = new byte[] { 0x89, 0x50, 0x4E, 0x47 }; // PNG header
        var result = svc.ExtractTextContent("image.png", content);

        result.Should().BeNull();
    }

    [Fact]
    public void ExtractTextContent_LargeFile_IsTruncated()
    {
        var svc = CreateService();
        var largeContent = new string('A', 60_000);
        var bytes = System.Text.Encoding.UTF8.GetBytes(largeContent);
        var result = svc.ExtractTextContent("large.txt", bytes);

        result.Should().NotBeNull();
        result!.Length.Should().BeLessThan(60_000);
        result.Should().Contain("truncated");
    }
}
