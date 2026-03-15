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
    // Расширенный класс состояния для работы со всеми модулями
    public class VehicleState
    {
        public ushort LastHealth;
        public bool IsFuelTankBroken;
        public bool IsTransmissionBroken; // ВОЗВРАЩЕНО
        public bool IsGunBroken;          // ВОЗВРАЩЕНО
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
            
            // Запуск цикла мониторинга (Polling)
            StartCoroutine(VehicleObserverLoop());
            
            Rocket.Core.Logging.Logger.Log("VehicleModulesSystem: Full Module Support (Observer Mode) Engaged.");
        }

        protected override void Unload()
        {
            StopAllCoroutines();
            TrackedVehicles.Clear();
        }

        private IEnumerator VehicleObserverLoop()
        {
            while (true)
            {
                // ToList() предотвращает ошибку изменения коллекции во время перечисления
                foreach (var vehicle in VehicleManager.vehicles.ToList())
                {
                    if (vehicle == null || vehicle.asset == null) continue;
                    if (!Configuration.Instance.TargetedVehicleIds.Contains(vehicle.asset.id)) continue;

                    if (!TrackedVehicles.TryGetValue(vehicle.instanceID, out VehicleState state))
                    {
                        state = new VehicleState { LastHealth = vehicle.health };
                        TrackedVehicles.Add(vehicle.instanceID, state);
                        continue;
                    }

                    // Если здоровье упало — рассчитываем шансы повреждения модулей
                    if (vehicle.health < state.LastHealth)
                    {
                        ProcessModuleDamage(vehicle, state);
                    }

                    state.LastHealth = vehicle.health;
                }
                yield return new WaitForSeconds(0.1f);
            }
        }

        private void ProcessModuleDamage(InteractableVehicle v, VehicleState s)
        {
            var config = Configuration.Instance;
            
            // Расчет шансов для всех систем
            if (UnityEngine.Random.value < config.ChanceFuelLeak && !s.IsFuelTankBroken)
            {
                s.IsFuelTankBroken = true;
                NotifyDriver(v, "КРИТ: Топливный бак пробит!", Color.red);
                StartCoroutine(FuelLeakRoutine(v, s));
            }

            if (UnityEngine.Random.value < config.ChanceTransmission && !s.IsTransmissionBroken)
            {
                s.IsTransmissionBroken = true;
                NotifyDriver(v, "КРИТ: Трансмиссия повреждена!", Color.red);
            }

            if (UnityEngine.Random.value < config.ChanceGunBroken && !s.IsGunBroken)
            {
                s.IsGunBroken = true;
                NotifyDriver(v, "КРИТ: Орудие заклинило!", Color.red);
            }

            if (UnityEngine.Random.value < config.ChanceFire && !s.IsOnFire)
            {
                StartCoroutine(FireRoutine(v, s));
            }
        }

        // --- Логика работы модулей ---

        private IEnumerator FuelLeakRoutine(InteractableVehicle v, VehicleState s)
        {
            while (s.IsFuelTankBroken && v != null && !v.isExploded && v.fuel > 0)
            {
                v.fuel = (ushort)Mathf.Max(0, v.fuel - 2);
                yield return new WaitForSeconds(1.0f);
            }
        }

        private IEnumerator FireRoutine(InteractableVehicle v, VehicleState s)
        {
            s.IsOnFire = true;
            TriggerInternalEffect(125, v.transform.position);
            for (int i = 0; i < 5; i++)
            {
                if (v == null || v.isExploded) break;
                VehicleManager.damage(v, 150, 1, false);
                yield return new WaitForSeconds(1.5f);
            }
            s.IsOnFire = false;
        }

        private void TriggerInternalEffect(ushort id, Vector3 pos)
        {
            if (Assets.find(EAssetType.EFFECT, id) is EffectAsset asset)
            {
                TriggerEffectParameters e = new TriggerEffectParameters(asset);
                e.position = pos;
                e.relevantDistance = 128f;
                EffectManager.triggerEffect(e);
            }
        }

        private void NotifyDriver(InteractableVehicle v, string msg, Color c)
        {
            if (v.passengers.Length > 0 && v.passengers[0].player != null)
                UnturnedChat.Say(v.passengers[0].player.playerID.steamID, msg, c);
        }
    }
}
