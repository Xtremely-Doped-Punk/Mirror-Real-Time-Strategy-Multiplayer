using Mirror;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace MultiplayerBasis
{
    public class MyPlayerMovement : NetworkBehaviour
    {
        private NavMeshAgent agent;
        private Camera MainCamera;

        // Start is called before the first frame update
        void Start()
        {
            agent = GetComponent<NavMeshAgent>();
        }

        // to prevent the server from running the update fn for player movenment only on client end,
        [ClientCallback] // we are using this attribute (without printing warning and stops server from doing it)
        void Update()
        {
            // as this funtion is now runnable by alll the clients,
            // we need to check for if the client trying to acces this methodology has the authority to run it ot not
            // i.e., only the owner-client of its player can access this method (hasAuthority --> isOwned) attribute updated
            if (!isOwned || !Input.GetMouseButton(1) || MainCamera == null) return;

            // update on Right Mouse button pressed
            var ray = MainCamera.ScreenPointToRay(Input.mousePosition); // converts screen mouse_position to world-space ray
            // using physics raycast, we are extending this ray in world env, until it hits anything, i.e. Mathf.Infinity
            if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity)) // return true if it hits some object
            {
                CmdMove(hit.point); // impact point on the object's collider
            }
        }

        #region Server

        [Command] private void CmdMove(Vector3 position)
        {

            // basically we are getting the mouse click position and validaing to travel the player till the valid point in environment
            // close the give direction using unity AI, i.e. using NavMesh components that knows all the valid positions in enviroment
            // based on static objects in the env, that can be visualized in navigation window in unity editor

            // check if the position paramater given is valid
            if (NavMesh.SamplePosition(position, out NavMeshHit hit, 1f, NavMesh.AllAreas))
                // the NavMeshHit returns certain attributes about the valid movement possible corresponding to the given position,
                // here we are specifying to consider all possible valid navigatable areas
                agent.SetDestination(hit.position);
        }

        #endregion

        #region Client

        // gettting the instance of camera, we are not getting the references in Start as
        // some (old) players instances might get all the camera references of other (new) players
        // as Start() is designed for the engine perspective and not client individuality
        public override void OnStartAuthority()
        {
            // thus we need to get to know only about the camera refence this particular client-player
            // thus this fn "OnStartAuthority" is like a start() to the client-player when a client connects to the server
            MainCamera = Camera.main;
        }

        #endregion
    }
}
