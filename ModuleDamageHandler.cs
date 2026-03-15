using System.Collections;
using SDG.Unturned;
using UnityEngine;
using Rocket.Unturned.Chat;
using Rocket.Unturned.Player;
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

            // --- МОДУЛИ ---
            if (!s.IsFuelTankBroken && roll < (cfg.ChanceFuelLeak + intensity))
            {
                s.IsFuelTankBroken = true;
                Send(v, "КРИТ: Топливный бак пробит!", Color.red);
                VehicleModulesPlugin.Instance.StartCoroutine(FuelRoutine(v, s));
            }

            if (!s.IsTransmissionBroken && roll < (cfg.ChanceTransmission + intensity))
            {
                s.IsTransmissionBroken = true;
                Send(v, "КРИТ: Трансмиссия повреждена!", Color.yellow);
                VehicleModulesPlugin.Instance.StartCoroutine(TransRoutine(v, s));
            }

            if (s.IsGunBroken)
            {
                if (Random.value < 0.50f) ExplodeBreach(v);
            }
            else if (roll < (cfg.ChanceGunBroken + intensity))
            {
                s.IsGunBroken = true;
                Send(v, "КРИТ: Орудие заклинило!", Color.red);
            }

            // --- ИВЕНТЫ ---
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

            foreach (var passenger in v.passengers)
            {
                if (passenger.player != null)
                {
                    // ВКЛЮЧАЕМ МЫШКУ И БЛОКИРУЕМ ИНТЕРФЕЙС
                    passenger.player.player.setPluginWidgetFlag(EPluginWidgetFlags.Modal, true);
                }
            }

            float timer = 5.0f;
            while (timer > 0 && v != null)
            {
                // Вместо запрещенного set_isEngineOn используем физическую заморозку
                var rb = v.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.velocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }
                timer -= 0.1f;
                yield return new WaitForSeconds(0.1f);
            }

            foreach (var passenger in v.passengers)
            {
                if (passenger.player != null)
                    passenger.player.player.setPluginWidgetFlag(EPluginWidgetFlags.Modal, false);
            }

            s.IsStunned = false;
        }

        private static IEnumerator SmokeRoutine(InteractableVehicle v, VehicleState s)
        {
            s.IsSmoking = true;
            while (s.IsSmoking && v != null && !v.isExploded)
            {
                EffectManager.sendEffect(134, 128, v.transform.position);
                foreach (var p in v.passengers)
                    if (p.player != null) p.player.player.life.askSuffocate(15);
                yield return new WaitForSeconds(1.0f);
            }
        }

        private static IEnumerator FireRoutine(InteractableVehicle v, VehicleState s)
        {
            s.IsOnFire = true;
            float t = 7.0f;
            while (t > 0 && v != null && !v.isExploded)
            {
                EffectManager.sendEffect(125, 128, v.transform.position);
                VehicleManager.damage(v, 150, 1, false);
                t -= 1.0f;
                yield return new WaitForSeconds(1.0f);
            }
            s.IsOnFire = false;
        }

        private static void ExplodeBreach(InteractableVehicle v)
        {
            Send(v, "КАТАСТРОФА: Разрыв казенника!", Color.red);
            EffectManager.sendEffect(45, 128, v.transform.position);
            VehicleManager.damage(v, 800, 1, false);
            
            foreach (var p in v.passengers)
            {
                if (p.player != null)
                {
                    // ИСПРАВЛЕНО: конвертация в CSteamID для askDamage
                    EPlayerKill kill;
                    p.player.player.life.askDamage(90, Vector3.up, EDeathCause.CHARGE, ELimb.SPINE, CSteamID.Nil, out kill);
                }
            }
        }

        private static IEnumerator FuelRoutine(InteractableVehicle v, VehicleState s)
        {
            while (s.IsFuelTankBroken && v != null && !v.isExploded && v.fuel > 0)
            {
                v.fuel = (ushort)Mathf.Max(0, v.fuel - 25);
                yield return new WaitForSeconds(1.0f);
            }
        }

        private static IEnumerator TransRoutine(InteractableVehicle v, VehicleState s)
        {
            yield return new WaitForSeconds(20);
            if (v != null) v.batteryCharge = 0;
        }

        private static void Send(InteractableVehicle v, string msg, Color c)
        {
            foreach (var p in v.passengers)
                if (p.player != null) UnturnedChat.Say(p.player.playerID.steamID, msg, c);
        }
    }
}
