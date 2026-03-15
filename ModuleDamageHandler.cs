using System.Collections;
using SDG.Unturned;
using UnityEngine;
using Rocket.Unturned.Chat;
using Rocket.Unturned.Player;

namespace VehicleModulesSystem
{
    public static class ModuleDamageHandler
    {
        public static void ProcessDamage(InteractableVehicle vehicle, VehicleState state, int damage)
        {
            var cfg = VehicleModulesPlugin.Instance.Configuration.Instance;
            float roll = Random.value;
            float intensity = damage / 500f; // Модификатор от силы удара

            // --- ЛОГИКА МОДУЛЕЙ ---

            // 1. Топливные баки (Утечка)
            if (!state.IsFuelTankBroken && roll < (cfg.ChanceFuelLeak + intensity))
            {
                state.IsFuelTankBroken = true;
                SendMessage(vehicle, "КРИТ: Топливный бак пробит! Утечка горючего.", Color.red);
                VehicleModulesPlugin.Instance.StartCoroutine(FuelLeakRoutine(vehicle, state));
            }

            // 2. Трансмиссия (Потеря аккумулятора через время)
            if (!state.IsTransmissionBroken && roll < (cfg.ChanceTransmission + intensity))
            {
                state.IsTransmissionBroken = true;
                SendMessage(vehicle, "КРИТ: Повреждение трансмиссии! Электроника откажет через 20с.", Color.yellow);
                VehicleModulesPlugin.Instance.StartCoroutine(TransmissionFailureRoutine(vehicle, state));
            }

            // 3. Орудие (Детонация снаряда)
            if (state.IsGunBroken)
            {
                if (Random.value < 0.50f) // 50% шанс детонации при каждом получении урона, если пушка уже сломана
                {
                    SendMessage(vehicle, "КАТАСТРОФА: Детонация снаряда в казеннике!", Color.red);
                    VehicleManager.damage(vehicle, 500, 1, false); // Огромный внутренний урон
                    ExplodeInternally(vehicle);
                }
            }
            else if (roll < (cfg.ChanceGunBroken + intensity))
            {
                state.IsGunBroken = true;
                SendMessage(vehicle, "КРИТ: Орудие заклинило! Риск детонации снаряда.", Color.red);
            }

            // --- ЛОГИКА ИВЕНТОВ ---

            // Пожар
            if (!state.IsOnFire && roll < (cfg.ChanceFire + intensity))
            {
                VehicleModulesPlugin.Instance.StartCoroutine(FireRoutine(vehicle, state));
            }

            // Задымление (Кислород)
            if (!state.IsSmoking && roll < (cfg.ChanceSmoke + intensity))
            {
                VehicleModulesPlugin.Instance.StartCoroutine(SmokeRoutine(vehicle, state));
            }

            // Оглушение
            if (!state.IsStunned && roll < (cfg.ChanceStun + intensity))
            {
                VehicleModulesPlugin.Instance.StartCoroutine(StunRoutine(vehicle, state));
            }
        }

        // --- КОРУТИНЫ ЭФФЕКТОВ ---

        private static IEnumerator FuelLeakRoutine(InteractableVehicle v, VehicleState s)
        {
            while (s.IsFuelTankBroken && v != null && !v.isExploded && v.fuel > 0)
            {
                v.fuel = (ushort)Mathf.Max(0, v.fuel - 12);
                yield return new WaitForSeconds(1.0f);
            }
        }

        private static IEnumerator TransmissionFailureRoutine(InteractableVehicle v, VehicleState s)
        {
            yield return new WaitForSeconds(Random.Range(15, 30));
            if (v != null && !v.isExploded)
            {
                v.batteryCharge = 0;
                SendMessage(v, "ТРАНСМИССИЯ: Аккумулятор полностью разряжен.", Color.red);
            }
        }

        private static IEnumerator FireRoutine(InteractableVehicle v, VehicleState s)
        {
            s.IsOnFire = true;
            SendMessage(v, "ПОЖАР! Техника взорвется через несколько секунд!", Color.red);
            float timer = Random.Range(6, 8);
            while (timer > 0 && v != null && !v.isExploded)
            {
                EffectManager.sendEffect(125, 128, v.transform.position); // Эффект огня
                VehicleManager.damage(v, 150, 1, false);
                timer -= 1.5f;
                yield return new WaitForSeconds(1.5f);
            }
            s.IsOnFire = false;
        }

        private static IEnumerator SmokeRoutine(InteractableVehicle v, VehicleState s)
        {
            s.IsSmoking = true;
            SendMessage(v, "ЗАДЫМЛЕНИЕ: Экипаж задыхается!", Color.gray);
            float duration = Random.Range(15, 30);
            while (duration > 0 && v != null && !v.isExploded)
            {
                EffectManager.sendEffect(134, 128, v.transform.position); // Эффект дыма
                foreach (var passenger in v.passengers)
                {
                    if (passenger.player != null)
                        passenger.player.player.life.askSuffocate(10); // Урон по кислороду
                }
                duration -= 2f;
                yield return new WaitForSeconds(2.0f);
            }
            s.IsSmoking = false;
        }

        private static IEnumerator StunRoutine(InteractableVehicle v, VehicleState s)
        {
            s.IsStunned = true;
            SendMessage(v, "ЭКИПАЖ ОГЛУШЕН! Управление заблокировано.", Color.yellow);
            yield return new WaitForSeconds(Random.Range(4, 5));
            s.IsStunned = false;
            SendMessage(v, "Экипаж пришел в себя.", Color.green);
        }

        private static void SendMessage(InteractableVehicle v, string msg, Color c)
        {
            foreach (var p in v.passengers)
                if (p.player != null) UnturnedChat.Say(p.player.playerID.steamID, msg, c);
        }

        private static void ExplodeInternally(InteractableVehicle v)
        {
            EffectManager.sendEffect(45, 128, v.transform.position); // Большой взрыв
        }
    }
}
