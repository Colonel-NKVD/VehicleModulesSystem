using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using Rocket.API;
using Rocket.Core.Plugins;
using Rocket.Unturned.Chat;
using Rocket.Unturned.Player;
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

            ProcessRandomModules(vehicle, state);

            // Логика "Клина": если пушка сломана, 50% шанс получить урон по себе при получении удара
            if (state.IsGunBroken && UnityEngine.Random.value < 0.50f)
            {
                ExplodeInternally(vehicle);
            }
        }

        private void ProcessRandomModules(InteractableVehicle v, VehicleState s)
        {
            float roll = UnityEngine.Random.value;
            var driver = v.passengers[0].player?.playerID.steamID;

            if (roll < Configuration.Instance.ChanceFuelLeak && !s.IsFuelTankBroken)
            {
                s.IsFuelTankBroken = true;
                UnturnedChat.Say(driver, "БАК ПРОБИТ!", Color.red);
                StartCoroutine(FuelLeakRoutine(v, s));
            }
            else if (roll < Configuration.Instance.ChanceTransmission && !s.IsTransmissionBroken)
            {
                s.IsTransmissionBroken = true;
                UnturnedChat.Say(driver, "ТРАНСМИССИЯ ПОВРЕЖДЕНА!", Color.red);
                StartCoroutine(TransmissionRoutine(v));
            }
            else if (roll < Configuration.Instance.ChanceGunBroken && !s.IsGunBroken)
            {
                s.IsGunBroken = true;
                UnturnedChat.Say(driver, "ОРУДИЕ ЗАКЛИНИЛО!", Color.red);
            }
            else if (roll < Configuration.Instance.ChanceFire)
            {
                StartCoroutine(FireRoutine(v, s));
            }
            else if (roll < Configuration.Instance.ChanceStun)
            {
                StartCoroutine(StunRoutine(v));
            }
        }

        // --- ЭФФЕКТЫ ---

        private IEnumerator StunRoutine(InteractableVehicle v)
        {
            var players = v.passengers.Where(p => p.player != null).Select(p => p.player.player).ToList();

            foreach (var p in players)
            {
                // ВКЛЮЧАЕМ КУРСОР И БЛОКИРУЕМ ИГРУ (Modal Flag)
                p.setPluginWidgetFlag(EPluginWidgetFlags.Modal, true);
                UnturnedChat.Say(p.channel.owner.playerID.steamID, "ВЫ ОГЛУШЕНЫ!", Color.yellow);
            }

            yield return new WaitForSeconds(5f);

            foreach (var p in players)
            {
                if (p != null) p.setPluginWidgetFlag(EPluginWidgetFlags.Modal, false);
            }
        }

        private IEnumerator FireRoutine(InteractableVehicle v, VehicleState s)
        {
            if (s.IsOnFire) yield break;
            s.IsOnFire = true;
            EffectManager.sendEffect(125, 64, v.transform.position); // Ванильный взрыв/огонь

            for (int i = 0; i < 8; i++)
            {
                if (v == null || v.isExploded || !s.IsOnFire) yield break;
                VehicleManager.damage(v, 150, 1, false);
                yield return new WaitForSeconds(1f);
            }
            if (v != null) VehicleManager.damage(v, 10000, 1, false);
        }

        private IEnumerator FuelLeakRoutine(InteractableVehicle v, VehicleState s)
        {
            while (s.IsFuelTankBroken && v != null && !v.isExploded)
            {
                if (v.fuel > 0) v.fuel -= 5;
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
