using System.Collections.Generic;
using System.IO;
using System.Security.AccessControl;
using System.Text;
using Newtonsoft.Json;

namespace StreamingApp.Utility
{
    internal class ConfigurationManager
    {
        public readonly Dictionary<string, string> Configs;

        public ConfigurationManager()
        {
            FileStream file;
            try
            {
                file = new FileStream("config.json", FileMode.Open);
            }
            catch (FileNotFoundException)
            {
                file = new FileStream("../../config.json", FileMode.Open);
            }
            string jsonString;
            using (var streamReader = new StreamReader(file, Encoding.UTF8))
            {
                jsonString = streamReader.ReadToEnd();
            }
            Configs  = JsonConvert.DeserializeObject<Dictionary<string, string>>(jsonString);
        }

        public string Get(string key)
        {
            return Configs[key];
        }


    }
}
