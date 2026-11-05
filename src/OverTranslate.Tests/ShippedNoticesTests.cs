using System.IO;
using Xunit;

namespace OverTranslate.Tests;

/// <summary>The MIT terms require these notices in every copy, and the About panel opens them by name.</summary>
public class ShippedNoticesTests
{
    [Theory]
    [InlineData("LICENSE.txt", "Copyright (c) 2026 Hon-Lu")]
    [InlineData("THIRD-PARTY-NOTICES.txt", "PaddleOCR")]
    public void Notice_is_copied_beside_the_executable(string fileName, string expected)
    {
        var path = Path.Combine(AppContext.BaseDirectory, fileName);
        Assert.True(File.Exists(path), path);
        Assert.Contains(expected, File.ReadAllText(path));
    }
}
