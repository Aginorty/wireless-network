﻿using SmartHome.Comunications;
using SmartHome.Products;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SmartHome.Comunications.Messages;
using DataLayer.Entities;
using DataLayer.Entities.HomeDevices;

namespace SmartHome.Memory
{
    public static class BinaryExtension
    {
        public static string ToStrigMac(this byte[] macAddress)
        {
            StringBuilder result = new StringBuilder();

            result.AppendFormat("{0:X2}", macAddress[0]);
            foreach (byte b in macAddress.Skip(1))
            {
                result.AppendFormat("-{0:X2}", b);
            }

            return result.ToString();
        }

        public static byte[] ToBinaryMac(this string macAddress)
        {
            var macSplit = macAddress.Split('-');

            byte[] result = new byte[macSplit.Length];

            for (int i = 0; i < macSplit.Length; i++)
            {
                result[i] = Byte.Parse(macSplit[i]);
            }

            return result;
        }

        /// <summary>
        /// Transform a DateTime in a Byte[] sort in Hours, Minutes and Seconds
        /// </summary>
        /// <param name="time"></param>
        /// <returns></returns>
        public static byte[] ToBinaryTime(this DateTime time)
        {
            Byte[] result = new Byte[3];
            result[0] = (byte)time.Hour;
            result[1] = (byte)time.Minute;
            result[2] = (byte)time.Second;

            return result;
        }

        public static byte[] ToBinaryDate(this DateTime time, bool littleEndian = true)
        {

            List<Byte> result = new List<Byte>();

            //TODO: Corregir!!
            string value = Enum.GetName(typeof(DayOfWeek), time.DayOfWeek);
            result.Add((byte)Enum.Parse(typeof(WeekDays), value));

            result.Add((byte)time.Day);
            result.Add((byte)time.Month);
            result.AddRange(((ushort)time.Year).UshortToByte(littleEndian));

            return result.ToArray();
        }

        public static byte[] UshortToByte(this ushort b, bool litteEndian = true)
        {
            byte[] result = new Byte[2];

            if (litteEndian)
            {
                result[0] = (byte)b;
                result[1] = (byte)(b >> 8);
            }
            else
            {
                result[0] = (byte)(b >> 8);
                result[1] = (byte)b;
            }

            return result;
        }

        //Some adds to easy the conversion
        public static List<PinPort> GetPinPortList(this Node node)
        {
            var dic = ProductConfiguration.GetShieldDictionary(node.Shield);
            List<PinPort> result = new List<PinPort>();

            foreach (var item in dic.Values)
            {
                result.AddRange(item.Item2);
            }

            return result;
        }
        

        /// <summary>
        /// Devuelve el Connector que tiene el pinPort Asociado, si no existe ningun conector entonces null
        /// </summary>
        /// <param name="node"></param>
        /// <param name="pinPort"></param>
        /// <returns></returns>
        public static Connector GetConnector(this Node node, PinPort pinPort)
        {
            if (!node.GetPinPortList().Contains(pinPort))
            {
                return null;
            }
            else
            {
                foreach (Connector connector in node.Connectors)
                {
                    if (connector.GetPinPort().Contains(pinPort))
                        return connector;
                }
            }
            return null;
        }
    }

    public partial class FirmwareUno
    {
        private byte GetLogicConfiguration(Connector connector, HomeDevice homeDevice)
        {
            PinPortConfiguration ppc = connector.GetPinPortConfiguration(homeDevice);
            byte res = 0x00;
            if (ppc.Output)
                res |= (byte)(0x01 << 3);
            if (ppc.DefaultValueD)
                res |= (byte)(0x01 << 2);
            res |= (byte)ppc.ChangeTypeD;

            return res;
        }
    }
}
