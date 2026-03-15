using System.Collections.Generic;
using System.Collections;
using SDG.Unturned;
using UnityEngine;
using Rocket.Unturned.Chat;
using Steamworks;

namespace VehicleModulesSystem
{
    public static class ModuleDamageHandler
    {
        public static void ProcessDamage(InteractableVehicle v, VehicleState s, int dmg)
        {
            var cfg = VehicleModulesPlugin.Instance.Configuration.Instance;
            
            if (s.IsOnFire) return;

            float intensity = Mathf.Clamp(dmg / 1500f, 0f, 0.25f); 

            if (!s.IsStunned && Random.value < (0.70f + intensity))
            {
                VehicleModulesPlugin.Instance.StartCoroutine(StunRoutine(v, s));
            }

            int criticalsThisHit = 0;
            int maxCriticals = dmg > 600 ? 2 : 1; 

            List<System.Action> moduleChecks = new List<System.Action>
            {
                () => {
                    if (!s.IsFuelTankBroken && Random.value < (cfg.ChanceFuelLeak + intensity)) {
                        s.IsFuelTankBroken = true;
                        SendChat(v, "!!! КРИТ: Пробит топливный бак !!!", Color.red);
                        VehicleModulesPlugin.Instance.StartCoroutine(FuelRoutine(v, s));
                        criticalsThisHit++;
                    }
                },
                () => {
                    if (!s.IsTransmissionBroken && Random.value < (cfg.ChanceTransmission + intensity)) {
                        SendChat(v, "[СИСТЕМА] Трансмиссия повреждена!", Color.yellow);
                        VehicleModulesPlugin.Instance.StartCoroutine(TransRoutine(v, s));
                        criticalsThisHit++;
                    }
                },
                () => {
                    if (s.IsGunBroken) {
                        if (Random.value < 0.25f) { 
                            ExplodeBreach(v);
                            criticalsThisHit++;
                        }
                    } else if (Random.value < (cfg.ChanceGunBroken + intensity)) {
                        s.IsGunBroken = true;
                        SendChat(v, "[СИСТЕМА] Орудие заклинило!", Color.red);
                        criticalsThisHit++;
                    }
                }
            };

            for (int i = 0; i < moduleChecks.Count; i++) {
                System.Action temp = moduleChecks[i];
                int randomIndex = Random.Range(i, moduleChecks.Count);
                moduleChecks[i] = moduleChecks[randomIndex];
                moduleChecks[randomIndex] = temp;
            }

            foreach (var check in moduleChecks) {
                if (criticalsThisHit >= maxCriticals) break;
                check.Invoke();
            }

            if (!s.IsOnFire && Random.value < (0.03f + (intensity * 0.5f)))
            {
                SendChat(v, "!!! ПОЖАР В БОЕВОМ ОТДЕЛЕНИИ !!!", Color.red);
                VehicleModulesPlugin.Instance.StartCoroutine(FireRoutine(v, s));
            }
            else if (!s.IsSmoking && Random.value < (cfg.ChanceSmoke + intensity))
            {
                SendChat(v, "[ВНИМАНИЕ] Задымление боевого отделения!", Color.gray);
                VehicleModulesPlugin.Instance.StartCoroutine(SmokeRoutine(v, s));
            }
        }

        // --- ОБНОВЛЕННАЯ МЕХАНИКА ДЫМА (15 секунд, урон по 2 ХП в секунду) ---
        private static IEnumerator SmokeRoutine(InteractableVehicle v, VehicleState s)
        {
            s.IsSmoking = true;
            int duration = 15; // Длительность задымления в секундах
            int elapsed = 0;

            while (s.IsSmoking && v != null && !v.isExploded && elapsed < duration)
            {
                // Оставляем визуальный хлопок дыма внутри, чтобы было видно угрозу
                EffectManager.sendEffect(110, 128, v.transform.position + Vector3.up * 1.5f);
                
                foreach (var p in v.passengers)
                {
                    if (p.player != null)
                    {
                        // Снимаем по 2 ХП каждую секунду
                        p.player.player.life.askDamage(2, Vector3.up, EDeathCause.BREATH, ELimb.SPINE, CSteamID.Nil, out EPlayerKill kill);
                    }
                }
                yield return new WaitForSeconds(1.0f);
                elapsed++;
            }
            
            // Завершение эффекта
            if (s != null)
            {
                s.IsSmoking = false;
                if (v != null && !v.isExploded) SendChat(v, "[СИСТЕМА] Боевое отделение проветрено.", Color.green);
            }
        }

        // --- НОВАЯ МЕХАНИКА: ПОЛЕВОЙ РЕМОНТ ---
        public static IEnumerator RepairRoutine(InteractableVehicle v, VehicleState s, ushort stationId, float radius)
        {
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

                // Проверка: находится ли танк всё ещё рядом со станцией починки
                if (!IsNearRepairStation(v.transform.position, stationId, radius))
                {
                    SendChat(v, "!!! РЕМОНТ ПРЕРВАН: Техника покинула зону обслуживания !!!", Color.red);
                    s.IsRepairing = false;
                    yield break;
                }

                // Уведомления экипажу каждые 10 секунд
                if (i > 0 && i % 10 == 0) 
                {
                    SendChat(v, $"... Восстановление систем: осталось {repairTime - i} сек ...", Color.gray);
                }

                yield return new WaitForSeconds(1.0f);
            }

            // Успешный финал: чиним ХП и сбрасываем все статусы поломок
            v.askRepair(v.asset.health); // Восстанавливаем ХП машины на 100%
            VehicleManager.sendVehicleHealth(v, v.health); // Синхронизируем для всех
            
            s.IsFuelTankBroken = false;
            s.IsTransmissionBroken = false;
            s.IsGunBroken = false;
            s.IsOnFire = false;
            s.IsSmoking = false;
            s.IsStunned = false;
            s.IsRepairing = false;
            
            SendChat(v, ">> ТЕХНИКА ПОЛНОСТЬЮ ВОССТАНОВЛЕНА. ГОТОВНОСТЬ К БОЮ 100% <<", Color.green);
        }

        // Вспомогательный метод для поиска баррикады-станции (Абсолютно безопасен и оптимизирован)
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
                                return true; // Нашли нужную станцию в радиусе!
                            }
                        }
                    }
                }
            }
            return false;
        }

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

                    EffectManager.sendEffect(139, 128, v.transform.position + randomOffset);
                }

                VehicleManager.damage(v, 130, 1, false);
                yield return new WaitForSeconds(0.8f);
            }
        }

        private static void ExplodeBreach(InteractableVehicle v)
        {
            EffectManager.sendEffect(45, 128, v.transform.position + Vector3.up * 2f);
            SendChat(v, "!!! РАЗРЫВ КАЗЕННИКА !!!", Color.red);
            
            VehicleManager.damage(v, 1000, 1, false);
            foreach (var p in v.passengers)
            {
                if (p.player != null)
                {
                    p.player.player.life.askDamage(80, Vector3.up, EDeathCause.CHARGE, ELimb.SPINE, CSteamID.Nil, out EPlayerKill k);
                }
            }
        }

        private static IEnumerator StunRoutine(InteractableVehicle v, VehicleState s)
        {
            s.IsStunned = true;
            SendChat(v, ">> ЭКИПАЖ КОНТУЖЕН <<", Color.yellow);
            foreach (var p in v.passengers)
                if (p.player != null) p.player.player.setPluginWidgetFlag(EPluginWidgetFlags.Modal, true);
            
            yield return new WaitForSeconds(5.0f);
            
            if (v != null)
                foreach (var p in v.passengers)
                    if (p.player != null) p.player.player.setPluginWidgetFlag(EPluginWidgetFlags.Modal, false);
            s.IsStunned = false;
        }

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

        private static IEnumerator TransRoutine(InteractableVehicle v, VehicleState s)
        {
            yield return new WaitForSeconds(Random.Range(10, 20));
            if (v != null)
            {
                s.IsTransmissionBroken = true;
                v.batteryCharge = 0;
                VehicleManager.sendVehicleFuel(v, v.fuel); 
                SendChat(v, "!!! КРИТ: Трансмиссия рассыпалась !!!", Color.red);
            }
        }

        public static void SendChat(InteractableVehicle v, string msg, Color c)
        {
            foreach (var p in v.passengers)
                if (p.player != null) UnturnedChat.Say(p.player.playerID.steamID, msg, c);
        }
    }
}
