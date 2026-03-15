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
            float intensity = dmg / 500f;

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

            // 4. ПОЖАР (ID 139 - Искры/Обломки)
            if (!s.IsOnFire && roll < (cfg.ChanceFire + intensity))
            {
                SendChat(v, "!!! ПОЖАР В МЕХАНИЗМАХ !!!", Color.red);
                VehicleModulesPlugin.Instance.StartCoroutine(FireRoutine(v, s));
            }

            // 5. ЗАДЫМЛЕНИЕ (ID 110 - Дым. граната)
            if (!s.IsSmoking && roll < (cfg.ChanceSmoke + intensity))
            {
                SendChat(v, "[ВНИМАНИЕ] Задымление боевого отделения!", Color.gray);
                VehicleModulesPlugin.Instance.StartCoroutine(SmokeRoutine(v, s));
            }

            // 6. КОНТУЗИЯ (STUN)
            if (!s.IsStunned && roll < (cfg.ChanceStun + intensity))
            {
                VehicleModulesPlugin.Instance.StartCoroutine(StunRoutine(v, s));
            }
        }

        // --- ЛОГИКА МОДУЛЕЙ С ЛОГГИРОВАНИЕМ ---

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
            SendChat(v, "[СИСТЕМА] Экипаж пришел в сознание.", Color.green);
        }

        private static IEnumerator SmokeRoutine(InteractableVehicle v, VehicleState s)
        {
            s.IsSmoking = true;
            while (s.IsSmoking && v != null && !v.isExploded)
            {
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
                EffectManager.sendEffect(139, 128, v.transform.position + Vector3.up);
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
                VehicleManager.sendVehicleBattery(v, 0);
                SendChat(v, "!!! КРИТ: Трансмиссия рассыпалась. Питание потеряно !!!", Color.red);
                Rocket.Core.Logging.Logger.Log($"[CRIT] {v.asset.vehicleName}: Аккумулятор выведен из строя поломкой трансмиссии.");
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
