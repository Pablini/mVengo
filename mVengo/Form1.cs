using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
//using Invengo.NetAPI.Core;
using Core = Invengo.NetAPI.Core;
using IRP1 = Invengo.NetAPI.Protocol.IRP1;
//using Invengo.NetAPI.Protocol.IRP1;


// including the M2Mqtt Library
using uPLibrary.Networking.M2Mqtt;
using uPLibrary.Networking.M2Mqtt.Messages;

namespace Invengo101
{
    public partial class Form1 : Form
    {

        // MQTT Integration
        MqttClient client;
        string clientId;
        string Topic = "";

        //IRP1.Reader reader = new IRP1.Reader("Reader1", "RS232", "COM1,115200");//COM
        //        IRP1.Reader reader = new IRP1.Reader("Reader1", "TCPIP_Client", "192.168.1.210:7086");//TCP--
        //       IRP1.ReadTag scanMsg = new IRP1.ReadTag(IRP1.ReadTag.ReadMemoryBank.EPC_6C);//
        //        IRP1.ReadTag msg = new IRP1.ReadTag(IRP1.ReadTag.ReadMemoryBank.TID_6C);//--
        //        IRP1.ReadTag msg = new IRP1.ReadTag(IRP1.ReadTag.ReadMemoryBank.EPC_TID_UserData_6C);//
        Double[] list;
        
        IRP1.Reader reader;
        IRP1.ReadTag msg;
        public delegate void OnReaderErrorMsgHandle(IRP1.Reader reader, string errString);
//        public event OnReaderErrorMsgHandle OnReaderErrorMsg;
        public delegate void OnReaderNotificationMsgHandle(IRP1.Reader reader, NotificationMessage nMsg);
 //       public event OnReaderNotificationMsgHandle OnReaderNotificationMsg;

        bool isNewPower = false;
        public bool isTryReconnNet;
        public int tryReconnNetTimeSpan;
        object lockobj = new object();//
        public Form1()
        {
            InitializeComponent();
            IRP1.Reader.OnApiException += new Core.ApiExceptionHandle(Reader_OnApiException);

            cbReadMB.Items.Add("EPC_6C");
            cbReadMB.Items.Add("TID_6C");
            cbReadMB.Items.Add("EPC_TID_UserData_6C");
            cbReadMB.SelectedIndex = 0;

            cbReaderMode.Items.Add("Fast Read"); // 0x04
            cbReaderMode.Items.Add("Dense Reader M=2"); // 0x05
            cbReaderMode.Items.Add("Dense Reader M=4"); // Dense Reader M=4
            cbReaderMode.Items.Add("AutoSet"); // 0xff
            cbReaderMode.SelectedIndex = 0;

            cbSession.Items.Add("0");
            cbSession.Items.Add("1");
            cbSession.Items.Add("2");
            cbSession.Items.Add("3");
            cbSession.SelectedIndex = 0;

            cbFlag.Items.Add("Single Inv A");
            cbFlag.Items.Add("Single Inv B");
            cbFlag.Items.Add("Double Inv A<->B");
            cbFlag.SelectedIndex = 0;

            cbGPO1.Items.Add("Low");
            cbGPO1.Items.Add("High");
            cbGPO1.SelectedIndex = 0;

            cbGPO2.Items.Add("Low");
            cbGPO2.Items.Add("High");
            cbGPO2.SelectedIndex = 0;

            cbGPO3.Items.Add("Low");
            cbGPO3.Items.Add("High");
            cbGPO3.SelectedIndex = 0;

            cbGPO4.Items.Add("Low");
            cbGPO4.Items.Add("High");
            cbGPO4.SelectedIndex = 0;

            cbGPITrigger.SelectedIndex = 0;
            cbTrigger.SelectedIndex = 3;

            btn_Disconnect.Enabled = false;
            btn_Stop.Enabled = false;
            btn_Stop.Enabled = false;
        }

        private void Form1_Load(object sender, EventArgs e)
        {

        }

        private void FormMain_FormClosing(object sender, FormClosingEventArgs e)
        {
            Environment.Exit(Environment.ExitCode);
        }

        private void btn_Connect_Click(object sender, EventArgs e)
        {
            reader = new IRP1.Reader("Reader1", "TCPIP_Client", tbIPconnect.Text + ":7086");//TCP
            msg = new IRP1.ReadTag(IRP1.ReadTag.ReadMemoryBank.EPC_6C);//
                        
            {


                // Added MQTT COde
                string BrokerAddress = "localhost";
                Topic = txtTopic.Text;
                if (Topic == "") Topic = "rfidX";
                client = new MqttClient(BrokerAddress);
                clientId = Guid.NewGuid().ToString();
                client.Connect(clientId);
                // subscribe to the topic with QoS 2
                //client.Subscribe(new string[] { Topic }, new byte[] { 2 });   // we need arrays as parameters because we can subscribe to different topics with one call
                //return; // For Debugging




                if (reader.Connect())
                {
                    reader.OnMessageNotificationReceived += new Invengo.NetAPI.Core.MessageNotificationReceivedHandle(reader_OnMessageNotificationReceived);
                    lbl_msg.Text = "Conn successful";

                    btn_Disconnect.Enabled = true;
                    btn_Connect.Enabled = false;
                    btn_ReadEPC.Enabled = true;
                    changeCtrlEnable("conn");
                    //btnQuery.PerformClick();
                    //btnModelSN.PerformClick();
                    //btn_Query.PerformClick();
                    //btnAntQuery.PerformClick();
                    //btnQueryRF.PerformClick();



                }
                else
                {
                    lbl_msg.Text = "Conn failed";
                    MessageBox.Show("Conn failed");
                }

            }

        }

        private void btn_Disconnect_Click(object sender, EventArgs e)
        {
            if (reader != null)
            {
                reader.OnMessageNotificationReceived -= new Invengo.NetAPI.Core.MessageNotificationReceivedHandle(reader_OnMessageNotificationReceived);
                reader.Disconnect();
                btn_Disconnect.Enabled = false;
                btn_Connect.Enabled = true;
                btn_ReadEPC.Enabled = false;
                changeCtrlEnable("disconn");

                // MQTT Disconnect
                client.Disconnect();
            }
            lbl_msg.Text = "disconn";

        }

        private void btn_ReadEPC_Click(object sender, EventArgs e)
        {
            lst_data.Items.Clear();


// enable RSSI to be sent in data stream

            Byte[] rssiConfig = new Byte[1];
            rssiConfig[0] = 0x01;//0x00：disable，0x01:enable
            IRP1.SysConfig_800 order = new IRP1.SysConfig_800(0x14, rssiConfig);//RSSI:0x14
            if (reader.Send(order))
                lbl_msg.Text = "RSSI enabled";
            else
            {
                lbl_msg.Text = "RSSI disabled";
            }

// enable reader UTC to be sent in data stream
            Byte[] utcConfig = new Byte[1];
            utcConfig[0] = 0x01;//0x00：disable，0x01:enable
            IRP1.SysConfig_800 order1 = new IRP1.SysConfig_800(0x18, utcConfig);
            if (reader.Send(order1))
                lbl_msg.Text = lbl_msg.Text + "UTC enabled";
            else
            {
                lbl_msg.Text = lbl_msg.Text + "UTC disabled";
            }



            if (reader != null && reader.IsConnected)
            {
                if (reader.Send(msg))
                    
                {
                    lbl_msg.Text = lbl_msg.Text + "scan.";
                    btn_Stop.Enabled = true;
                    btn_ReadEPC.Enabled = false;

                }
            }
        }

        private void btn_Stop_Click(object sender, EventArgs e)
        {
            if (reader != null && reader.IsConnected)
            {
                if (reader.Send(new IRP1.PowerOff()))
                {
                    lbl_msg.Text = "stop";
                    btn_Stop.Enabled = false;
                    btn_ReadEPC.Enabled = true;
                }
            }

        }


        void Reader_OnApiException(Core.ErrInfo e)
        {
            if (e.Ei.ErrCode == "FF22")
            {
                showMsg(e.Ei.ErrMsg);
            }
            else if (e.Ei.ErrCode == "FF24")//
            {
                showMsg(e.Ei.ErrMsg);
            }
        }

        static string lastEPC="XX";

        bool SendMQTT(IRP1.RXD_TagData msg)
        {
            //showMsg(); -- Desplega Mensajes
            string payload = "";
            string Antenna = "99";
            string epc = "XY";

            lock (lockobj)
            {
                epc = Core.Util.ConvertByteArrayToHexString(msg.ReceivedMessage.EPC);
                //string tid = Core.Util.ConvertByteArrayToHexString(msg.ReceivedMessage.TID);
                //string userdata = Core.Util.ConvertByteArrayToHexString(msg.ReceivedMessage.UserData);
                //string UTC = Core.Util.ConvertByteArrayToHexString(msg.ReceivedMessage.RXDTime);
                Antenna = msg.ReceivedMessage.Antenna.ToString();
                String nowString = DateTime.Now.ToString("yyyy-MM-dd|HH:mm:ss,fff");// you can use this if you want the time from the Host
                //string rxdTime = ReadTimeToString(msg.ReceivedMessage.RXDTime);
                //nowString = rxdTime;

                Byte[] bRssi = msg.ReceivedMessage.RSSI;
                string rssi = "";
                rssi = bRssi[0].ToString("X2"); // X2 displays in HEX

                payload = nowString + "|"+ kutil.getRNC(epc) + "|" + "NONE" + "|" + rssi;
                // payload = kutil.getRNC(epc) + "|" + tid + "|" + userdata + "|RSSI:" + rssi + "|UTC: " + nowString + "|Ant: " + Antenna;
            }

            //string topic = "rfid" + Antenna;

            // Deduplication Routine
            if (epc == lastEPC) return false;
            else lastEPC = epc;

            //Send to MQTT
            client.Publish(Topic, Encoding.UTF8.GetBytes(payload), MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE, true);
            
            return true;
        }
        
        void reader_OnMessageNotificationReceived(Invengo.NetAPI.Core.BaseReader reader, Invengo.NetAPI.Core.IMessageNotification msg)
        {

           // String nowString = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
//            MessageBox.Show(nowString); //brett
           // String utcString = nowString;

            if (msg.StatusCode != 0)
            {
                showMsg(msg.ErrInfo);
                return;
            }
            String msgType = msg.GetMessageType();
            msgType = msgType.Substring(msgType.LastIndexOf('.') + 1);
            switch (msgType)
            {
                #region RXD_TagData
                case "RXD_TagData":
                    {
                        IRP1.RXD_TagData m = (IRP1.RXD_TagData)msg;
                        string rssi = m.ReceivedMessage.RSSI[0].ToString();//RSSI
                        SendMQTT(m); // Sends to MQTT
                        display(m); // Local Display
                    }
                    break;
                #endregion
                #region RXD_IOTriggerSignal_800
                case "RXD_IOTriggerSignal_800":
                    {
                        IRP1.RXD_IOTriggerSignal_800 m = (IRP1.RXD_IOTriggerSignal_800)msg;
                        if (m.ReceivedMessage.IsStart)
                        {
                            changeCtrlEnable("scan");
                           // lbl_msg.Text = " Reading";
                           // btn_Stop.Enabled = true;
                           // btn_ReadEPC.Enabled = false;
                        }
                        else
                        {
                            changeCtrlEnable("conn");
                            //lbl_msg.Text = " stop";
                            //btn_Stop.Enabled = false;
                            //btn_ReadEPC.Enabled = true;

                        }
                    }
                    break;
                #endregion
            }
        }

        
        private void changeCtrlEnable(string state)
        {
            if (this.InvokeRequired)
            {
                changeCtrlEnableHandle h = new changeCtrlEnableHandle(changeCtrlEnableMethod);
                this.BeginInvoke(h, state);
            }
            else
                changeCtrlEnableMethod(state);
        }

        private delegate void changeCtrlEnableHandle(string state);

        private void changeCtrlEnableMethod(string state)
        {
            switch (state)
            {
                case "conn":
                    btn_Stop.Enabled = false;
                    btn_ReadEPC.Enabled = true;
                    gbReaderIP.Enabled = true;
                    gbReaderInfo.Enabled = true;
                    gbAntennaConfig.Enabled = true;
                    gbRFSettings.Enabled = true;
                    gbIOSettings.Enabled = true;
                    gbIOTrigger.Enabled = true;

                    break;
                case "disconn":
                    gbReaderIP.Enabled = false;
                    gbReaderInfo.Enabled = false;
                    gbAntennaConfig.Enabled = false;
                    gbRFSettings.Enabled = false;
                    gbIOSettings.Enabled = false;
                    gbIOTrigger.Enabled = false;

                    
                    break;
                case "scan":
                    btn_Stop.Enabled = true;
                    btn_ReadEPC.Enabled = false;
                    break;
            }
        }



        private delegate void showMsgHandle(string str);

        private void showMsg(string str)
        {
            if (this.InvokeRequired)
            {
                showMsgHandle h = new showMsgHandle(showMsgMethod);
                this.BeginInvoke(h, str);
            }
            else
            {
                showMsgMethod(str);
            }
        }

        private void showMsgMethod(string str)
        {
            lbl_msg.Text = str;
        }

        private delegate void displayHandle(IRP1.RXD_TagData msg);

        private void display(IRP1.RXD_TagData msg)
        {
            if (this.InvokeRequired)
            {
                displayHandle h = new displayHandle(displayMethod);
                lst_data.BeginInvoke(h, msg);
  
            }
            else
                displayMethod(msg);
        }

        private void displayMethod(IRP1.RXD_TagData msg)
        {
            lock (lockobj)
            {
                string epc = Core.Util.ConvertByteArrayToHexString(msg.ReceivedMessage.EPC);
                string tid = Core.Util.ConvertByteArrayToHexString(msg.ReceivedMessage.TID);
                string userdata = Core.Util.ConvertByteArrayToHexString(msg.ReceivedMessage.UserData);
                string UTC = Core.Util.ConvertByteArrayToHexString(msg.ReceivedMessage.RXDTime);
                string Antenna = msg.ReceivedMessage.Antenna.ToString();
                String nowString = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");// you can use this if you want the time from the Host
                string rxdTime = ReadTimeToString(msg.ReceivedMessage.RXDTime);
                nowString = rxdTime;

                Byte[] bRssi = msg.ReceivedMessage.RSSI;
                string rssi = "";
                rssi = bRssi[0].ToString("X2"); // X2 displays in HEX

                lst_data.Items.Add(epc + " " + tid + " " + userdata + " RSSI:" + rssi + " UTC: " + nowString + " Ant: " + Antenna);
            }
        }

        private void lst_data_SelectedIndexChanged(object sender, EventArgs e)
        {

        }

        private void cbAnt4_CheckedChanged(object sender, EventArgs e)
        {

        }

        private void btnAntQuery_Click(object sender, EventArgs e)
        {
            // Brett: Query successfully gets the antenna power and Q value
            // it does not get the selected antenna ports, memory bank or continuous or single read status.
            Boolean isSuc = false;
                        
            IRP1.SysQuery_800 order = new IRP1.SysQuery_800(0x68, 0x00);
            if (reader.Send(order))
            {
                isSuc = true;
                isNewPower = true;

                list = new Double[order.ReceivedMessage.QueryData.Length];
                for (int i = 0; i < list.Length; i++)
                {
                    list[i] = (Double)i;
                }
                
                // query power
                IRP1.SysQuery_800 order1 = new IRP1.SysQuery_800(0x65, 0x00);// power param
                if (reader.Send(order1))
                {
                    this.nudAnt1.Text = list[order1.ReceivedMessage.QueryData[0]].ToString();
                    this.nudAnt2.Text = list[order1.ReceivedMessage.QueryData[1]].ToString();
                    this.nudAnt3.Text = list[order1.ReceivedMessage.QueryData[2]].ToString();
                    this.nudAnt4.Text = list[order1.ReceivedMessage.QueryData[3]].ToString();
                }

                IRP1.TagOperationQuery_6C msg = new IRP1.TagOperationQuery_6C(0x10);
                if (reader.Send(msg))
                {
                    
                    numQ.Value = msg.ReceivedMessage.QueryData[0];
                }
                else
                {
                    MessageBox.Show("Error getting Q");
                }

            }
        }

        private void btnApplyTxPwr_Click(object sender, EventArgs e)
        {
            #region Configure Antenna 1;
            if (nudAnt1.Text != string.Empty)
            {
                lbl_msg.Text = "";
                String strSuc1 = "";
                String strFai1 = "";
                Byte[] aData = new Byte[2];
                aData[0] = 0x00;//Antenna #1
                aData[1] = (Byte)Convert.ToInt16(nudAnt1.Text);
                IRP1.SysConfig_800 order = new IRP1.SysConfig_800(0x65, aData);
                if (reader.Send(order))
                    strSuc1 += "1,";
                else
                    strFai1 += "1,";
                if (strSuc1 == "1,")
                {
                    lbl_msg.Text = lbl_msg.Text + "Ant1 Success, ";
                }
                else lbl_msg.Text = lbl_msg.Text + "Ant1 Fail, ";
            }
            #endregion

            
            #region Configure Antenna 2;
            if (nudAnt2.Text != string.Empty)
            {
                String strSuc = "";
                String strFai = "";
                Byte[] aData = new Byte[2];
                aData[0] = 0x01;//Antenna #2
                aData[1] = (Byte)Convert.ToInt16(nudAnt2.Text);
                IRP1.SysConfig_800 order = new IRP1.SysConfig_800(0x65, aData);
                if (reader.Send(order))
                    strSuc += "1,";
                else
                    strFai += "1,";
                if (strSuc == "1,")
                {
                    lbl_msg.Text = lbl_msg.Text + "Ant2 Success, ";
                }
                else lbl_msg.Text = lbl_msg.Text + "Ant2 Fail, ";
                #endregion
            }


                #region Configure Antenna 3;
            if (nudAnt3.Text != string.Empty)
            {

                String strSuc = "";
                String strFai = "";
                Byte[] aData = new Byte[2];
                aData[0] = 0x02;//Antenna #3
                aData[1] = (Byte)Convert.ToInt16(nudAnt3.Text);
                IRP1.SysConfig_800 order = new IRP1.SysConfig_800(0x65, aData);
                if (reader.Send(order))
                    strSuc += "1,";
                else
                    strFai += "1,";
                if (strSuc == "1,")
                {
                    lbl_msg.Text = lbl_msg.Text + "Ant3 Success, ";
                }
                else lbl_msg.Text = lbl_msg.Text + "Ant3 Fail, ";
                #endregion
            }

            #region Configure Antenna 4;
            if (nudAnt4.Text != string.Empty)
            {

                String strSuc = "";
                String strFai = "";
                Byte[] aData = new Byte[2];
                aData[0] = 0x03;//Antenna #4
                aData[1] = (Byte)Convert.ToInt16(nudAnt4.Text);
                IRP1.SysConfig_800 order = new IRP1.SysConfig_800(0x65, aData);
                if (reader.Send(order))
                    strSuc += "1,";
                else
                    strFai += "1,";
                if (strSuc == "1,")
                {
                    lbl_msg.Text = lbl_msg.Text + "Ant4 Success, ";
                }
                else lbl_msg.Text = lbl_msg.Text + "Ant4 Fail, ";
            #endregion
            }


            #region Configure Memory Bank, Antenna, Read Method and Q value
            IRP1.ReadTag.ReadMemoryBank rmb = (IRP1.ReadTag.ReadMemoryBank)Enum.Parse(typeof(IRP1.ReadTag.ReadMemoryBank), cbReadMB.Items[cbReadMB.SelectedIndex].ToString());
            msg = new IRP1.ReadTag(rmb);
//            MessageBox.Show("Q = " + msg.Q.ToString() + "  Is loop = " +  msg.IsLoop.ToString());
            byte a = 0x80;//
            if (cbAnt1.Checked)
                a += 0x01;
            if (cbAnt2.Checked)
                a += 0x02;
            if (cbAnt3.Checked)
                a += 0x04;
            if (cbAnt4.Checked)
                a += 0x08;
            msg.Antenna = a;
            msg.IsLoop = (rbContinuous.Checked) ? true : false;
            msg.Q = (byte)numQ.Value;

//set Q
                            byte[] bs = new byte[1];
                            bs[0] = (Byte)numQ.Value;
                            IRP1.TagOperationConfig_6C msg1 = new IRP1.TagOperationConfig_6C(0x10, bs);
                            if (reader.Send(msg1))
                                lbl_msg.Text = lbl_msg.Text + "Q = " + numQ.Value.ToString();
                            else
                                lbl_msg.Text = lbl_msg.Text + "Error Setting Q";
            
            


#endregion

        }

        private void gbAntennaConfig_Enter(object sender, EventArgs e)
        {

        }

        private void btnQuery_Click(object sender, EventArgs e)
        {
            IRP1.SysQuery_800 msg = new IRP1.SysQuery_800(0x06);
            if (reader.Send(msg))
            {
                getIP(msg.ReceivedMessage.QueryData);
            }
            else
            {
               gbReaderIP.Enabled = false;
            }
        }

        void getIP(Byte[] data)
        {
            if (data.Length < 12) return;
            // 
            String ip = data[0].ToString() + "."
                + data[1].ToString() + "."
                + data[2].ToString() + "."
                + data[3].ToString();
            String subnet = data[4].ToString() + "."
                + data[5].ToString() + "."
                + data[6].ToString() + "."
                + data[7].ToString();
            String gateway = data[8].ToString() + "."
                + data[9].ToString() + "."
                + data[10].ToString() + "."
                + data[11].ToString();
            // 
            this.tbIP.Text = ip;
            this.tbSubnet.Text = subnet;
            this.tbGateway.Text = gateway;

        }

        private void btnSetIP_Click(object sender, EventArgs e)
        {
            if (!isIP(tbIP.Text.Trim())
              || !isIP(tbSubnet.Text.Trim())
              || !isIP(tbGateway.Text.Trim()))
            {
                MessageBox.Show("IP is invalid");
                return;
            }

            #region IP
            Byte[] ipData = new Byte[12];
            int p = 0;

            String[] aryip = tbIP.Text.Split('.');
            foreach (String str in aryip)
            {
                ipData[p] = (Byte)int.Parse(str);
                p++;
            }
            aryip = tbSubnet.Text.Split('.');
            foreach (String str in aryip)
            {
                ipData[p] = (Byte)int.Parse(str);
                p++;
            }
            aryip = tbGateway.Text.Split('.');
            foreach (String str in aryip)
            {

                ipData[p] = (Byte)int.Parse(str);
                p++;
            }
            #endregion

            IRP1.SysConfig_800 order = new IRP1.SysConfig_800(
                0x06,//config ip
                ipData);
            if (reader.Send(order))
            {
                MessageBox.Show("Config successful");
            }
            else
            {
                MessageBox.Show("Config failed");
            }
        }

        public bool isIP(String ip)
        {
            //
            return System.Text.RegularExpressions.Regex.IsMatch(ip, @"^((2[0-4]\d|25[0-5]|[01]?\d\d?)\.){3}(2[0-4]\d|25[0-5]|[01]?\d\d?)$");
        }

        private void btnSetReconnect_Click(object sender, EventArgs e)
        {
          

        }

        private void btn_Query_Click(object sender, EventArgs e)
        {
            IRP1.Gpi_800 order = new IRP1.Gpi_800();
            if (reader.Send(order, 1000))
            {
                Byte b = order.ReceivedMessage.QueryData[0];
                Byte c = order.ReceivedMessage.QueryData[1];

                if ((b & 0x01).ToString() == "0")
                    tbGPI1.Text = "Low";
                else
                    tbGPI1.Text = "High";

                if ((b & 0x02).ToString() == "0")
                    tbGPI2.Text = "Low";
                else
                    tbGPI2.Text = "High";

            }

            
            
            /*
                textBox8.Text = comboBox7.Items[(b & 0x02) >> 1].ToString();
                textBox9.Text = comboBox5.Items[b & 0x01].ToString();
                if (textBox2.Enabled)
                    textBox2.Text = comboBox10.Items[(b & 0x04) >> 2].ToString();
                if (textBox1.Enabled)
                    textBox1.Text = comboBox9.Items[(b & 0x08) >> 3].ToString();
            }
            else
            {
                groupBox2.Enabled = false;
            }
            */
        }

        private void btnSetRF_Click(object sender, EventArgs e)
        {

            #region Set reader mode;
            byte b = 0x00;
            if (cbReaderMode.Text == "Fast Read")
            {
                b = 0x04;
            }
            else if (cbReaderMode.Text == "Dense Reader M=2")
            {
                b = 0x05;
            }
            else if (cbReaderMode.Text == "Dense Reader M=4")
            {
                b = 0x02;
            }
            else if (cbReaderMode.Text == "AutoSet")
            {
                b = 0xff;
            }
            else
            {
                MessageBox.Show("Error setting mode = default to Fast Read");
                b = 0x04;
            }
            lbl_msg.Text = "";

            IRP1.SysConfig_800 order = new IRP1.SysConfig_800((Byte)0x19, new Byte[] { b });
            if (reader.Send(order))
                lbl_msg.Text = lbl_msg.Text + "Reader Mode = " + cbReaderMode.Text;
            else
                lbl_msg.Text = lbl_msg.Text + "Error setting Reader Mode";
            #endregion

            #region Set Session and Inventory Flag
            byte[] bs = new byte[2];
            bs[0] = (Byte)cbSession.SelectedIndex;
            bs[1] = (Byte)cbFlag.SelectedIndex;
            IRP1.TagOperationConfig_6C order1 = new IRP1.TagOperationConfig_6C(0x12, bs);
            if (reader.Send(order1))
                lbl_msg.Text = lbl_msg.Text + " Session = " + cbSession.Text + " Flag = " + cbFlag.Text;
            else
                lbl_msg.Text = lbl_msg.Text + "Error setting Session and Inventory Flag";
            #endregion
//            MessageBox.Show(cbSession.SelectedIndex.ToString() + " " + cbFlag.SelectedIndex.ToString());

        }

        private void btnQueryRF_Click(object sender, EventArgs e)
        {
            #region reader mode
            {
                IRP1.SysQuery_800 msg = new IRP1.SysQuery_800(0x19);
                if (reader.Send(msg))
                {
                    switch (msg.ReceivedMessage.QueryData[0])
                    {
                        case 0x04:
                            cbReaderMode.SelectedIndex = 0;
                            break;
                        case 0x05:
                            cbReaderMode.SelectedIndex = 1;
                            break;
                        case 0x02:
                            cbReaderMode.SelectedIndex = 2;
                            break;
                        case 0xff:
                            cbReaderMode.SelectedIndex = 3;
                            break;
                        case 0x09:
                            cbReaderMode.SelectedIndex = 4;
                            break;
                        default:
                            cbReaderMode.SelectedIndex = -1;
                            break;
                    }
                    cbReaderMode.Enabled = true;
                }
                else
                    cbReaderMode.Enabled = false;
            }
            #endregion

            #region Session
            {
                IRP1.TagOperationQuery_6C msg = new IRP1.TagOperationQuery_6C(0x12);
                if (reader.Send(msg))
                {
                    // query session
                    cbSession.SelectedIndex = msg.ReceivedMessage.QueryData[0];
                    // query inventory flag
                    cbFlag.SelectedIndex = msg.ReceivedMessage.QueryData[1];
            //        MessageBox.Show(msg.ReceivedMessage.QueryData[0].ToString() + " " + msg.ReceivedMessage.QueryData[1].ToString());
                }
                else
                {
                    MessageBox.Show("Query Error - Session and Flag");
                }
            }
            #endregion


        }

        private void btnModelSN_Click(object sender, EventArgs e)
        {
            tbModelNum.Text = reader.ModelNumber;
            tbPortType.Text = reader.PortType;
            tbReaderName.Text = reader.ReaderName;
            tbProtocolVersion.Text = reader.ProtocolVersion;
            IRP1.SysQuery_800 msg = new IRP1.SysQuery_800(0x27); //returns serial number
            /*
            IRP1.SysQuery_800 msg = new IRP1.SysQuery_800(0x27); // returns serial number
            IRP1.SysQuery_800 msg = new IRP1.SysQuery_800(0x26); // returns 1.10
            IRP1.SysQuery_800 msg = new IRP1.SysQuery_800(0x25); // returns 1.10
            IRP1.SysQuery_800 msg = new IRP1.SysQuery_800(0x24); // returns nothing
            IRP1.SysQuery_800 msg = new IRP1.SysQuery_800(0x23); // returns 3.34
            IRP1.SysQuery_800 msg = new IRP1.SysQuery_800(0x22); // returns odd characters
            IRP1.SysQuery_800 msg = new IRP1.SysQuery_800(0x21); // returns 8861CA
            IRP1.SysQuery_800 msg = new IRP1.SysQuery_800(0x20); // returns XC-RF861
            IRP1.SysQuery_800 msg = new IRP1.SysQuery_800(0x19); // returns ascii character
            IRP1.SysQuery_800 msg = new IRP1.SysQuery_800(0x18); // returns blank
            IRP1.SysQuery_800 msg = new IRP1.SysQuery_800(0x17); // returns blank
            IRP1.SysQuery_800 msg = new IRP1.SysQuery_800(0x16); // returns blank
            IRP1.SysQuery_800 msg = new IRP1.SysQuery_800(0x15); // returns blank
            IRP1.SysQuery_800 msg = new IRP1.SysQuery_800(0x14); // returns blank
            IRP1.SysQuery_800 msg = new IRP1.SysQuery_800(0x13); // returns nothing
            IRP1.SysQuery_800 msg = new IRP1.SysQuery_800(0x12); // returns blank
            IRP1.SysQuery_800 msg = new IRP1.SysQuery_800(0x11); // returns nothning
            IRP1.SysQuery_800 msg = new IRP1.SysQuery_800(0x10); // returns ascii
            IRP1.SysQuery_800 msg = new IRP1.SysQuery_800(0x09); // returns nothing 
            IRP1.SysQuery_800 msg = new IRP1.SysQuery_800(0x08); // returns blank
            IRP1.SysQuery_800 msg = new IRP1.SysQuery_800(0x07); // returns blank
            IRP1.SysQuery_800 msg = new IRP1.SysQuery_800(0x06); // returns ????
            IRP1.SysQuery_800 msg = new IRP1.SysQuery_800(0x05); // returns blank
            IRP1.SysQuery_800 msg = new IRP1.SysQuery_800(0x04); // returns 
            IRP1.SysQuery_800 msg = new IRP1.SysQuery_800(0x03); // returns ascii
            IRP1.SysQuery_800 msg = new IRP1.SysQuery_800(0x02); // returns blank?
            IRP1.SysQuery_800 msg = new IRP1.SysQuery_800(0x01); // returns blank
            IRP1.SysQuery_800 msg = new IRP1.SysQuery_800(0x00); // returns odd character
             
             * 
            */
            if (reader.Send(msg))
            {

                string sn = Encoding.ASCII.GetString(msg.ReceivedMessage.QueryData);
                if (sn.Length > 18)
                    sn = sn.Substring(0, 18);
                tbSN.Text = sn;
            }

        }

        private void button1_Click(object sender, EventArgs e)
        {

            
        }



        private void cbReadMB_SelectedIndexChanged(object sender, EventArgs e)
        {

        }

        private void groupBox1_Enter(object sender, EventArgs e)
        {

        }

        private void btn_Config_Click(object sender, EventArgs e)
        {

            Byte d1 = (Byte)cbGPO1.SelectedIndex;
            IRP1.Gpo_800 msg1 = new IRP1.Gpo_800(0x01, d1);
            if (reader.Send(msg1))
                lbl_msg.Text = "GPO1 set";
            else
                lbl_msg.Text = "Error setting GPO1";



            Byte d2 = (Byte)cbGPO2.SelectedIndex;
            IRP1.Gpo_800 msg2 = new IRP1.Gpo_800(0x02, d2);
            if (reader.Send(msg2))
                lbl_msg.Text = lbl_msg.Text + " GPO2 set";
            else
                lbl_msg.Text = lbl_msg.Text + " Error setting GPO2";


            Byte d3 = (Byte)cbGPO3.SelectedIndex;
            IRP1.Gpo_800 msg3 = new IRP1.Gpo_800(0x03, d3);
            if (reader.Send(msg3))
                lbl_msg.Text = lbl_msg.Text + " GPO3 set";
            else
                lbl_msg.Text = lbl_msg.Text + " Error setting GPO3";


            Byte d4 = (Byte)cbGPO4.SelectedIndex;
            IRP1.Gpo_800 msg4 = new IRP1.Gpo_800(0x04, d4);
            if (reader.Send(msg4))
                lbl_msg.Text = lbl_msg.Text + " GPO4 set";
            else
                lbl_msg.Text = lbl_msg.Text + " Error setting GPO4";


        }

        private void btnIOTriggerConfig_Click(object sender, EventArgs e)
        {
            // Brett: This code isn't working yet.  can't figure out where IMessage is coming from.//
            
            Core.IMessage msg1 = this.msg;
            Core.IMessage msg2 = new IRP1.PowerOff_800();

            msg1.PortType = "";
            byte[] msg1Buff = msg1.TransmitterData;
            msg2.PortType = "";
            byte[] msg2Buff = msg2.TransmitterData;
            byte[] pData = new byte[5 + msg1Buff.Length + msg2Buff.Length];
            pData[0] = (byte)(cbGPITrigger.SelectedIndex + 1);
            pData[1] = (byte)(cbTrigger.SelectedIndex);
            pData[2] = (byte)(radioButton1.Checked ? 0 : 1);
            pData[3] = (byte)(numTime.Value / 256);
            pData[4] = (byte)(numTime.Value % 256);
            Array.Copy(msg1Buff, 0, pData, 5, msg1Buff.Length);
            Array.Copy(msg2Buff, 0, pData, 5 + msg1Buff.Length, msg2Buff.Length);
            IRP1.SysConfig_800 msg = new IRP1.SysConfig_800(0xE2, pData);
            if (reader.Send(msg))
            {
                MessageBox.Show("Config successful");
            }
            else
            {
                MessageBox.Show("Config failed");
            }
            
        }

        private void btnClear_Click(object sender, EventArgs e)
        {
            lst_data.Items.Clear();
        }

        private void btnIOTriggerQuery_Click(object sender, EventArgs e)
        {

        }

        public class NotificationMessage : EventArgs
        {
            string rName;
            public string ReaderName
            {
                get { return rName; }
                set { rName = value; }
            }

            string tagType;
            public string TagType
            {
                get { return tagType; }
                set { tagType = value; }
            }

            string epc;
            public string EPC
            {
                get { return epc; }
                set { epc = value; }
            }

            string tid;
            public string TID
            {
                get { return tid; }
                set { tid = value; }
            }

            string ud;
            public string Userdata
            {
                get { return ud; }
                set { ud = value; }
            }

            string reserved;
            public string Reserved
            {
                get { return reserved; }
                set { reserved = value; }
            }

            string rssi;
            public string RSSI
            {
                get { return rssi; }
                set { rssi = value; }
            }

            byte ant;
            public byte Antenna
            {
                get { return ant; }
                set { ant = value; }
            }

            string nowString;
            public string ReadTime
            {
                get { return nowString; }
                set { nowString = value; }
            }

            public NotificationMessage(
                string rName,
                string tagType,
                string epc,
                string tid,
                string ud,
                string reserved,
                string rssi,
                byte ant,
                string nowString)
            {
                this.rName = rName;
                this.tagType = tagType;
                this.epc = epc;
                this.tid = tid;
                this.ud = ud;
                this.reserved = reserved;
                this.rssi = rssi;
                this.ant = ant;
                this.nowString = nowString;
            }
        }

        public static String ReadTimeToString(Byte[] readTime)
        {
            String str = "";
            if (readTime == null)
                return "";
            if (readTime.Length == 6)
            {
                str = "20" + readTime[0].ToString("X2") + "-"
                    + readTime[1].ToString("X2") + "-"
                    + readTime[2].ToString("X2") + " "
                    + readTime[3].ToString("X2") + ":"
                    + readTime[4].ToString("X2") + ":"
                    + readTime[5].ToString("X2") + "(BCD)";
            }
            else if (readTime.Length == 8)
            {
                DateTime dt = DateTime.Parse("1970-01-01").
                    AddSeconds((readTime[0] << 24) + (readTime[1] << 16) + (readTime[2] << 8) + readTime[3]);
                str = dt.ToString("yyyy-MM-dd HH:mm:ss");
                UInt32 ms = ((UInt32)((readTime[4] << 24) + (readTime[5] << 16) + (readTime[6] << 8) + (readTime[7])) / 1000);
                if (ms < 1000)
                    str += "," + ms.ToString().PadLeft(3, '0') + "(UTC)";
                else
                    str = "";
            }
            return str;
        }

        private void pictureBox1_Click(object sender, EventArgs e)
        {

        }

        private void button1_Click_1(object sender, EventArgs e)
        {
            client.Publish("rfid1", Encoding.UTF8.GetBytes("This is a Test from Invengo"), MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE, true);
        }

        private void label31_Click(object sender, EventArgs e)
        {

        }
    }
}

