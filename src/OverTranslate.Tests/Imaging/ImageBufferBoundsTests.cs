using System.Drawing;
using OverTranslate.Imaging;
using Xunit;

namespace OverTranslate.Tests.Imaging;

public class ImageBufferBoundsTests
{
    [Theory]
    [InlineData(1073741824, 1, (int)PixelType.F32C1)]
    [InlineData(1073741824, 1, (int)PixelType.U8C3)]
    [InlineData(65536, 65536, (int)PixelType.U8C1)]
    [InlineData(int.MaxValue, int.MaxValue, (int)PixelType.F32C3)]
    public void RejectsOversizedStorageBeforeAllocating(int width, int height, int pixelType)
    {
        var type = (PixelType)pixelType;
        Assert.Throws<ArgumentOutOfRangeException>(() => new ImageBuffer(width, height, type));
        Assert.Throws<ArgumentOutOfRangeException>(() => ImageBuffer.Uninitialized(width, height, type));
    }

    [Theory]
    [InlineData(1, 0, int.MaxValue, 1)]
    [InlineData(0, 1, 1, int.MaxValue)]
    public void RejectsViewsWhoseRightOrBottomWraps(int x, int y, int width, int height)
    {
        using var parent = new ImageBuffer(4, 4, PixelType.U8C1);
        Assert.Throws<ArgumentOutOfRangeException>(() => new ImageBuffer(parent, new Rectangle(x, y, width, height)));
    }

    [Fact]
    public void ValidViewKeepsItsPixelsAfterParentIsDisposed()
    {
        using var parent = new ImageBuffer(4, 4, PixelType.U8C1);
        parent.SetTo(42);
        using var view = new ImageBuffer(parent, new Rectangle(1, 1, 3, 3));
        parent.Dispose();
        Assert.Equal(new byte[] { 42, 42, 42 }, view.RowSpan(2).ToArray());
    }
}
