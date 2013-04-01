﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Extensions.Enums;

namespace SmartHome.Network.HomeDevices
{
    public abstract class HomeDevice
    {
        public int ID { get; set; }
        public String Name { get; set; }
        public Connector Connector { get; set; } //if null then isn't connected
        public Enums.ConnectorType ConnectorType { get; set; }
        public Position Position { get; set; }
        public List<Action> Actions { get; set; }

        public void Link(Connector Connector)
        {
            if (Connector.HomeDevice != null)
            {
                throw new Exception("Connector it's available for other HomeDevice, please unlink firts the other homedevice :D");
            }

            this.Connector.HomeDevice = this;
            this.Connector = Connector;
        }

        public void Unlink()
        {
            this.Connector.HomeDevice = null;
            this.Connector = null;
        }


        public virtual void RefreshState()
        {

        }
    }
}