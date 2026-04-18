using System;
using System.Collections.Generic;
using System.Collections;
using Rocket.API;
using Rocket.Core.Plugins;
using Rocket.Unturned.Events;
using Rocket.Unturned.Player;
using Rocket.Unturned.Chat;
using SDG.Unturned;
using UnityEngine;
using HarmonyLib;

namespace VehicleModulesSystem
{
    public class VehicleModulesPlugin : RocketPlugin<VehicleModulesConfig> 
    {
        public static VehicleModulesPlugin Instance;
        public Dictionary<uint, VehicleState> TrackedVehicles = new Dictionary<uint, VehicleState>();
        private Harmony harmony;

        protected override void Load()
        {
            Instance = this;
            
            // Harmony (если используешь патчи в других файлах)
            try {
                harmony = new Harmony("com.ironmud.vehiclemodules");
                harmony.PatchAll();
            } catch (Exception e) {
                Rocket.Core.Logging.Logger.Log("Harmony Error: " + e.Message);
            }

            UnturnedPlayerEvents.OnPlayerDeath += OnPlayerDeath;
            
            if (Configuration.Instance.AllowedVehicleIds == null)
                Configuration.Instance.AllowedVehicleIds = new List<ushort>();

            StartCoroutine(VehicleHealthWatcher());
            
            Rocket.Core.Logging.Logger.Log("================================================");
            Rocket.Core.Logging.Logger.Log("--- [VehicleModules] СИСТЕМА ЗАПУЩЕНА ---");
            Rocket.Core.Logging.Logger.Log($"--- Отслеживается ID техники: {Configuration.Instance.AllowedVehicleIds.Count} ---");
            Rocket.Core.Logging.Logger.Log("================================================");
        }

        protected override void Unload()
        {
            StopAllCoroutines();
            TrackedVehicles.Clear();
            Instance = null;
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
            if (player?.Player != null)
                player.Player.setPluginWidgetFlag(EPluginWidgetFlags.Modal, false);
        }

        private IEnumerator VehicleHealthWatcher()
        {
            yield return new WaitForSeconds(3.0f); // Даем серверу прогрузиться

            while (true)
            {
                if (VehicleManager.vehicles == null) { yield return new WaitForSeconds(1.0f); continue; }

                for (int i = VehicleManager.vehicles.Count - 1; i >= 0; i--)
                {
                    var vehicle = VehicleManager.vehicles[i];
                    if (vehicle == null || vehicle.asset == null || vehicle.isExploded) continue;

                    // ПРОВЕРКА ID: Если танка нет в списке, датчик на него не реагирует
                    if (!Configuration.Instance.AllowedVehicleIds.Contains(vehicle.id)) continue;

                    VehicleState state = GetVehicleState(vehicle);

                    // ДАТЧИК УРОНА
                    if (vehicle.health < state.LastHealth)
                    {
                        int damageTaken = state.LastHealth - vehicle.health;
                        
                        // ЛОГ В КОНСОЛЬ (для тебя)
                        Rocket.Core.Logging.Logger.Log($"[SENSOR] Техника {vehicle.id} получила {damageTaken} урона.");

                        // СООБЩЕНИЕ ЭКИПАЖУ (как в старые добрые)
                        ModuleDamageHandler.SendChat(vehicle, $"[ДАТЧИК] Попадание! -{damageTaken} HP. Осталось: {vehicle.health}", Color.yellow);

                        // ЛОГИКА БРОНИ И КРИТОВ
                        if (damageTaken < Configuration.Instance.MinDamageForCrit && UnityEngine.Random.value < Configuration.Instance.ChanceDeflect)
                        {
                            vehicle.askRepair((ushort)damageTaken);
                            VehicleManager.sendVehicleHealth(vehicle, vehicle.health);
                            ModuleDamageHandler.SendChat(vehicle, "[БРОНЯ] Рикошет! Урон не нанесен.", Color.green);
                        }
                        else
                        {
                            ModuleDamageHandler.ProcessDamage(vehicle, state, damageTaken);
                        }
                    }

                    // АКТИВНЫЕ ЭФФЕКТЫ
                    if (state.IsStunned)
                    {
                        var rb = vehicle.GetComponent<Rigidbody>();
                        if (rb != null) { rb.velocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
                    }

                    if (state.IsTransmissionBroken && vehicle.batteryCharge > 0)
                    {
                        vehicle.batteryCharge = 0;
                        VehicleManager.sendVehicleFuel(vehicle, vehicle.fuel);
                    }

                    state.LastHealth = vehicle.health;
                }
                yield return new WaitForSeconds(0.5f);
            }
        }

        // ПОЛНОСТЬЮ ИСПРАВЛЕННЫЙ МЕТОД БИНТА (FIX CS7036)
        public IEnumerator BandageRoutine(UnturnedPlayer player, ushort bandageId)
        {
            yield return new WaitForSeconds(Configuration.Instance.BandageUseTimeSeconds);
            
            if (player != null && player.IsInVehicle && !player.Dead)
            {
                var items = player.Inventory.search(bandageId, true, true);
                if (items.Count > 0)
                {
                    byte page = items[0].page;
                    byte x = items[0].jar.x;
                    byte y = items[0].jar.y;
                    byte index = player.Inventory.getIndex(page, x, y);

                    player.Inventory.removeItem(page, index);
                    player.Player.life.askHeal(Configuration.Instance.BandageHealAmount, true, true);
                    UnturnedChat.Say(player, "Вы перевязали раны экипажа.", Color.green);
                }
            }
        }
    }
}
