using Mirror;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace RTS
{
    public class MiniMap : MonoBehaviour, IPointerDownHandler, IDragHandler
    {
        [SerializeField] private RectTransform minimapRect = null;
        [SerializeField] private Transform mapTransform = null;
        private CameraControllerConfigurationSO ccConfigSO;

        private CameraController camController;
        private Transform playerCamera;
        private float mapScaleX;
        private float mapScaleZ;

        private void Start()
        {
            ccConfigSO = (NetworkManager.singleton as CustomNetworkManager).CameraControllerConfigurationSO;
            mapScaleX = mapTransform.localScale.x/2;
            mapScaleZ = mapTransform.localScale.z/2; // in 3d, we are using 'xz' plane for world env
        }
        private void Update()
        {
            // -------------------------------------- [ Fail Safes ] -----------------------------------------
            if (camController == null) // just for safty, incase to work without offline and online scenes
            {
                if (!(NetworkClient.connection == null || NetworkClient.connection.identity == null))
                {
                    camController = NetworkClient.connection.identity.GetComponent<RTSPlayer>().CamController;
                    playerCamera = camController.PlayerCameraTransform;
                }
                else return;
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            MoveCamera();
        }

        public void OnDrag(PointerEventData eventData)
        {
            MoveCamera();
        }

        private void MoveCamera()
        {
            /* RectTransformUtility.ScreenPointToLocalPointInRectangle:
            coverts screen position of mouse relative to the local rect-transform given */
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                minimapRect, // local rect-transform
                Mouse.current.position.ReadValue(), // screen-point
                null, // camera not required for this case
                out Vector2 localPoint))  // save the relative-local-point
                return;

            Vector2 normalizedPoint = new Vector2(
                (localPoint.x - minimapRect.rect.x) / minimapRect.rect.width,
                (localPoint.y - minimapRect.rect.y) / minimapRect.rect.height);
            // normalizing the local-point which can be scaled back to world-point

            // using the above normalized value, we are using it as time value,
            // i.e. interpolation value bet 0 and 1 in between map scale
            Vector3 newCameraPos = new Vector3(
                Mathf.Lerp(-mapScaleX, mapScaleX, normalizedPoint.x),
                playerCamera.position.y, // y axis position of cam will remain constant
                Mathf.Lerp(-mapScaleZ, mapScaleZ, normalizedPoint.y));

            playerCamera.position = newCameraPos - new Vector3(0f, 0f, ccConfigSO.OffsetZ);
        }
    }

}