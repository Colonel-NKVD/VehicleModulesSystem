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

            // 1. Механика Рикошета
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

            // Контузия всего экипажа (общий шанс от попадания)
            if (!s.IsStunned && Random.value < (0.10f + intensity))
            {
                Logger.Log($"[CRIT] Контузия экипажа {v.asset.name}");
                VehicleModulesPlugin.Instance.StartCoroutine(StunRoutine(v, s, 5f));
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
                        
                        // Спец-контузия для 3-го места (наводчик/стрелок) на 15 секунд
                        ApplySeatStun(v, 2, 15f); 
                        criticalsThisHit++;
                    }
                }
            };

            // Рандомизация проверок
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

            // Проверка на пожар и задымление
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

        // ВЕРНУЛ ЭФФЕКТ ДЫМА + ОСТАВИЛ UI И УРОН
        private static IEnumerator SmokeRoutine(InteractableVehicle v, VehicleState s)
        {
            s.IsSmoking = true;
            var cfg = VehicleModulesPlugin.Instance.Configuration.Instance;
            int duration = 15;
            int elapsed = 0;

            while (s.IsSmoking && v != null && !v.isExploded && elapsed < duration)
            {
                // Визуальный эффект дыма снаружи
                EffectManager.sendEffect(36010, 128, v.transform.position + Vector3.up * 1.5f);

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

        // ОБНОВЛЕННЫЙ ПОЛЕВОЙ РЕМОНТ (35 секунд)
        public static IEnumerator RepairRoutine(InteractableVehicle v, VehicleState s, ushort stationId, float radius)
        {
            var cfg = VehicleModulesPlugin.Instance.Configuration.Instance;
            s.IsRepairing = true;
            SendChat(v, ">> ИНИЦИИРОВАН ПОЛЕВОЙ РЕМОНТ. НЕ ПОКИДАЙТЕ ЗОНУ 35 СЕКУНД <<", Color.cyan);
            
            int repairTime = 35;
            
            for (int i = 0; i < repairTime; i++)
            {
                if (v == null || v.isExploded) 
                {
                    if (s != null) s.IsRepairing = false;
                    yield break;
                }

                if (!IsNearRepairStation(v.transform.position, stationId, radius))
                {
                    SendChat(v, "!!! РЕМОНТ ПРЕРВАН: Техника покинула зону обслуживания !!!", Color.red);
                    s.IsRepairing = false;
                    yield break;
                }

                if (i > 0 && i % 10 == 0) 
                {
                    SendChat(v, $"... Восстановление систем: осталось {repairTime - i} сек ...", Color.gray);
                }

                yield return new WaitForSeconds(1.0f);
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
            
            SendChat(v, ">> ТЕХНИКА ПОЛНОСТЬЮ ВОССТАНОВЛЕНА. ГОТОВНОСТЬ К БОЮ 100% <<", Color.green);
        }

        public static bool IsNearRepairStation(Vector3 position, ushort targetId, float radius)
        {
            float sqrRadius = radius * radius;
            for (byte x = 0; x < Regions.WORLD_SIZE; x++)
            {
                for (byte y = 0; y < Regions.WORLD_SIZE; y++)
                {
                    if (BarricadeManager.regions[x, y] != null)
                    {
                        foreach (BarricadeDrop drop in BarricadeManager.regions[x, y].drops)
                        {
                            if (drop.asset.id == targetId && (drop.model.position - position).sqrMagnitude <= sqrRadius)
                            {
                                return true;
                            }
                        }
                    }
                }
            }
            return false;
        }

        private static IEnumerator StunRoutine(InteractableVehicle v, VehicleState s, float time)
        {
            s.IsStunned = true;
            SendChat(v, ">> ЭКИПАЖ КОНТУЖЕН <<", Color.yellow);
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

        // ВЕРНУЛ ЭФФЕКТЫ ОГНЯ С РАНДОМНЫМ СМЕЩЕНИЕМ
        private static IEnumerator FireRoutine(InteractableVehicle v, VehicleState s)
        {
            s.IsOnFire = true;
            while (s.IsOnFire && v != null && !v.isExploded)
            {
                for (int i = 0; i < 3; i++)
                {
                    Vector3 randomOffset = 
                        v.transform.right * Random.Range(-1.5f, 1.5f) +     
                        v.transform.forward * Random.Range(-3.5f, 3.5f) +   
                        Vector3.up * Random.Range(1.8f, 3.0f);              

                    EffectManager.sendEffect(59062, 128, v.transform.position + randomOffset);
                }

                VehicleManager.damage(v, 130, 1, false);
                yield return new WaitForSeconds(0.8f);
            }
        }

        // ВЕРНУЛ ЭФФЕКТ УТЕЧКИ ТОПЛИВА
        private static IEnumerator FuelRoutine(InteractableVehicle v, VehicleState s)
        {
            while (s.IsFuelTankBroken && v != null && !v.isExploded && v.fuel > 0)
            {
                EffectManager.sendEffect(16, 128, v.transform.position + Vector3.up);
                v.fuel = (ushort)Mathf.Max(0, v.fuel - 35);
                VehicleManager.sendVehicleFuel(v, v.fuel);
                yield return new WaitForSeconds(1.0f);
            }
        }

        // ВЕРНУЛ ЭФФЕКТ ВЗРЫВА И УРОН ЭКИПАЖУ
        private static void ExplodeBreach(InteractableVehicle v)
        {
            if (v == null || v.isExploded) return;
            
            EffectManager.sendEffect(21619, 128, v.transform.position + Vector3.up * 2f);
            SendChat(v, "!!! ВЗРЫВ В БОЕВОМ ОТДЕЛЕНИИ !!!", Color.red);
            
            VehicleManager.damage(v, 5000, 1, false);
            
            if (v.passengers != null)
            {
                foreach (var p in v.passengers)
                {
                    if (p?.player != null)
                    {
                        p.player.player.life.askDamage(80, Vector3.up, EDeathCause.CHARGE, ELimb.SPINE, CSteamID.Nil, out EPlayerKill k);
                    }
                }
            }
        }
    }
}
