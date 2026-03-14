using Rocket.API;
using System.Collections.Generic;

namespace VehicleModulesSystem
{
    public class VehicleModulesConfig : IRocketPluginConfiguration
    {
        public List<ushort> TargetedVehicleIds;
        public ushort RepairStationBarricadeId;
        public float RepairStationRadius;

        public float ChanceFuelLeak;
        public float ChanceTransmission;
        public float ChanceGunBroken;
        public float ChanceFire;
        public float ChanceSmoke;
        public float ChanceStun;

        public void LoadDefaults()
        {
            TargetedVehicleIds = new List<ushort> { 120, 121, 137 };
            RepairStationBarricadeId = 55000;
            RepairStationRadius = 20f;

            ChanceFuelLeak = 0.05f;
            ChanceTransmission = 0.05f;
            ChanceGunBroken = 0.05f;
            ChanceFire = 0.03f;
            ChanceSmoke = 0.07f;
            ChanceStun = 0.05f;
        }
    }
}
