using Rocket.API;

namespace VehicleModulesSystem
{
    public class VehicleModulesConfig : IRocketPluginConfiguration
    {
        public ushort MinDamageThreshold;
        public float FireDamage;
        public ushort TurretWeaponID;
        public ushort RepairResourceID;
        public byte ResourcePerModule;
        public ushort BarricadeRepairID;
        public float RepairRadius;
        
        // Новые параметры v7.0
        public float ChainReactionChance; // Шанс, что утечка топлива вызовет пожар
        public float SaveInterval; // Интервал автосохранения (сек)
        public bool EnableCameraShake;

        public void LoadDefaults()
        {
            MinDamageThreshold = 25;
            FireDamage = 4.0f;
            TurretWeaponID = 1300;
            RepairResourceID = 67;
            ResourcePerModule = 3;
            BarricadeRepairID = 328;
            RepairRadius = 15f;
            
            ChainReactionChance = 0.05f; // 5% шанс каждый тик при утечке
            SaveInterval = 300f; 
            EnableCameraShake = true;
        }
    }
}
