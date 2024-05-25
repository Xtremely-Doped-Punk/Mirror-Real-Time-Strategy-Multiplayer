using Mirror;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RTS
{
    public class RTSPlayer : NetworkBehaviour
    {
        // just for the sake serialization sake as list<list<type>> serialization is not supported in unity
        // (if not required, just directly put List<Building> in place of Items in the below dict)
        [Serializable] private class BaseItems { public List<Building> baseBuildings = new(); }

        private PlayerBase SelectedBase; // server side handled for each resp player

        [SerializeField] private SerializedDictionary<PlayerBase, BaseItems> myBases; // if a base is destroyed, destroy its buildings
        public List<UnitBehaviour> myUnits; // if player is dead, then destoy all of his units
        public List<PlayerBase> MyBases => myBases.Keys.ToList();
        public List<Building> MyBuildings
        {
            get
            {
                return myBases.Values.ToList().SelectMany(x => x.baseBuildings).ToList();
                // https://stackoverflow.com/questions/1191054/how-to-merge-a-list-of-lists-with-same-type-of-items-to-a-single-list-of-items
            }
        }
        public List<UnitBehaviour> MyUnits => myUnits;

        [SerializeField][SyncVar(hook = nameof(ClientSyncResourcesUpdates))] private int gold = 500;
        public int Gold => gold;

        [SerializeField] private Color teamColor; public Color TeamColor => teamColor;

        /* difference between readonly and get only properties:
            Setting the private field of your class as readonly allows you to set the field value only in the constructor of the class 
            (using an inline assignment or a defined constructor method). You will not be able to change it later. 
            readonly class fields are often used for variables that are initialized during class construction, 
            and will never be changed later on. In short, if you need to ensure your property value will never be changed from the outside, 
            but you need to be able to change it from inside your class code, use a "Get-only" property. 
            If you need to store a value which will never change once its initial value has been set, use a readonly field.
         */
        /*
            3 possible modes of host connection:
            --> client only
            --> server only
            --> both client and server
            thus make sure in any event handling of method calling, is not called twice according to the nature of the connection

            generally the order of server & client callbacks are:
            1. StartServer
            2. StartClient (if exists)
            3. StopClient (if exists)
            4. StopServer
         */

        [SerializeField] CameraController camController; public CameraController CamController => camController;

        private void Start()
        {
            //Debug.Log(GameSession.Instance);
            GameSession.Instance.AddPlayerRef(this);
            // slightly bright colors for player team assignment
            teamColor = new Color(Random.Range(0.05f, 1f), Random.Range(0.05f, 1f), Random.Range(0.05f, 1f));
            if (transform.rotation != Quaternion.identity) transform.rotation = Quaternion.identity; // reset rotation to make sure camera controller doesn't get affected
        }

        #region Server

        public override void OnStartServer()
        {
            // ----- units -----
            // to make sure event handling function is called only once in a build
            UnitBehaviour.onUnitSpawned += HandleUnitSpawned; //ServerHandleUnitSpawned
            // all connections as: [server only, server & client] only executes this block
            UnitBehaviour.onUnitDespawned += HandleUnitDespawned; //ServerHandleUnitDespawned

            // --- buildings ---
            Building.onBuildingSpawned += HandleBuildingSpawned;
            Building.onBuildingDespawned += HandleBuildingDespawned;

            // ----- bases -----
            PlayerBase.onBaseCreated += HandleBaseSpawned;
            // make changes to base's buildings from server side only
            PlayerBase.onBaseDestroyed += ServerHandleBaseDespawned;
        }

        public override void OnStopServer()
        {
            UnitBehaviour.onUnitSpawned -= HandleUnitSpawned; //ServerHandleUnitSpawned
            UnitBehaviour.onUnitDespawned -= HandleUnitDespawned; //ServerHandleUnitDespawned

            // --- buildings ---
            Building.onBuildingSpawned -= HandleBuildingSpawned;
            Building.onBuildingDespawned -= HandleBuildingDespawned;

            // ----- bases -----
            PlayerBase.onBaseCreated -= HandleBaseSpawned;
            // make changes to base's buildings from server side only
            PlayerBase.onBaseDestroyed -= ServerHandleBaseDespawned; 
        }

        #endregion

        #region Client

        public override void OnStartAuthority() // authoritized client can only execute this funtion
        {
            /*
                refer OnStopClient explanation, here also same
                only one thing different is that OnStartAuthority() only runs on the client that has authority over the object
                thus, need for checking isOwned is not neccessary here

                also one more thing to notice in OnStartAuthority()..., this method gets called even before some attributes like
                isClientOnly/isClient/isServerOnly/isServer are initialled into the network-behaviour of the object,
                thus we can't use "if (isClientOnly)" to ensure this block runs only at client only device host
                
                NetworkServer is common class for server managing attributes for all that is initaillized as soon as
                the player is connected to the server, thus if it is client only connection, then below attrib is used...
                "NetworkServer.active" => representing if the currently hosted device is the active/acting server (true for server & client also)
                thus in case to ensure client only connection to run this block, we use "negation of NetworkServer.active"
            */
            if (!NetworkServer.active)// all connections as: [client only] only executes this block
            {
                // ----- units -----
                UnitBehaviour.onUnitSpawned += HandleUnitSpawned; //AuthorityHandleUnitSpawned
                UnitBehaviour.onUnitDespawned += HandleUnitDespawned; //AuthorityHandleUnitDespawned

                // --- buildings ---
                Building.onBuildingSpawned += HandleBuildingSpawned;
                Building.onBuildingDespawned += HandleBuildingDespawned;

                // ----- bases -----
                PlayerBase.onBaseCreated += HandleBaseSpawned;
                PlayerBase.onBaseDestroyed += ClientHandleBaseDespawned;
            }
        }

        public override void OnStopClient()
        {
            /*
                we are checking if isOwned here itself to optimize adding
                HandleUnitDeSpawned Delegate irrespective of it belonged to the client or not
                thus rather than check inside the delegate for each object if it belonged to client or not,
                this is more optimized...
            */
            if (isClientOnly && isOwned)// all connections as: [client only] only executes this block
            {
                // ----- units -----
                UnitBehaviour.onUnitSpawned -= HandleUnitSpawned; //AuthorityHandleUnitSpawned
                UnitBehaviour.onUnitDespawned -= HandleUnitDespawned; //AuthorityHandleUnitDespawned

                // --- buildings ---
                Building.onBuildingSpawned -= HandleBuildingSpawned;
                Building.onBuildingDespawned -= HandleBuildingDespawned;

                // ----- bases -----
                PlayerBase.onBaseCreated -= HandleBaseSpawned;
                PlayerBase.onBaseDestroyed -= ClientHandleBaseDespawned;
            }
        }

        #endregion

        // so far, have made combined handler for server as well as client side call for both Spawn/Despawn handling
        // for now make sure, base spawns before building or unit is spamed..

        private void HandleUnitSpawned(UnitBehaviour unit)
        {
            // server only (or) server & client
            if (isServer && unit.connectionToClient.connectionId == connectionToClient.connectionId)
            // check if the spawned unit is owned to same connectionID of the resp player
            // i.e., making sure the client who owns the player is the same client who owns the unit
            {
                myUnits.Add(unit);
            }
            else if (isClientOnly) // client only
            {
                // this block needs to be executed only a client,
                // that is why the condition "isClientOnly" is put above
                //if (!isOwned) return; //==> authority check has been direct added before itself
                // as we are not the server, we cant check the connection id information of other players in the server,
                // thus skip that condition checking part and directly add the unit into list of player controlled units
                myUnits.Add(unit);
            }
        }
        private void HandleUnitDespawned(UnitBehaviour unit)
        {
            // server only (or) server & client
            if (isServer && unit.connectionToClient.connectionId == connectionToClient.connectionId)
            // check if the spawned unit is owned to same connectionID of the resp player
            // i.e., making sure the client who owns the player is the same client who owns the unit
            {
                myUnits.Remove(unit);
            }
            else if (isClientOnly) // client only
            {
                //if (!isOwned) return; //==> authority check has been direct added before itself
                // as we are not the server, we cant check the connection id information of other players in the server,
                // thus skip that condition checking part and directly add the unit into list of player controlled units
                myUnits.Remove(unit);
            }
            /* 
                currently this handler, is called whenever any unit is spawned on any player:
                - if connection is sever (or) server & client, it takes responsibility to update every client-player
                  connected to the server, with resp to the unit spawn by them in thier 'RTSPlayer' Script
                - if connection is client only, then only the player of that client's resp spawned units are updated in the
             */
        }

        private void HandleBuildingSpawned(Building building)
        {
            // as isClientOnly and authority check has been made while handling...
            if (isServer && !(building.connectionToClient.connectionId == connectionToClient.connectionId)) return;
            // only condition to check is for server, to check if it is belonging to player as server handles all the players in it
            // btw this is same condition as above, just improvised...

            if (myBases.TryGetValue(SelectedBase, out var @BaseItem))
                @BaseItem.baseBuildings.Add(building);
            else throw new Exception("Base not selected, invalid building spawning");
        }
        private void HandleBuildingDespawned(Building building)
        {
            if (isServer && !(building.connectionToClient.connectionId == connectionToClient.connectionId)) return;
            // server check if not belongs to the resp player script, then returns

            if (myBases.TryGetValue(SelectedBase, out var @BaseItem)) // incase of base destruction (base entry is already deleted)
                @BaseItem.baseBuildings.Add(building);
        }

        private void HandleBaseSpawned(PlayerBase Base)
        {
            if (isServer && !(Base.connectionToClient.connectionId == connectionToClient.connectionId)) return;
            myBases.Add(Base, new());

            // need to change into better way of handling this later
            // for now, set the latest spawned base as selected base...
            SelectedBase = Base;
        }
        private void ClientHandleBaseDespawned(PlayerBase Base) => myBases.Remove(Base); // client only handler

        public event Action<int> ClientUI_OnResourceUpdated;
        private void ClientSyncResourcesUpdates(int oldval, int newval)
        {
            gold = newval; // simply to look at from client's end as well

            ClientUI_OnResourceUpdated?.Invoke(newval);
        }
        #region Server Again
        // public static event Action<int> ServerOnPlayerDeath;
        // this above method suggested in tutorial and subcribing to this event in the unit, building, scripts is just and overhead for me
        // as i have already structurized in player script, in the format, it contains player's bases and its resp buildings in it, players units alltogether

        private void ServerHandleBaseDespawned(PlayerBase Base)
        {
            // --->      authority check     <---
            if (!(Base.connectionToClient.connectionId == connectionToClient.connectionId)) return;
            
            // ---> destroy base's buildings <---
            var currentBaseBuildings = myBases[Base].baseBuildings;
            for ( int i = 0; i < currentBaseBuildings.Count; i++) // destroys the buildings in the base
            {
                // we are will be needing to call deal_damage(full health) to maintain the proper flow of gameobject destruction
                currentBaseBuildings[i].HealthConfig.DealDamage(float.MaxValue); // max value to ensure that it will be dead
            }

            // --->   remove base reference  <---
            myBases.Remove(Base); // delete every object inside base before deleting from the dict instance

            // ---> finally destroy the base <---
            // (this event is raised at Health.OnDeath.Inoked => HandleBaseDeath, not when gameobject of base is destroyed as its reference is needed here)
            NetworkServer.Destroy(Base.gameObject);

            CheckPlayerDead();         
        }

        [Server] private void CheckPlayerDead()
        {
            if (myBases.Count != 0) return;

            // delete all units of player
            //ToList() to create a copy of unit, as while iterating through them itself, it gets removed from list my another onDeath event
            foreach (UnitBehaviour unit in myUnits.ToList()) 
                unit.HealthConfig.DealDamage(float.MaxValue);

            // disable player, make spectator
            int id = connectionToClient.connectionId;
            Debug.Log($"ServerSide: Player-{id} Dead!");

            //ServerOnPlayerDeath?.Invoke(id); // destroys the units of player
            GameSession.Instance.RemovePlayerRef(this);
        }

        // temp building spawner
        [Command] public void cmdPlaceBuildingonBase(int buyableID, Vector3 position, Quaternion rotation)
        {
            // verify conditions from server side again

            // then place if all satisfies
            Buyable buildingToPlace = null;
            try
            {
                buildingToPlace = (Buyable)(NetworkManager.singleton as CustomNetworkManager).BuyablesIDMap[buyableID];
                Debug.Log("Buyable Obj: " + buildingToPlace);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                (NetworkManager.singleton as CustomNetworkManager).BuyablesIDMap.Keys.ToList().ForEach(key => Debug.Log(key));
                Debug.Log("Invalid Buyable ID: "+buyableID);
                return; 
            }

            if (CanReduceGold(buildingToPlace.Price))
            {
                GameObject buildingInstance = Instantiate(buildingToPlace.gameObject, position, rotation);
                CustomNetworkManager.SpawnOnServer(buildingInstance, connectionToClient, buildingToPlace.name);
            }
            else
            {
                // call client end to say dont have enough resources
                Debug.Log("Dont have enuf resouces to place the building");
            }
        }

        [Server] public void AddUpGold(int amt)
        {
            gold += amt;
        }
        [Server] public bool CanReduceGold(int amt)
        {
            if (amt > gold) return false;
            else
            {
                gold -= amt;
                return true;
            }
        }
        #endregion
    }
}
