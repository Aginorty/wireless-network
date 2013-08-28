﻿#region Using Statements
using DataLayer.Entities.HomeDevices;
using SmartHome.Communications.Messages;
#endregion

namespace SmartHome.BusinessEntities.BusinessHomeDevice
{
    public static class BusinessDimmable
    {
        [OperationAttribute]        
        public static OperationMessage On(this Dimmable dimmable)
        {
            return dimmable.PercentageDimmer(1);
        }

        [OperationAttribute]
        public static OperationMessage Off(this Dimmable dimmable)
        {
            return dimmable.PercentageDimmer(0);
        }

        [OperationAttribute]
        public static OperationMessage OnTime(this Dimmable dimmable, byte seconds)
        {
            return dimmable.PercentageDimmer(1, seconds);
        }

        [OperationAttribute]
        public static OperationMessage Switch(this Dimmable dimmable)
        {
            if (dimmable.Value != 0)
            {
                return dimmable.Off();
            }
            else
            {
                return dimmable.PercentageDimmer(dimmable.Value.Value);
            }
        }

        [OperationAttribute]
        public static OperationMessage PercentageDimmer(this Dimmable dimmable, float percentage)
        {
            return dimmable.PercentageDimmer(percentage, 0);
        }

        [OperationAttribute]
        public static OperationMessage PercentageDimmer(this Dimmable dimmable, float percentage, byte seconds)
        {
            int value = (int)(percentage * 100);

            ushort destinationAddress = (ushort)(dimmable.Connector == null ? 0 : dimmable.Connector.Node.Address);

            return OperationMessage.DimmerWrite((ushort)dimmable.Id, (byte)(percentage * byte.MaxValue / 100.0), 0, destinationAddress);
        }

        public static OperationMessage RefreshState(this Dimmable dimmable)
        {
            ushort destinationAddress = (ushort)(dimmable.Connector == null ? 0 : dimmable.Connector.Node.Address);

            return OperationMessage.DimmerRead((ushort)dimmable.Id, destinationAddress);
        }
    }
}
