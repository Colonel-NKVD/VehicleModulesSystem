using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using Rocket.API;
using Rocket.Core.Plugins;
using Rocket.Unturned.Chat;
using SDG.Unturned;
using UnityEngine;

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
            VehicleManager.onDamageVehicleRequested += OnVehicleDamaged;
        }

        protected override void Unload()
        {
            VehicleManager.onDamageVehicleRequested -= OnVehicleDamaged;
            StopAllCoroutines();
            TrackedVehicles.Clear();
        }

        private void OnVehicleDamaged(ref DamageVehicleParameters parameters, ref bool shouldAllow)
        {
            InteractableVehicle vehicle = parameters.vehicle;
            if (vehicle == null || !Configuration.Instance.TargetedVehicleIds.Contains(vehicle.asset.id)) return;

            if (!TrackedVehicles.TryGetValue(vehicle.instanceID, out VehicleState state))
            {
                state = new VehicleState();
                TrackedVehicles.Add(vehicle.instanceID, state);
            }

            ProcessModuleDamage(vehicle, state);

            // Клин орудия: шанс внутреннего взрыва при получении урона
            if (state.IsGunBroken && UnityEngine.Random.value < 0.50f)
            {
                ExplodeInternally(vehicle);
            }
        }

        private void ProcessModuleDamage(InteractableVehicle v, VehicleState s)
        {
            float roll = UnityEngine.Random.value;
            var driverID = v.passengers[0].player?.playerID.steamID;

            // Расчет шансов для каждого модуля отдельно
            if (UnityEngine.Random.value < Configuration.Instance.ChanceFuelLeak && !s.IsFuelTankBroken)
            {
                s.IsFuelTankBroken = true;
                UnturnedChat.Say(driverID, "КРИТИЧЕСКИЙ УРОН: Бак пробит!", Color.red);
                StartCoroutine(FuelLeakRoutine(v, s));
            }
            
            if (UnityEngine.Random.value < Configuration.Instance.ChanceTransmission && !s.IsTransmissionBroken)
            {
                s.IsTransmissionBroken = true;
                UnturnedChat.Say(driverID, "КРИТИЧЕСКИЙ УРОН: Трансмиссия повреждена!", Color.red);
                StartCoroutine(TransmissionRoutine(v));
            }

            if (UnityEngine.Random.value < Configuration.Instance.ChanceGunBroken && !s.IsGunBroken)
            {
                s.IsGunBroken = true;
                UnturnedChat.Say(driverID, "КРИТИЧЕСКИЙ УРОН: Орудие заклинило!", Color.red);
            }

            if (UnityEngine.Random.value < Configuration.Instance.ChanceFire)
            {
                StartCoroutine(FireRoutine(v, s));
            }

            if (UnityEngine.Random.value < Configuration.Instance.ChanceSmoke)
            {
                StartCoroutine(SmokeRoutine(v, s));
            }

            if (UnityEngine.Random.value < Configuration.Instance.ChanceStun)
            {
                StartCoroutine(StunRoutine(v));
            }
        }

        private IEnumerator StunRoutine(InteractableVehicle v)
        {
            var passengers = v.passengers.Where(p => p.player != null).Select(p => p.player.player).ToList();

            foreach (var p in passengers)
            {
                // Modal флаг открывает "пустое" окно, блокирует управление и вызывает курсор
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
                foreach (var seat in v.passengers.Where(p => p.player != null))
                    seat.player.player.life.askSuffocate(10);
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
