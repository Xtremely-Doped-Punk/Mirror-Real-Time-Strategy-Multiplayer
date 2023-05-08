using Mirror;
using System;
using UnityEngine;
// unity's AI engine help is navigation of the nav-agent with resp statically based environment
using UnityEngine.AI;
using UnityEngine.Events;

namespace RTS
{
    [RequireComponent(typeof(NavMeshAgent), typeof(Health), typeof(FiringBehaviour))]
    public class UnitBehaviour : NetworkBehaviour
    {
        [Header("Component References")]
        [SerializeField] private NavMeshAgent agentConfig;
        [SerializeField] private TargetBehaviour targetingConfig; public TargetBehaviour TargetingConfig => targetingConfig;
        [SerializeField] private FiringBehaviour firingConfig;
        [SerializeField] private Health healthConfig; public Health HealthConfig => healthConfig;

        [Header("Unit Properties")]
        [SerializeField] private int trainingCost = 100; public int TrainingCost => trainingCost;
        [SerializeField] private UnityEvent onSelected = null;
        [SerializeField] private UnityEvent onDeselected = null;

        // to keep track of each player's units spawned by them resp (kept track in RTSPlayer.cs)
        public static event Action<UnitBehaviour> onUnitSpawned;
        public static event Action<UnitBehaviour> onUnitDespawned;

        // Try getting the required references when the component is placed itself
        void Reset()
        {
            agentConfig = GetComponent<NavMeshAgent>();
            firingConfig = GetComponent<FiringBehaviour>();
            healthConfig = GetComponent<Health>();
            targetingConfig = GetComponent<TargetBehaviour>();
        }


        #region Server

        [ServerCallback] private void Update() // making sure it only called from server end
        {
            if (targetingConfig.activeTarget != null)
            {
                /*
                    if the unit is assigned to target run this block of updatation
                    so that the unit always tries to follows the target-obj (enemy-obj)
                    to stay in the range of attacking of the target, later when in the range
                    only then the unit can shoot projectiles to damage the target
                */

                // ---------------------------------- [ Chase & Stop ] -------------------------------------

                if (!targetingConfig.CheckInRange()) // if not in attacking range
                {
                    // --- chase ---
                    firingConfig.inRange = false; // if target is not in range, can't shoot

                    agentConfig.SetDestination(targetingConfig.activeTarget.transform.position);
                    // cant use aim-point here, as it is elevated in y-axis, will be used for shoot projectiles direction
                }
                else
                {
                    // --- stop ---
                    firingConfig.inRange = true; // now that the target is in range, we can shoot projectiles towards it

                    if (agentConfig.hasPath) // if in target's range and agent's path still exist
                        agentConfig.ResetPath();
                }
            }
            else
            {

                firingConfig.inRange = false; // if no target is preset
                /*
                    basically the movement of nav-mesh agent, when a destination target location is set,
                    it always tries to go that position, even after reaching the stopping distance radius of the target
                    because its path is not cleared. so what it mean is that, if that unit was moved my other agent 
                    out of the stopping dist rad, then it again tries go to the destination radius as the path was not cleared after
                    reaching the desired destination's stoppping distance radius.
                 */
                if (!agentConfig.hasPath) return; // to stop the agent from calculating and reseting path at the same time
                if (agentConfig.remainingDistance < agentConfig.stoppingDistance) agentConfig.ResetPath();
                // thus we have check every frame, if it had reached the destination radius, and if reached, reset/clear path of the agent
            }
        }

        public override void OnStartServer()
        {
            // base.OnStartServer(); // empty definition in base class (thus, commented)
            onUnitSpawned?.Invoke(this); //ServeronUnitDespawned
            // event invoked in devices connected as: [server only, server & client] {refer RTS Player script for details}

            healthConfig.onDeath += HandleUnitDeath;
            GameSession.ServerOnGameOver += GameOverDisabler;
        }

        public override void OnStopServer()
        {
            // base.OnStopServer(); // // empty definition in base class (thus, commented)
            onUnitDespawned?.Invoke(this); //ServeronUnitDespawned
            // event invoked in devices connected as: [server only, server & client] {refer RTS Player script for details}

            healthConfig.onDeath -= HandleUnitDeath;
            GameSession.ServerOnGameOver += GameOverDisabler;
        }

        [Server]
        private void GameOverDisabler()
        {
            // to stop immediately targetting system so that any remaining units cant fire
            targetingConfig.ClearTarget();
            // stop movement immedieately
            if (agentConfig.hasPath)
                agentConfig.ResetPath();
        }

        [Server]
        private void HandleUnitDeath()
        {
            // add additional handling for the resp unit getting destroyed...

            //finally delete
            NetworkServer.Destroy(gameObject);
        }

        // individual [ClientCallback] unit movement, has been removed; all the selected is grouply moved by the selection handler
        [Command]
        public void cmdMove(Vector3 position)
        {
            ServerMoveUnit(position);
        }

        [Server] 
        public void ServerMoveUnit(Vector3 position)
        {
            // when ever move-command is given to a unit, it overrides its any other older command,
            // such as to clear the target so that it doesn't follow the target to attach even after given a move command
            targetingConfig.ClearTarget(); // even an invalid attempt to move clears the target of the unit


            // basically we are getting the mouse click position and validaing to travel the player till the valid point in environment
            // close the give direction using unity AI, i.e. using NavMesh components that knows all the valid positions in enviroment
            // based on static objects in the env, that can be visualized in navigation window in unity editor

            // check if the position paramater given is valid
            if (NavMesh.SamplePosition(position, out NavMeshHit hit, 1f, NavMesh.AllAreas))
                // the NavMeshHit returns certain attributes about the valid movement possible corresponding to the given position,
                // here we are specifying to consider all possible valid navigatable areas
                agentConfig.SetDestination(hit.position);
        }

        #endregion

        #region Client

        /* explanation for cond "if (isClientOnly)"
            here i have both server/authority onUnitSpawned as common ActionEvent, thus i need to put this condtion
            to avoid calling its attached delegate fn() two times when this game is hosted as server and client
         */
        public override void OnStartAuthority() //OnStartClient() but with authority check
        {
            //if (!isOwned) return; // skip (needed if we are executing in OnStartClient())
            /* suppose if a host is a client as well as server, then both On-Start/Stop-Client/Sever is called on it script,
               thus we need to make sure that only once the functionality to track player's units is called once
               i.e., we are checking is the host is connected as a client, then to run functionality to track player's units
               from client's end as server code is not run in that build...
            */
            if (!NetworkServer.active)  //i.e. isClientOnly refer RTSPlayer for why?
                onUnitSpawned?.Invoke(this); //AuthorityonUnitDespawned
            // event invoked in devices connected as: [client only] {refer RTS Player script for details}
        }
        public override void OnStopClient()
        {
            /* 
                notice that we are'nt using OnStopAuthority() as it gets called only if an object's authority is directly removed ingame
                at the server end, so that does'nt guarantee that, when object is destroyed, its authority is pulled before its destoyed
                conclusion: it doesn't work like OnStartAuthority() as for that case, new game object instanciated and then
                assigned authority by the server end upon client request to spawn it...
            */
            if (!isOwned) return;
            /* 
               we need to make sure that only once the functionality to track player's units is called once
               i.e., we are checking is the host is connected as a client, then to run functionality to track player's units
               from client's end as server code is not run in that build...
            */
            if (isClientOnly)
                onUnitDespawned?.Invoke(this); //AuthorityonUnitDespawned
            // event invoked in devices connected as: [client only] {refer RTS Player script for details}
        }

        [Client] public void Select()
        {
            if (!isOwned) return;
            onSelected?.Invoke(); // invoke event functions, if not null
        }
        [Client] public void Deselect()
        {
            if (!isOwned) return;
            onDeselected?.Invoke(); // invoke event functions, if not null
        }

        #endregion
    }
}
