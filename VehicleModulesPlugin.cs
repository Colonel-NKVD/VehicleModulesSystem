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
            Rocket.Core.Logging.Logger.Log("VehicleModules: Система мониторинга инициализирована.");
            StartCoroutine(SafeStart());
        }

        private IEnumerator SafeStart()
        {
            yield return new WaitForSeconds(3.0f);
            StartCoroutine(VehicleHealthWatcher());
            Rocket.Core.Logging.Logger.Log("VehicleModules: Наблюдатель запущен (Интервал 0.5с).");
        }

        protected override void Unload()
        {
            StopAllCoroutines();
            TrackedVehicles.Clear();
        }

        private IEnumerator VehicleHealthWatcher()
        {
            while (true)
            {
                if (VehicleManager.vehicles == null) { yield return new WaitForSeconds(1.0f); continue; }

                for (int i = VehicleManager.vehicles.Count - 1; i >= 0; i--)
                {
                    var vehicle = VehicleManager.vehicles[i];
                    if (vehicle == null || vehicle.asset == null || vehicle.isExploded) continue;
                    if (!Configuration.Instance.TargetedVehicleIds.Contains(vehicle.asset.id)) continue;

                    // Установка или получение датчика
                    if (!TrackedVehicles.TryGetValue(vehicle.instanceID, out VehicleState state))
                    {
                        state = new VehicleState { LastHealth = vehicle.health };
                        TrackedVehicles.Add(vehicle.instanceID, state);
                        continue;
                    }

                    // АНАЛИЗ УРОНА
                    if (vehicle.health < state.LastHealth)
                    {
                        int damageTaken = state.LastHealth - vehicle.health;
                        Rocket.Core.Logging.Logger.Log($"[HIT] {vehicle.asset.vehicleName} ({vehicle.instanceID}) получил {damageTaken} урона.");
                        ProcessModuleDamage(vehicle, state, damageTaken);
                    }

                    // ОБНОВЛЕНИЕ ПАМЯТИ
                    state.LastHealth = vehicle.health;

                    // ПРИНУДИТЕЛЬНЫЙ СТОП (Трансмиссия)
                    if (state.IsTransmissionBroken) ApplyHardStop(vehicle);
                }
                yield return new WaitForSeconds(0.5f);
            }
        }

        private void ProcessModuleDamage(InteractableVehicle v, VehicleState s, int dmg)
        {
            var cfg = Configuration.Instance;
            float roll = UnityEngine.Random.value;
            float damageMod = dmg / 500f; // Чем сильнее удар, тем выше шанс крита

            if (!s.IsFuelTankBroken && roll < (cfg.ChanceFuelLeak + damageMod))
            {
                s.IsFuelTankBroken = true;
                Notify(v, "КРИТ: Топливный бак пробит!", Color.red);
                StartCoroutine(FuelLeakRoutine(v, s));
            }

            if (!s.IsTransmissionBroken && roll < (cfg.ChanceTransmission + damageMod))
            {
                s.IsTransmissionBroken = true;
                Notify(v, "КРИТ: Трансмиссия выведена из строя!", Color.red);
            }

            if (!s.IsOnFire && roll < (cfg.ChanceFire + damageMod))
            {
                StartCoroutine(FireRoutine(v, s));
            }
        }

        private void ApplyHardStop(InteractableVehicle v)
        {
            var rb = v.GetComponent<Rigidbody>();
            if (rb != null && rb.velocity.magnitude > 0.1f)
            {
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }

        private IEnumerator FuelLeakRoutine(InteractableVehicle v, VehicleState s)
        {
            while (s.IsFuelTankBroken && v != null && !v.isExploded && v.fuel > 0)
            {
                v.fuel = (ushort)Mathf.Max(0, v.fuel - 10);
                yield return new WaitForSeconds(1.0f);
            }
        }

        private IEnumerator FireRoutine(InteractableVehicle v, VehicleState s)
        {
            s.IsOnFire = true;
            s.IsSmoking = true;
            Notify(v, "ВНИМАНИЕ: Пожар в моторном отсеке!", Color.red);
            while (s.IsOnFire && v != null && !v.isExploded)
            {
                SpawnEffect(125, v.transform.position);
                VehicleManager.damage(v, 130, 1, false);
                yield return new WaitForSeconds(1.5f);
            }
            s.IsOnFire = false;
        }

        private void SpawnEffect(ushort id, Vector3 pos)
        {
            if (Assets.find(EAssetType.EFFECT, id) is EffectAsset asset)
                EffectManager.triggerEffect(new TriggerEffectParameters(asset) { position = pos, relevantDistance = 128f });
        }

        private void Notify(InteractableVehicle v, string msg, Color c)
        {
            if (v.passengers.Length > 0 && v.passengers[0].player != null)
                UnturnedChat.Say(v.passengers[0].player.playerID.steamID, msg, c);
        }
    }
}
