using Mirror;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace RTS
{
    public class GameSession : NetworkBehaviour
    {
        [SerializeField] private List<RTSPlayer> activePlayers = new();  // to be active only at server end
        
        // singleton
        private static GameSession instance;
        public static GameSession Instance { get { return instance; } }
        private void Awake()
        {
            if (instance == null)
                instance = this;
            else
                Destroy(this);

            DisplayParent.SetActive(false);
        }
        #region Server
        public static event Action ServerOnGameOver; // server side when for managing things
                                                     // (like stop any kind of movement/shooting interface in any object)

        [Server] private void CheckGameOver()
        {
            if (activePlayers.Count == 1)
            {
                // stop game
                int playerID = activePlayers[0].connectionToClient.connectionId;
                rpcGameOver($"Player-{playerID}"); // client end
                ServerOnGameOver?.Invoke(); // server end
            }
        }
        public void AddPlayerRef(RTSPlayer player) 
        {
            if (NetworkServer.active) // need to be managed by server only
            {
                activePlayers.Add(player);
            }
        }
        public void RemovePlayerRef(RTSPlayer player)
        {
            if (NetworkServer.active)
            {
                activePlayers.Remove(player);
                CheckGameOver();
            }
        }
        #endregion

        #region Client
        public static event Action<string> ClientOnGameOver; // client side when for managing things
                                                             // (like popping UI gameover screen)
        [ClientRpc] private void rpcGameOver(string winner)
        {
            Debug.Log("ClientSide: Game Over! " + winner);
            ClientOnGameOver?.Invoke(winner);
            ClientHandleGameOverScreen(winner);
        }

        [SerializeField] private TMP_Text WinDisplayText = null;
        [SerializeField] private GameObject DisplayParent = null;
        private void ClientHandleGameOverScreen(string winner) // for managing UI
        {
            WinDisplayText.text = winner + " Won! \nGame Over!";
            DisplayParent.SetActive(true);
        }
        public void LeaveGame() // ui button functionality
        {
            if (NetworkClient.isConnected)
            {
                if (NetworkServer.active && NetworkClient.isConnected) // server&client conn
                {
                    // stop hosting, i.e. disconnect server and host of same device at same time
                    NetworkManager.singleton.StopHost();
                    // later fix, allows them to not to stop host server also (leave and join as new client) ...
                }
                else
                {
                    // disconnect client
                    NetworkManager.singleton.StopClient();
                }
            }
            // incase server only, we dont require this option...
        }
        #endregion
    }
}