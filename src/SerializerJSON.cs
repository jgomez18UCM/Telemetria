﻿using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Telemetria
{
    public class SerializerJSON
    {
        public object Serialize(Event e)
        {
            return JsonConvert.SerializeObject(e, Formatting.None);
        }
    }
}