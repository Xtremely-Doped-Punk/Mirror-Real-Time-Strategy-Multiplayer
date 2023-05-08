using Mirror;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RTS
{
    public class GoldMine : Building
    {
        [SerializeField] private int AmountGeneratedInInterval = 10;
        [SerializeField] private float IntervalDuration = 2.5f;
        private float timer;

        public override void OnStartServer()
        {
            base.OnStartServer();
            timer = IntervalDuration;

            healthConfig.onDeath += HandleGoldMineDeath;
            GameSession.ServerOnGameOver += GameOverDisabler;
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            healthConfig.onDeath -= HandleGoldMineDeath;
            GameSession.ServerOnGameOver -= GameOverDisabler;
        }

        private void GameOverDisabler()
        {
            enabled = false;
        }

        private void HandleGoldMineDeath()
        {
            NetworkServer.Destroy(gameObject);
        }

        [ServerCallback]
        private void Update()
        {
            timer -= Time.deltaTime;
            if (timer < 0)
            {
                timer = IntervalDuration;
                player.AddUpGold(AmountGeneratedInInterval);
            }
        }
    }
}