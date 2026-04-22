using BattleShip.Core.Domain;
using BattleShip.Core.Services;
using BattleShip.LanServer.Contracts;

namespace BattleShip.LanServer;

/// <summary>
/// Authoritative, thread-safe game state for a LAN session.
/// All mutations go through a single lock and are validated against the current phase,
/// the caller's token, and the game rules. Clients are never trusted — fleet placements
/// submitted by the guest are re-validated via <see cref="IGameEngine.TryPlaceShip"/>.
/// </summary>
public sealed class LanGameSession
{
    private readonly object _sync = new();
    private readonly IGameEngine _engine;

    private PlayerSlot? _host;
    private PlayerSlot? _guest;
    private GamePhase _phase = GamePhase.WaitingForClient;
    private PlayerRole _turn = PlayerRole.Host;
    private PlayerRole? _winner;

    public event Action? StateChanged;

    public LanGameSession(IGameEngine engine)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
    }

    /// <summary>
    /// Register the host player with an already-deployed board (produced by the Setup screen).
    /// Must be called exactly once before the server accepts connections.
    /// </summary>
    public ConnectResponse RegisterHost(string name, Board deployedBoard)
    {
        ArgumentNullException.ThrowIfNull(deployedBoard);
        if (deployedBoard.Ships.Count != Fleet.StandardComposition.Count)
        {
            throw new ArgumentException("Host board must have the full standard fleet deployed.", nameof(deployedBoard));
        }

        lock (_sync)
        {
            if (_host is not null)
            {
                throw new InvalidOperationException("Host is already registered.");
            }

            var slot = new PlayerSlot
            {
                Id = Guid.NewGuid(),
                Token = Guid.NewGuid().ToString("N"),
                Name = SanitizeName(name, fallback: "Host"),
                Role = PlayerRole.Host,
                Board = deployedBoard,
                Deployed = true,
            };
            _host = slot;
            RaiseChangedNoLock();
            return new ConnectResponse(slot.Id, slot.Token, slot.Role, slot.Name);
        }
    }

    /// <summary>
    /// Register a guest player. Allowed only while the phase is <see cref="GamePhase.WaitingForClient"/>.
    /// </summary>
    public ActionResult<ConnectResponse> RegisterGuest(string? name)
    {
        lock (_sync)
        {
            if (_host is null)
            {
                return ActionResult<ConnectResponse>.Fail(ActionErrorCodes.InvalidPhase, "Host has not started a game.");
            }
            if (_guest is not null)
            {
                return ActionResult<ConnectResponse>.Fail(ActionErrorCodes.SessionFull, "Session already has two players.");
            }
            if (_phase != GamePhase.WaitingForClient)
            {
                return ActionResult<ConnectResponse>.Fail(ActionErrorCodes.InvalidPhase, "Game is not accepting new players.");
            }

            var slot = new PlayerSlot
            {
                Id = Guid.NewGuid(),
                Token = Guid.NewGuid().ToString("N"),
                Name = SanitizeName(name, fallback: "Guest"),
                Role = PlayerRole.Guest,
                Board = new Board(),
                Deployed = false,
            };
            _guest = slot;
            _phase = GamePhase.Deploying;
            RaiseChangedNoLock();
            return ActionResult<ConnectResponse>.Ok(new ConnectResponse(slot.Id, slot.Token, slot.Role, slot.Name));
        }
    }

    public ActionResult<StateResponse> Deploy(string token, IReadOnlyList<PlacementDto> placements)
    {
        ArgumentNullException.ThrowIfNull(placements);

        lock (_sync)
        {
            if (!TryResolveSlot(token, out var slot))
            {
                return ActionResult<StateResponse>.Fail(ActionErrorCodes.Unauthorized, "Unknown or missing token.");
            }
            if (_phase != GamePhase.Deploying)
            {
                return ActionResult<StateResponse>.Fail(ActionErrorCodes.InvalidPhase, "Deployment is not open.");
            }
            if (slot.Deployed)
            {
                return ActionResult<StateResponse>.Fail(ActionErrorCodes.AlreadyDeployed, "You have already submitted your fleet.");
            }
            if (placements.Count != Fleet.StandardComposition.Count)
            {
                return ActionResult<StateResponse>.Fail(
                    ActionErrorCodes.InvalidFleet,
                    $"Expected {Fleet.StandardComposition.Count} placements, got {placements.Count}.");
            }

            // Build a fresh board from the client-submitted placements using the authoritative engine.
            // Anything illegal (overlap, OOB, duplicate type, wrong composition) is rejected atomically.
            var staged = new Board(slot.Board.Size);
            foreach (var placement in placements)
            {
                var result = _engine.TryPlaceShip(
                    staged,
                    placement.Type,
                    placement.Origin.ToDomain(),
                    placement.Orientation);
                if (!result.Success)
                {
                    return ActionResult<StateResponse>.Fail(
                        ActionErrorCodes.InvalidFleet,
                        $"Invalid placement for {placement.Type}: {result.Error}.");
                }
            }

            var submitted = staged.Ships.Select(s => s.Type).ToHashSet();
            if (!submitted.SetEquals(Fleet.StandardComposition))
            {
                return ActionResult<StateResponse>.Fail(
                    ActionErrorCodes.InvalidFleet,
                    "Fleet composition does not match the standard fleet.");
            }

            slot.Board = staged;
            slot.Deployed = true;

            if ((_host?.Deployed ?? false) && (_guest?.Deployed ?? false))
            {
                _phase = GamePhase.InProgress;
                _turn = PlayerRole.Host;
            }

            RaiseChangedNoLock();
            return ActionResult<StateResponse>.Ok(BuildStateNoLock(slot));
        }
    }

    public ActionResult<FireResponse> Fire(string token, CoordinateDto target)
    {
        lock (_sync)
        {
            if (!TryResolveSlot(token, out var slot))
            {
                return ActionResult<FireResponse>.Fail(ActionErrorCodes.Unauthorized, "Unknown or missing token.");
            }
            if (_phase == GamePhase.Finished)
            {
                return ActionResult<FireResponse>.Fail(ActionErrorCodes.GameFinished, "The game is over.");
            }
            if (_phase != GamePhase.InProgress)
            {
                return ActionResult<FireResponse>.Fail(ActionErrorCodes.InvalidPhase, "The game has not started.");
            }
            if (_turn != slot.Role)
            {
                return ActionResult<FireResponse>.Fail(ActionErrorCodes.InvalidTurn, "It is not your turn.");
            }

            var opponent = slot.Role == PlayerRole.Host ? _guest : _host;
            if (opponent is null)
            {
                return ActionResult<FireResponse>.Fail(ActionErrorCodes.InvalidPhase, "Opponent not present.");
            }

            var shot = _engine.ProcessShot(opponent.Board, target.ToDomain());
            if (shot.Outcome == ShotOutcome.Invalid)
            {
                var message = shot.Error switch
                {
                    ShotError.OutOfBounds => "Target is out of bounds.",
                    ShotError.AlreadyShot => "That cell has already been shot.",
                    _ => "Invalid shot.",
                };
                return ActionResult<FireResponse>.Fail(ActionErrorCodes.InvalidShot, message);
            }

            PlayerRole? nextTurn;
            if (shot.FleetDestroyed)
            {
                _phase = GamePhase.Finished;
                _winner = slot.Role;
                nextTurn = null;
            }
            else
            {
                _turn = slot.Role == PlayerRole.Host ? PlayerRole.Guest : PlayerRole.Host;
                nextTurn = _turn;
            }

            var outcome = shot.Outcome switch
            {
                ShotOutcome.Hit => ShotOutcomeDto.Hit,
                ShotOutcome.Sunk => ShotOutcomeDto.Sunk,
                _ => ShotOutcomeDto.Miss,
            };

            RaiseChangedNoLock();
            return ActionResult<FireResponse>.Ok(new FireResponse(
                outcome,
                target,
                shot.Outcome == ShotOutcome.Sunk ? shot.ShipHit?.Type : null,
                shot.FleetDestroyed,
                nextTurn));
        }
    }

    public StateResponse GetState(string? token)
    {
        lock (_sync)
        {
            PlayerSlot? slot = null;
            if (!string.IsNullOrEmpty(token))
            {
                TryResolveSlot(token, out slot);
            }
            return BuildStateNoLock(slot);
        }
    }

    public bool TryGetRoleForToken(string token, out PlayerRole role)
    {
        lock (_sync)
        {
            if (TryResolveSlot(token, out var slot))
            {
                role = slot.Role;
                return true;
            }
            role = default;
            return false;
        }
    }

    public GamePhase Phase
    {
        get { lock (_sync) { return _phase; } }
    }

    public bool HasGuest
    {
        get { lock (_sync) { return _guest is not null; } }
    }

    /// <summary>
    /// Public copy of the host's <see cref="ConnectResponse"/>. Used by the in-process
    /// host to construct a <see cref="Client.LocalLanClient"/>. Returns <c>null</c> until
    /// <see cref="RegisterHost"/> has been called.
    /// </summary>
    public ConnectResponse? HostIdentity
    {
        get
        {
            lock (_sync)
            {
                return _host is null
                    ? null
                    : new ConnectResponse(_host.Id, _host.Token, _host.Role, _host.Name);
            }
        }
    }

    private StateResponse BuildStateNoLock(PlayerSlot? self)
    {
        var hostView = _host is null ? null : BuildPublicView(_host);
        var guestView = _guest is null ? null : BuildPublicView(_guest);

        PlayerPrivateView? privateView = null;
        if (self is not null)
        {
            privateView = new PlayerPrivateView(
                self.Id,
                self.Name,
                self.Board.Ships.Select(ToShipDto).ToList());
        }

        return new StateResponse(
            _phase,
            _phase == GamePhase.InProgress ? _turn : null,
            _winner,
            hostView,
            guestView,
            privateView);
    }

    private static PlayerPublicView BuildPublicView(PlayerSlot slot)
    {
        var hits = new List<CoordinateDto>();
        var misses = new List<CoordinateDto>();
        var sunkShips = new List<ShipDto>();

        for (int x = 0; x < slot.Board.Size; x++)
        {
            for (int y = 0; y < slot.Board.Size; y++)
            {
                var cell = slot.Board[x, y];
                var dto = new CoordinateDto(x, y);
                if (cell.State == CellState.Hit) hits.Add(dto);
                else if (cell.State == CellState.Miss) misses.Add(dto);
            }
        }

        foreach (var ship in slot.Board.Ships.Where(s => s.IsSunk))
        {
            sunkShips.Add(ToShipDto(ship));
        }

        return new PlayerPublicView(
            slot.Id,
            slot.Name,
            slot.Role,
            Connected: true,
            Deployed: slot.Deployed,
            ShipsRemaining: slot.Board.Ships.Count(s => !s.IsSunk),
            hits,
            misses,
            sunkShips);
    }

    private static ShipDto ToShipDto(Ship ship) => new(
        ship.Type,
        ship.Length,
        ship.Orientation,
        ship.Coordinates.Select(CoordinateDto.From).ToList(),
        ship.Hits.Select(CoordinateDto.From).ToList(),
        ship.IsSunk);

    private bool TryResolveSlot(string? token, out PlayerSlot slot)
    {
        if (!string.IsNullOrEmpty(token))
        {
            if (_host is not null && _host.Token == token) { slot = _host; return true; }
            if (_guest is not null && _guest.Token == token) { slot = _guest; return true; }
        }
        slot = null!;
        return false;
    }

    private void RaiseChangedNoLock()
    {
        // Raise outside the lock to avoid re-entrancy deadlocks.
        var handler = StateChanged;
        if (handler is null) return;
        Task.Run(() =>
        {
            try { handler(); } catch { /* observers must not break the session */ }
        });
    }

    private static string SanitizeName(string? name, string fallback)
    {
        if (string.IsNullOrWhiteSpace(name)) return fallback;
        var trimmed = name.Trim();
        return trimmed.Length > 32 ? trimmed[..32] : trimmed;
    }

    private sealed class PlayerSlot
    {
        public Guid Id;
        public required string Token;
        public required string Name;
        public PlayerRole Role;
        public required Board Board;
        public bool Deployed;
    }
}
