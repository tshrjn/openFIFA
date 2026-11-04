namespace OpenFifa.Core
{
    /// <summary>Kind of a value parsed by <see cref="JsonParser"/>.</summary>
    public enum JsonNodeKind
    {
        Null = 0,
        Bool = 1,
        Number = 2,
        String = 3,
        Array = 4,
        Object = 5
    }
}
