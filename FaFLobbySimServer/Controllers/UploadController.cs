using Microsoft.AspNetCore.Mvc;

namespace FaFLobbySimServer.Controllers;

[ApiController]
[Route("upload")]
public class UploadController : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Upload([FromBody] OccupancyUpload upload)
    {
        await Task.CompletedTask;

        SystemStore.StoreLatest(upload.Identifier, upload.Occupied, upload.Total);

        return Ok();
    }
}