using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;

namespace MultiplayerBasis
{
    public class MyNetworkManager : NetworkManager
    {
        public override void OnClientConnect()
        {
            base.OnClientConnect();
            Debug.Log("Connected to Server");
        }

        public override void OnServerAddPlayer(NetworkConnectionToClient conn)
        {
            base.OnServerAddPlayer(conn);

            // NetworkConnectionToClient has the identity refereing to the NetworkIdentity component of the player prefab,
            // from which gameobject we can get other components
            MyNetworkPlayer player = conn.identity.GetComponent<MyNetworkPlayer>();

            player.SetDisplayName($"PlayerID_{numPlayers}"); //setting player's display name based no.of players in server to make it unique
            player.SetColor(new Color(Random.Range(0f, 1f), Random.Range(0f, 1f), Random.Range(0f, 1f))); // setting up with random color
                                                                                                          // inside rand.range(dont put 0 to 1) as it considers as int and calls the overloaded function tat return rand.int in that range

            Debug.Log($"No.of Players = {numPlayers}");
        }
    }
}
