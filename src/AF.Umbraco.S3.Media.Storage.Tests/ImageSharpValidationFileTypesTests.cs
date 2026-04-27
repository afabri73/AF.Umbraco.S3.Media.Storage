using AF.Umbraco.S3.Media.Storage.Core;
using SixLabors.ImageSharp;
using Xunit;

namespace AF.Umbraco.S3.Media.Storage.Tests;

/// <summary>
/// Verifies the selection of formats that should go through ImageSharp validation.
/// </summary>
public sealed class ImageSharpValidationFileTypesTests
{
    /// <summary>
    /// Ensures SVG files are excluded from ImageSharp validation because they are not decoded by the package.
    /// </summary>
    /// <param name="fileName">Uploaded SVG file name.</param>
    /// <param name="contentType">Content type declared by the client or resolved by the provider.</param>
    [Theory]
    [InlineData("vector.svg", "image/svg+xml")]
    [InlineData("vector.svg", "image/svg")]
    public void RequiresValidation_SvgFiles_ReturnsFalse(string fileName, string contentType)
    {
        bool requiresValidation = ImageSharpValidationFileTypes.RequiresValidation(fileName, contentType);

        Assert.False(requiresValidation);
    }

    /// <summary>
    /// Ensures PNG files still require validation and invalid content is rejected by ImageSharp.
    /// </summary>
    [Fact]
    public void RequiresValidation_InvalidPng_ReturnsTrueAndImageSharpRejectsContent()
    {
        bool requiresValidation = ImageSharpValidationFileTypes.RequiresValidation("invalid.png", "image/png");

        Assert.True(requiresValidation);

        using var stream = new MemoryStream("not a png"u8.ToArray());

        Assert.Throws<UnknownImageFormatException>(() => Image.DetectFormat(stream));
    }
}
