namespace FaFLobbySimClient;

/// <summary>
/// Indicates how full a lobby is.
/// </summary>
/// <param name="Total">Total number of slots available.</param>
/// <param name="Occupied">Total number of slots occupied by players, AI or closed.</param>
internal record Occupancy(int Total, int Occupied);