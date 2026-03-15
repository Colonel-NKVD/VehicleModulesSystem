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
    // Тот самый класс состояния, просто расширенный новыми флагами
    public class VehicleState
    {
        public ushort LastHealth;
        public uint InstanceID;
        public bool IsFuelTankBroken;
        public bool IsTransmissionBroken;
        public bool IsGunBroken;
        public bool IsOnFire;
        public bool IsSmoking;
        public bool IsStunned;
    }

    public class VehicleModulesPlugin : RocketPlugin<VehicleModulesConfig> 
    {
        public static VehicleModulesPlugin Instance;
        public Dictionary<uint, VehicleState> TrackedVehicles = new Dictionary<uint, VehicleState>();

        protected override void Load()
        {
            Instance = this;
            Rocket.Core.Logging.Logger.Log("--- [OBSERVER] Система мониторинга запущена ---");
            StartCoroutine(VehicleHealthWatcher());
        }

        protected override void Unload()
        {
            StopAllCoroutines();
            TrackedVehicles.Clear();
            Rocket.Core.Logging.Logger.Log("--- [OBSERVER] Система остановлена ---");
        }

        // ТВОЙ ЭТАЛОННЫЙ ДАТЧИК
        private IEnumerator VehicleHealthWatcher()
        {
            yield return new WaitForSeconds(3.0f);
            while (true)
            {
                if (VehicleManager.vehicles == null)
                {
                    yield return new WaitForSeconds(1.0f);
                    continue;
                }

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
                        Rocket.Core.Logging.Logger.Log($"[NEW] Датчик установлен: {vehicle.asset.vehicleName}");
                        continue;
                    }

                    // ЗДЕСЬ ТВОЯ МАГИЯ ОТСЛЕЖИВАНИЯ
                    if (vehicle.health < state.LastHealth)
                    {
                        int damageTaken = state.LastHealth - vehicle.health;
                        Rocket.Core.Logging.Logger.Log($"[DAMAGE] {vehicle.asset.vehicleName} получил {damageTaken} урона!");
                        UnturnedChat.Say($"[Датчик] {vehicle.asset.vehicleName} получил {damageTaken} ед. урона!", Color.yellow);
                        
                        // МЫ ПРОСТО ОТПРАВЛЯЕМ ДАННЫЕ В МОЗГ, НЕ ТРОГАЯ ЦИКЛ
                        ModuleDamageHandler.ProcessDamage(vehicle, state, damageTaken);
                    }
                    else if (vehicle.health > state.LastHealth)
                    {
                        Rocket.Core.Logging.Logger.Log($"[REPAIR] Техника {vehicle.asset.vehicleName} была починена.");
                    }

                    // ФИЗИЧЕСКИЕ ЭФФЕКТЫ (Остановка, если оглушен)
                    if (state.IsStunned || (state.IsTransmissionBroken && vehicle.batteryCharge == 0))
                    {
                        var rb = vehicle.GetComponent<Rigidbody>();
                        if (rb != null) { rb.velocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
                    }

                    state.LastHealth = vehicle.health;
                }
                yield return new WaitForSeconds(0.5f);
            }
        }
    }
}
