using System.Drawing;
using OverTranslate.Imaging;
using Xunit;

namespace OverTranslate.Tests.Imaging;

public class ResizeBoundsTests
{
    [Fact]
    public void LargeByteAreaAverageDoesNotOverflow()
    {
        using var source = new ImageBuffer(3072, 3072, PixelType.U8C1);
        source.SetTo(255);
        using var target = Resize.To(source, new Size(1, 1), Interpolation.Area);
        Assert.Equal(255, target.RowSpan(0)[0]);
    }

    [Fact]
    public unsafe void LargeFloatAreaAverageDoesNotDependOnWorkerStackSize()
    {
        using var source = new ImageBuffer(1024, 1024, PixelType.F32C1);
        source.SetTo(0.25);
        using var target = Resize.To(source, new Size(1, 1), Interpolation.Area);
        Assert.Equal(0.25f, target.Row<float>(0)[0]);
    }
}
