using System.Collections.Generic;
using System.Collections;
using SDG.Unturned;
using UnityEngine;
using Rocket.Unturned.Chat;
using Steamworks;
using Logger = Rocket.Core.Logging.Logger;

namespace VehicleModulesSystem
{
    public static class ModuleDamageHandler
    {
        public static void ProcessDamage(InteractableVehicle v, VehicleState s, int dmg)
        {
            if (v == null || v.asset == null || s == null) return;
            
            var cfg = VehicleModulesPlugin.Instance.Configuration.Instance;
            if (s.IsOnFire) return;

            // 1. Рикошет
            float maxHealth = v.asset.health;
            if (dmg < (maxHealth * cfg.RicochetThresholdPercent))
            {
                if (Random.value < cfg.RicochetChance)
                {
                    Logger.Log($"[HIT] Рикошет по {v.asset.name} (урон {dmg})");
                    SendChat(v, ">>> РИКОШЕТ / БРОНЯ НЕ ПРОБИТА <<<", Color.white);
                    return; 
                }
            }

            // 2. Минимальный урон для крита
            if (dmg < cfg.MinDamageForCritical) 
            {
                Logger.Log($"[HIT] Урон {dmg} ниже порога крита ({cfg.MinDamageForCritical})");
                return;
            }

            float intensity = Mathf.Clamp(dmg / 1500f, 0f, 0.25f); 

            // Контузия экипажа
            if (!s.IsStunned && Random.value < (0.10f + intensity))
            {
                Logger.Log($"[CRIT] Контузия экипажа {v.asset.name}");
                VehicleModulesPlugin.Instance.StartCoroutine(StunRoutine(v, s, 5));
            }

            int criticalsThisHit = 0;
            int maxCriticals = dmg > 600 ? 2 : 1; 

            List<System.Action> moduleChecks = new List<System.Action>
            {
                () => {
                    if (!s.IsFuelTankBroken && Random.value < (cfg.ChanceFuelLeak + intensity)) {
                        s.IsFuelTankBroken = true;
                        Logger.Log($"[CRIT] Пробит бак {v.asset.name}");
                        SendChat(v, "!!! КРИТ: Пробит топливный бак !!!", Color.red);
                        VehicleModulesPlugin.Instance.StartCoroutine(FuelRoutine(v, s));
                        criticalsThisHit++;
                    }
                },
                () => {
                    if (!s.IsTransmissionBroken && Random.value < (cfg.ChanceTransmission + intensity)) {
                        s.IsTransmissionBroken = true;
                        Logger.Log($"[CRIT] Поломка трансмиссии {v.asset.name}");
                        SendChat(v, "[СИСТЕМА] Трансмиссия повреждена!", Color.yellow);
                        VehicleModulesPlugin.Instance.StartCoroutine(TransRoutine(v, s));
                        criticalsThisHit++;
                    }
                },
                () => {
                    if (s.IsGunBroken) {
                        if (Random.value < 0.25f) ExplodeBreach(v);
                    } else if (Random.value < (cfg.ChanceGunBroken + intensity)) {
                        s.IsGunBroken = true;
                        Logger.Log($"[CRIT] Заклинило орудие {v.asset.name}");
                        SendChat(v, "[СИСТЕМА] Орудие заклинило!", Color.red);
                        ApplySeatStun(v, 2, 15f); // Seat 3 (Index 2)
                        criticalsThisHit++;
                    }
                }
            };

            // Перемешивание
            for (int i = 0; i < moduleChecks.Count; i++) {
                int randomIndex = Random.Range(i, moduleChecks.Count);
                var temp = moduleChecks[i];
                moduleChecks[i] = moduleChecks[randomIndex];
                moduleChecks[randomIndex] = temp;
            }

            foreach (var check in moduleChecks) {
                if (criticalsThisHit >= maxCriticals) break;
                check.Invoke();
            }

            // Пожар и Дым
            if (!s.IsOnFire && Random.value < (0.03f + (intensity * 0.5f)))
            {
                Logger.Log($"[CRIT] Пожар в {v.asset.name}");
                SendChat(v, "!!! ПОЖАР В БОЕВОМ ОТДЕЛЕНИИ !!!", Color.red);
                VehicleModulesPlugin.Instance.StartCoroutine(FireRoutine(v, s));
            }
            else if (!s.IsSmoking && Random.value < (cfg.ChanceSmoke + intensity))
            {
                Logger.Log($"[CRIT] Задымление в {v.asset.name}");
                SendChat(v, "[ВНИМАНИЕ] Задымление боевого отделения!", Color.gray);
                VehicleModulesPlugin.Instance.StartCoroutine(SmokeRoutine(v, s));
            }
        }

        private static IEnumerator TransRoutine(InteractableVehicle v, VehicleState s)
        {
            yield return new WaitForSeconds(Random.Range(5, 10));
            if (v != null && s.IsTransmissionBroken && !v.isExploded)
            {
                v.batteryCharge = 0;
                VehicleManager.sendVehicleBatteryCharge(v, 0); 
            }
        }
        
        public static void SendChat(InteractableVehicle v, string msg, Color c)
        {
            if (v?.passengers == null) return;
            foreach (var p in v.passengers)
                if (p?.player != null) UnturnedChat.Say(p.player.playerID.steamID, msg, c);
        }

        private static void ApplySeatStun(InteractableVehicle v, int seatIndex, float duration)
        {
            if (v?.passengers != null && v.passengers.Length > seatIndex && v.passengers[seatIndex].player != null)
            {
                var p = v.passengers[seatIndex].player;
                UnturnedChat.Say(p.playerID.steamID, ">> ВАС КОНТУЗИЛО ПРИ ПОВРЕЖДЕНИИ КАЗЕННИКА! <<", Color.yellow);
                VehicleModulesPlugin.Instance.StartCoroutine(SinglePlayerStun(p.player, duration));
            }
        }

        private static IEnumerator SinglePlayerStun(Player p, float duration)
        {
            if (p == null) yield break;
            p.setPluginWidgetFlag(EPluginWidgetFlags.Modal, true);
            yield return new WaitForSeconds(duration);
            if (p != null) p.setPluginWidgetFlag(EPluginWidgetFlags.Modal, false);
        }

        private static IEnumerator SmokeRoutine(InteractableVehicle v, VehicleState s)
        {
            s.IsSmoking = true;
            var cfg = VehicleModulesPlugin.Instance.Configuration.Instance;
            int duration = 15;
            int elapsed = 0;

            while (s.IsSmoking && v != null && !v.isExploded && elapsed < duration)
            {
                if (v.passengers != null)
                {
                    foreach (var p in v.passengers)
                    {
                        if (p?.player != null)
                        {
                            p.player.player.life.askDamage(2, Vector3.up, EDeathCause.BREATH, ELimb.SPINE, CSteamID.Nil, out EPlayerKill kill);
                            EffectManager.sendUIEffect(cfg.SmokeUIEffectID, 12345, p.player.playerID.steamID, true);
                        }
                    }
                }
                yield return new WaitForSeconds(1.0f);
                elapsed++;
            }
            
            s.IsSmoking = false;
            if (v != null && !v.isExploded && v.passengers != null)
            {
                foreach (var p in v.passengers)
                    if (p?.player != null) EffectManager.askEffectClearByID(cfg.SmokeUIEffectID, p.player.playerID.steamID);
                SendChat(v, "[СИСТЕМА] Боевое отделение проветрено.", Color.green);
            }
        }

        public static IEnumerator RepairRoutine(InteractableVehicle v, VehicleState s, ushort stationId, float radius)
        {
            var cfg = VehicleModulesPlugin.Instance.Configuration.Instance;
            s.IsRepairing = true;
            SendChat(v, ">> ИНИЦИИРОВАН ПОЛЕВОЙ РЕМОНТ (35 сек) <<", Color.cyan);
            
            yield return new WaitForSeconds(35.0f);

            if (v == null || v.isExploded || !IsNearRepairStation(v.transform.position, stationId, radius))
            {
                if (s != null) s.IsRepairing = false;
                SendChat(v, "!!! РЕМОНТ ПРЕРВАН !!!", Color.red);
                yield break;
            }

            v.askRepair(v.asset.health);
            VehicleManager.sendVehicleHealth(v, v.health);
            
            if (cfg.RepairFixesTransmission) s.IsTransmissionBroken = false;

            s.IsFuelTankBroken = false;
            s.IsGunBroken = false;
            s.IsOnFire = false;
            s.IsSmoking = false;
            s.IsStunned = false;
            s.IsRepairing = false;
            
            SendChat(v, ">> ТЕХНИКА ВОССТАНОВЛЕНА <<", Color.green);
        }

        public static bool IsNearRepairStation(Vector3 position, ushort targetId, float radius)
        {
            float sqrRadius = radius * radius;
            for (byte x = 0; x < Regions.WORLD_SIZE; x++)
                for (byte y = 0; y < Regions.WORLD_SIZE; y++)
                    if (BarricadeManager.regions[x, y] != null)
                        foreach (BarricadeDrop drop in BarricadeManager.regions[x, y].drops)
                            if (drop.asset.id == targetId && (drop.model.position - position).sqrMagnitude <= sqrRadius)
                                return true;
            return false;
        }

        private static IEnumerator StunRoutine(InteractableVehicle v, VehicleState s, float time)
        {
            s.IsStunned = true;
            if (v?.passengers != null)
            {
                foreach (var p in v.passengers)
                    if (p?.player != null) p.player.player.setPluginWidgetFlag(EPluginWidgetFlags.Modal, true);
            }
            
            yield return new WaitForSeconds(time);
            
            if (v != null && !v.isExploded && v.passengers != null)
            {
                foreach (var p in v.passengers)
                    if (p?.player != null) p.player.player.setPluginWidgetFlag(EPluginWidgetFlags.Modal, false);
            }
            if (s != null) s.IsStunned = false;
        }

        private static IEnumerator FireRoutine(InteractableVehicle v, VehicleState s)
        {
            s.IsOnFire = true;
            while (s.IsOnFire && v != null && !v.isExploded)
            {
                VehicleManager.damage(v, 130, 1, false);
                yield return new WaitForSeconds(0.8f);
            }
        }

        private static IEnumerator FuelRoutine(InteractableVehicle v, VehicleState s)
        {
            while (s.IsFuelTankBroken && v != null && !v.isExploded && v.fuel > 0)
            {
                v.fuel = (ushort)Mathf.Max(0, v.fuel - 35);
                VehicleManager.sendVehicleFuel(v, v.fuel);
                yield return new WaitForSeconds(1.0f);
            }
        }

        private static void ExplodeBreach(InteractableVehicle v)
        {
            if (v == null || v.isExploded) return;
            EffectManager.sendEffect(45, 128, v.transform.position + Vector3.up * 2f);
            VehicleManager.damage(v, 1000, 1, false);
        }
    }
}
