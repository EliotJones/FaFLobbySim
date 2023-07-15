using System.Globalization;
using Lib.AspNetCore.ServerSentEvents;
using Microsoft.AspNetCore.Mvc;

namespace FaFLobbySimServer.Controllers;

[ApiController]
[Route("upload")]
public class UploadController : ControllerBase
{
    private readonly IServerSentEventsService _sseService;

    public UploadController(IServerSentEventsService sseService)
    {
        _sseService = sseService;
    }

    [HttpPost]
    public async Task<IActionResult> Upload([FromBody] OccupancyUpload upload)
    {
        SystemStore.StoreLatest(upload.Identifier, upload.Occupied, upload.Total);

        await _sseService.SendEventAsync(new ServerSentEvent
        {
            Id = upload.Identifier,
            Data = new List<string>
            {
                upload.Occupied.ToString("F0", CultureInfo.InvariantCulture),
                upload.Total.ToString("F0", CultureInfo.InvariantCulture)
            }
        });

        return Ok();
    }
}