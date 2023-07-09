using FaFLobbySimClient;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace FafLobbySimClientTests;

public class IntegrationTests
{
    private static string GetImagePath(string fileName)
    {
        var documentFolder =
            Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "SourceImages"));

        return Path.Combine(documentFolder, fileName);
    }

    [Fact]
    public void SinglePlayerHostPartiallyWindowed()
    {
        var occupancy = RunTest("original4cff3a39.png");

        Assert.Equal(1, occupancy.Occupied);
        Assert.Equal(8, occupancy.Total);
    }

    [Fact]
    public void HostAndAiMaximized()
    {
        var occupancy = RunTest("original5b21f480.png");

        Assert.Equal(2, occupancy.Occupied);
        Assert.Equal(4, occupancy.Total);
    }

    [Fact]
    public void SinglePlayerHostMaximized()
    {
        var occupancy = RunTest("original082f3293.png");

        Assert.Equal(1, occupancy.Occupied);
        Assert.Equal(8, occupancy.Total);
    }

    [Fact]
    public void TwoNonContiguousPlayersMaximized()
    {
        var occupancy = RunTest("original97f1247d.png");

        Assert.Equal(2, occupancy.Occupied);
        Assert.Equal(12, occupancy.Total);
    }

    [Fact]
    public void FourPlayersWindowed()
    {
        var occupancy = RunTest("original531973b7.png");

        Assert.Equal(4, occupancy.Occupied);
        Assert.Equal(12, occupancy.Total);
    }

    [Fact]
    public void ThreePlayersWithChat()
    {
        var occupancy = RunTest("thresholded3playerswithchat.png");

        Assert.Equal(2, occupancy.Occupied);
        Assert.Equal(12, occupancy.Total);
    }

    [Fact]
    public void FullTwelvePlayerGame()
    {
        var occupancy = RunTest("thresholded56b8d021.png");

        Assert.Equal(12, occupancy.Occupied);
        Assert.Equal(12, occupancy.Total);
    }

    private static Occupancy RunTest(string filename)
    {
        var imagePath = GetImagePath(filename);

        var image = Image.Load<Rgb24>(imagePath);

        var words = new WordDetector(false).Detect(image, _ => string.Empty);

        var occupancy =
            new CalculateLobbyOccupancyHandler().Calculate(words, new WidthHeight(image.Width, image.Height));

        Assert.NotNull(occupancy);

        return occupancy;
    }
}