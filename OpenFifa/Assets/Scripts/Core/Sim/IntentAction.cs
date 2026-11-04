namespace OpenFifa.Core
{
    /// <summary>
    /// Actions a <see cref="PlayerIntent"/> can request. One-shot actions (passes, shots, tackles) start a
    /// wind-up and resolve at a contact frame. Jockey and Shield are held modifiers: send them every tick
    /// you want them active.
    /// </summary>
    public enum IntentAction
    {
        None = 0,
        Pass = 1,
        ThroughPass = 2,
        Lob = 3,
        Cross = 4,
        Shoot = 5,
        FinesseShot = 6,
        DrivenShot = 7,
        Tackle = 8,
        SlideTackle = 9,
        Jockey = 10,
        Shield = 11,
    }
}
