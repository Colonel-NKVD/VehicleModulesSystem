using System;
using System.Collections.Generic;
using System.Reflection;
using SDG.Unturned;
using UnityEngine;
using VehicleModulesSystem;

public class VehicleTracker : MonoBehaviour
{
    private InteractableVehicle vehicle;
    private ushort lastHealth;
    public Dictionary<TankModule, float> ModuleHP = new Dictionary<TankModule, float>();
    private float tickTimer = 0f;
    private float effectTimer = 0f;

    void Start()
    {
        vehicle = GetComponent<InteractableVehicle>();
        if (vehicle == null) return;
        
        lastHealth = vehicle.health;

        // Загрузка данных или инициализация
        if (VehicleModulesPlugin.Instance.SavedVehicleData.TryGetValue(vehicle.instanceID, out var saved))
        {
            foreach (var entry in saved) 
                if (Enum.TryParse(entry.Key, out TankModule m)) ModuleHP[m] = entry.Value;
        }
        else
        {
            foreach (TankModule m in Enum.GetValues(typeof(TankModule))) ModuleHP[m] = 100f;
        }
    }

    void Update()
    {
        if (vehicle == null || vehicle.isDead) return;

        // 1. ДЕТЕКЦИЯ УРОНА (сделали чувствительнее)
        if (vehicle.health < lastHealth)
        {
            ushort damageTaken = (ushort)(lastHealth - vehicle.health);
            ApplyImpact(damageTaken);
            lastHealth = vehicle.health;
        }
        else if (vehicle.health > lastHealth) // Если машину починили ремкой
        {
            lastHealth = vehicle.health;
        }

        // 2. ВИЗУАЛЬНЫЕ ЭФФЕКТЫ (то, чего не хватало)
        if (Time.time - effectTimer > 0.5f)
        {
            effectTimer = Time.time;
            SpawnVisualEffects();
        }

        // 3. СИСТЕМНАЯ ЛОГИКА (Раз в секунду)
        if (Time.time - tickTimer > 1f)
        {
            tickTimer = Time.time;
            ProcessModuleLogic();
        }
    }

    private void SpawnVisualEffects()
    {
        // Если горит топливо — спавним огонь (ID 134 или 45 — стандартные эффекты Unturned)
        if (ModuleHP[TankModule.FuelLeak] < 50f)
        {
            // Эффект огня в центре машины
            EffectManager.sendEffect(134, 16f, transform.position + Vector3.up);
        }

        // Если выбит двигатель — пускаем дым
        if (ModuleHP[TankModule.Engine] < 30f)
        {
            EffectManager.sendEffect(45, 16f, transform.position + Vector3.up * 1.5f);
        }
    }

    private void ProcessModuleLogic()
    {
        // Логика пожара
        if (ModuleHP[TankModule.FuelLeak] < 40f)
        {
            float fireDamage = VehicleModulesPlugin.Instance.Configuration.Instance.FireDamage;
            vehicle.askDamage((ushort)fireDamage, false, Steamworks.CSteamID.Nil, EDamageOrigin.Unknown);
            
            // Если пожарная система цела, она понемногу тушит
            if (ModuleHP[TankModule.Fire] > 0)
            {
                ModuleHP[TankModule.FuelLeak] += 2f;
                ModuleHP[TankModule.Fire] -= 1f; 
            }
        }

        // Логика двигателя
        if (ModuleHP[TankModule.Engine] <= 0)
        {
            // Если двигатель в ноль — глушим его принудительно
            if (vehicle.isEngineOn) vehicle.askFillFuel(0); 
        }
    }

    public void ApplyImpact(ushort damage)
    {
        // Если урон меньше порога из конфига — модули не страдают
        if (damage < VehicleModulesPlugin.Instance.Configuration.Instance.MinDamageThreshold) return;

        // Тряска камеры при попадании
        if (VehicleModulesPlugin.Instance.Configuration.Instance.EnableCameraShake)
            EffectManager.sendEffect(45, 24f, transform.position); 

        // Рандомно выбираем модуль для повреждения
        Array values = Enum.GetValues(typeof(TankModule));
        TankModule hit = (TankModule)values.GetValue(UnityEngine.Random.Range(0, values.Length));
        
        ModuleHP[hit] = Mathf.Max(0, ModuleHP[hit] - (damage * 1.2f));
        
        NotifyCrew($"<color=red>[СИСТЕМА]</color> Попадание в узел: {hit}!");
        MarkDirty();
    }

    private void MarkDirty()
    {
        var data = new Dictionary<string, float>();
        foreach (var entry in ModuleHP) data.Add(entry.Key.ToString(), entry.Value);
        VehicleModulesPlugin.Instance.SavedVehicleData[vehicle.instanceID] = data;
        VehicleModulesPlugin.Instance.IsDirty = true;
    }

    private void NotifyCrew(string msg)
    {
        foreach (var p in vehicle.passengers)
            if (p?.player != null)
                ChatManager.serverSendMessage(msg, Color.white, null, p.player.player.channel.owner, EChatMode.SAY, "https://i.imgur.com/7S6S8S8.png", true);
    }

    public string GetModuleStatus(TankModule mod)
    {
        float hp = ModuleHP[mod];
        if (hp >= 90) return VehicleModulesPlugin.Instance.Translate("Status_Perfect");
        if (hp > 40) return VehicleModulesPlugin.Instance.Translate("Status_Damaged");
        if (hp > 0) return VehicleModulesPlugin.Instance.Translate("Status_Critical");
        return VehicleModulesPlugin.Instance.Translate("Status_Destroyed");
    }
}
