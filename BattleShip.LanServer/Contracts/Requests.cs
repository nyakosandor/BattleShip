using BattleShip.Core.Domain;

namespace BattleShip.LanServer.Contracts;

public sealed record ConnectRequest(string? Name);

public sealed record PlacementDto(ShipType Type, CoordinateDto Origin, Orientation Orientation);

public sealed record DeployRequest(IReadOnlyList<PlacementDto> Placements);

public sealed record FireRequest(CoordinateDto Target);
