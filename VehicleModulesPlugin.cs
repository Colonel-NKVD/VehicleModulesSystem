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
using Steamworks;

namespace VehicleModulesSystem
{
    public class VehicleModulesPlugin : RocketPlugin<VehicleModulesConfig> 
    {
        public static VehicleModulesPlugin Instance;
        public Dictionary<uint, VehicleState> TrackedVehicles = new Dictionary<uint, VehicleState>();

        protected override void Load()
        {
            Instance = this;
            UnturnedPlayerEvents.OnPlayerDeath += OnPlayerDeath;
            
            Rocket.Core.Logging.Logger.Log("================================================");
            Rocket.Core.Logging.Logger.Log("--- [VEHICLE MODULES] Легкий режим загружен ---");
            Rocket.Core.Logging.Logger.Log("--- HARMONY ОТКЛЮЧЕН. Используется ванильное API ---");
            Rocket.Core.Logging.Logger.Log("================================================");
            
            StartCoroutine(VehicleHealthWatcher());
        }

        protected override void Unload()
        {
            UnturnedPlayerEvents.OnPlayerDeath -= OnPlayerDeath;
            StopAllCoroutines();
            TrackedVehicles.Clear();
            Rocket.Core.Logging.Logger.Log("[VEHICLE MODULES] Выгружен.");
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

        public IEnumerator BandageRoutine(UnturnedPlayer player, ushort bandageId)
        {
            yield return new WaitForSeconds(Configuration.Instance.BandageUseTimeSeconds);

            if (player == null || player.Dead || !player.IsInVehicle)
            {
                UnturnedChat.Say(player, "Перевязка прервана!", Color.red);
                yield break;
            }

            var items = player.Inventory.search(bandageId, true, true);
            if (items.Count > 0)
            {
                player.Inventory.removeItem(items[0].page, player.Inventory.getIndex(items[0].page, items[0].jar.x, items[0].jar.y));
                player.Player.life.askHeal(Configuration.Instance.BandageHealAmount, true, true);
                UnturnedChat.Say(player, "Вы успешно перевязали раны.", Color.green);
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
                    
                    // Если техника взорвана или её нет в списке AllowedVehicleIds - пропускаем
                    if (vehicle == null || vehicle.isExploded || !Configuration.Instance.AllowedVehicleIds.Contains(vehicle.id)) 
                        continue;

                    VehicleState state = GetVehicleState(vehicle);

                    if (vehicle.health < state.LastHealth)
                    {
                        int damageTaken = state.LastHealth - vehicle.health;
                        float maxHealth = vehicle.asset.health;

                        // ДЕБАГ-ЛОГ (поможет нам понять, видит ли плагин урон вообще)
                        Rocket.Core.Logging.Logger.Log($"[DEBUG] Техника {vehicle.id} получила урон: {damageTaken}. Порог для крита: {Configuration.Instance.MinDamageForCrit}");

                        // ЛОГИКА РИКОШЕТА
                        if (damageTaken < (maxHealth * 0.20f) && UnityEngine.Random.value < Configuration.Instance.ChanceDeflect)
                        {
                            vehicle.askRepair((ushort)damageTaken);
                            VehicleManager.sendVehicleHealth(vehicle, vehicle.health);
                            ModuleDamageHandler.SendChat(vehicle, "РИКОШЕТ! Броня не пробита.", Color.green);
                            Rocket.Core.Logging.Logger.Log($"[DEBUG] Сработал рикошет по {vehicle.id}.");
                            
                            state.LastHealth = vehicle.health;
                            continue;
                        }

                        // ЛОГИКА КРИТОВ
                        if (damageTaken >= Configuration.Instance.MinDamageForCrit)
                        {
                            ModuleDamageHandler.SendChat(vehicle, $"[ВНИМАНИЕ] Пробитие! Получено {damageTaken} ед. урона.", Color.yellow);
                            ModuleDamageHandler.ProcessDamage(vehicle, state, damageTaken);
                        }
                        else
                        {
                            Rocket.Core.Logging.Logger.Log($"[DEBUG] Урон {damageTaken} слишком мал для вызова крита (нужно >= {Configuration.Instance.MinDamageForCrit}).");
                        }
                    }
                    else if (vehicle.health > state.LastHealth)
                    {
                        // Полная починка сбрасывает все поломки
                        if (vehicle.health == vehicle.asset.health)
                        {
                            state.IsTransmissionBroken = false;
                            state.IsFuelTankBroken = false;
                            state.IsGunBroken = false;
                            state.IsOnFire = false;
                            state.IsSmoking = false;
                            state.IsStunned = false;
                        }
                    }

                    // --- ПОСТОЯННЫЕ ЭФФЕКТЫ ---

                    // Задымление
                    if (state.IsSmoking)
                    {
                        EffectManager.sendEffect(Configuration.Instance.SmokeVisualEffectId, 128, vehicle.transform.position + Vector3.up * 1.5f);
                    }

                    // Трансмиссия: глушим движок и тормозим танк физически
                    if (state.IsTransmissionBroken)
                    {
                        if (vehicle.batteryCharge > 0)
                        {
                            vehicle.batteryCharge = 0; // Садим аккум, чтобы нельзя было завестись
                        }
                        
                        // Если танк катится — жестко гасим его скорость
                        var rb = vehicle.GetComponent<Rigidbody>();
                        if (rb != null && rb.velocity.magnitude > 0.1f)
                        {
                            rb.velocity = Vector3.Lerp(rb.velocity, Vector3.zero, Time.deltaTime * 2f);
                        }
                    }

                    state.LastHealth = vehicle.health;
                }
                yield return new WaitForSeconds(0.5f);
            }
        }
    }
}
