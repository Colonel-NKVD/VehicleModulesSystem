using System;
using System.Collections.Generic;
using System.Collections;
using Rocket.API;
using Rocket.Core.Plugins;
using Rocket.Unturned.Chat;
using SDG.Unturned;
using UnityEngine;

namespace VehicleModulesSystem
{
    public class VehicleModulesPlugin : RocketPlugin<VehicleModulesConfig> 
    {
        public static VehicleModulesPlugin Instance;
        public Dictionary<uint, VehicleState> TrackedVehicles = new Dictionary<uint, VehicleState>();

        protected override void Load()
        {
            Instance = this;
            // ФИКС ВЕЧНОГО КУРСОРA: Сбрасываем Modal при смерти
            PlayerLife.onPlayerDied += OnPlayerDied;
            
            Rocket.Core.Logging.Logger.Log("--- [OBSERVER] Система мониторинга запущена ---");
            StartCoroutine(VehicleHealthWatcher());
        }

        protected override void Unload()
        {
            PlayerLife.onPlayerDied -= OnPlayerDied;
            StopAllCoroutines();
            TrackedVehicles.Clear();
        }

        private void OnPlayerDied(PlayerLife life, EDiedHabit habit)
        {
            // Гарантируем возврат управления после респавна
            life.player.setPluginWidgetFlag(EPluginWidgetFlags.Modal, false);
        }

        private IEnumerator VehicleHealthWatcher()
        {
            yield return new WaitForSeconds(3.0f);
            while (true)
            {
                if (VehicleManager.vehicles == null) { yield return new WaitForSeconds(1.0f); continue; }

                for (int i = VehicleManager.vehicles.Count - 1; i >= 0; i--)
                {
                    var vehicle = VehicleManager.vehicles[i];
                    if (vehicle == null || vehicle.asset == null || vehicle.isExploded) 
                    {
                        if (vehicle != null && TrackedVehicles.ContainsKey(vehicle.instanceID))
                            TrackedVehicles.Remove(vehicle.instanceID);
                        continue;
                    }

                    if (!TrackedVehicles.TryGetValue(vehicle.instanceID, out VehicleState state))
                    {
                        state = new VehicleState { InstanceID = vehicle.instanceID, LastHealth = vehicle.health };
                        TrackedVehicles.Add(vehicle.instanceID, state);
                        continue;
                    }

                    // ДАТЧИК УРОНА
                    if (vehicle.health < state.LastHealth)
                    {
                        int damageTaken = state.LastHealth - vehicle.health;
                        ModuleDamageHandler.ProcessDamage(vehicle, state, damageTaken);
                    }

                    // ФИЗИЧЕСКОЕ ПОДАВЛЕНИЕ (Работает мгновенно, не нужно выходить из авто)
                    if (state.IsStunned)
                    {
                        var rb = vehicle.GetComponent<Rigidbody>();
                        if (rb != null) { rb.velocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
                    }

                    // ФИКС АККУМУЛЯТОРА: Если трансмиссия бита, заряд всегда 0
                    if (state.IsTransmissionBroken && vehicle.batteryCharge > 0)
                    {
                        vehicle.batteryCharge = 0;
                    }

                    state.LastHealth = vehicle.health;
                }
                yield return new WaitForSeconds(0.5f);
            }
        }
    }
}
