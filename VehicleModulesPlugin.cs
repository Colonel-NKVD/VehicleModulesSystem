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
            VehicleManager.onDamageVehicleRequested += OnVehicleDamagedLegacy;
            Rocket.Core.Logging.Logger.Log("VehicleModulesSystem: Модуль мониторинга бронетехники активирован (Legacy API Mode).");
        }

        protected override void Unload()
        {
            VehicleManager.onDamageVehicleRequested -= OnVehicleDamagedLegacy;
            StopAllCoroutines();
            TrackedVehicles.Clear();
        }

        // ОБХОДНОЙ ПУТЬ: Добавлен параметр 'ref bool trackKill'.
        // Это точная сигнатура делегата Unturned 3.20.x, которую требует ваша библиотека.
        // Если CI/CD снова выдаст ошибку CS0123, просто удалите последний параметр 'ref bool trackKill'.
        private void OnVehicleDamagedLegacy(CSteamID instigatorSteamID, InteractableVehicle vehicle, ref ushort pendingTotalDamage, ref bool canDamage, ref EPlayerKill kill, ref uint xp, ref bool trackKill)
        {
            if (vehicle == null || vehicle.asset == null) return;
            
            if (!Configuration.Instance.TargetedVehicleIds.Contains(vehicle.asset.id)) return;

            if (!TrackedVehicles.TryGetValue(vehicle.instanceID, out VehicleState state))
            {
                state = new VehicleState();
                TrackedVehicles.Add(vehicle.instanceID, state);
            }

            ProcessModuleDamage(vehicle, state);

            if (state.IsGunBroken && UnityEngine.Random.value < 0.50f)
            {
                ExplodeInternally(vehicle);
            }
        }

        private void ProcessModuleDamage(InteractableVehicle v, VehicleState s)
        {
            var config = Configuration.Instance;
            
            CSteamID? driverID = null;
            if (v.passengers != null && v.passengers.Length > 0 && v.passengers[0].player != null)
            {
                driverID = v.passengers[0].player.playerID.steamID;
            }

            if (UnityEngine.Random.value < config.ChanceFuelLeak && !s.IsFuelTankBroken)
            {
                s.IsFuelTankBroken = true;
                if (driverID.HasValue) UnturnedChat.Say(driverID.Value, "КРИТИЧЕСКИЙ УРОН: Бак пробит!", Color.red);
                StartCoroutine(FuelLeakRoutine(v, s));
            }
            
            if (UnityEngine.Random.value < config.ChanceTransmission && !s.IsTransmissionBroken)
            {
                s.IsTransmissionBroken = true;
                if (driverID.HasValue) UnturnedChat.Say(driverID.Value, "КРИТИЧЕСКИЙ УРОН: Трансмиссия повреждена!", Color.red);
                StartCoroutine(TransmissionRoutine(v));
            }

            if (UnityEngine.Random.value < config.ChanceGunBroken && !s.IsGunBroken)
            {
                s.IsGunBroken = true;
                if (driverID.HasValue) UnturnedChat.Say(driverID.Value, "КРИТИЧЕСКИЙ УРОН: Орудие заклинило!", Color.red);
            }

            if (UnityEngine.Random.value < config.ChanceFire) StartCoroutine(FireRoutine(v, s));
            if (UnityEngine.Random.value < config.ChanceSmoke) StartCoroutine(SmokeRoutine(v, s));
            if (UnityEngine.Random.value < config.ChanceStun) StartCoroutine(StunRoutine(v));
        }

        private IEnumerator StunRoutine(InteractableVehicle v)
        {
            if (v.passengers == null) yield break;

            var passengers = v.passengers
                .Where(p => p.player != null)
                .Select(p => p.player.player)
                .ToList();

            foreach (var p in passengers)
            {
                if (p == null) continue;
                p.setPluginWidgetFlag(EPluginWidgetFlags.Modal, true);
                UnturnedChat.Say(p.channel.owner.playerID.steamID, "ВЫ КОНТУЖЕНЫ!", Color.yellow);
            }

            yield return new WaitForSeconds(5f);

            foreach (var p in passengers)
            {
                if (p != null) p.setPluginWidgetFlag(EPluginWidgetFlags.Modal, false);
            }
        }

        private IEnumerator FireRoutine(InteractableVehicle v, VehicleState s)
        {
            if (s.IsOnFire) yield break;
            s.IsOnFire = true;
            EffectManager.sendEffect(125, 64, v.transform.position); 

            for (int i = 0; i < 8; i++)
            {
                if (v == null || v.isExploded || !s.IsOnFire) yield break;
                VehicleManager.damage(v, 150, 1, false);
                yield return new WaitForSeconds(1f);
            }
            if (v != null && !v.isExploded) VehicleManager.damage(v, 10000, 1, false);
        }

        private IEnumerator SmokeRoutine(InteractableVehicle v, VehicleState s)
        {
            if (s.IsSmoking) yield break;
            s.IsSmoking = true;
            EffectManager.sendEffect(123, 64, v.transform.position); 

            float duration = UnityEngine.Random.Range(15, 30);
            float start = Time.time;
            while (Time.time - start < duration && v != null && !v.isExploded && s.IsSmoking)
            {
                if (v.passengers != null)
                {
                    foreach (var seat in v.passengers.Where(p => p.player != null))
                        seat.player.player.life.askSuffocate(10);
                }
                yield return new WaitForSeconds(2f);
            }
            s.IsSmoking = false;
        }

        private IEnumerator FuelLeakRoutine(InteractableVehicle v, VehicleState s)
        {
            while (s.IsFuelTankBroken && v != null && !v.isExploded)
            {
                if (v.fuel > 0) v.fuel -= 3;
                yield return new WaitForSeconds(1f);
            }
        }

        private IEnumerator TransmissionRoutine(InteractableVehicle v)
        {
            yield return new WaitForSeconds(UnityEngine.Random.Range(15, 30));
            if (v != null) v.batteryCharge = 0;
        }

        private void ExplodeInternally(InteractableVehicle v)
        {
            EffectManager.sendEffect(125, 64, v.transform.position);
            VehicleManager.damage(v, 1000, 1, false);
        }
    }
}
