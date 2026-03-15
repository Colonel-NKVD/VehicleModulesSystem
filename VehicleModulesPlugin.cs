using System;
using System.Collections.Generic;
using System.Collections;
using Rocket.API;
using Rocket.Core.Plugins;
using Rocket.Unturned.Chat;
using Rocket.Unturned.Events;
using Rocket.Unturned.Player;
using SDG.Unturned;
using UnityEngine;

namespace VehicleModulesSystem
{
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
            UnturnedPlayerEvents.OnPlayerDeath += OnPlayerDeath;
            
            Rocket.Core.Logging.Logger.Log("================================================");
            Rocket.Core.Logging.Logger.Log("--- [OBSERVER] Система мониторинга запущена ---");
            Rocket.Core.Logging.Logger.Log("--- Протокол: Дизельпанк / Grimdark 1917+ ---");
            Rocket.Core.Logging.Logger.Log("================================================");
            
            StartCoroutine(VehicleHealthWatcher());
        }

        protected override void Unload()
        {
            UnturnedPlayerEvents.OnPlayerDeath -= OnPlayerDeath;
            StopAllCoroutines();
            TrackedVehicles.Clear();
            Rocket.Core.Logging.Logger.Log("[OBSERVER] Система аварийно остановлена.");
        }

        private void OnPlayerDeath(UnturnedPlayer player, EDeathCause cause, ELimb limb, Steamworks.CSteamID murderer)
        {
            if (player != null && player.Player != null)
            {
                player.Player.setPluginWidgetFlag(EPluginWidgetFlags.Modal, false);
                Rocket.Core.Logging.Logger.Log($"[STUN_FIX] Сброшен Modal для игрока: {player.CharacterName}");
            }
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
                    if (vehicle == null || vehicle.isExploded) 
                    {
                        if (vehicle != null && TrackedVehicles.ContainsKey(vehicle.instanceID))
                            TrackedVehicles.Remove(vehicle.instanceID);
                        continue;
                    }

                    if (!TrackedVehicles.TryGetValue(vehicle.instanceID, out VehicleState state))
                    {
                        state = new VehicleState { InstanceID = vehicle.instanceID, LastHealth = vehicle.health };
                        TrackedVehicles.Add(vehicle.instanceID, state);
                        Rocket.Core.Logging.Logger.Log($"[NEW] Объект взят на мониторинг: {vehicle.asset.vehicleName} (ID: {vehicle.instanceID})");
                        continue;
                    }

                    // --- РАСШИРЕННЫЙ ДАТЧИК УРОНА ---
                    if (vehicle.health < state.LastHealth)
                    {
                        int damageTaken = state.LastHealth - vehicle.health;
                        Rocket.Core.Logging.Logger.Log($"[DAMAGE_EVENT] {vehicle.asset.vehicleName} получил {damageTaken} урона. (Остаток: {vehicle.health})");
                        
                        // Сообщение экипажу о получении урона
                        ModuleDamageHandler.SendChat(vehicle, $"[ДАТЧИК] Получено {damageTaken} ед. урона! Состояние: {vehicle.health}/{vehicle.asset.health}", Color.yellow);
                        
                        ModuleDamageHandler.ProcessDamage(vehicle, state, damageTaken);
                    }
                    else if (vehicle.health > state.LastHealth)
                    {
                        Rocket.Core.Logging.Logger.Log($"[REPAIR_EVENT] {vehicle.asset.vehicleName} восстановлен: {state.LastHealth} -> {vehicle.health}");
                    }

                    // --- ПОДАВЛЕНИЕ СИСТЕМ ---
                    if (state.IsStunned)
                    {
                        var rb = vehicle.GetComponent<Rigidbody>();
                        if (rb != null) { rb.velocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
                    }

                    if (state.IsTransmissionBroken && vehicle.batteryCharge > 0)
                    {
                        vehicle.batteryCharge = 0;
                        // Исправлено: Синхронизация через FuelManager, так как sendVehicleBattery не существует
                        VehicleManager.sendVehicleFuel(vehicle, vehicle.fuel);
                    }

                    state.LastHealth = vehicle.health;
                }
                yield return new WaitForSeconds(0.5f);
            }
        }
    }
}
