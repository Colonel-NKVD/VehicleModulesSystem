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
    // Профессиональный подход: выносим логику состояния в отдельный класс
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

            // Подписываемся на актуальное событие. 
            // DamageVehicleParameters — это стандарт Unturned 2024-2026 гг.
            VehicleManager.onDamageVehicleRequested += OnVehicleDamaged;
            
            Rocket.Core.Logging.Logger.Log("VehicleModulesSystem: [Advanced Mode] Система активна. Используется DamageVehicleParameters.");
        }

        protected override void Unload()
        {
            VehicleManager.onDamageVehicleRequested -= OnVehicleDamaged;
            StopAllCoroutines();
            TrackedVehicles.Clear();
        }

        // РЕШЕНИЕ ОШИБКИ CS0246: 
        // 1. Убедитесь, что в ссылках проекта (References) есть SDG.NetTransport.dll.
        // 2. Убедитесь, что Assembly-CSharp.dll в папке libs на GitHub — это не пустышка.
        private void OnVehicleDamaged(ref DamageVehicleParameters parameters, ref bool shouldAllow)
        {
            InteractableVehicle vehicle = parameters.vehicle;

            if (vehicle == null || vehicle.asset == null) return;
            
            // Фильтрация по конфигу
            if (!Configuration.Instance.TargetedVehicleIds.Contains(vehicle.asset.id)) return;

            if (!TrackedVehicles.TryGetValue(vehicle.instanceID, out VehicleState state))
            {
                state = new VehicleState();
                TrackedVehicles.Add(vehicle.instanceID, state);
            }

            // Логика повреждения модулей
            ApplyModuleLogic(vehicle, state, parameters.instigator);

            // Если орудие сломано — шанс детонации при каждом попадании
            if (state.IsGunBroken && UnityEngine.Random.value < 0.50f)
            {
                TriggerInternalExplosion(vehicle);
            }
        }

        private void ApplyModuleLogic(InteractableVehicle v, VehicleState s, object instigator)
        {
            var config = Configuration.Instance;
            CSteamID? driverID = GetDriverSteamID(v);

            // Пробитие топливного бака
            if (UnityEngine.Random.value < config.ChanceFuelLeak && !s.IsFuelTankBroken)
            {
                s.IsFuelTankBroken = true;
                NotifyDriver(driverID, "ВНИМАНИЕ: Топливный бак пробит!", Color.red);
                StartCoroutine(FuelLeakRoutine(v, s));
            }
            
            // Поломка трансмиссии
            if (UnityEngine.Random.value < config.ChanceTransmission && !s.IsTransmissionBroken)
            {
                s.IsTransmissionBroken = true;
                NotifyDriver(driverID, "ВНИМАНИЕ: Трансмиссия повреждена!", Color.red);
                StartCoroutine(TransmissionRoutine(v));
            }

            // Заклинивание орудия
            if (UnityEngine.Random.value < config.ChanceGunBroken && !s.IsGunBroken)
            {
                s.IsGunBroken = true;
                NotifyDriver(driverID, "ВНИМАНИЕ: Орудие заклинило!", Color.red);
            }

            // Спецэффекты: Огонь, Дым, Контузия
            if (UnityEngine.Random.value < config.ChanceFire) StartCoroutine(FireRoutine(v, s));
            if (UnityEngine.Random.value < config.ChanceSmoke) StartCoroutine(SmokeRoutine(v, s));
            if (UnityEngine.Random.value < config.ChanceStun) StartCoroutine(StunRoutine(v));
        }

        #region Helpers & Routines

        private void NotifyDriver(CSteamID? id, string message, Color color)
        {
            if (id.HasValue) UnturnedChat.Say(id.Value, message, color);
        }

        private CSteamID? GetDriverSteamID(InteractableVehicle v)
        {
            if (v.passengers != null && v.passengers.Length > 0 && v.passengers[0].player != null)
                return v.passengers[0].player.playerID.steamID;
            return null;
        }

        private IEnumerator StunRoutine(InteractableVehicle v)
        {
            var players = v.passengers.Where(p => p.player != null).Select(p => p.player.player).ToList();
            foreach (var p in players)
            {
                p.setPluginWidgetFlag(EPluginWidgetFlags.Modal, true);
                UnturnedChat.Say(p.channel.owner.playerID.steamID, "ВЫ КОНТУЖЕНЫ!", Color.yellow);
            }
            yield return new WaitForSeconds(5f);
            foreach (var p in players) if (p != null) p.setPluginWidgetFlag(EPluginWidgetFlags.Modal, false);
        }

        private IEnumerator FireRoutine(InteractableVehicle v, VehicleState s)
        {
            if (s.IsOnFire) yield break;
            s.IsOnFire = true;
            SpawnEffect(125, v.transform.position);

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
            SpawnEffect(123, v.transform.position);

            float end = Time.time + UnityEngine.Random.Range(15, 30);
            while (Time.time < end && v != null && !v.isExploded)
            {
                foreach (var seat in v.passengers.Where(p => p.player != null))
                    seat.player.player.life.askSuffocate(10);
                yield return new WaitForSeconds(2f);
            }
            s.IsSmoking = false;
        }

        private IEnumerator FuelLeakRoutine(InteractableVehicle v, VehicleState s)
        {
            while (s.IsFuelTankBroken && v != null && !v.isExploded && v.fuel > 0)
            {
                v.fuel -= 3;
                yield return new WaitForSeconds(1f);
            }
        }

        private IEnumerator TransmissionRoutine(InteractableVehicle v)
        {
            yield return new WaitForSeconds(UnityEngine.Random.Range(15, 30));
            if (v != null) v.batteryCharge = 0;
        }

        private void TriggerInternalExplosion(InteractableVehicle v)
        {
            SpawnEffect(125, v.transform.position);
            VehicleManager.damage(v, 1000, 1, false);
        }

        // Ультра-профессиональный метод спавна эффектов без Warning CS0618
        private void SpawnEffect(ushort id, Vector3 pos)
        {
            EffectAsset asset = Assets.find(EAssetType.EFFECT, id) as EffectAsset;
            if (asset != null)
            {
                TriggerEffectParameters effect = new TriggerEffectParameters(asset);
                effect.position = pos;
                effect.relevantDistance = 128f;
                EffectManager.triggerEffect(effect);
            }
        }
        #endregion
    }
}
