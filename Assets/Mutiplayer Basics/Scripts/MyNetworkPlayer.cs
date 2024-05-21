using UnityEngine;
using Mirror;
using TMPro;

namespace MultiplayerBasis
{
    public class MyNetworkPlayer : NetworkBehaviour
    {
        // #region "name" is used to fomatting the code in small small region that share common functionality
        #region references
        [SerializeField] TMP_Text displayText;
        [SerializeField] Renderer colorRenderer;
        #endregion

        #region sync vars
        // attribute to sync the changes in server to all the connected clients
        // note: if a client changes a sync-var, nothing happens, only if server updates it, it is reflected in other clients
        [SyncVar(hook = nameof(HandleNameUpdate))]
        [SerializeField] private string dispName = "Missing Name";

        [SyncVar(hook = nameof(HandleColorUpdate))] // pass the fn name as string to hook to call it when ever this variable is changed in the network
        [SerializeField] private Color dispColor = Color.white;
        #endregion


        #region Server

        // if a client trys to access this, it throws a warning
        [Server] // server attribute to prevent clients from running this code fn
        public void SetDisplayName(string displayName) { 
            // applying validation here make the NetworkManager also checks
            // so its like keeping validation on server and client side of network connection
            // but here so simplicity we are gonna keep validation only from client end, in "CmdSetDispName"
            this.dispName = displayName; }
        [Server] public void SetColor(Color color) { this.dispColor = color; }

        // [Command] attribute helps the client to the call the resp function on the server.
        // RPC -> Remote Procedural Call
        // [ClientRpc] attribute helps the server to the call the resp function on "ALL" the clients connected to it.
        // [TargetRpc] attribute helps the server to the call the resp function on a "specific" client connected to it.

        [Command]
        private void CmdSetDispName(string newDispName)
        {
            if (!newDispName.Contains(' ')) // if the new name has Whitespaces, ignore the request
            {
                RpcLogClientNames(this.dispName, newDispName); SetDisplayName(newDispName);
            }
            else
                Debug.LogWarning("Player Name cant have white-spaces!!!");
        }
        // [2]. this acts permission granting role by the serv allowing to change the resp client's name
        // [3]. which changes the sync var, which is then automatically updated in all the clients after the particular refesh frequency

        [ClientRpc] private void RpcLogClientNames(string oldName, string newName) { Debug.Log($"Player Name changed from \"{oldName}\" to \"{newName}\""); }
        #endregion

        #region Client
        private void HandleNameUpdate(string oldID, string newID)
        // note for for "hook" functions, it requires 2 parameters with old value and new updated value
        {
            displayText.text = newID;
        }
        private void HandleColorUpdate(Color oldColor, Color newColor)
        // note for for "hook" functions, it requires 2 parameters with old value and new updated value
        {
            colorRenderer.material.SetColor("_BaseColor", newColor); // "_BaseColor" is the shaderID property of material
                                                                     // material.color = newColor also works the same way
        }

        // context menus, are the options in the inspector, when right clicked over the component resp
        // using context menu simply to check if "CmdSetDispName" is properly executing
        [ContextMenu(nameof(SetMyName))]
        private void SetMyName() { 
            CmdSetDispName("default_name");
            //CmdSetDispName("default name");
        }
        // [1]. this acts as request to server from the client to change its name
        // if a client_id tries to access this functionallity of other client_id, then it throws
        #endregion
    }
}
