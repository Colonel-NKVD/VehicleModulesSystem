using Rocket.Core.Plugins;
using Rocket.API.Collections;
using SDG.Unturned;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace VehicleModulesSystem
{
    public class VehicleModulesPlugin : RocketPlugin<VehicleModulesConfig>
    {
        public static VehicleModulesPlugin Instance;
        public Dictionary<uint, Dictionary<string, float>> SavedVehicleData = new Dictionary<uint, Dictionary<string, float>>();
        private string dataPath;
        private float lastSaveTime;
        public bool IsDirty = false; // Флаг: нужно ли сохранять данные

        protected override void Load()
        {
            Instance = this;
            dataPath = Path.Combine(Directory, "VehicleData.json");
            LoadData();

            VehicleManager.onVehicleAdded += OnVehicleSpawned;
            foreach (var v in VehicleManager.vehicles) AddTracker(v);
        }

        void Update()
        {
            // Автосохранение только если данные изменились
            if (IsDirty && Time.time - lastSaveTime > Configuration.Instance.SaveInterval)
            {
                SaveData();
            }
        }

        protected override void Unload()
        {
            SaveData();
            VehicleManager.onVehicleAdded -= OnVehicleSpawned;
        }

        public override TranslationList DefaultTranslations => new TranslationList
        {
            { "Status_Perfect", "Штатно" },
            { "Status_Damaged", "Поврежден" },
            { "Status_Critical", "Критический износ" },
            { "Status_Destroyed", "РАЗРУШЕН" },
            { "Alert_ChainReaction", "[!] Топливо вспыхнуло от перегрева!" }
        };

        private void OnVehicleSpawned(InteractableVehicle v) => AddTracker(v);
        private void AddTracker(InteractableVehicle v)
        {
            if (v != null && v.gameObject.GetComponent<VehicleTracker>() == null)
                v.gameObject.AddComponent<VehicleTracker>();
        }

        public void SaveData() 
        { 
            File.WriteAllText(dataPath, JsonConvert.SerializeObject(SavedVehicleData)); 
            lastSaveTime = Time.time;
            IsDirty = false;
        }

        private void LoadData() 
        { 
            if (File.Exists(dataPath)) 
                SavedVehicleData = JsonConvert.DeserializeObject<Dictionary<uint, Dictionary<string, float>>>(File.ReadAllText(dataPath)); 
        }
    }
}
