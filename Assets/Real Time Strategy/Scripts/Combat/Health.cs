using Mirror;
using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RTS
{
    [RequireComponent(typeof(TargetBehaviour))]
    public class Health : NetworkBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("References Needed")]
        [SerializeField] private TargetBehaviour targetingConfig;
        [SerializeField] private Transform minimapView;
        private Renderer[] minimapRenderers;

        [Header("Health Properties")]
        [SerializeField] private float maxHealth = 100;
        [SyncVar(hook = nameof(HandleHealthUpdated))] private float currentHealth;
        [SyncVar(hook = nameof(ClientHandleTeamColorSet))] private Color playerTeamColor;
        // we are using sync var, so that client can update its UI based these info's from client end itself
        // as these UI for each client varies and is not commonly managableby the server

        public event Action onDeath; // this event is performed only at server side as we have sync var manage at client end (note: not static)

        private void Reset()
        {
            targetingConfig = GetComponent<TargetBehaviour>();
        }

        #region Server

        public override void OnStartServer()
        {
            currentHealth = maxHealth;
            playerTeamColor = connectionToClient.identity.GetComponent<RTSPlayer>().TeamColor; 
            // set team color after connected to server
        }

        [Server]
        public void DealDamage(float dmgAmount)
        {
            // fail safe to to infinite loop call,
            // like a object whose health is already less than 0 and invoked onDeath, cant invoke the onDeath event again
            if (!targetingConfig.isTargetable || !(currentHealth > 0)) return;
            
            // now deal damage
            currentHealth = Mathf.Max(currentHealth - dmgAmount, 0);

            if (currentHealth <= 0)
            {
                // destroy and add points to opponent
                onDeath?.Invoke(); // only invoked the first time

                //Debug.Log(gameObject + " Dead");
                // so far lets just assume anything who's health has been completely reduced,
                // despite of what object it is, we just delete it (added in resp scripts that have have subscribed to "onDeath")
            }
        }

        #endregion

        #region Client

        private void HandleHealthUpdated(float oldHealth, float newHeath) // sync-hook funtion should contain 2 parameters (old and new values)
        {
            // handle the correspond UI componenects to change according to updated current health
            // (here for simplicity, we are directly calling the functions)
            // this can handled by an event also, by subcribing the funtions to it when ever current health changes

            // update health bar
            HealthBarUpdation(newHeath, maxHealth);
        }

        private void ClientHandleTeamColorSet(Color oldColor, Color newColor)
        {
            healthBarImg.color = newColor;
            foreach (Renderer ren in minimapRenderers)
                ren.material.color = newColor;
        }

        #endregion

        #region UI
        [Header("UI References Needed")]
        [SerializeField] Canvas healthBarObj;
        [SerializeField] Image healthBarImg;

        private void HealthBarUpdation(float current, float max)
        {
            healthBarImg.fillAmount = current / max;
        }

        private void Awake()
        {
            healthBarObj.worldCamera = Camera.main;
            healthBarObj.gameObject.SetActive(false);
            minimapRenderers = minimapView.GetComponentsInChildren<Renderer>();
        }

        /* when-ever mouse is hovered over over this object's collider, these funtions are called based on their resp nature:
            note: functions: (OnMouseEnter, OnMouseExit,etc) [*1*] only work in old input system, new input system doesn't support this yet
            thus we will be using IPointer interfaces [*2*] to restrict the game-mode to new input system also this is advantageous in another way:
            in case of [*1*], it only works for colliders detected on the object to which the script is attached to. It doesn't get triggered on
            colliders of child gameobjects of parent gameobject on which we are trying to call this functionality for this case. [*2*] overcomes this.
            (as the colliders are present in the child gameobjects here which is what causes a lot of overhead even in raycasting)
        */

        public void OnPointerEnter(PointerEventData eventData)
        {
            healthBarObj.gameObject.SetActive(true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            healthBarObj.gameObject.SetActive(false);
        }

        #endregion
    }
}
