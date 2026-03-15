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
        public bool IsRepairing;
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

        public VehicleState GetVehicleState(InteractableVehicle v)
        {
            if (v == null) return null;
            if (!TrackedVehicles.TryGetValue(v.instanceID, out VehicleState state))
            {
                state = new VehicleState { InstanceID = v.instanceID, LastHealth = v.health };
                TrackedVehicles.Add(v.instanceID, state);
            }
            return state;
        }

        private void OnPlayerDeath(UnturnedPlayer player, EDeathCause cause, ELimb limb, Steamworks.CSteamID murderer)
        {
            if (player != null && player.Player != null)
            {
                player.Player.setPluginWidgetFlag(EPluginWidgetFlags.Modal, false);
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
                    
                    // --- КРИТИЧЕСКИЙ ФИЛЬТР ПО ID ---
                    // Проверяем: существует ли машина, не взорвана ли она и есть ли её ID в списке разрешенных в конфиге
                    if (vehicle == null || vehicle.isExploded || Configuration.Instance.AllowedVehicleIds == null || !Configuration.Instance.AllowedVehicleIds.Contains(vehicle.id)) 
                    {
                        // Если машина не в списке (или конфиг пуст), удаляем её из мониторинга и идем дальше
                        if (vehicle != null && TrackedVehicles.ContainsKey(vehicle.instanceID))
                            TrackedVehicles.Remove(vehicle.instanceID);
                        continue;
                    }

                    // Если код дошел сюда — значит танк "свой" и мы начинаем работу
                    VehicleState state = GetVehicleState(vehicle);

                    // Расширенный датчик урона
                    if (vehicle.health < state.LastHealth)
                    {
                        int damageTaken = state.LastHealth - vehicle.health;
                        ModuleDamageHandler.SendChat(vehicle, $"[ДАТЧИК] Получено {damageTaken} ед. урона! Состояние: {vehicle.health}/{vehicle.asset.health}", Color.yellow);
                        ModuleDamageHandler.ProcessDamage(vehicle, state, damageTaken);
                    }
                    else if (vehicle.health > state.LastHealth)
                    {
                        // Просто лог восстановления (например, обычной ремкой)
                        // state.LastHealth обновится в конце цикла
                    }

                    // Физическая заморозка при контузии (Stun)
                    if (state.IsStunned)
                    {
                        var rb = vehicle.GetComponent<Rigidbody>();
                        if (rb != null) 
                        { 
                            rb.velocity = Vector3.zero; 
                            rb.angularVelocity = Vector3.zero; 
                        }
                    }

                    // Блокировка хода при поломке трансмиссии (слив батареи)
                    if (state.IsTransmissionBroken && vehicle.batteryCharge > 0)
                    {
                        vehicle.batteryCharge = 0;
                        VehicleManager.sendVehicleFuel(vehicle, vehicle.fuel); // Синхронизация состояния
                    }

                    state.LastHealth = vehicle.health;
                }
                yield return new WaitForSeconds(0.5f);
            }
        }
    }
}
