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

            // ПОДПИСКА: Самый актуальный делегат требует именно два REF параметра.
            VehicleManager.onDamageVehicleRequested += OnDamageRequested;
            
            Rocket.Core.Logging.Logger.Log("VehicleModulesSystem: [Elite Build] Система инициализирована через DamageVehicleParameters.");
        }

        protected override void Unload()
        {
            VehicleManager.onDamageVehicleRequested -= OnDamageRequested;
            StopAllCoroutines();
            TrackedVehicles.Clear();
        }

        // РЕШЕНИЕ CS0123 и CS0246:
        // Используем полное имя типа SDG.Unturned.DamageVehicleParameters.
        // Это "пробивает" любые проблемы с областью видимости типов в проекте.
        private void OnDamageRequested(ref SDG.Unturned.DamageVehicleParameters parameters, ref bool shouldAllow)
        {
            // Получаем объект транспорта напрямую из структуры параметров
            InteractableVehicle vehicle = parameters.vehicle;

            if (vehicle == null || vehicle.asset == null) return;
            
            // Фильтрация по ID из конфига
            if (!Configuration.Instance.TargetedVehicleIds.Contains(vehicle.asset.id)) return;

            // Идентификация конкретного экземпляра по instanceID
            if (!TrackedVehicles.TryGetValue(vehicle.instanceID, out VehicleState state))
            {
                state = new VehicleState();
                TrackedVehicles.Add(vehicle.instanceID, state);
            }

            ProcessAdvancedDamage(vehicle, state);
        }

        private void ProcessAdvancedDamage(InteractableVehicle v, VehicleState s)
        {
            var config = Configuration.Instance;
            CSteamID? driver = GetDriver(v);

            // Логика повреждения модулей
            if (UnityEngine.Random.value < config.ChanceFuelLeak && !s.IsFuelTankBroken)
            {
                s.IsFuelTankBroken = true;
                if (driver.HasValue) UnturnedChat.Say(driver.Value, "КРИТИЧЕСКИЙ УРОН: Топливный бак пробит!", Color.red);
                StartCoroutine(FuelLeakLoop(v, s));
            }

            if (UnityEngine.Random.value < config.ChanceFire) StartCoroutine(FireLoop(v, s));
        }

        private void SpawnModernEffect(ushort id, Vector3 pos)
        {
            // Устраняем CS0618 через современный API
            EffectAsset asset = Assets.find(EAssetType.EFFECT, id) as EffectAsset;
            if (asset != null)
            {
                TriggerEffectParameters parameters = new TriggerEffectParameters(asset);
                parameters.position = pos;
                parameters.relevantDistance = 128f;
                EffectManager.triggerEffect(parameters);
            }
        }

        private CSteamID? GetDriver(InteractableVehicle v)
        {
            if (v.passengers != null && v.passengers.Length > 0 && v.passengers[0].player != null)
                return v.passengers[0].player.playerID.steamID;
            return null;
        }

        private IEnumerator FuelLeakLoop(InteractableVehicle v, VehicleState s)
        {
            while (s.IsFuelTankBroken && v != null && !v.isExploded && v.fuel > 0)
            {
                v.fuel -= 2;
                yield return new WaitForSeconds(1f);
            }
        }

        private IEnumerator FireLoop(InteractableVehicle v, VehicleState s)
        {
            if (s.IsOnFire) yield break;
            s.IsOnFire = true;
            SpawnModernEffect(125, v.transform.position);
            for (int i = 0; i < 5; i++)
            {
                if (v == null || v.isExploded) break;
                VehicleManager.damage(v, 200, 1, false);
                yield return new WaitForSeconds(1.5f);
            }
        }
    }
}
