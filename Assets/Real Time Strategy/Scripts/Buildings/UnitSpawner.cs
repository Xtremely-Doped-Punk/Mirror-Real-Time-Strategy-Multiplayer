using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RTS
{
    public class UnitSpawner : Building, IPointerClickHandler // like building barracks
        // events basically are designed for UI, thus it requires EventSystem component in the to work
        // also add the physics raycaster compoment in camera, that allows us to click on objects in 3D world space
    {
        [Header("Spawning References Needed")]
        [SerializeField] private UnitBehaviour unitPrefab;
        [SerializeField] private Transform spawnPoint;
        [SerializeField] private TMP_Text trainingQueueText;
        [SerializeField] private Image timerImg;

        [Header("Spawner Config")]
        [SerializeField] private int maxQueue = 5;
        [SerializeField] private float spawnRange = 7f;
        [SerializeField] private float trainingTimePerUnit = 5f;

        [SyncVar(hook=nameof(ClientUI_HandleTrainingQueueChange))] private int trainingQueue;
        [SyncVar] private float timer;
        float smooth_damp_vel;

        private void Update()
        {
            if (isServer)
            {
                if (trainingQueue == 0) return;
                timer += Time.deltaTime;
                if (timer < trainingTimePerUnit) return;

                // spawn now
                timer = 0;
                ProduceUnit();
            }
            if (isClient)
            {
                float newProgress = timer / trainingTimePerUnit;

                if (newProgress < timerImg.fillAmount)
                {
                    timerImg.fillAmount = newProgress;
                }
                else
                {
                    // smooth transistion as this a sync var that is updated per frame in server end and
                    // then only synced to client, thus the delay...
                    timerImg.fillAmount = Mathf.SmoothDamp(
                        timerImg.fillAmount,
                        newProgress,
                        ref smooth_damp_vel,
                        0.1f);

                }
            }
        }

        // server code should contain the main logic
        #region Server

        public override void OnStartServer()
        {
            base.OnStartServer();
            healthConfig.onDeath += HandleBuildingDeath;
        }
        public override void OnStopServer()
        {
            base.OnStopServer();
            healthConfig.onDeath -= HandleBuildingDeath;
        }

        [Server]
        private void HandleBuildingDeath()
        {
            // add additional handling for the resp building getting destroyed...

            //finally delete
            NetworkServer.Destroy(gameObject); // temp fix as for of now, spawner and base are applied in same object (will be fixed later)
        }

        [Command] private void cmdSpawnUnit()
        {
            if ((trainingQueue >= maxQueue) ||
                !player.CanReduceGold(unitPrefab.TrainingCost)) // this will automaticlly reduce the amt if eligible
                return;
                
            trainingQueue++;
        }
        [Server] private void ProduceUnit()
        {
            var unitInstance = Instantiate(unitPrefab, spawnPoint.position, spawnPoint.rotation);

            // NetworkServer is static class present in the network manager, such that there always exists only one network manager
            // which could be access through static functionallities present in it
            CustomNetworkManager.SpawnOnServer(unitInstance.gameObject, connectionToClient, unitPrefab.name);
            // connectionToClient, class is present in the client-player-gameobject connected to the server
            // if this 2nd parameter is not given it becomes a server owned instance and cannot be accessed by any clients

            // spawn and move them away slightly for other units to also spawn in
            Vector3 spawnOffset = Random.insideUnitSphere * spawnRange;
            spawnOffset.y = spawnPoint.position.y;

            unitInstance.ServerMoveUnit(spawnOffset + spawnPoint.position);
            trainingQueue--;
        }

        #endregion

        // client simply request the calls from the server end
        #region Client

        // whenever this gameobject is clicked upon, this event triggers this function
        public void OnPointerClick(PointerEventData eventData)
        {
            if (!isOwned) return;
            if (eventData.button == PointerEventData.InputButton.Left) // spawn if the obj is clicked using left-mouse button
            {
                cmdSpawnUnit();
            }
        }

        private void ClientUI_HandleTrainingQueueChange(int oldval, int newval)
        {
            trainingQueueText.text = newval.ToString();
        }
        #endregion
    }
}
