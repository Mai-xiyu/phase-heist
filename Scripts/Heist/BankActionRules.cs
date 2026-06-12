namespace PhaseHeist;

/// <summary>
/// 银行行为可用性判定。HUD 交互提示与服务端裁决共用同一套规则，
/// 保证「显示可按」与「按下生效」一致。
/// </summary>
public static class BankActionRules
{
    /// <summary>行为当前对该玩家是否可用；不可用时给出拒绝文案 key。</summary>
    public static bool Check(BankActionType action, PlayerController player, HeistGameMode mode, out string deniedKey)
    {
        bool locked = mode.DoorsLocked;
        bool robber = player.Team == PlayerTeam.Robber;
        bool police = player.Team == PlayerTeam.Police;

        switch (action)
        {
            case BankActionType.EnterBank:
                deniedKey = "st.enterbank.denied";
                return robber && !locked;

            case BankActionType.ChangeDisguise:
                deniedKey = "st.disguise.denied";
                return robber && !locked;

            case BankActionType.PoliceShoutDown:
                deniedKey = "st.shout.denied";
                return police && !locked;

            case BankActionType.Negotiate:
                deniedKey = "st.negotiate.locked";
                return locked;

            case BankActionType.VaultLoot:
                deniedKey = "st.vault.denied";
                return robber && locked && !player.CarryingLoot;

            case BankActionType.ReleaseHostage:
            case BankActionType.KillHostage:
                deniedKey = "st.host.denied";
                return robber && locked && mode.HostagesRemaining > 0;

            case BankActionType.FakeHostage:
                deniedKey = "st.fake.denied";
                return robber && locked && !player.IsFakeHostage;

            case BankActionType.RecordMessage:
                deniedKey = "st.record.denied";
                return robber && locked && player.IsFakeHostage;

            case BankActionType.PoliceEntryFront:
            case BankActionType.PoliceEntrySide:
            case BankActionType.PoliceEntryBack:
                if (!police)
                {
                    deniedKey = "st.entry.denied";
                    return false;
                }

                deniedKey = "st.entry.early";
                return mode.PoliceCanBreach;

            case BankActionType.BlockSideExit:
            case BankActionType.BlockSewerExit:
                deniedKey = "st.block.denied";
                return police && locked;

            case BankActionType.EscapeSideDoor:
            case BankActionType.EscapeSewer:
                deniedKey = "st.escape.needloot";
                return robber && mode.LootTaken;
        }

        deniedKey = "st.unknown";
        return false;
    }
}
