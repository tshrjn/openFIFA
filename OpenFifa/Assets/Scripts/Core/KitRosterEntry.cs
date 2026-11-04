using System;

namespace OpenFifa.Core
{
    /// <summary>
    /// One squad member as stored in kit library JSON (placeholder or licensed data).
    /// Public fields so UnityEngine.JsonUtility can (de)serialize it.
    /// </summary>
    [Serializable]
    public class KitRosterEntry
    {
        /// <summary>Surname as printed on the shirt back.</summary>
        public string Name = "";

        /// <summary>Squad number (1-99).</summary>
        public int Number = 1;
    }
}
