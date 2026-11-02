public enum PartyRole
{
    None,
    Host,
    Client1,
    Client2
}

public enum PartyOperation
{
    None,
    Connecting,
    Creating,
    Joining,
    Updating,
    Leaving
}

public enum PartyEndReason
{
    Left,
    Kicked,
    HostCancelled,
    HostDisconnected,
    HostAlone,
    Disconnected,
    InvalidRoom
}

/// <summary>Datos de consulta; no expone objetos de Photon a los menus.</summary>
public sealed class PartyMember
{
    public int ActorNumber { get; }
    public string NickName { get; }
    public PartyRole Role { get; }

    public PartyMember(int actorNumber, string nickName, PartyRole role)
    {
        ActorNumber = actorNumber;
        NickName = nickName;
        Role = role;
    }
}

public static class PartyRules
{
    public const int MaxPlayers = 3;
    public const int MaxNameLength = 15;
    public const float JoinCooldownSeconds = 5f;

    public static bool TryNormalizeName(string value, out string name)
    {
        name = (value ?? string.Empty).Trim();
        return name.Length > 0 && name.Length <= MaxNameLength &&
               name.IndexOfAny(new[] { '\r', '\n', '\t' }) < 0;
    }

    public static bool IsValidRoomCode(string value)
    {
        if (value == null || value.Length != 5)
            return false;

        foreach (char character in value)
            if (character < '0' || character > '9')
                return false;

        return true;
    }

    public static string RoleLabel(PartyRole role)
    {
        switch (role)
        {
            case PartyRole.Host: return "Host";
            case PartyRole.Client1: return "Cliente1";
            case PartyRole.Client2: return "Cliente2";
            default: return "Conectando";
        }
    }

    public static string EndMessage(PartyEndReason reason)
    {
        switch (reason)
        {
            case PartyEndReason.Kicked: return "El host te expulso de la sala. Puedes volver a entrar en 5 segundos.";
            case PartyEndReason.HostCancelled: return "La sala se disolvio.";
            case PartyEndReason.HostDisconnected: return "El host se desconecto. La sala se disolvio.";
            case PartyEndReason.HostAlone: return "Te quedaste solo. La sala se disolvio.";
            case PartyEndReason.Disconnected: return "Se perdio la conexion con Photon. Saliste de la party.";
            case PartyEndReason.InvalidRoom: return "Esta sala no es compatible con la party de Treetop.";
            default: return "Saliste de la sala. Puedes volver a entrar en 5 segundos.";
        }
    }
}
