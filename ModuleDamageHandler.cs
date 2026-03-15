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
            
            // Если танк горит, экипажу не до новых поломок
            if (s.IsOnFire) return;

            // 1. БАЛАНС УРОНА: Ограничиваем влияние "гигантского" урона от взрывов.
            // Максимальный бонус к шансу поломки от одного удара составит +25% (0.25f).
            float intensity = Mathf.Clamp(dmg / 1500f, 0f, 0.25f); 

            // 2. КОНТУЗИЯ (70% базовый шанс + интенсивность удара)
            if (!s.IsStunned && Random.value < (0.70f + intensity))
            {
                VehicleModulesPlugin.Instance.StartCoroutine(StunRoutine(v, s));
            }

            // 3. ЛИМИТ ПОЛОМОК: Танк не может развалиться весь от одного снаряда.
            int criticalsThisHit = 0;
            int maxCriticals = dmg > 600 ? 2 : 1; // Мощный взрыв ломает до 2 модулей, обычный - 1

            // 4. СИСТЕМА СПРАВЕДЛИВОГО РАНДОМА (Shuffle)
            // Добавляем проверки в список, чтобы выбрать случайный модуль, который примет удар
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
                        if (Random.value < 0.25f) { // Взрыв казенника при повторном крите в орудие
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

            // Перемешиваем список, чтобы ни у одного модуля не было "приоритета"
            for (int i = 0; i < moduleChecks.Count; i++) {
                System.Action temp = moduleChecks[i];
                int randomIndex = Random.Range(i, moduleChecks.Count);
                moduleChecks[i] = moduleChecks[randomIndex];
                moduleChecks[randomIndex] = temp;
            }

            // Выполняем проверки до исчерпания лимита поломок
            foreach (var check in moduleChecks) {
                if (criticalsThisHit >= maxCriticals) break;
                check.Invoke();
            }

            // 5. РЕДКИЕ ЯВЛЕНИЯ: Пожар и Дым
            // Пожар имеет минимальный шанс прока (3% база + небольшая часть от урона)
            if (!s.IsOnFire && Random.value < (0.03f + (intensity * 0.5f)))
            {
                SendChat(v, "!!! ПОЖАР В БОЕВОМ ОТДЕЛЕНИИ !!!", Color.red);
                VehicleModulesPlugin.Instance.StartCoroutine(FireRoutine(v, s));
            }
            // Задымление срабатывает чаще пожара, но реже обычных поломок
            else if (!s.IsSmoking && Random.value < (cfg.ChanceSmoke + intensity))
            {
                SendChat(v, "[ВНИМАНИЕ] Задымление боевого отделения!", Color.gray);
                VehicleModulesPlugin.Instance.StartCoroutine(SmokeRoutine(v, s));
            }
        }

        private static IEnumerator SmokeRoutine(InteractableVehicle v, VehicleState s)
        {
            s.IsSmoking = true;
            while (s.IsSmoking && v != null && !v.isExploded)
            {
                EffectManager.sendEffect(110, 128, v.transform.position + Vector3.up * 1.5f);
                
                foreach (var p in v.passengers)
                {
                    if (p.player != null)
                    {
                        p.player.player.life.askDamage(10, Vector3.up, EDeathCause.BREATH, ELimb.SPINE, CSteamID.Nil, out EPlayerKill kill);
                    }
                }
                yield return new WaitForSeconds(2.0f); 
            }
        }

        private static IEnumerator FireRoutine(InteractableVehicle v, VehicleState s)
        {
            s.IsOnFire = true;
            while (s.IsOnFire && v != null && !v.isExploded)
            {
                // ПРОФЕССИОНАЛЬНОЕ РАСПРОСТРАНЕНИЕ ОГНЯ
                // Каждый тик (0.8с) мы спавним 3 эффекта искр/огня в СЛУЧАЙНЫХ координатах корпуса.
                // Танк будет казаться полностью охваченным пламенем со всех сторон.
                for (int i = 0; i < 3; i++)
                {
                    Vector3 randomOffset = 
                        v.transform.right * Random.Range(-1.5f, 1.5f) +     // Ширина: от левого до правого борта
                        v.transform.forward * Random.Range(-3.5f, 3.5f) +   // Длина: от кормы до носа
                        Vector3.up * Random.Range(1.8f, 3.0f);              // Высота: по всей верхней проекции

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
