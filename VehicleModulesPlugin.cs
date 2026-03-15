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
        public ushort LastHealth;
        public bool IsFuelTankBroken;
        public bool IsTransmissionBroken;
        public bool IsGunBroken;
        public bool IsOnFire;
        public bool IsSmoking; // ДОБАВЛЕНО: Теперь CommandVfix увидит это поле
    }

    public class VehicleModulesPlugin : RocketPlugin<VehicleModulesConfig>
    {
        public static VehicleModulesPlugin Instance;
        public Dictionary<uint, VehicleState> TrackedVehicles = new Dictionary<uint, VehicleState>();

        protected override void Load()
        {
            Instance = this;
            StartCoroutine(VehicleHealthObserver());
            Rocket.Core.Logging.Logger.Log("VehicleModulesSystem: Наблюдатель запущен. Использование актуальных API (TriggerEffectParameters).");
        }

        protected override void Unload()
        {
            StopAllCoroutines();
            TrackedVehicles.Clear();
        }

        private IEnumerator VehicleHealthObserver()
        {
            while (true)
            {
                for (int i = VehicleManager.vehicles.Count - 1; i >= 0; i--)
                {
                    var vehicle = VehicleManager.vehicles[i];
                    if (vehicle == null || vehicle.asset == null || vehicle.isExploded) continue;

                    if (!Configuration.Instance.TargetedVehicleIds.Contains(vehicle.asset.id)) continue;

                    if (!TrackedVehicles.TryGetValue(vehicle.instanceID, out VehicleState state))
                    {
                        state = new VehicleState { LastHealth = vehicle.health };
                        TrackedVehicles.Add(vehicle.instanceID, state);
                        continue;
                    }

                    // Обработка урона
                    if (vehicle.health < state.LastHealth)
                    {
                        int damageTaken = state.LastHealth - vehicle.health;
                        ExecuteDamageLogic(vehicle, state, damageTaken);
                    }

                    // ОБХОД READ-ONLY SPEED: Если трансмиссия сломана, принудительно гасим инерцию
                    if (state.IsTransmissionBroken)
                    {
                        ForceStopVehicle(vehicle);
                    }

                    state.LastHealth = vehicle.health;
                }
                yield return new WaitForSeconds(0.1f);
            }
        }

        private void ForceStopVehicle(InteractableVehicle v)
        {
            // Поскольку .speed теперь read-only, мы работаем напрямую с Rigidbody
            // Это профессиональный способ остановить технику в Unturned
            if (v.GetComponent<Rigidbody>() is Rigidbody rb)
            {
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }

        private void ExecuteDamageLogic(InteractableVehicle v, VehicleState s, int damage)
        {
            var cfg = Configuration.Instance;
            float damageFactor = damage / 100f;

            if (!s.IsFuelTankBroken && UnityEngine.Random.value < (cfg.ChanceFuelLeak + damageFactor))
            {
                s.IsFuelTankBroken = true;
                NotifyVehicle(v, "ВНИМАНИЕ: Пробит топливный бак!", Color.red);
                StartCoroutine(FuelLeakRoutine(v, s));
            }

            if (!s.IsTransmissionBroken && UnityEngine.Random.value < (cfg.ChanceTransmission + damageFactor))
            {
                s.IsTransmissionBroken = true;
                NotifyVehicle(v, "КРИТ: Трансмиссия уничтожена!", Color.red);
            }

            if (!s.IsOnFire && UnityEngine.Random.value < (cfg.ChanceFire + damageFactor))
            {
                StartCoroutine(FireRoutine(v, s));
            }
        }

        private IEnumerator FuelLeakRoutine(InteractableVehicle v, VehicleState s)
        {
            while (s.IsFuelTankBroken && v != null && !v.isExploded && v.fuel > 0)
            {
                v.fuel = (ushort)Mathf.Max(0, v.fuel - 5);
                yield return new WaitForSeconds(1f);
            }
        }

        private IEnumerator FireRoutine(InteractableVehicle v, VehicleState s)
        {
            s.IsOnFire = true;
            s.IsSmoking = true; // Устанавливаем статус для CommandVfix
            
            while (s.IsOnFire && v != null && !v.isExploded)
            {
                TriggerModernEffect(125, v.transform.position);
                VehicleManager.damage(v, 150, 1, false);
                yield return new WaitForSeconds(1.5f);
                
                // Если машина починена через команду во время пожара
                if (!s.IsOnFire) break; 
            }
            s.IsOnFire = false;
        }

        // РЕШЕНИЕ CS0618: Используем TriggerEffectParameters вместо устаревшего sendEffect
        private void TriggerModernEffect(ushort id, Vector3 pos)
        {
            EffectAsset asset = Assets.find(EAssetType.EFFECT, id) as EffectAsset;
            if (asset != null)
            {
                TriggerEffectParameters parameters = new TriggerEffectParameters(asset);
                parameters.position = pos;
                parameters.relevantDistance = 128f;
                EffectManager.triggerEffect(parameters);
            }
        }

        private void NotifyVehicle(InteractableVehicle v, string text, Color color)
        {
            if (v.passengers.Length > 0 && v.passengers[0].player != null)
                UnturnedChat.Say(v.passengers[0].player.playerID.steamID, text, color);
        }
    }
}
