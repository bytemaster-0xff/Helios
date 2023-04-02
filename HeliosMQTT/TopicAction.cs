using System;
using System.Collections.Generic;

namespace GadrocsWorkshop.Helios.Interfaces.HeliosMQTT
{
    public class TopicAction
    {
        public string Id { get; set; }
        public string Device { get; set; }
        public string Topic { get; set; }
        public string DefaultValue { get; set; }
        public string Description { get; set; }
        public string UnitName { get; set; } = "NoValue";
   
        public IEnumerable<String> Validate()
        {
            var errors = new List<string>();
            if (String.IsNullOrEmpty(Device)) errors.Add("Device is a required field.");
            if (String.IsNullOrEmpty(Topic)) errors.Add("Topic is a required field.");
            if (String.IsNullOrEmpty(UnitName)) errors.Add("Unit Name is a required field.");

            return errors;
        }
    }
}
