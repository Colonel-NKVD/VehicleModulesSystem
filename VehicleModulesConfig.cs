using Rocket.API;
using System.Collections.Generic;

namespace VehicleModulesSystem
{
    public class VehicleModulesConfig : IRocketPluginConfiguration
    {
        public List<ushort> TargetedVehicleIds; 
        public float ChanceFuelLeak;            
        public float ChanceTransmission;        
        public float ChanceFire;                
        
        // Исправлено: метод должен называться LoadDefaults
        public void LoadDefaults()
        {
            TargetedVehicleIds = new List<ushort> { 120, 121, 137 }; 
            ChanceFuelLeak = 0.15f;
            ChanceTransmission = 0.10f;
            ChanceFire = 0.05f;
        }
    }
}
