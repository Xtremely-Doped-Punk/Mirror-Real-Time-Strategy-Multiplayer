using Cinemachine;
using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RTS
{
    public class CameraController : NetworkBehaviour
    {
        [SerializeField] private CinemachineVirtualCamera playerVirtualCamera = null; public Transform PlayerCameraTransform => playerVirtualCamera.transform;
        private CameraControllerConfigurationSO ccConfigSO;
        private Vector2 previousInput;

        private PlayerControls controls;


        public override void OnStartLocalPlayer()
        {
            Debug.Log("Local Camera Controller Setup");
            ccConfigSO = (NetworkManager.singleton as CustomNetworkManager).CameraControllerConfigurationSO;
            playerVirtualCamera.m_Lens.FieldOfView = ccConfigSO.CameraFOV;
            var pos = PlayerCameraTransform.position;
            PlayerCameraTransform.position = new Vector3(pos.x, ccConfigSO.WorldYLimit, pos.z);
            var rot = PlayerCameraTransform.eulerAngles;
            PlayerCameraTransform.eulerAngles = new Vector3(ccConfigSO.AngleOfInclination, rot.y, rot.z);
        }

        public override void OnStartAuthority()
        {
            playerVirtualCamera.gameObject.SetActive(true);

            controls = new PlayerControls();

            controls.Player.MoveCamera.performed += SetPreviousInput;
            controls.Player.MoveCamera.canceled += SetPreviousInput;

            controls.Enable();
        }

        [ClientCallback]
        private void Update()
        {
            if (!isOwned || !Application.isFocused) { return; }

            UpdateCameraPosition();
        }

        private void UpdateCameraPosition()
        {
            Vector3 pos = PlayerCameraTransform.position;

            if (previousInput == Vector2.zero)
            {
                Vector3 cursorMovement = Vector3.zero;

                Vector2 cursorPosition = Mouse.current.position.ReadValue();

                if (cursorPosition.y >= Screen.height - ccConfigSO.ScreenBorderThickness.y)
                {
                    cursorMovement.z -= 1;
                }
                else if (cursorPosition.y <= ccConfigSO.ScreenBorderThickness.y)
                {
                    cursorMovement.z += 1;
                }
                if (cursorPosition.x >= Screen.width - ccConfigSO.ScreenBorderThickness.x)
                {
                    cursorMovement.x -= 1;
                }
                else if (cursorPosition.x <= ccConfigSO.ScreenBorderThickness.x)
                {
                    cursorMovement.x += 1;
                }

                pos += ccConfigSO.Speed * Time.deltaTime * cursorMovement.normalized;
            }
            else
            {
                pos += ccConfigSO.Speed * Time.deltaTime * new Vector3(previousInput.x, 0f, previousInput.y);
            }
            //Debug.Log("Unclamped goto pos:" + pos);
            pos.x = Mathf.Clamp(pos.x, ccConfigSO.WorldXLimits.x, ccConfigSO.WorldXLimits.y);
            pos.z = Mathf.Clamp(pos.z, ccConfigSO.WorldZLimits.x, ccConfigSO.WorldZLimits.y);
            //Debug.Log("Clamped goto pos:" + pos);
            PlayerCameraTransform.position = pos;
        }

        private void SetPreviousInput(InputAction.CallbackContext ctx)
        {
            previousInput = ctx.ReadValue<Vector2>();
        }
    }

}