using Mirror;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace RTS
{
    public class ResourcesUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text resoucesText;
        RTSPlayer player;

        private void Update()
        {
            if (player == null) // just for safty, incase to work without offline and online scenes
            {
                if (!(NetworkClient.connection == null || NetworkClient.connection.identity == null))
                {
                    player = NetworkClient.connection.identity.GetComponent<RTSPlayer>();
                    if (player != null)
                    {
                        player.ClientUI_OnResourceUpdated += UpdateResourcesUI;
                        UpdateResourcesUI(player.Gold);
                    }
                }
                else return;
            }
        }
        private void OnDestroy()
        {
            if (player != null)
                player.ClientUI_OnResourceUpdated -= UpdateResourcesUI;
        }

        private void UpdateResourcesUI(int gold)
        {
            resoucesText.text = $"Gold:{gold}";
        }
    }
}