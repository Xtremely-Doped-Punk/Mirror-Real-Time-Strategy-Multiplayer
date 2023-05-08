using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem; // new input system
using Mirror;

namespace RTS
{
    public class UnitSelectionHandler : MonoBehaviour // client end only UI
    {
        // singleton
        private static UnitSelectionHandler instance;
        public static UnitSelectionHandler Instance { get { return instance; } }
        private void Awake()
        {
            if (instance == null)
                instance = this;
            else
                Destroy(this);
        }

        [SerializeField] private RectTransform seletionArea;
        // note: dont forget to remove the ""Graphic Raycaster" component from UI canvas
        // so that it doesn't block the ray casted onto 3d world to select/make the units move, etc;
        // also keep the canvas as constant pixel size, so that our selection area is not affected canvas scaling...
        [SerializeField] private LayerMask selectionLayers;
        [SerializeField] private LayerMask commandLayers;

        private RTSPlayer player;

        private Camera mainCam;
        private Vector2 mouseStartPos;

        private List<UnitBehaviour> SelectedUnits = new();

        private void Start()
        {
            mainCam = Camera.main;
            if (!(NetworkClient.connection == null || NetworkClient.connection.identity == null))
                player = NetworkClient.connection.identity.GetComponent<RTSPlayer>();
            // getting the player that is connected through the current running host connection
            /*
                Note: if these isnt a connection, the 'connection' attribute value would be null
                thus make sure add offline scene and online scene in network manager, so that this object's start()
                is called when the online scene is called only when the host connects to the game.
             */
            seletionArea.gameObject.SetActive(false); // selection drag rectangle UI, initial set to off

            // whenever a unit - gameobject is destroyed by enemy player,
            // we need to make sure if it is in the listed of selected units my current player, that reference needs to be removed
            // kindaa similar to updating the MyUnits in RTSPlayer.cs but here only if selected condtion is added
            UnitBehaviour.onUnitDespawned += HandleDespawnIfSelected; //AuthorityonUnitDespawned
            GameSession.ClientOnGameOver += HandleGameOver;
        }

        private void OnDestroy()
        {
            UnitBehaviour.onUnitDespawned -= HandleDespawnIfSelected; //AuthorityonUnitDespawned
            GameSession.ClientOnGameOver -= HandleGameOver;
            /* 
                we are using authority's handling on unit being despawned as selection handler is seperate entity
                that runs on client end and not managed by the server, i.e. consider selection handler only run from client's end
                even though u might see here, simple Start() and OnDestroy() is used, might be need to changed to resp client only calls
                like OnStartClient() and OnStopClient() resp as if device is connect as server only, then it must'nt have the abily to 
                selection handling any units spawned in the server, (in case of hosting as server & client, it would automatically get called)
            */
        }

        private void HandleGameOver(string obj)
        {
            // disable selection handler after game over...
            enabled = false; // this stops the update loop
        }

        private void HandleDespawnIfSelected(UnitBehaviour unit)
        {
            //if (SelectedUnits.Contains(unit))
            // we dont the above condition as Remove(obj) tries to remove from list and if successful returns true else false..
            SelectedUnits.Remove(unit);
        }

        private void Update()
        {
            // -------------------------------------- [ Fail Safes ] -----------------------------------------
            if (player == null) // just for safty, incase to work without offline and online scenes
            {
                if (!(NetworkClient.connection == null || NetworkClient.connection.identity == null))
                    player = NetworkClient.connection.identity.GetComponent<RTSPlayer>();
                else return;
            }

            // ---------------------------------- [ Selection Handling ] -------------------------------------
            // single click selection
            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                // start selection area (for multi-objects selection in the screen)
                StartSelectionArea();
            }
            // drag multi object selection
            if (Mouse.current.leftButton.isPressed)
            {
                // update selection area (for multi-objects selection in the screen)
                UpdateSelectionArea();
            }
            if (Mouse.current.leftButton.wasReleasedThisFrame)
            {
                // end selection area (for multi-objects selection in the screen)
                ClearSelectionArea();
            }

            // ----------------------------------- [ Command Handling ] --------------------------------------
            // grouply updating the movement of all selected units
            // Old-Input-System: Input.GetMouseButton(1); for Right Mouse button pressed
            if (Mouse.current.rightButton.wasPressedThisFrame)
            {
                // Old-Input-System: Input.mousePosition; for mouse position in the screen
                var ray = mainCam.ScreenPointToRay(Mouse.current.position.ReadValue());
                if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, commandLayers)) // return true if it hits some object
                {

                    // check if the player has clicked on a enemy object (if yes set target to that unit)
                    if (CustomNetworkManager.TryGetComponentThoroughly<TargetBehaviour>(hit.collider.gameObject, out var targObj))
                    {
                        if (targObj.isOwned) return;  // if the client owns the object, then we can't set friendly units as target
                        if (targObj.isTargetable)
                            SetTargetForSelected(targObj);
                    }
                    else // else move to that point
                        MoveSelected(hit.point);
                }
            }
        }

        private void StartSelectionArea()
        {
            if (!Keyboard.current.ctrlKey.isPressed)
            {// extend selection by not clearing the previous selection if 'ctrl' key is held down
             // clear the previous selection
                foreach (var unit in SelectedUnits) unit.Deselect(); //deselect all
                SelectedUnits.Clear();
            }

            // setup UI
            seletionArea.gameObject.SetActive(true);
            mouseStartPos = Mouse.current.position.ReadValue();
        }

        private void UpdateSelectionArea()
        {
            var AreaVec = Mouse.current.position.ReadValue() - mouseStartPos;
            seletionArea.sizeDelta = new Vector2(Mathf.Abs(AreaVec.x), Mathf.Abs(AreaVec.y));

            var centre = mouseStartPos + AreaVec / 2; // centre of selection area,(in screen space)
            // which need to converted to range[0,1] which is done by dividing with screen dimentions
            // so that it can be set as anchor points
            seletionArea.anchorMin = seletionArea.anchorMax = new Vector2(centre.x / Screen.width, centre.y / Screen.height);
            /*
                as the pivot is centre, we need make sure that anchor point is added with (size of rect)/2 
                i.e., added length/2 and width/2. Because as per our convention of draging a rect,
                the start poiny needs to at the corner of rect, not at centre(pivot)
            */
        }

        private void ClearSelectionArea()
        {
            // clear up UI
            seletionArea.gameObject.SetActive(false);
            var mouseEndPos = Mouse.current.position.ReadValue();

            if (seletionArea.sizeDelta.sqrMagnitude > 0) // drag selection box
            {
                var max = new Vector2(Mathf.Max(mouseStartPos.x, mouseEndPos.x), Mathf.Max(mouseStartPos.y, mouseEndPos.y));
                var min = new Vector2(Mathf.Min(mouseStartPos.x, mouseEndPos.x), Mathf.Min(mouseStartPos.y, mouseEndPos.y));
                foreach (UnitBehaviour unit in player.MyUnits)
                {
                    if (SelectedUnits.Contains(unit)) continue;
                    // converting objects in 3D space to 2D screen point (as it is easier and faster)
                    // rather than check any ray from 2D selection area that hits on the 3D object colliders
                    // (like did for "single click" in below else block)
                    var screen_point = mainCam.WorldToScreenPoint(unit.transform.position);
                    if ((screen_point.x >= min.x && screen_point.x <= max.x) && (screen_point.y >= min.y && screen_point.y <= max.y))
                    {
                        SelectedUnits.Add(unit);
                        unit.Select();
                    }
                }
            }
            else // single click
            {
                var ray = mainCam.ScreenPointToRay(mouseEndPos);
                if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, selectionLayers))
                // return true only if it hits any object of the given layers
                {
                    var unit = hit.collider.GetComponentInParent<UnitBehaviour>();
                    // note using trigger on collider, allows it not to physically collide with other objects,
                    // but it can still detect raycasts and projectiles as well
                    // (for this tank game particulaarly we dont need physical properties of collider)

                    if (unit != null)
                    // just for safty to check if the object out ray (wrt mouse) hits and object having unit component
                    {
                        // check if that particular unit is (owned by)/(has authority) by the current client/user/player
                        if (unit.isOwned && !SelectedUnits.Contains(unit))
                        {
                            SelectedUnits.Add(unit);
                            unit.Select();
                        }
                    }
                }
            }
        }
        private void MoveSelected(Vector3 impactPos)
        {
            foreach (UnitBehaviour unit in SelectedUnits)
            {
                unit.cmdMove(impactPos); // impact point on the object's collider
            }
        }

        private void SetTargetForSelected(TargetBehaviour targObj)
        {
            foreach (UnitBehaviour unit in SelectedUnits)
            {
                unit.TargetingConfig.cmdSetTarget(targObj);
            }
        }
    }
}
