using Godot;
using System;

namespace PhaseHeist;

public enum HeistPhase
{
    Setup,
    StreetInfiltration,
    Lockdown,
    Negotiation,
    Assault,
    Escape,
    Scored,
    Failed,
}

public partial class HeistGameMode : Node
{
    [Signal]
    public delegate void PhaseChangedEventHandler(int newPhase);

    [Signal]
    public delegate void RulesChangedEventHandler();

    [Export] public float NegotiationSeconds { get; set; } = Balance.NegotiationSeconds;
    [Export] public float PoliceEntryUnlockRatio { get; set; } = Balance.PoliceBreachUnlockRatio;
    [Export] public int StartingHostages { get; set; } = 7;

    public HeistPhase CurrentPhase { get; private set; } = HeistPhase.Setup;
    public bool DoorsLocked { get; private set; }
    public bool LootTaken { get; private set; }
    public bool RecordingPrepared { get; private set; }
    public bool RobberDisguisedAsHostage { get; private set; }
    public EscapeRoute BlockedRoute { get; private set; } = EscapeRoute.None;
    public int HostagesRemaining { get; private set; }
    public int HostagesReleased { get; private set; }
    public int HostagesKilled { get; private set; }
    public int RobberScore { get; private set; }
    public int PoliceScore { get; private set; } = 100;

    private double _lockdownStartedUnix;
    private double _deadlineUnix;
    private readonly System.Collections.Generic.Dictionary<PlayerTeam, double> _negotiateCooldownEnds = new();

    public override void _Ready()
    {
        HeistService.Instance?.RegisterGameMode(this);
        HostagesRemaining = StartingHostages;

        if (MultiplayerGuard.IsServerOrOffline(Multiplayer))
        {
            TransitionTo(HeistPhase.StreetInfiltration);
            BroadcastState();
        }
    }

    public override void _Process(double delta)
    {
        if (!MultiplayerGuard.IsServerOrOffline(Multiplayer) || _deadlineUnix <= 0.0)
        {
            return;
        }

        if (CurrentPhase == HeistPhase.Negotiation && PoliceCanBreach)
        {
            TransitionTo(HeistPhase.Assault);
            BroadcastState();
        }

        if (Time.GetUnixTimeFromSystem() >= _deadlineUnix &&
            CurrentPhase != HeistPhase.Scored &&
            CurrentPhase != HeistPhase.Failed)
        {
            TransitionTo(HeistPhase.Failed);
            BroadcastState();
        }
    }

    public float RemainingSeconds => _deadlineUnix <= 0.0
        ? 0.0f
        : Math.Max(0.0f, (float)(_deadlineUnix - Time.GetUnixTimeFromSystem()));

    public float ElapsedLockdownSeconds => _lockdownStartedUnix <= 0.0
        ? 0.0f
        : Math.Max(0.0f, (float)(Time.GetUnixTimeFromSystem() - _lockdownStartedUnix));

    public bool PoliceCanBreach => DoorsLocked && ElapsedLockdownSeconds >= NegotiationSeconds * PoliceEntryUnlockRatio;

    public void BeginLockdown()
    {
        if (DoorsLocked)
        {
            return;
        }

        DoorsLocked = true;
        _lockdownStartedUnix = Time.GetUnixTimeFromSystem();
        _deadlineUnix = _lockdownStartedUnix + NegotiationSeconds;
        ApplyDoorState(immediate: false);
        TransitionTo(HeistPhase.Negotiation);
        BroadcastState();
    }

    public void NotifyLootTaken()
    {
        if (LootTaken)
        {
            return;
        }

        LootTaken = true;
        RobberScore += Balance.ScoreLootTaken;

        if (CurrentPhase == HeistPhase.Negotiation || CurrentPhase == HeistPhase.Assault)
        {
            TransitionTo(HeistPhase.Escape);
        }

        BroadcastState();
    }

    /// <summary>谈判加分；每队带冷却防刷分。返回是否生效。</summary>
    public bool RecordNegotiation(PlayerTeam team)
    {
        if (!DoorsLocked || CurrentPhase is HeistPhase.Scored or HeistPhase.Failed)
        {
            return false;
        }

        double now = Time.GetUnixTimeFromSystem();
        if (_negotiateCooldownEnds.TryGetValue(team, out double ends) && now < ends)
        {
            return false;
        }

        _negotiateCooldownEnds[team] = now + Balance.NegotiateCooldownSeconds;

        if (team == PlayerTeam.Robber)
        {
            RobberScore += Balance.ScoreNegotiate;
        }
        else
        {
            PoliceScore += Balance.ScoreNegotiate;
        }

        BroadcastState();
        return true;
    }

    public void PrepareRecording()
    {
        if (!DoorsLocked)
        {
            return;
        }

        RecordingPrepared = true;
        RobberScore += 8;
        BroadcastState();
    }

    public void DisguiseAsHostage()
    {
        if (!DoorsLocked)
        {
            return;
        }

        RobberDisguisedAsHostage = true;
        RobberScore += 10;
        BroadcastState();
    }

    public bool ReleaseHostage()
    {
        if (HostagesRemaining <= 0)
        {
            return false;
        }

        HostagesRemaining--;
        HostagesReleased++;
        PoliceScore += Balance.ScoreHostageReleasedPolice;
        RobberScore += Balance.ScoreHostageReleasedRobber;
        BroadcastState();
        return true;
    }

    public bool KillHostage()
    {
        if (HostagesRemaining <= 0)
        {
            return false;
        }

        HostagesRemaining--;
        HostagesKilled++;
        PoliceScore = Math.Max(0, PoliceScore - Balance.ScoreHostageKilledPolicePenalty);
        RobberScore = Math.Max(0, RobberScore - Balance.ScoreHostageKilledRobberPenalty);
        BroadcastState();
        return true;
    }

    public void BlockRoute(EscapeRoute route)
    {
        if (route == EscapeRoute.None)
        {
            return;
        }

        BlockedRoute = route;
        PoliceScore += 5;
        BroadcastState();
    }

    public void CompleteEscape(EscapeRoute route)
    {
        if (!LootTaken)
        {
            return;
        }

        RobberScore += route == EscapeRoute.Sewer ? Balance.ScoreEscapeSewer : Balance.ScoreEscapeSide;
        if (BlockedRoute == route)
        {
            RobberScore += Balance.ScoreEscapeThroughBlock;
            PoliceScore = Math.Max(0, PoliceScore - Balance.ScoreEscapeThroughBlock);
        }

        TransitionTo(HeistPhase.Scored);
        BroadcastState();
    }

    /// <summary>
    /// 逮捕计分。只有携带金库目标的劫匪被捕才直接终结回合，
    /// 普通劫匪被捕仅大额加分（多劫匪局不至于一人失误全队立即失败）。
    /// </summary>
    public void ArrestRobber(bool endsRound)
    {
        PoliceScore += Balance.ScoreArrest;

        if (endsRound)
        {
            TransitionTo(HeistPhase.Failed);
        }

        BroadcastState();
    }

    /// <summary>击倒对方阵营成员的评分。</summary>
    public void AddKillScore(PlayerTeam killerTeam)
    {
        if (killerTeam == PlayerTeam.Police)
        {
            PoliceScore += Balance.ScoreKillByPolice;
        }
        else
        {
            RobberScore += Balance.ScoreKillByRobber;
        }

        BroadcastState();
    }

    public void TransitionTo(HeistPhase phase)
    {
        if (CurrentPhase == phase)
        {
            return;
        }

        CurrentPhase = phase;
        EmitSignal(SignalName.PhaseChanged, (int)phase);
        EmitSignal(SignalName.RulesChanged);
        GD.Print($"[Heist] phase -> {phase}");
    }

    public void BroadcastState()
    {
        EmitSignal(SignalName.RulesChanged);

        if (MultiplayerGuard.HasPeer(Multiplayer) && Multiplayer.IsServer())
        {
            Rpc(nameof(SyncHeistStateRpc),
                (int)CurrentPhase,
                DoorsLocked,
                LootTaken,
                RecordingPrepared,
                RobberDisguisedAsHostage,
                (int)BlockedRoute,
                HostagesRemaining,
                HostagesReleased,
                HostagesKilled,
                RobberScore,
                PoliceScore,
                _lockdownStartedUnix,
                _deadlineUnix);
        }
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true)]
    public void SyncHeistStateRpc(
        int phase,
        bool doorsLocked,
        bool lootTaken,
        bool recordingPrepared,
        bool robberDisguisedAsHostage,
        int blockedRoute,
        int hostagesRemaining,
        int hostagesReleased,
        int hostagesKilled,
        int robberScore,
        int policeScore,
        double lockdownStartedUnix,
        double deadlineUnix)
    {
        CurrentPhase = (HeistPhase)phase;
        DoorsLocked = doorsLocked;
        LootTaken = lootTaken;
        RecordingPrepared = recordingPrepared;
        RobberDisguisedAsHostage = robberDisguisedAsHostage;
        BlockedRoute = (EscapeRoute)blockedRoute;
        HostagesRemaining = hostagesRemaining;
        HostagesReleased = hostagesReleased;
        HostagesKilled = hostagesKilled;
        RobberScore = robberScore;
        PoliceScore = policeScore;
        _lockdownStartedUnix = lockdownStartedUnix;
        _deadlineUnix = deadlineUnix;
        ApplyDoorState(immediate: true);
        CallDeferred(nameof(ApplyDoorStateDeferred));
        EmitSignal(SignalName.PhaseChanged, phase);
        EmitSignal(SignalName.RulesChanged);
    }

    private void ApplyDoorStateDeferred()
    {
        ApplyDoorState(immediate: true);
    }

    private void ApplyDoorState(bool immediate)
    {
        if (!IsInsideTree())
        {
            return;
        }

        foreach (Node node in GetTree().GetNodesInGroup("bank_main_doors"))
        {
            if (node is BankDoor door)
            {
                door.SetClosed(DoorsLocked, immediate);
            }
        }
    }
}
