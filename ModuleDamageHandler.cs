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
            float roll = Random.value;
            // Интенсивность зависит от урона
            float intensity = dmg / 500f;

            // --- БЛОКИРОВКА ЭФФЕКТОВ ПРИ ПОЖАРЕ ---
            // Если танк уже горит, мы блокируем выпадение НОВЫХ критических эффектов
            // от урона, который наносит сам пожар или внешние источники.
            if (s.IsOnFire)
            {
                // Урон технике всё равно наносится (в FireRoutine),
                // но новые проверки на криты не проводятся.
                return; 
            }

            // --- ПРОВЕРКА КРИТИЧЕСКИХ ЭФФЕКТОВ (Только если нет пожара) ---

            // 1. ТОПЛИВНЫЙ БАК (ID 16)
            if (!s.IsFuelTankBroken && roll < (cfg.ChanceFuelLeak + intensity))
            {
                s.IsFuelTankBroken = true;
                SendChat(v, "!!! КРИТИЧЕСКОЕ ПОВРЕЖДЕНИЕ: Пробит топливный бак !!!", Color.red);
                Rocket.Core.Logging.Logger.Log($"[CRIT] {v.asset.vehicleName}: Пробит бак.");
                VehicleModulesPlugin.Instance.StartCoroutine(FuelRoutine(v, s));
            }

            // 2. ТРАНСМИССИЯ
            if (!s.IsTransmissionBroken && roll < (cfg.ChanceTransmission + intensity))
            {
                SendChat(v, "[СИСТЕМА] Повреждение трансмиссии! Электроника под угрозой.", Color.yellow);
                VehicleModulesPlugin.Instance.StartCoroutine(TransRoutine(v, s));
            }

            // 3. ОРУДИЕ (БЛОК РАЗРЫВА)
            if (s.IsGunBroken)
            {
                if (Random.value < 0.50f)
                {
                    SendChat(v, "!!! РАЗРЫВ КАЗЕННИКА !!!", Color.red);
                    ExplodeBreach(v);
                }
            }
            else if (roll < (cfg.ChanceGunBroken + intensity))
            {
                s.IsGunBroken = true;
                SendChat(v, "[СИСТЕМА] Орудие заклинило. Огонь невозможен.", Color.red);
                Rocket.Core.Logging.Logger.Log($"[CRIT] {v.asset.vehicleName}: Орудие выведено из строя.");
            }

            // 4. ПОЖАР (ID 139 - Искры/Обломки, используем как огонь на корпусе)
            if (!s.IsOnFire && roll < (cfg.ChanceFire + intensity))
            {
                SendChat(v, "!!! ПОЖАР В МЕХАНИЗМАХ !!!", Color.red);
                VehicleModulesPlugin.Instance.StartCoroutine(FireRoutine(v, s));
            }

            // 5. ЗАДЫМЛЕНИЕ (ID 110)
            if (!s.IsSmoking && roll < (cfg.ChanceSmoke + intensity))
            {
                SendChat(v, "[ВНИМАНИЕ] Задымление боевого отделения!", Color.gray);
                VehicleModulesPlugin.Instance.StartCoroutine(SmokeRoutine(v, s));
            }

            // 6. КОНТУЗИЯ
            if (!s.IsStunned && roll < (cfg.ChanceStun + intensity))
            {
                VehicleModulesPlugin.Instance.StartCoroutine(StunRoutine(v, s));
            }
        }

        private static IEnumerator StunRoutine(InteractableVehicle v, VehicleState s)
        {
            s.IsStunned = true;
            SendChat(v, ">> ЭКИПАЖ КОНТУЖЕН: Потеря управления на 5с <<", Color.yellow);
            foreach (var p in v.passengers)
                if (p.player != null) p.player.player.setPluginWidgetFlag(EPluginWidgetFlags.Modal, true);

            yield return new WaitForSeconds(5.0f);

            if (v != null)
                foreach (var p in v.passengers)
                    if (p.player != null) p.player.player.setPluginWidgetFlag(EPluginWidgetFlags.Modal, false);
            s.IsStunned = false;
        }

        private static IEnumerator SmokeRoutine(InteractableVehicle v, VehicleState s)
        {
            s.IsSmoking = true;
            while (s.IsSmoking && v != null && !v.isExploded)
            {
                // ID, Radius, Position
                EffectManager.sendEffect(110, 128, v.transform.position);
                foreach (var p in v.passengers)
                    if (p.player != null) p.player.player.life.askSuffocate(15);
                yield return new WaitForSeconds(1.5f); 
            }
        }

        private static IEnumerator FireRoutine(InteractableVehicle v, VehicleState s)
        {
            s.IsOnFire = true;
            while (s.IsOnFire && v != null && !v.isExploded)
            {
                // ПРОФЕССИОНАЛЬНОЕ РАЗМЕЩЕНИЕ ОГНЯ НА КОРПУСЕ
                // Спавним 3 эффекта в разных точках корпуса, приподнятых вверх.
                // Точка 1: Центр корпуса, чуть выше
                EffectManager.sendEffect(139, 128, v.transform.position + Vector3.up * 1.5f);
                // Точка 2: Спереди корпуса (используем transform.forward)
                EffectManager.sendEffect(139, 128, v.transform.position + v.transform.forward * 2.0f + Vector3.up * 1.2f);
                // Точка 3: Сзади корпуса
                EffectManager.sendEffect(139, 128, v.transform.position - v.transform.forward * 2.0f + Vector3.up * 1.2f);

                // Урон технике от огня
                VehicleManager.damage(v, 120, 1, false);
                yield return new WaitForSeconds(0.8f);
            }
        }

        private static IEnumerator FuelRoutine(InteractableVehicle v, VehicleState s)
        {
            while (s.IsFuelTankBroken && v != null && !v.isExploded && v.fuel > 0)
            {
                EffectManager.sendEffect(16, 128, v.transform.position);
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
                // Синхронизация через FuelManager
                VehicleManager.sendVehicleFuel(v, v.fuel); 
                SendChat(v, "!!! КРИТ: Трансмиссия рассыпалась. Питание потеряно !!!", Color.red);
            }
        }

        private static void ExplodeBreach(InteractableVehicle v)
        {
            EffectManager.sendEffect(45, 128, v.transform.position);
            VehicleManager.damage(v, 800, 1, false);
            foreach (var p in v.passengers)
                if (p.player != null)
                    p.player.player.life.askDamage(95, Vector3.up, EDeathCause.CHARGE, ELimb.SPINE, CSteamID.Nil, out EPlayerKill k);
        }

        public static void SendChat(InteractableVehicle v, string msg, Color c)
        {
            foreach (var p in v.passengers)
                if (p.player != null) UnturnedChat.Say(p.player.playerID.steamID, msg, c);
        }
    }
}
