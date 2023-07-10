using FlaUI.Core.Capturing;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace FaFLobbySimClient;

internal class LobbyMonitor
{
    private readonly TryScreenshotProcess _tryScreenshot;
    private readonly DetectImageWords _wordDetector;
    private readonly CalculateLobbyOccupancy _calculateOccupancy;
    private readonly WriteOccupancyForJob _writeOccupancy;
    private readonly WriteOutput _log;

    public LobbyMonitor(
        TryScreenshotProcess tryScreenshot,
        DetectImageWords wordDetector,
        CalculateLobbyOccupancy calculateOccupancy,
        WriteOccupancyForJob writeOccupancy,
        WriteOutput log)
    {
        _tryScreenshot = tryScreenshot;
        _wordDetector = wordDetector;
        _calculateOccupancy = calculateOccupancy;
        _writeOccupancy = writeOccupancy;
        _log = log;
    }

    public async Task Monitor(string identifier)
    {
        do
        {
            var delayDurationSeconds = 5;
            if (_tryScreenshot(out var screenshot))
            {
                var incrementId = Guid.NewGuid().ToString("D")[..8];

                string PathGetter(string fileName)
                {
                    var filenameonly = Path.GetFileNameWithoutExtension(fileName);

                    var path = Path.Combine(@"D:\temp\", filenameonly + incrementId + Path.GetExtension(fileName));

                    return path;
                }

                try
                {
                    var image = ScreenshotToImage(screenshot);

                    var words = _wordDetector(image, PathGetter);

                    var occupancy = _calculateOccupancy(words, new WidthHeight(image.Width, image.Height));

                    if (occupancy != null)
                    {
                        _log($"Current occupancy is {occupancy.Occupied}/{occupancy.Total}", false);

                        await _writeOccupancy(identifier, occupancy);
                    }
                    else
                    {
                        _log("Could not calculate occupancy.", true);
                    }
                }
                catch (Exception ex)
                {
                    _log($"Error: {ex}", true);
                    delayDurationSeconds = 20;
                }
            }
            else
            {
                _log("FAF Client not currently active or visible, skipping", true);
                delayDurationSeconds = 20;
            }

            await Task.Delay(TimeSpan.FromSeconds(delayDurationSeconds));
        } while (true);
    }

    private static Image<Rgb24> ScreenshotToImage(CaptureImage screenshot)
    {
        using var memoryStream = new MemoryStream();

        screenshot.Bitmap.Save(memoryStream, System.Drawing.Imaging.ImageFormat.Png);
        memoryStream.Seek(0, SeekOrigin.Begin);

        var image = Image.Load<Rgb24>(memoryStream);

        return image;
    }
}