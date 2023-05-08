using Mirror;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RTS
{
    /*  consider this as the main capital territory,
        if this is destroyed, all the buildings and units of it will be destroyed (or)
        opponent captures all the belongings in the base upto what they are intact

        also a player might have more than one bases is also possibility we are considering improving upon further,
        i.e. a player must always have atleast 1 active base at any point of time in game if not, 
        he loses and becomes a spectator until last player captures all the bases to himself...
    */

    public class PlayerBase : Buyable
    {
        // static event raising when a base is spawned or despawned
        public static event Action<PlayerBase> onBaseCreated;
        public static event Action<PlayerBase> onBaseDestroyed;

        [Header("References Needed")]
        [SerializeField] Health health; // reference resp health component

        [Header("Base Config")]
        [SerializeField] private float baseRange = 15f;
        public bool withinBaseRange(Vector3 point)
        {
            if ((point - transform.position).sqrMagnitude < baseRange * baseRange)
                return true;
            else
                return false;
        }

        #region Server

        public override void OnStartServer()
        {
            health.onDeath += HandleBaseDeath;
            onBaseCreated?.Invoke(this);
        }
        public override void OnStopServer()
        {
            health.onDeath -= HandleBaseDeath;
        }
        [Server]
        private void HandleBaseDeath()
        {
            // add additional handling for the resp base capital getting destroyed...
            // destroy base everythin inside base booom, play animation whatev etc

            //finally delete 
            //NetworkServer.Destroy(gameObject);
            // (in case of base do not delete, its reference is need to destroy its resp buildings of the base)
            // thus we disable this gameobject for time being while its Destroy() will be handled RTSPlayer
            gameObject.SetActive(false); // lets check if this works

            onBaseDestroyed?.Invoke(this); // as the gameobject cant be destroyed anymore as its reference is needed, we will call the repective event here itself
            //NetworkServer.ReferenceEquals(gameObject, gameObject);
        }

        #endregion

        #region Client
        public override void OnStartAuthority()
        {
            if (!NetworkServer.active)  //i.e. isClientOnly refer RTSPlayer for why?
                onBaseCreated?.Invoke(this);
        }
        public override void OnStopClient()
        {
            if (isClientOnly && isOwned)
                onBaseDestroyed?.Invoke(this);
        }
        #endregion
    }
}
