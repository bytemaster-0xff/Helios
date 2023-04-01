using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GadrocsWorkshop.Helios.Interfaces.HeliosMQTT
{
    public class SubscribedTopic
    {
        public enum PayloadTypes
        {
            None,
            Int,
            Real
        }

        public string Topic { get; set; }

        public PayloadTypes PayloadType { get; set; } = PayloadTypes.None;
    }
}
