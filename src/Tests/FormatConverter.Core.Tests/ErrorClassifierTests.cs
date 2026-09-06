using FormatConverter.Core.Tools;

namespace FormatConverter.Core.Tests;

public class ErrorClassifierTests
{
    [Theory]
    [InlineData("Invalid data found when processing input", "输入损坏")]
    [InlineData("moov atom not found", "输入损坏")]
    [InlineData("No space left on device", "磁盘空间不足")]
    [InlineData("Access is denied", "文件被占用或无权限")]
    [InlineData("The process cannot access the file because it is being used by another process", "文件被占用或无权限")]
    [InlineData("Unknown encoder 'libx999'", "编码不支持")]
    public void Classify_Returns_Category(string message, string expected)
    {
        Assert.Equal(expected, ErrorClassifier.Classify(message));
    }

    [Fact]
    public void Classify_Cancelled_And_Unknown_Return_Empty()
    {
        Assert.Equal("", ErrorClassifier.Classify("已取消"));
        Assert.Equal("", ErrorClassifier.Classify("something else entirely"));
    }

    [Fact]
    public void WithCategory_Prefixes_Category()
    {
        Assert.Equal("[输入损坏] moov atom not found",
            ErrorClassifier.WithCategory("moov atom not found"));
        Assert.Equal("已取消", ErrorClassifier.WithCategory("已取消"));
    }
}
