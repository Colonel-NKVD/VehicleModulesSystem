using System.Collections;
using SDG.Unturned;
using UnityEngine;
using Rocket.Unturned.Chat;
using Rocket.Unturned.Player;

namespace VehicleModulesSystem
{
    public static class ModuleDamageHandler
    {
        public static void ProcessDamage(InteractableVehicle v, VehicleState s, int dmg)
        {
            var cfg = VehicleModulesPlugin.Instance.Configuration.Instance;
            float roll = Random.value;
            float intensity = dmg / 500f;

            // --- КРИТИЧЕСКИЕ ПОВРЕЖДЕНИЯ МОДУЛЕЙ ---

            // 1. Топливо (Расход увеличен для реализма)
            if (!s.IsFuelTankBroken && roll < (cfg.ChanceFuelLeak + intensity))
            {
                s.IsFuelTankBroken = true;
                Send(v, "ВНИМАНИЕ: Пробит топливный бак! Утечка горючего.", Color.red);
                VehicleModulesPlugin.Instance.StartCoroutine(FuelRoutine(v, s));
            }

            // 2. Трансмиссия (Полная остановка через время)
            if (!s.IsTransmissionBroken && roll < (cfg.ChanceTransmission + intensity))
            {
                s.IsTransmissionBroken = true;
                Send(v, "КРИТ: Поломка трансмиссии. Скоро электроника выйдет из строя.", Color.yellow);
                VehicleModulesPlugin.Instance.StartCoroutine(TransRoutine(v, s));
            }

            // 3. Орудие (Разрыв казенника — фатально для башни)
            if (s.IsGunBroken)
            {
                if (Random.value < 0.50f) ExplodeBreach(v);
            }
            else if (roll < (cfg.ChanceGunBroken + intensity))
            {
                s.IsGunBroken = true;
                Send(v, "КРИТ: Орудие заклинило!", Color.red);
            }

            // --- ИВЕНТЫ (ВИЗУАЛ И ГЕЙМПЛЕЙ) ---

            // ПОЖАР (Эффект ID 125 — Чистое пламя)
            if (!s.IsOnFire && roll < (cfg.ChanceFire + intensity))
                VehicleModulesPlugin.Instance.StartCoroutine(FireRoutine(v, s));

            // ЗАДЫМЛЕНИЕ (Эффект ID 134 — Густая копоть)
            if (!s.IsSmoking && roll < (cfg.ChanceSmoke + intensity))
                VehicleModulesPlugin.Instance.StartCoroutine(SmokeRoutine(v, s));

            // ОГЛУШЕНИЕ (Блокировка управления + Курсор)
            if (!s.IsStunned && roll < (cfg.ChanceStun + intensity))
                VehicleModulesPlugin.Instance.StartCoroutine(StunRoutine(v, s));
        }

        // --- ПРОФЕССИОНАЛЬНАЯ РЕАЛИЗАЦИЯ ЭФФЕКТОВ ---

        private static IEnumerator StunRoutine(InteractableVehicle v, VehicleState s)
        {
            s.IsStunned = true;
            Send(v, "ЭКИПАЖ КОНТУЖЕН! Управление потеряно.", Color.yellow);

            // Блокируем ввод для каждого пассажира
            foreach (var passenger in v.passengers)
            {
                if (passenger.player != null)
                {
                    // Включаем курсор и блокируем управление игрой (WASD, Мышь, Выстрел)
                    passenger.player.player.setPluginWidgetFlag(EPluginWidgetFlags.Modal, true);
                }
            }

            // Танк мгновенно глохнет и останавливается
            v.isEngineOn = false;
            var rb = v.GetComponent<Rigidbody>();
            if (rb != null) { rb.velocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }

            yield return new WaitForSeconds(5.0f);

            // Снимаем блокировку
            foreach (var passenger in v.passengers)
            {
                if (passenger.player != null)
                    passenger.player.player.setPluginWidgetFlag(EPluginWidgetFlags.Modal, false);
            }

            s.IsStunned = false;
            Send(v, "Экипаж восстановил контроль.", Color.green);
        }

        private static IEnumerator SmokeRoutine(InteractableVehicle v, VehicleState s)
        {
            s.IsSmoking = true;
            Send(v, "В ОТСЕКЕ ДЫМ! Экипаж задыхается.", Color.gray);

            while (s.IsSmoking && v != null && !v.isExploded)
            {
                // Посылаем густой черный дым (ID 134)
                EffectManager.sendEffect(134, 128, v.transform.position);

                foreach (var passenger in v.passengers)
                {
                    if (passenger.player != null)
                        // 15% урона по кислороду каждую секунду
                        passenger.player.player.life.askSuffocate(15);
                }
                yield return new WaitForSeconds(1.0f);
            }
        }

        private static IEnumerator FireRoutine(InteractableVehicle v, VehicleState s)
        {
            s.IsOnFire = true;
            float lifeTime = Random.Range(6, 8);
            
            while (lifeTime > 0 && v != null && !v.isExploded)
            {
                // Посылаем эффект огня (ID 125)
                EffectManager.sendEffect(125, 128, v.transform.position);
                
                // Наносим урон самой технике (быстрая смерть танка)
                VehicleManager.damage(v, 150, 1, false);
                lifeTime -= 1.0f;
                yield return new WaitForSeconds(1.0f);
            }
            s.IsOnFire = false;
        }

        private static void ExplodeBreach(InteractableVehicle v)
        {
            Send(v, "КАТАСТРОФА: Разрыв казенника внутри башни!", Color.red);
            // Эффект крупного взрыва (ID 45)
            EffectManager.sendEffect(45, 128, v.transform.position);
            
            // Наносим огромный урон и убиваем/раним экипаж
            VehicleManager.damage(v, 800, 1, false);
            foreach (var p in v.passengers)
            {
                if (p.player != null)
                    p.player.player.life.askDamage(90, Vector3.up, EDeathCause.CHARGE, ELimb.SPINE, v.instanceID, out EPlayerKill kill);
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
            yield return new WaitForSeconds(Random.Range(15, 30));
            if (v != null) { v.batteryCharge = 0; Send(v, "Электрика танка сгорела.", Color.red); }
        }

        private static void Send(InteractableVehicle v, string msg, Color c)
        {
            foreach (var p in v.passengers)
                if (p.player != null) UnturnedChat.Say(p.player.playerID.steamID, msg, c);
        }
    }
}
