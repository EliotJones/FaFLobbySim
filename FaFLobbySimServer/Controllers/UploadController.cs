using Microsoft.AspNetCore.Mvc;

namespace FaFLobbySimServer.Controllers;

[ApiController]
[Route("upload")]
public class UploadController : ControllerBase
{
    [HttpPost]
    public IActionResult Upload([FromBody] OccupancyUpload upload)
    {
        SystemStore.StoreLatest(upload.Identifier, upload.Occupied, upload.Total);

        return Ok();
    }
}