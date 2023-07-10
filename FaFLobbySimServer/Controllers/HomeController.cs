﻿using System.Text;
using System.Web;
using Microsoft.AspNetCore.Mvc;

namespace FaFLobbySimServer.Controllers;

[Route("")]
public class HomeController : ControllerBase
{
    private const string HomeTemplate = """
        <!DOCTYPE html>
        <html>
            <head>
                <title>Choose a lobby</title>
            </head>
            <body>
                <form action="lobby" method="get" class="form-example">
                    <label for="lobby">Lobby id:</label>
                    <input type="text" id="lobby" name="id"/>
                    <button type="submit">Go</button>
                </form>
            </body>
        </html>
        """;

    private const string LobbyTemplate = """
        <!DOCTYPE html>
        <html>
            <head>
                <title>Lobby {0}</title>
            </head>
            <body>
                <h1>Lobby Report: {0}</h1>
                <p>Latest Occupancy is: {1}</p>
            </body>
        </html>
        """;

    [HttpGet]
    public IActionResult Home()
    {
        return Content(HomeTemplate, "text/html", Encoding.UTF8);
    }

    [HttpGet("lobby")]
    public IActionResult Lobby([FromQuery] string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return NotFound();
        }

        var idSafeish = HttpUtility.HtmlEncode(id);

        string occStr;
        if (SystemStore.TryGetOccupancyRecords(id, out var records) && records.Count > 0)
        {
            occStr = $"{records[^1].Occupied} / {records[^1].Total}";
        }
        else
        {
            occStr = "(unknown)";
        }

        var html = string.Format(LobbyTemplate,
            idSafeish,
            occStr);

        return Content(html, "text/html", Encoding.UTF8);
    }
}