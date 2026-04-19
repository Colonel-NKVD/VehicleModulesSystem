using System;
using System.Collections.Generic;
using Rocket.API;
using Rocket.Core.Plugins;
using Rocket.Unturned.Events;
using Rocket.Unturned.Player;
using SDG.Unturned;
using UnityEngine;
using Logger = Rocket.Core.Logging.Logger;

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
        
        // Анти-спам для дебага машин, которые датчик пропускает
        private HashSet<uint> _debugIgnored = new HashSet<uint>();
        private float _timer = 0f;

        protected override void Load()
        {
            Instance = this;
            
            Console.WriteLine("!!! [DEBUG] MODULES_SYSTEM: ЗАПУСК ПЛАГИНА !!!");
            Logger.Log("--- [ДАТЧИК] Инициализация системы... ---");

            UnturnedPlayerEvents.OnPlayerDeath += OnPlayerDeath;

            if (Configuration.Instance.AllowedVehicleIds == null)
                Configuration.Instance.AllowedVehicleIds = new List<ushort>();

            Logger.Log($"[ДАТЧИК] Разрешенных ID в конфиге: {Configuration.Instance.AllowedVehicleIds.Count}");
            foreach(var id in Configuration.Instance.AllowedVehicleIds)
            {
                Logger.Log($"[ДАТЧИК] Зарегистрирован ID: {id}");
            }
        }

        protected override void Unload()
        {
            UnturnedPlayerEvents.OnPlayerDeath -= OnPlayerDeath;
            TrackedVehicles.Clear();
            _debugIgnored.Clear();
            Logger.Log("[ДАТЧИК] Система отключена.");
        }

        public VehicleState GetVehicleState(InteractableVehicle v)
        {
            if (v == null) return null;
            if (!TrackedVehicles.TryGetValue(v.instanceID, out VehicleState state))
            {
                state = new VehicleState { InstanceID = v.instanceID, LastHealth = v.health };
                TrackedVehicles.Add(v.instanceID, state);
                Logger.Log($"[ОБНАРУЖЕНИЕ] Объект захвачен: {v.asset.name} (ID: {v.asset.id}) | Инстанс: {v.instanceID}");
            }
            return state;
        }

        private void OnPlayerDeath(UnturnedPlayer player, EDeathCause cause, ELimb limb, Steamworks.CSteamID murderer)
        {
            if (player?.Player != null)
                player.Player.setPluginWidgetFlag(EPluginWidgetFlags.Modal, false);
        }

        // ИСПОЛЬЗУЕМ НАТИВНЫЙ UPDATE ВМЕСТО КОРУТИНЫ (МАКСИМАЛЬНАЯ СТАБИЛЬНОСТЬ)
        public void Update()
        {
            _timer += Time.deltaTime;
            if (_timer < 0.5f) return; // Проверка каждые 0.5 секунд
            _timer = 0f;

            if (VehicleManager.vehicles == null) return;

            var allowedIds = Configuration.Instance.AllowedVehicleIds;

            for (int i = VehicleManager.vehicles.Count - 1; i >= 0; i--)
            {
                try 
                {
                    var vehicle = VehicleManager.vehicles[i];
                    
                    if (vehicle == null || vehicle.asset == null) continue;

                    if (vehicle.isExploded || vehicle.health == 0)
                    {
                        if (TrackedVehicles.ContainsKey(vehicle.instanceID))
                        {
                            TrackedVehicles.Remove(vehicle.instanceID);
                            Logger.Log($"[ДАТЧИК] Объект уничтожен и удален из трекера: {vehicle.asset.name}");
                        }
                        continue;
                    }

                    ushort currentId = vehicle.asset.id;
                    
                    // Если машины нет в списке
                    if (allowedIds == null || !allowedIds.Contains(currentId)) 
                    {
                        if (TrackedVehicles.ContainsKey(vehicle.instanceID))
                            TrackedVehicles.Remove(vehicle.instanceID);

                        if (!_debugIgnored.Contains(vehicle.instanceID))
                        {
                            _debugIgnored.Add(vehicle.instanceID);
                            // РАДАР: Напишет ровно один раз, если машина не прошла фильтр
                            Logger.Log($"[РАДАР] Игнорирую: {vehicle.asset.name} (ID: {currentId}) - отсутствует в AllowedVehicleIds.");
                        }
                        continue;
                    }

                    // Машина в списке - гарантированно захватываем
                    VehicleState state = GetVehicleState(vehicle);
                    if (state == null) continue;

                    // Детекция урона
                    if (vehicle.health < state.LastHealth)
                    {
                        int damageTaken = state.LastHealth - vehicle.health;
                        Logger.Log($"[УРОН] Зафиксировано попадание по {vehicle.asset.name} ({currentId}): {damageTaken} ед. HP: {vehicle.health}/{vehicle.asset.health}");
                        
                        ModuleDamageHandler.ProcessDamage(vehicle, state, damageTaken);
                    }
                    // Детекция ремонта горелкой
                    else if (vehicle.health > state.LastHealth)
                    {
                        Logger.Log($"[РЕМОНТ] Техника {vehicle.asset.name} восстановлена на {vehicle.health - state.LastHealth} HP.");
                    }

                    // Синхронизация эффектов
                    if (state.IsStunned)
                    {
                        var rb = vehicle.GetComponent<Rigidbody>();
                        if (rb != null) { rb.velocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
                    }

                    if (state.IsTransmissionBroken && vehicle.batteryCharge > 0)
                    {
                        vehicle.batteryCharge = 0;
                        VehicleManager.sendVehicleBatteryCharge(vehicle, 0);
                    }

                    state.LastHealth = vehicle.health;
                }
                catch (Exception ex)
                {
                    Logger.LogError($"[WATCHER ERROR] Сбой обработки инстанса: {ex.Message}");
                }
            }
        }
    }
}
