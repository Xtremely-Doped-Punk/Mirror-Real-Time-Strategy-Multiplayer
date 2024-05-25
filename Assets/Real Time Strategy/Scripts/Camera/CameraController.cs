using Cinemachine;
using Mirror;
using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RTS
{
    public class CameraController : NetworkBehaviour
    {
        [SerializeField] private CinemachineVirtualCamera playerVirtualCamera = null; 
        public Transform PlayerCameraTransform => playerVirtualCamera.transform;
        float PlayerCameraFOV { get => playerVirtualCamera.m_Lens.FieldOfView; set => playerVirtualCamera.m_Lens.FieldOfView =value; }
        
        private CameraControllerConfigurationSO ccConfigSO;
        private Vector2 prevMoveInput;
        private float prevZoomInput;

        private PlayerControls controls;


        public override void OnStartLocalPlayer()
        {
            Debug.Log("Local Camera Controller Setup");
            ccConfigSO = (NetworkManager.singleton as CustomNetworkManager).CameraControllerConfigurationSO;
            PlayerCameraFOV = ccConfigSO.CameraFOV;
            var pos = PlayerCameraTransform.position;
            PlayerCameraTransform.position = new Vector3(pos.x, ccConfigSO.WorldYLimit, pos.z);
            var rot = PlayerCameraTransform.eulerAngles;
            PlayerCameraTransform.eulerAngles = new Vector3(ccConfigSO.AngleOfInclination, rot.y, rot.z);
        }

        public override void OnStartAuthority()
        {
            playerVirtualCamera.gameObject.SetActive(true);

            controls = new PlayerControls();

            controls.Player.MoveCamera.performed += SetPreviousMoveInput;
            controls.Player.MoveCamera.canceled += SetPreviousMoveInput;
            controls.Player.ZoomCamera.performed += SetPreviousZoomInput;
            controls.Player.ZoomCamera.canceled += SetPreviousZoomInput;

            controls.Enable();
        }

        [ClientCallback]
        private void Update()
        {
            if (!isOwned || !Application.isFocused) { return; }

            UpdateCameraPosition();
            UpdateCameraZoom();
        }

        private void UpdateCameraZoom()
        {
            if (prevZoomInput < 0.01) return;
            Debug.Log($"ScrollDelta: inp={prevZoomInput}, mouse={Mouse.current.scroll.ReadValue()}");
            
            //prevZoomInput = Mouse.current.scroll.ReadValue().normalized.y;
            
            PlayerCameraFOV = Mathf.Clamp(PlayerCameraFOV + prevZoomInput,
                ccConfigSO.CameraFOV - ccConfigSO.ZoomDeviation,
                ccConfigSO.CameraFOV + ccConfigSO.ZoomDeviation);
        }

        private void UpdateCameraPosition()
        {
            Vector3 pos = PlayerCameraTransform.position;

            if (prevMoveInput == Vector2.zero)
            {
                Vector3 cursorMovement = Vector3.zero;

                Vector2 cursorPosition = Mouse.current.position.ReadValue();

                if (cursorPosition.y >= Screen.height - ccConfigSO.ScreenBorderThickness.y)
                {
                    cursorMovement.z += 1;
                }
                else if (cursorPosition.y <= ccConfigSO.ScreenBorderThickness.y)
                {
                    cursorMovement.z -= 1;
                }
                if (cursorPosition.x >= Screen.width - ccConfigSO.ScreenBorderThickness.x)
                {
                    cursorMovement.x += 1;
                }
                else if (cursorPosition.x <= ccConfigSO.ScreenBorderThickness.x)
                {
                    cursorMovement.x -= 1;
                }

                pos += ccConfigSO.MoveSpeed * Time.deltaTime * cursorMovement.normalized;
            }
            else
            {
                pos += ccConfigSO.MoveSpeed * Time.deltaTime * new Vector3(prevMoveInput.x, 0f, prevMoveInput.y);
            }
            //Debug.Log("Unclamped goto pos:" + pos);
            pos.x = Mathf.Clamp(pos.x, ccConfigSO.WorldXLimits.x, ccConfigSO.WorldXLimits.y);
            pos.z = Mathf.Clamp(pos.z, ccConfigSO.WorldZLimits.x, ccConfigSO.WorldZLimits.y);
            //Debug.Log("Clamped goto pos:" + pos);
            PlayerCameraTransform.position = pos;
        }

        private void SetPreviousMoveInput(InputAction.CallbackContext moveInp)
        {
            prevMoveInput = moveInp.ReadValue<Vector2>();
        }

        private void SetPreviousZoomInput(InputAction.CallbackContext zoomInp)
        {
            prevZoomInput = zoomInp.ReadValue<float>();
        }
    }

}