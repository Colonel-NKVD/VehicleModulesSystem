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

            // МОДУЛИ
            if (!s.IsFuelTankBroken && roll < (cfg.ChanceFuelLeak + intensity))
            {
                s.IsFuelTankBroken = true;
                Send(v, "КРИТ: Бак пробит! Горючее вытекает.", Color.red);
                VehicleModulesPlugin.Instance.StartCoroutine(FuelRoutine(v, s));
            }

            if (!s.IsTransmissionBroken && roll < (cfg.ChanceTransmission + intensity))
            {
                // Запускаем корутину отказа (аккумулятор умрет через время)
                VehicleModulesPlugin.Instance.StartCoroutine(TransRoutine(v, s));
            }

            if (s.IsGunBroken) { if (Random.value < 0.50f) ExplodeBreach(v); }
            else if (roll < (cfg.ChanceGunBroken + intensity)) { s.IsGunBroken = true; Send(v, "КРИТ: Орудие повреждено!", Color.red); }

            // ЭФФЕКТЫ
            if (!s.IsOnFire && roll < (cfg.ChanceFire + intensity))
                VehicleModulesPlugin.Instance.StartCoroutine(FireRoutine(v, s));

            if (!s.IsSmoking && roll < (cfg.ChanceSmoke + intensity))
                VehicleModulesPlugin.Instance.StartCoroutine(SmokeRoutine(v, s));

            if (!s.IsStunned && roll < (cfg.ChanceStun + intensity))
                VehicleModulesPlugin.Instance.StartCoroutine(StunRoutine(v, s));
        }

        private static IEnumerator StunRoutine(InteractableVehicle v, VehicleState s)
        {
            s.IsStunned = true;
            Send(v, "ЭКИПАЖ КОНТУЖЕН!", Color.yellow);

            foreach (var p in v.passengers)
                if (p.player != null) p.player.player.setPluginWidgetFlag(EPluginWidgetFlags.Modal, true);

            yield return new WaitForSeconds(5.0f);

            if (v != null)
            {
                foreach (var p in v.passengers)
                    if (p.player != null) p.player.player.setPluginWidgetFlag(EPluginWidgetFlags.Modal, false);
            }
            s.IsStunned = false;
        }

        private static IEnumerator SmokeRoutine(InteractableVehicle v, VehicleState s)
        {
            s.IsSmoking = true;
            while (s.IsSmoking && v != null && !v.isExploded)
            {
                // Усиленный дым (два потока)
                EffectManager.sendEffect(134, 128, v.transform.position);
                EffectManager.sendEffect(134, 128, v.transform.position + Vector3.up * 1.5f);
                
                foreach (var p in v.passengers)
                    if (p.player != null) p.player.player.life.askSuffocate(15);
                yield return new WaitForSeconds(0.7f); 
            }
        }

        private static IEnumerator FireRoutine(InteractableVehicle v, VehicleState s)
        {
            s.IsOnFire = true;
            float t = 8.0f;
            while (t > 0 && v != null && !v.isExploded)
            {
                // Тройной огонь по всему корпусу
                EffectManager.sendEffect(125, 128, v.transform.position);
                EffectManager.sendEffect(125, 128, v.transform.position + v.transform.forward * 2.5f);
                EffectManager.sendEffect(125, 128, v.transform.position - v.transform.forward * 2.5f);
                
                VehicleManager.damage(v, 160, 1, false);
                t -= 1.0f;
                yield return new WaitForSeconds(1.0f);
            }
            s.IsOnFire = false;
        }

        private static void ExplodeBreach(InteractableVehicle v)
        {
            Send(v, "КАТАСТРОФА: Взрыв в боевом отделении!", Color.red);
            EffectManager.sendEffect(45, 128, v.transform.position);
            EffectManager.sendEffect(139, 128, v.transform.position);
            VehicleManager.damage(v, 800, 1, false);
            foreach (var p in v.passengers)
                if (p.player != null) p.player.player.life.askDamage(95, Vector3.up, EDeathCause.CHARGE, ELimb.SPINE, CSteamID.Nil, out EPlayerKill k);
        }

        private static IEnumerator TransRoutine(InteractableVehicle v, VehicleState s)
        {
            Send(v, "ТРАНСМИССИЯ: Скрежет металла...", Color.yellow);
            yield return new WaitForSeconds(UnityEngine.Random.Range(10, 20));
            if (v != null)
            {
                s.IsTransmissionBroken = true;
                v.batteryCharge = 0;
                EffectManager.sendEffect(61, 128, v.transform.position); // Искры
                Send(v, "КРИТ: Трансмиссия рассыпалась! Питание потеряно.", Color.red);
            }
        }

        private static IEnumerator FuelRoutine(InteractableVehicle v, VehicleState s)
        {
            while (s.IsFuelTankBroken && v != null && !v.isExploded && v.fuel > 0)
            {
                v.fuel = (ushort)Mathf.Max(0, v.fuel - 30);
                yield return new WaitForSeconds(1.0f);
            }
        }

        private static void Send(InteractableVehicle v, string msg, Color c)
        {
            foreach (var p in v.passengers)
                if (p.player != null) UnturnedChat.Say(p.player.playerID.steamID, msg, c);
        }
    }
}
