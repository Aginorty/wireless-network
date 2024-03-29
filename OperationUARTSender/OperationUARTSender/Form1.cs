﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.IO.Ports;
using System.IO;
using System.Threading;

namespace OperationUARTSender
{
    public partial class Form1 : Form
    {
        #region Enumerators
        enum SerialReceiverState
        {
            USART_RECEIVER_IDLE_RX_STATE,
            USART_RECEIVER_MAGIC_RX_STATE,
            USART_RECEIVER_SOF_RX_STATE,
            USART_RECEIVER_DATA_RX_STATE,
            USART_RECEIVER_EOF_RX_STATE,
            USART_RECEIVER_ERROR_RX_STATE
        };

        enum ConfigErrorCodes
        {
            OK = 0,
            ERROR_FRAGMENT_TOTAL_NOT_EXPECTED,
            ERROR_FRAGMENT_ORDER,
            ERROR_WAITING_FIRST_FRAGMENT,
            ERROR_CONFIG_SIZE_TOO_BIG,
            ERROR_CONFIG_INVALID_CHECKSUM,
            ERROR_CONFIG_SIZE_NOT_EXPECTED,
            ERROR_BUSY_RECEIVING_STATE
        };

        enum DayOfWeeks
        {
            SUN = 1,
            SAT = 2,
            FRI = 4,
            THU = 8,
            WED = 16,
            TUE = 32,
            MON = 64,
            UNK = 128
        };
        #endregion

        // Magic symbol to start SOF end EOF sequences with. Should be duplicated if
        // occured inside the message.
        const byte APP_MAGIC_SYMBOL = 0x10;
        byte[] SOF = { APP_MAGIC_SYMBOL, 0x02 };
        byte[] EOF = { APP_MAGIC_SYMBOL, 0x03 };

        SerialPort serial = new SerialPort();
        SerialReceiverState serialState = SerialReceiverState.USART_RECEIVER_IDLE_RX_STATE;
        List<byte> currentMessage = new List<byte>();
        byte CheckSum;

        const int MAX_CONTENT_SIZE = 50;
        List<Operation> configWriteBuffer = new List<Operation>();
        List<Operation> configReadBuffer = new List<Operation>();

        Dictionary<byte[], ushort> networkDevices = new Dictionary<byte[], ushort>(new ByteArrayComparer());

        event EventHandler OperationReceived;

        ushort DestinationAddress
        {
            get
            {
                return Convert.ToUInt16(textBoxDestAddress.Text, 16);
            }
        }

        ushort ParameterAddress
        {
            get
            {
                return Convert.ToUInt16(textBoxParamAddress.Text, 16);
            }
        }

        void OnOperationReceived(InputMessage oper)
        {
            if (OperationReceived != null)
                OperationReceived(oper, null);
        }

        public Form1()
        {
            InitializeComponent();

            comboBox1_MouseClick(this, null);
            serial.DataReceived += new SerialDataReceivedEventHandler(serial_DataReceived);
            OperationReceived += new EventHandler(Form1_OperationReceived);
        }

        public void SendOperation(Operation operation)
        {
            OutputMessage outputMessage = new OutputMessage()
            {
                SecurityEnabled = true,
                RoutingEnabled = true,
                EndPoint = 1,
                Retries = 3,
                Content = operation
            };

            SendData(outputMessage.ToBinary());
        }

        public void SendConfigFile(ushort destinationAddress, string inputFilename)
        {
            byte[] fileBytes = File.ReadAllBytes(inputFilename);

            configWriteBuffer.Clear();
            byte numberOfFrames = (byte)(fileBytes.Length / MAX_CONTENT_SIZE);
            int frameSize = 0;
            for (byte i = 0; i <= numberOfFrames; i++)
            {
                Operation currentOp = new Operation()
                {
                    SourceAddress = 0x00,
                    DestinationAddress = destinationAddress,
                    OpCode = Operation.OPCodes.ConfigWrite,
                    Args = new byte[MAX_CONTENT_SIZE + 2]
                };

                frameSize = Math.Min(MAX_CONTENT_SIZE, fileBytes.Length - (i * MAX_CONTENT_SIZE));
                currentOp.Args[0] = (byte)((numberOfFrames << 4) | (i & 0xF));
                currentOp.Args[1] = (byte)frameSize;
                Buffer.BlockCopy(fileBytes, (i * MAX_CONTENT_SIZE), currentOp.Args, 2, frameSize);

                configWriteBuffer.Add(currentOp);
            }

            SendOperation(configWriteBuffer[0]);
        }

        public void SendJoinRequestResponse(ushort destinationAddress)
        {
            byte[] RSAKey = new byte[16];
            Random r = new Random();
            r.NextBytes(RSAKey);

            SendOperation(new Operation() { SourceAddress = 0x00, DestinationAddress = destinationAddress, OpCode = Operation.OPCodes.JoinRequestResponse, Args = RSAKey });
        }

        public void SendJoinAcceptResponse(ushort destinationAddress, byte[] macAddress, byte[] AESKey)
        {
            List<byte> operationArgs = new List<byte>();

            if (!networkDevices.ContainsKey(macAddress))
            {
                networkDevices.Add(macAddress, (ushort)(0x4004 + networkDevices.Count));
            }

            operationArgs.AddRange(BitConverter.GetBytes(networkDevices[macAddress]));
            operationArgs.AddRange(Encoding.ASCII.GetBytes("TestSecurityKey0"));

            SendOperation(new Operation() { SourceAddress = 0x00, DestinationAddress = destinationAddress, OpCode = Operation.OPCodes.JoinAcceptResponse, Args = operationArgs.ToArray() });
        }

        #region Private Methods
        private void SendData(byte[] buffer)
        {
            List<byte> frame = new List<byte>();
            byte cs = 0;

            frame.Add(SOF[0]);
            frame.Add(SOF[1]);

            for (int i = 0; i < buffer.Length; i++)
            {
                if (buffer[i] == APP_MAGIC_SYMBOL)
                {
                    frame.Add(APP_MAGIC_SYMBOL);
                    cs += APP_MAGIC_SYMBOL;
                }
                frame.Add(buffer[i]);
                cs += buffer[i];
            }

            frame.Add(EOF[0]);
            frame.Add(EOF[1]);

            cs += SOF[0];
            cs += SOF[1];
            cs += EOF[0];
            cs += EOF[1];

            frame.Add(cs);

            serial.Write(frame.ToArray(), 0, frame.Count);
        }

        private void SendDateTime(ushort destinationAddress, bool writeOperation)
        {
            var dat = DateTime.Now;
            var dow = (int)Enum.Parse(typeof(DayOfWeeks), dat.DayOfWeek.ToString().ToUpper().Substring(0, 3));

            SendOperation(new Operation()
            {
                SourceAddress = 0x00,
                DestinationAddress = destinationAddress,
                OpCode = writeOperation ? Operation.OPCodes.DateTimeWrite : Operation.OPCodes.DateTimeReadResponse,
                Args = new byte[] { (byte)dow, (byte)dat.Day, (byte)dat.Month, (byte)dat.Year, (byte)(dat.Year >> 8), (byte)dat.Hour, (byte)dat.Minute, (byte)dat.Second }
            });
        }

        private void SaveConfigFile()
        {
            List<byte> rawBytes = new List<byte>();

            foreach (Operation op in configReadBuffer)
            {
                rawBytes.AddRange(op.Args.Skip(2));
            }

            //TODO: Calculate Checksum
            ushort configLength = (ushort)((((ushort)rawBytes[2]) << 8) | (ushort)rawBytes[1]);

            if (configLength == rawBytes.Count)
            {
                this.BeginInvoke((Action)(() =>
                {
                    var res = saveFileDialog1.ShowDialog();

                    if (res == System.Windows.Forms.DialogResult.OK)
                    {
                        System.IO.File.WriteAllBytes(saveFileDialog1.FileName, rawBytes.ToArray());
                    }
                }));
            }
            else
            {
                PrintMessage("CONFIG ERROR. UNEXPECTED SIZE");
            }
        }

        private void PrintMessage(string message)
        {
            this.BeginInvoke((Action)(() =>
            {
                StringBuilder sb = new StringBuilder();
                sb.Append("[" + DateTime.Now.ToLongTimeString() + "]   ->   ");
                sb.Append(message);
                listBox1.Items.Add(sb.ToString());
                listBox1.SelectedIndex = listBox1.Items.Count - 1;
            }));
        }
        #endregion

        #region EventCallbacks
        private void comboBox1_MouseClick(object sender, MouseEventArgs e)
        {
            comboBox1.Items.Clear();
            comboBox1.Items.AddRange(SerialPort.GetPortNames());

            if (comboBox1.Items.Count > 0)
            {
                comboBox1.SelectedIndex = comboBox1.Items.Count - 1;
                buttonOpen.Enabled = true;
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (buttonOpen.Text == "Open")
            {
                if (comboBox1.SelectedIndex > -1)
                {
                    serial.BaudRate = 38400;
                    serial.PortName = comboBox1.Text;
                    serial.Open();

                    buttonOpen.Text = "Close";
                    comboBox1.Enabled = false;

                    foreach (var control in this.Controls)
                    {
                        if (control is Button && control != buttonOpen)
                        {
                            ((Button)control).Enabled = true;
                        }
                    }
                }
            }
            else
            {
                serial.Close();

                buttonOpen.Text = "Open";
                comboBox1.Enabled = true;

                foreach (var control in this.Controls)
                {
                    if (control is Button && control != buttonOpen)
                    {
                        ((Button)control).Enabled = false;
                    }
                }
            }
        }

        private void buttonCmd_Click(object sender, EventArgs e)
        {
            if (sender == buttonDigSwitch)
            {
                SendOperation(new Operation() { SourceAddress = 0x00, DestinationAddress = DestinationAddress, OpCode = Operation.OPCodes.LogicSwitch, Args = new byte[] { 0x01, 0x00, 0x00 } });
            }
            else if (sender == buttonSwitchTime)
            {
                SendOperation(new Operation() { SourceAddress = 0x00, DestinationAddress = DestinationAddress, OpCode = Operation.OPCodes.LogicSwitch, Args = new byte[] { 0x01, 0x00, 0x02 } });
            }
            else if (sender == buttonDigRead)
            {
                SendOperation(new Operation() { SourceAddress = 0x00, DestinationAddress = DestinationAddress, OpCode = Operation.OPCodes.LogicRead, Args = new byte[] { 0x01, 0x00 } });
            }
            else if (sender == buttonCheckSum)
            {
                SendOperation(new Operation() { SourceAddress = 0x00, DestinationAddress = DestinationAddress, OpCode = Operation.OPCodes.ConfigChecksumRead, Args = new byte[] { } });
            }
            else if (sender == buttonTemp)
            {
                SendOperation(new Operation() { SourceAddress = 0x00, DestinationAddress = DestinationAddress, OpCode = Operation.OPCodes.TemperatureRead, Args = new byte[] { 0x02, 0x00 } });
            }
            else if (sender == buttonHum)
            {
                SendOperation(new Operation() { SourceAddress = 0x00, DestinationAddress = DestinationAddress, OpCode = Operation.OPCodes.HumidityRead, Args = new byte[] { 0x03, 0x00 } });
            }
            else if (sender == buttonPresence)
            {
                SendOperation(new Operation() { SourceAddress = 0x00, DestinationAddress = DestinationAddress, OpCode = Operation.OPCodes.PresenceRead, Args = new byte[] { 0x04, 0x00 } });
            }
            else if (sender == buttonDateTime)
            {
                SendOperation(new Operation() { SourceAddress = 0x00, DestinationAddress = DestinationAddress, OpCode = Operation.OPCodes.DateTimeRead, Args = new byte[] { } });
            }
            else if (sender == buttonMAC)
            {
                SendOperation(new Operation() { SourceAddress = 0x00, DestinationAddress = DestinationAddress, OpCode = Operation.OPCodes.MacRead, Args = new byte[] { } });
            }
            else if (sender == buttonPing)
            {
                SendOperation(new Operation() { SourceAddress = 0x00, DestinationAddress = DestinationAddress, OpCode = Operation.OPCodes.PingRequest, Args = new byte[] { } });
            }
            else if (sender == buttonFirm)
            {
                SendOperation(new Operation() { SourceAddress = 0x00, DestinationAddress = DestinationAddress, OpCode = Operation.OPCodes.FirmwareVersionRead, Args = new byte[] { } });
            }
            else if (sender == buttonBaseModel)
            {
                SendOperation(new Operation() { SourceAddress = 0x00, DestinationAddress = DestinationAddress, OpCode = Operation.OPCodes.BaseModelRead, Args = new byte[] { } });
            }
            else if (sender == buttonShieldModel)
            {
                SendOperation(new Operation() { SourceAddress = 0x00, DestinationAddress = DestinationAddress, OpCode = Operation.OPCodes.ShieldModelRead, Args = new byte[] { } });
            }
            else if (sender == buttonNextHop)
            {
                SendOperation(new Operation() { SourceAddress = 0x00, DestinationAddress = DestinationAddress, OpCode = Operation.OPCodes.NextHopRead, Args = BitConverter.GetBytes(ParameterAddress) });
            }
            else if (sender == buttonReset)
            {
                SendOperation(new Operation() { SourceAddress = 0x00, DestinationAddress = DestinationAddress, OpCode = Operation.OPCodes.Reset, Args = new byte[] { } });
            }
            else if (sender == buttonDimmerWrite)
            {
                SendOperation(new Operation() { SourceAddress = 0x00, DestinationAddress = DestinationAddress, OpCode = Operation.OPCodes.DimmerWrite, Args = new byte[] { 0x01, 0x00, (byte)trackBar1.Value, 0 } });
            }
        }

        private void button7_Click(object sender, EventArgs e)
        {
            var res = openFileDialog1.ShowDialog();

            if (res == System.Windows.Forms.DialogResult.OK)
            {
                SendConfigFile(DestinationAddress, openFileDialog1.FileName);
            }
        }

        private void button8_Click(object sender, EventArgs e)
        {
            SendDateTime(DestinationAddress, true);
        }

        private void button9_Click(object sender, EventArgs e)
        {
            configReadBuffer.Clear();

            SendOperation(new Operation() { SourceAddress = 0x00, DestinationAddress = DestinationAddress, OpCode = Operation.OPCodes.ConfigRead, Args = new byte[] { } });
        }

        private void buttonClean_Click(object sender, EventArgs e)
        {
            listBox1.Items.Clear();
        }

        void Form1_OperationReceived(object sender, EventArgs e)
        {
            InputMessage message = (sender as InputMessage);
            Operation operation = message.Content;

            string msgToPrint = "";

            if (operation.OpCode == Operation.OPCodes.ConfigWriteResponse)
            {
                if (configWriteBuffer.Count > 0)
                {
                    if (operation.Args[0] == configWriteBuffer[0].Args[0])
                    {
                        if (operation.Args[1] == (byte)ConfigErrorCodes.OK)
                        {
                            configWriteBuffer.RemoveAt(0);
                            if (configWriteBuffer.Count > 0)
                            {
                                SendOperation(configWriteBuffer[0]);
                            }
                            else
                            {
                                msgToPrint = String.Format("CONFIG. UPDATE DONE TO 0x{0:X4}", operation.SourceAddress);
                            }
                        }
                        else
                        {
                            configWriteBuffer.Clear();
                            msgToPrint = "CONFIG. ERROR CODE: " + Enum.GetName(typeof(ConfigErrorCodes), (object)operation.Args[1]);
                        }
                    }
                    else
                    {
                        int rcvFragment = operation.Args[0] & 0x0F;
                        int rcvTotalFragment = (operation.Args[0] & 0xF0) >> 4;
                        int expFragment = configWriteBuffer[0].Args[0] & 0x0F;
                        int expTotalFragment = (configWriteBuffer[0].Args[0] & 0xF0) >> 4;

                        msgToPrint = String.Format("CONFIG. INVALID PROTOCOL: RECEIVED({0}/{1}) EXPECTED({2}/{3})",
                            rcvFragment, rcvTotalFragment, expFragment, expTotalFragment);

                        configWriteBuffer.Clear();
                    }
                }
            }
            else if (operation.OpCode == Operation.OPCodes.TemperatureReadResponse || operation.OpCode == Operation.OPCodes.HumidityReadResponse)
            {
                if (operation.Args[2] == 0xFF)
                {
                    msgToPrint = String.Format("SENSOR 0x{1:X4} FROM 0x{0:X4} UNKNOWN",
                        operation.SourceAddress,
                        ((ushort)operation.Args[1]) << 8 | operation.Args[0]);
                }
                else
                {
                    string baseStr = operation.OpCode == Operation.OPCodes.TemperatureReadResponse ?
                        "TEMPERATURE RECEIVED FROM 0x{0:X4} SENSOR {1}: {2}ºC" :
                        "HUMIDITY RECEIVED FROM 0x{0:X4} SENSOR {1}: {2}%";

                    msgToPrint = String.Format(baseStr,
                         operation.SourceAddress,
                         (((ushort)operation.Args[1]) << 8 | operation.Args[0]),
                         operation.Args[2]);
                }
            }
            else if (operation.OpCode == Operation.OPCodes.PresenceReadResponse)
            {
                string valStr = (operation.Args[2] == 0xFF) ? "UNKNOWN" : (operation.Args[2] != 0) ? "DETECTED" : "NOT DETECTED";

                msgToPrint = String.Format("PRESENCE READ FROM 0x{0:X4}: 0x{1:X4} {2}",
                    operation.SourceAddress,
                    ((ushort)operation.Args[1]) << 8 | operation.Args[0],
                    valStr);
            }
            else if (operation.OpCode == Operation.OPCodes.ConfigChecksumResponse)
            {
                msgToPrint = String.Format("CHECKSUM RECEIVED FROM 0x{0:X4}: 0x{1:X4}",
                    operation.SourceAddress,
                    ((ushort)operation.Args[1]) << 8 | operation.Args[0]);
            }
            else if (operation.OpCode == Operation.OPCodes.LogicReadResponse)
            {
                msgToPrint = String.Format("LOGIC READ FROM 0x{0:X4}: 0x{1:X4} {2}",
                    operation.SourceAddress,
                    ((ushort)operation.Args[1]) << 8 | operation.Args[0],
                    (operation.Args[2] == 0xFF) ? "UNKNOWN" : (operation.Args[2] != 0).ToString());
            }
            else if (operation.OpCode == Operation.OPCodes.DimmerReadResponse)
            {
                msgToPrint = String.Format("DIMMER READ FROM 0x{0:X4}: 0x{1:X4} {2}",
                    operation.SourceAddress,
                    ((ushort)operation.Args[1]) << 8 | operation.Args[0],
                    (operation.Args[2] == 0xFF) ? "UNKNOWN" : operation.Args[2].ToString());
            }
            else if (operation.OpCode == Operation.OPCodes.DateTimeRead) //TIME SYNC REQUEST!
            {
                SendDateTime(operation.SourceAddress, false);
                msgToPrint = String.Format("TIME SYNC RESPONSE SENT TO 0x{0:X4}", operation.SourceAddress);
            }
            else if (operation.OpCode == Operation.OPCodes.DateTimeReadResponse)
            {
                string DateTimeStr = string.Empty;

                if (operation.Args[0] != 0xFF)//Valid DATETIME
                {
                    string DateStr = String.Format("{0}  {1:d2}/{2:d2}/{3:d4}",
                        Enum.GetName(typeof(DayOfWeeks), (object)operation.Args[0]),
                        operation.Args[1],
                        operation.Args[2],
                        ((ushort)operation.Args[4]) << 8 | operation.Args[3]);

                    string TimeStr = String.Format(" {0:d2}:{1:d2}:{2:d2}",
                        operation.Args[5],
                        operation.Args[6],
                        operation.Args[7]);

                    DateTimeStr = DateStr + " " + TimeStr;
                }
                else
                {
                    DateTimeStr = "INVALID";
                }
                msgToPrint = String.Format("DATETIME READ FROM 0x{0:X4} -> ", operation.SourceAddress) + DateTimeStr;
            }
            else if (operation.OpCode == Operation.OPCodes.MacReadResponse)
            {
                msgToPrint = String.Format("MAC FROM 0x{0:X4} -> 0x{1:X2}-0x{2:X2}-0x{3:X2}-0x{4:X2}-0x{5:X2}-0x{6:X2}",
                    operation.SourceAddress,
                    operation.Args[0],
                    operation.Args[1],
                    operation.Args[2],
                    operation.Args[3],
                    operation.Args[4],
                    operation.Args[5]);
            }
            else if (operation.OpCode == Operation.OPCodes.FirmwareVersionReadResponse)
            {
                msgToPrint = String.Format("FIRMWARE VERSION FROM 0x{0:X4} -> {1}",
                    operation.SourceAddress,
                    operation.Args[0]);
            }
            else if (operation.OpCode == Operation.OPCodes.BaseModelReadResponse)
            {
                msgToPrint = String.Format("BASE MODEL FROM 0x{0:X4} -> {1}",
                    operation.SourceAddress,
                    operation.Args[0]);
            }
            else if (operation.OpCode == Operation.OPCodes.ShieldModelReadResponse)
            {
                msgToPrint = String.Format("SHIELD MODEL FROM 0x{0:X4} -> {1}",
                    operation.SourceAddress,
                    operation.Args[0]);
            }
            else if (operation.OpCode == Operation.OPCodes.NextHopReadResponse)
            {
                msgToPrint = String.Format("NEXTHOP READ FROM 0x{0:X4} -> ADDRESS: 0x{1:X4}   NEXTHOP: 0x{2:X4}   SCORE: {3}   LQI: {4}",
                    operation.SourceAddress,
                    ((ushort)operation.Args[1] << 8 | operation.Args[0]),
                    ((ushort)operation.Args[3] << 8 | operation.Args[2]),
                    operation.Args[4],
                    operation.Args[5]);
            }
            else if (operation.OpCode == Operation.OPCodes.PingResponse)
            {
                msgToPrint = String.Format("PING RESPONSE RECEIVED FROM 0x{0:X4}", operation.SourceAddress);
            }
            else if (operation.OpCode == Operation.OPCodes.JoinRequest)
            {
                msgToPrint = String.Format("JOIN REQUEST RECEIVED FROM 0x{0:X4}", operation.SourceAddress);
                SendJoinRequestResponse(operation.SourceAddress);
            }
            else if (operation.OpCode == Operation.OPCodes.JoinAbort)
            {
                msgToPrint = String.Format("JOIN ABORT RECEIVED FROM 0x{0:X4} -> NUMBER OF RESPONSES: {1}", operation.SourceAddress, operation.Args[0]);
            }
            else if (operation.OpCode == Operation.OPCodes.WakeUp)
            {
                msgToPrint = String.Format("WAKEUP RECEIVED FROM 0x{0:X4}", operation.SourceAddress);
            }
            else if (operation.OpCode == Operation.OPCodes.JoinAccept)
            {
                byte[] macAddress = operation.Args.Take(6).ToArray();
                byte[] aesKey = operation.Args.Skip(6).ToArray();

                StringBuilder sb = new StringBuilder();
                sb.AppendFormat("JOIN ACCEPT RECEIVED FROM 0x{0:X4} -> MAC: ", operation.SourceAddress);

                foreach (byte b in macAddress)
                {
                    sb.AppendFormat("0x{0:X2} ", b);
                }

                sb.Append("  AES-KEY: ");

                foreach (byte b in aesKey)
                {
                    sb.AppendFormat("0x{0:X2} ", b);
                }

                msgToPrint = sb.ToString();

                SendJoinAcceptResponse(operation.SourceAddress, macAddress, aesKey);
            }
            else if (operation.OpCode == Operation.OPCodes.ConfigReadResponse)
            {
                int fragment = operation.Args[0] & 0x0F;
                int totalFragment = (operation.Args[0] & 0xF0) >> 4;

                Operation lastOp = null;
                if (configReadBuffer.Count > 0)
                    lastOp = configReadBuffer.Last();

                if (lastOp == null || (lastOp != null && operation.Args[0] == lastOp.Args[0] + 1))
                {
                    if (operation.Args[0] == 0 && operation.Args[1] == 0) //ERROR
                    {
                        msgToPrint = String.Format("CONFIG READ RESPONSE FROM 0x{0:X4} -> BUSY SENDING CONFIG STATE",
                                               operation.SourceAddress);
                    }
                    else //OK
                    {
                        msgToPrint = String.Format("CONFIG READ RESPONSE FROM 0x{0:X4} -> {1}/{2} OK",
                            operation.SourceAddress,
                            fragment,
                            totalFragment);

                        configReadBuffer.Add(operation);
                        SendOperation(new Operation() { SourceAddress = 0x00, DestinationAddress = DestinationAddress, OpCode = Operation.OPCodes.ConfigReadConfirmation, Args = new byte[] { operation.Args[0], 0x00 } });

                        if (fragment == totalFragment)
                        {
                            SaveConfigFile();
                        }
                    }
                }
                else
                {
                    msgToPrint = String.Format("CONFIG READ RESPONSE FROM 0x{0:X4} -> {1}/{2} ERROR",
                        operation.SourceAddress,
                        fragment,
                        totalFragment);

                    //Send error code back
                    SendOperation(new Operation() { SourceAddress = 0x00, DestinationAddress = DestinationAddress, OpCode = Operation.OPCodes.ConfigReadConfirmation, Args = new byte[] { operation.Args[0], 0x01 } });
                }
            }
            else
            {
                msgToPrint = operation.ToString();
            }

            if (!string.IsNullOrWhiteSpace(msgToPrint))
                PrintMessage(String.Format("RSSI: {0} NEXTHOP: 0x{1:X4}     {2}", message.RSSI, message.NextHop, msgToPrint));
        }

        void serial_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            byte c;

            while (serial.BytesToRead > 0)
            {
                bool acceptByte = false;

                c = (byte)serial.ReadByte();

                switch (serialState)
                {
                    case SerialReceiverState.USART_RECEIVER_IDLE_RX_STATE:
                        if (APP_MAGIC_SYMBOL == c)
                        {
                            serialState = SerialReceiverState.USART_RECEIVER_SOF_RX_STATE;
                            currentMessage.Clear();
                            CheckSum = 0;
                        }
                        break;

                    case SerialReceiverState.USART_RECEIVER_SOF_RX_STATE:
                        if (SOF[1] == c)
                        {
                            serialState = SerialReceiverState.USART_RECEIVER_DATA_RX_STATE;
                        }
                        else
                        {
                            serialState = SerialReceiverState.USART_RECEIVER_IDLE_RX_STATE;
                        }
                        break;

                    case SerialReceiverState.USART_RECEIVER_DATA_RX_STATE:
                        if (APP_MAGIC_SYMBOL == c)
                        {
                            serialState = SerialReceiverState.USART_RECEIVER_MAGIC_RX_STATE;
                        }
                        else
                        {
                            acceptByte = true;
                        }
                        break;

                    case SerialReceiverState.USART_RECEIVER_MAGIC_RX_STATE:
                        if (APP_MAGIC_SYMBOL == c)
                        {
                            serialState = SerialReceiverState.USART_RECEIVER_DATA_RX_STATE;
                            acceptByte = true;
                        }
                        else if (EOF[1] == c)
                        {
                            serialState = SerialReceiverState.USART_RECEIVER_EOF_RX_STATE;
                        }
                        else
                        {
                            serialState = SerialReceiverState.USART_RECEIVER_IDLE_RX_STATE;
                            PrintMessage("TRAMA DEFECTUOSA");
                        }
                        break;

                    case SerialReceiverState.USART_RECEIVER_EOF_RX_STATE:
                        if (CheckSum == c)
                        {
                            serialState = SerialReceiverState.USART_RECEIVER_IDLE_RX_STATE;
                            InputMessage inputMessage = new InputMessage();
                            inputMessage.FromBinary(currentMessage.ToArray());

                            if (inputMessage.Content.Args != null)
                                OnOperationReceived(inputMessage);
                        }
                        else
                        {
                            serialState = SerialReceiverState.USART_RECEIVER_IDLE_RX_STATE;
                            PrintMessage("CHECKSUM INCORRECTO");
                        }
                        break;

                    default:
                        break;
                }

                CheckSum += c;

                if (acceptByte)
                {
                    currentMessage.Add(c);
                }
            }
        }

        #endregion

        private void trackBar1_Scroll(object sender, EventArgs e)
        {
            int value = ((TrackBar)sender).Value;

            if (value < 5)
                label3.Text = "OFF";
            else
                label3.Text = (int)this.Map(value, 0, 128, 0, 100) + "%";
        }

        private float Map(float value, float fromSource, float toSource, float fromTarget, float toTarget)
        {
            return (value - fromSource) / (toSource - fromSource) * (toTarget - fromTarget) + fromTarget;
        }
    }
}
