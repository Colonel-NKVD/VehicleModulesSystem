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
using Steamworks;

namespace VehicleModulesSystem
{
    public class VehicleModulesPlugin : RocketPlugin<VehicleModulesConfig> 
    {
        public static VehicleModulesPlugin Instance;
        public Dictionary<uint, VehicleState> TrackedVehicles = new Dictionary<uint, VehicleState>();
        
        public const string HarmonyInstanceId = "com.ironandmud.vehiclemodules";
        private Harmony harmony;

        protected override void Load()
        {
            Instance = this;
            
            try 
            {
                harmony = new Harmony(HarmonyInstanceId);
                harmony.PatchAll();
                Rocket.Core.Logging.Logger.Log("--- [HARMONY] Патчи успешно применены ---");
            }
            catch (Exception e) 
            {
                Rocket.Core.Logging.Logger.Log("--- [HARMONY] КРИТИЧЕСКАЯ ОШИБКА: " + e.Message);
            }

            UnturnedPlayerEvents.OnPlayerDeath += OnPlayerDeath;
            
            if (Configuration.Instance.AllowedVehicleIds == null)
            {
                Configuration.Instance.AllowedVehicleIds = new List<ushort>();
                Rocket.Core.Logging.Logger.LogWarning("[ВНИМАНИЕ] AllowedVehicleIds был null. Создан пустой список.");
            }
            
            StartCoroutine(VehicleHealthWatcher());
        }

        protected override void Unload()
        {
            if (harmony != null)
            {
                harmony.UnpatchAll(HarmonyInstanceId);
            }
            UnturnedPlayerEvents.OnPlayerDeath -= OnPlayerDeath;
            StopAllCoroutines();
            TrackedVehicles.Clear();
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

        private void OnPlayerDeath(UnturnedPlayer player, EDeathCause cause, ELimb limb, CSteamID murderer)
        {
            if (player != null && player.Player != null)
                player.Player.setPluginWidgetFlag(EPluginWidgetFlags.Modal, false);
        }

        // --- ВОССТАНОВЛЕННАЯ ЛОГИКА БИНТА (CS1061 FIX) ---
        public IEnumerator BandageRoutine(UnturnedPlayer player, ushort bandageId)
        {
            yield return new WaitForSeconds(Configuration.Instance.BandageUseTimeSeconds);

            // Проверяем, что игрок не вышел, не умер и все еще в технике
            if (player == null || player.Player == null || player.Dead || !player.IsInVehicle)
            {
                yield break; 
            }

            var items = player.Inventory.search(bandageId, true, true);
            if (items.Count > 0)
            {
                // Забираем бинт
                player.Inventory.removeItem(items[0].page, player.Inventory.getIndex(items[0].page, items[0].jar.x, items[0].jar.y));
                
                // Лечим (восстанавливаем ХП и снимаем кровотечение)
                player.Player.life.askHeal(Configuration.Instance.BandageHealAmount, true, true);
                UnturnedChat.Say(player, ">> ПЕРЕВЯЗКА ЭКИПАЖА ЗАВЕРШЕНА <<", Color.green);
            }
            else
            {
                UnturnedChat.Say(player, "[ОШИБКА] Бинт не найден в инвентаре!", Color.red);
            }
        }

        private IEnumerator VehicleHealthWatcher()
        {
            Rocket.Core.Logging.Logger.Log("[SENSOR-DEBUG] Поток датчика успешно запущен. Ожидание транспорта...");
            yield return new WaitForSeconds(3.0f);
            
            int tickCounter = 0;

            while (true)
            {
                try
                {
                    if (VehicleManager.vehicles == null) 
                    { 
                        yield return new WaitForSeconds(1.0f); 
                        continue; 
                    }

                    // Пульс датчика: каждые 10 секунд пишем в консоль, чтобы доказать, что корутина жива
                    tickCounter++;
                    if (tickCounter >= 20) 
                    {
                        Rocket.Core.Logging.Logger.Log($"[SENSOR-HEARTBEAT] Датчик активен. Машин на сервере: {VehicleManager.vehicles.Count}. В вайтлисте: {Configuration.Instance.AllowedVehicleIds.Count}");
                        tickCounter = 0;
                    }

                    for (int i = VehicleManager.vehicles.Count - 1; i >= 0; i--)
                    {
                        var vehicle = VehicleManager.vehicles[i];
                        
                        // Пропускаем пустышки
                        if (vehicle == null || vehicle.asset == null) continue;

                        ushort vId = vehicle.asset.id;

                        // ФИЛЬТР: Датчик работает ТОЛЬКО с техникой из AllowedVehicleIds
                        if (vehicle.isExploded || !Configuration.Instance.AllowedVehicleIds.Contains(vId)) 
                        {
                            if (TrackedVehicles.ContainsKey(vehicle.instanceID))
                                TrackedVehicles.Remove(vehicle.instanceID);
                            continue;
                        }

                        // === С ЭТОГО МОМЕНТА ДАТЧИК СМОТРИТ ТОЛЬКО НА РАЗРЕШЕННУЮ ТЕХНИКУ ===
                        VehicleState state = GetVehicleState(vehicle);

                        if (vehicle.health < state.LastHealth)
                        {
                            int damageTaken = state.LastHealth - vehicle.health;
                            float maxHealth = vehicle.asset.health;

                            // ЖЕСТКИЙ ДЕБАГ ТОЛЬКО ПРИ ПОЛУЧЕНИИ УРОНА
                            Rocket.Core.Logging.Logger.Log($"[SENSOR-TRIGGER] Техника {vId} (Instance: {vehicle.instanceID}) получила УРОН: {damageTaken}! (Здоровье: {state.LastHealth} -> {vehicle.health})");

                            if (damageTaken < (maxHealth * 0.20f) && UnityEngine.Random.value < Configuration.Instance.ChanceDeflect)
                            {
                                vehicle.askRepair((ushort)damageTaken);
                                VehicleManager.sendVehicleHealth(vehicle, vehicle.health);
                                ModuleDamageHandler.SendChat(vehicle, "[СИСТЕМА] РИКОШЕТ! Броня не пробита.", Color.green);
                                Rocket.Core.Logging.Logger.Log($"[SENSOR-DEBUG] Урон по технике {vId} ушел в РИКОШЕТ.");
                            }
                            else if (damageTaken >= Configuration.Instance.MinDamageForCrit) 
                            {
                                Rocket.Core.Logging.Logger.Log($"[SENSOR-DEBUG] Урон по технике {vId} преодолел порог. Запуск просчета критов.");
                                ModuleDamageHandler.SendChat(vehicle, $"[ДАТЧИК] Получено {damageTaken} ед. урона! Состояние: {vehicle.health}/{maxHealth}", Color.yellow);
                                ModuleDamageHandler.ProcessDamage(vehicle, state, damageTaken);
                            }
                            else
                            {
                                Rocket.Core.Logging.Logger.Log($"[SENSOR-DEBUG] Урон {damageTaken} по технике {vId} слишком мал для крита (Мин. порог: {Configuration.Instance.MinDamageForCrit}).");
                            }
                        }
                        else if (vehicle.health > state.LastHealth && vehicle.health == vehicle.asset.health)
                        {
                            state.IsTransmissionBroken = false;
                            state.IsFuelTankBroken = false;
                            state.IsGunBroken = false;
                            state.IsOnFire = false;
                            state.IsSmoking = false;
                            state.IsStunned = false;
                            Rocket.Core.Logging.Logger.Log($"[SENSOR-DEBUG] Техника {vId} полностью отремонтирована. Модули восстановлены.");
                        }

                        if (state.IsStunned)
                        {
                            var rb = vehicle.GetComponent<Rigidbody>();
                            if (rb != null) 
                            { 
                                rb.velocity = Vector3.zero; 
                                rb.angularVelocity = Vector3.zero; 
                            }
                        }

                        if (state.IsSmoking)
                        {
                            EffectManager.sendEffect(36009, 128, vehicle.transform.position + Vector3.up * 1.5f);
                        }

                        // --- ИСПРАВЛЕНИЕ АПИ UNTURNED (CS0117 FIX) ---
                        // Глушим машину батареей, а синхронизируем пакетом топлива как в старой стабильной сборке
                        if (state.IsTransmissionBroken && vehicle.batteryCharge > 0)
                        {
                            vehicle.batteryCharge = 0;
                            VehicleManager.sendVehicleFuel(vehicle, vehicle.fuel); 
                        }

                        state.LastHealth = vehicle.health;
                    }
                }
                catch (Exception ex)
                {
                    // ЕСЛИ ЧТО-ТО ПОШЛО НЕ ТАК - МЫ УВИДИМ ЭТО, А ДАТЧИК НЕ УМРЕТ
                    Rocket.Core.Logging.Logger.LogError("[SENSOR-CRITICAL] Ошибка в цикле датчика: " + ex.Message);
                    Rocket.Core.Logging.Logger.LogError(ex.StackTrace);
                }
                
                yield return new WaitForSeconds(0.5f);
            }
        }
    }
}
