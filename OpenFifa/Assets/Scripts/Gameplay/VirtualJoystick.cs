using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using OpenFifa.Core;

namespace OpenFifa.Gameplay
{
    /// <summary>
    /// Virtual joystick for touch input (iPad).
    /// Dynamic: appears at touch point rather than a fixed position.
    /// Implements IInputProvider interface pattern.
    /// </summary>
    public class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [SerializeField] private RectTransform _outerRing;
        [SerializeField] private RectTransform _innerThumb;
        [SerializeField] private float _outerRadius = 100f;
        [SerializeField] private float _deadZone = 0.1f;

        private VirtualJoystickLogic _logic;

        /// <summary>Current movement vector (-1 to 1 on each axis).</summary>
        public Vector2 Movement => new Vector2(
            _logic != null ? _logic.OutputX : 0f,
            _logic != null ? _logic.OutputY : 0f
        );

        /// <summary>Whether the joystick is being touched.</summary>
        public bool IsActive => _logic != null && _logic.IsActive;

        private void Awake()
        {
            _logic = new VirtualJoystickLogic(_outerRadius, _deadZone);

            if (_outerRing != null)
                _outerRing.gameObject.SetActive(false);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            _logic.OnPointerDown(eventData.position.x, eventData.position.y);

            if (_outerRing != null)
            {
                _outerRing.position = eventData.position;
                _outerRing.gameObject.SetActive(true);
            }

            if (_innerThumb != null)
                _innerThumb.position = eventData.position;
        }

        public void OnDrag(PointerEventData eventData)
        {
            _logic.OnDrag(eventData.position.x, eventData.position.y);

            if (_innerThumb != null && _outerRing != null)
            {
                Vector2 direction = new Vector2(_logic.OutputX, _logic.OutputY);
                _innerThumb.position = (Vector2)_outerRing.position + direction * _outerRadius;
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _logic.OnPointerUp();

            if (_outerRing != null)
                _outerRing.gameObject.SetActive(false);

            if (_innerThumb != null && _outerRing != null)
                _innerThumb.position = _outerRing.position;
        }
    }
}
