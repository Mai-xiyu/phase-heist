using Godot;

namespace PhaseHeist;

public static class MultiplayerGuard
{
    public static bool HasPeer(MultiplayerApi multiplayer)
    {
        return multiplayer.MultiplayerPeer != null;
    }

    public static bool IsServerOrOffline(MultiplayerApi multiplayer)
    {
        return !HasPeer(multiplayer) || multiplayer.IsServer();
    }

    public static bool IsClient(MultiplayerApi multiplayer)
    {
        return HasPeer(multiplayer) && !multiplayer.IsServer();
    }

    public static int RemoteSenderIdOrZero(MultiplayerApi multiplayer)
    {
        return HasPeer(multiplayer) ? multiplayer.GetRemoteSenderId() : 0;
    }
}
