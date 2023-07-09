namespace FaFLobbySimServer;

public class OccupancyUpload
{
    public string Identifier { get; set; } = string.Empty;

    public string ClientId { get; set; } = string.Empty;

    public int Occupied { get; set; }

    public int Total { get; set; }
}