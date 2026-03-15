using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using Rocket.API;
using Rocket.Core.Plugins;
using Rocket.Unturned.Chat;
using SDG.Unturned;
using UnityEngine;
using Steamworks;

namespace VehicleModulesSystem
{
    public class VehicleState
    {
        public bool IsFuelTankBroken;
        public bool IsTransmissionBroken;
        public bool IsGunBroken;
        public bool IsOnFire;
        public bool IsSmoking;
    }

    public class VehicleModulesPlugin : RocketPlugin<VehicleModulesConfig>
    {
        public static VehicleModulesPlugin Instance;
        public Dictionary<uint, VehicleState> TrackedVehicles = new Dictionary<uint, VehicleState>();

        protected override void Load()
        {
            Instance = this;
            // Использование 7-параметровой сигнатуры для гарантированной компиляции
            VehicleManager.onDamageVehicleRequested += OnDamageRequested;
            
            Rocket.Core.Logging.Logger.Log("VehicleModulesSystem: [Compatibility Mode] Система запущена.");
        }

        protected override void Unload()
        {
            VehicleManager.onDamageVehicleRequested -= OnDamageRequested;
            StopAllCoroutines();
            TrackedVehicles.Clear();
        }

        // ОБХОД ОШИБКИ: Используем полную сигнатуру делегата. 
        // Это избавляет от необходимости искать структуру DamageVehicleParameters.
        private void OnDamageRequested(CSteamID instigator, InteractableVehicle vehicle, ref ushort damage, ref bool canDamage, ref EPlayerKill kill, ref uint xp, ref bool trackKill)
        {
            if (vehicle == null || vehicle.asset == null) return;
            if (!Configuration.Instance.TargetedVehicleIds.Contains(vehicle.asset.id)) return;

            if (!TrackedVehicles.TryGetValue(vehicle.instanceID, out VehicleState state))
            {
                state = new VehicleState();
                TrackedVehicles.Add(vehicle.instanceID, state);
            }

            ApplyAdvancedDamageLogic(vehicle, state);
        }

        private void ApplyAdvancedDamageLogic(InteractableVehicle v, VehicleState s)
        {
            var config = Configuration.Instance;
            CSteamID? driver = GetDriver(v);

            if (UnityEngine.Random.value < config.ChanceFuelLeak && !s.IsFuelTankBroken)
            {
                s.IsFuelTankBroken = true;
                if (driver.HasValue) UnturnedChat.Say(driver.Value, "ПРОБИТ БАК!", Color.red);
                StartCoroutine(FuelLeak(v, s));
            }

            if (UnityEngine.Random.value < config.ChanceFire) StartCoroutine(FireRoutine(v, s));
            
            // Если орудие повреждено (шанс 50%), вызываем внутренний взрыв
            if (s.IsGunBroken && UnityEngine.Random.value < 0.5f) TriggerInternalDetonation(v);
        }

        private void TriggerInternalDetonation(InteractableVehicle v)
        {
            SpawnModernEffect(125, v.transform.position);
            VehicleManager.damage(v, 1000, 1, false);
        }

        private void SpawnModernEffect(ushort id, Vector3 pos)
        {
            if (Assets.find(EAssetType.EFFECT, id) is EffectAsset asset)
            {
                TriggerEffectParameters e = new TriggerEffectParameters(asset);
                e.position = pos;
                e.relevantDistance = 128f;
                EffectManager.triggerEffect(e);
            }
        }

        private CSteamID? GetDriver(InteractableVehicle v) => 
            (v.passengers.Length > 0 && v.passengers[0].player != null) ? (CSteamID?)v.passengers[0].player.playerID.steamID : null;

        private IEnumerator FuelLeak(InteractableVehicle v, VehicleState s)
        {
            while (s.IsFuelTankBroken && v != null && !v.isExploded && v.fuel > 0)
            {
                v.fuel -= 5;
                yield return new WaitForSeconds(1f);
            }
        }

        private IEnumerator FireRoutine(InteractableVehicle v, VehicleState s)
        {
            if (s.IsOnFire) yield break;
            s.IsOnFire = true;
            SpawnModernEffect(125, v.transform.position);
            for (int i = 0; i < 5; i++)
            {
                if (v == null || v.isExploded) break;
                VehicleManager.damage(v, 200, 1, false);
                yield return new WaitForSeconds(1f);
            }
        }
    }
}
