using UnityEngine;

// Identidad de red del objeto real; nunca se agrega a la copia equipada del arma.
public sealed class MultiplayerItemIdentity : MonoBehaviour
{
    [SerializeField] private string embeddedId;
    public string Id { get; set; }
    public string Run { get; set; }
    public string EmbeddedId => embeddedId;
}
