namespace PhaseHeist;

public enum BankActionType
{
    EnterBank,
    ChangeDisguise,
    PoliceShoutDown,
    Negotiate,
    VaultLoot,
    ReleaseHostage,
    KillHostage,
    FakeHostage,
    RecordMessage,
    PoliceEntryFront,
    PoliceEntrySide,
    PoliceEntryBack,
    BlockSideExit,
    BlockSewerExit,
    EscapeSideDoor,
    EscapeSewer,
}

public enum EscapeRoute
{
    None,
    SideDoor,
    Sewer,
}

public enum BankNpcKind
{
    Civilian,
    Hostage,
}

