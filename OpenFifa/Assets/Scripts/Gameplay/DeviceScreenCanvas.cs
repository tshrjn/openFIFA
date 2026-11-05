using UnityEngine;

namespace OpenFifa.Gameplay
{
    /// <summary>
    /// Marks a Screen Space canvas that belongs on the iPad's own screen (touch controls, companion UI).
    /// While an external monitor is connected, every other overlay canvas (HUD, scoreboard, menus) moves
    /// to the monitor; canvases with this component stay on the iPad.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DeviceScreenCanvas : MonoBehaviour
    {
    }
}
