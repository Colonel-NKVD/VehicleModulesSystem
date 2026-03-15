using Rocket.API;
using System.Collections.Generic;

namespace VehicleModulesSystem
{
    public class VehicleModulesConfig : IRocketPluginConfiguration
    {
        public List<ushort> TargetedVehicleIds;
        
        // Шансы (0.0 - 1.0)
        public float ChanceFuelLeak;
        public float ChanceTransmission;
        public float ChanceGunBroken;
        public float ChanceFire;
        public float ChanceSmoke;
        public float ChanceStun;

        public void LoadDefaults()
        {
            TargetedVehicleIds = new List<ushort> { 120, 121, 137 };
            ChanceFuelLeak = 0.15f;
            ChanceTransmission = 0.10f;
            ChanceGunBroken = 0.12f;
            ChanceFire = 0.05f;
            ChanceSmoke = 0.20f;
            ChanceStun = 0.10f;
        }
    }
}
