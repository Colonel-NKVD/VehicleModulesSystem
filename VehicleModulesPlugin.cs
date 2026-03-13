using Rocket.API;

namespace VehicleModulesSystem
{
    public class VehicleModulesConfiguration : IRocketPluginConfiguration
    {
        public ushort MinDamageThreshold;
        public float FireDamage;
        public ushort TurretWeaponID;
        public ushort RepairResourceID;
        public byte ResourcePerModule;
        public ushort BarricadeRepairID;
        public float RepairRadius;
        
        public float ChainReactionChance; 
        public float SaveInterval; 
        public bool EnableCameraShake;

        public void Defaults() // В RocketMod метод называется Defaults, а не LoadDefaults
        {
            MinDamageThreshold = 25;
            FireDamage = 4.0f;
            TurretWeaponID = 1300;
            RepairResourceID = 67;
            ResourcePerModule = 3;
            BarricadeRepairID = 328;
            RepairRadius = 15f;
            
            ChainReactionChance = 0.05f; 
            SaveInterval = 300f; 
            EnableCameraShake = true;
        }
    }
}
