using UnityEngine;
using UnityEngine.InputSystem.EnhancedTouch;
using OpenFifa.Core;

namespace OpenFifa.Gameplay
{
    /// <summary>
    /// Manages local multiplayer on a single device via split touch zones.
    /// Player 1 controls left half, Player 2 controls right half.
    /// Each player gets independent joystick and action button input.
    /// </summary>
    public class LocalMultiplayerManager : MonoBehaviour
    {
        [SerializeField] private VirtualJoystick player1Joystick;
        [SerializeField] private VirtualJoystick player2Joystick;

        private SplitTouchZoneLogic _zoneLogic;
        private InputRouter _inputRouter;
        private LocalMultiplayerConfig _config;

        /// <summary>Current movement input for Player 1.</summary>
        public Vector2 Player1Input { get; private set; }

        /// <summary>Current movement input for Player 2.</summary>
        public Vector2 Player2Input { get; private set; }

        private void Awake()
        {
            _config = new LocalMultiplayerConfig();
            _zoneLogic = new SplitTouchZoneLogic(Screen.width, Screen.height);
            _inputRouter = new InputRouter(_zoneLogic);
        }

        private void OnEnable()
        {
            EnhancedTouchSupport.Enable();
        }

        private void OnDisable()
        {
            EnhancedTouchSupport.Disable();
        }

        private void Update()
        {
            ProcessTouches();
        }

        private void ProcessTouches()
        {
            var touches = UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches;

            // Clear router for fresh frame
            _inputRouter.ClearAll();

            foreach (var touch in touches)
            {
                int fingerId = touch.finger.index;
                float x = touch.screenPosition.x;
                float y = touch.screenPosition.y;

                _inputRouter.ProcessTouch(fingerId, x, y);

                int player = _inputRouter.GetOwningPlayer(fingerId);
                if (player == 0 && player1Joystick != null)
                {
                    Player1Input = player1Joystick.OutputVector;
                }
                else if (player == 1 && player2Joystick != null)
                {
                    Player2Input = player2Joystick.OutputVector;
                }
            }

            // Reset inputs for released touches
            if (touches.Count == 0)
            {
                Player1Input = Vector2.zero;
                Player2Input = Vector2.zero;
            }
        }
    }
}
