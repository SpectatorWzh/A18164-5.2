using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Sockets;
using System.Threading;
using System.Net;
using System.Drawing;
using System.Globalization;
using System.Data.SqlClient;
using System.Data;

namespace AGVSystem
{
    class Cls_A18164BtnClient
    {
        static string ConnectStr = "";
        public string connectStr
        {
            get { return ConnectStr; }
            set { ConnectStr = value; }
        }
        public string IPAdress;
        public bool connected = false;
        public Socket clientSocket;
        private IPEndPoint hostEndPoint;
        private Byte[] SendDataPro;
        private Byte[] RecvDataPro;
        private Byte[] SendData;
        private Byte[] RecvData;
        private Byte SendOrRecv;
        private AutoResetEvent autoConnectEvent = new AutoResetEvent(false);
        private SocketAsyncEventArgs lisnterSocketAsyncEventArgs;
        ClsDBConn_sql db = new ClsDBConn_sql();
        public delegate void StartListeHandler();
        public event StartListeHandler StartListen;

        public delegate void ReceiveMsgHandler(byte[] info);
        public event ReceiveMsgHandler OnMsgReceived;

        private List<SocketAsyncEventArgs> s_lst = new List<SocketAsyncEventArgs>();

        public Cls_A18164BtnClient(string hostName)
        {
            IPAdress = hostName;
            IPAddress[] hostAddresses = Dns.GetHostAddresses(hostName);
            this.hostEndPoint = new IPEndPoint(hostAddresses[hostAddresses.Length - 1], 8899);
            this.clientSocket = new Socket(this.hostEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
        }
        /// <summary>
        /// 连接服务端
        /// </summary>
        /// <returns></returns>
        private bool Connect()
        {
            using (SocketAsyncEventArgs args = new SocketAsyncEventArgs())
            {
                args.UserToken = this.clientSocket;
                args.RemoteEndPoint = this.hostEndPoint;
                args.Completed += new EventHandler<SocketAsyncEventArgs>(this.OnConnect);
                this.clientSocket.ConnectAsync(args);
                bool flag = autoConnectEvent.WaitOne(1000);
                //SocketError err = args.SocketError;
                if (this.connected)
                {
                    this.lisnterSocketAsyncEventArgs = new SocketAsyncEventArgs();
                    byte[] buffer = new byte[50];
                    this.lisnterSocketAsyncEventArgs.UserToken = this.clientSocket;
                    this.lisnterSocketAsyncEventArgs.SetBuffer(buffer, 0, buffer.Length);
                    this.lisnterSocketAsyncEventArgs.Completed += new EventHandler<SocketAsyncEventArgs>(this.OnReceive);
                    this.StartListen();
                    return true;
                }
                return false;
            }
        }
        /// <summary>
        /// 判断有没有连接上
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnConnect(object sender, SocketAsyncEventArgs e)
        {
            this.connected = (e.SocketError == SocketError.Success);
            autoConnectEvent.Set();
        }
        /// <summary>
        /// 发送
        /// </summary>
        /// <param name="mes"></param>
        public void Send(Byte[] mes)
        {
            if (this.connected)
            {
                EventHandler<SocketAsyncEventArgs> handler = null;
                byte[] buffer = mes;
                SocketAsyncEventArgs senderSocketAsyncEventArgs = null;
                lock (s_lst)
                {
                    if (s_lst.Count > 0)
                    {
                        senderSocketAsyncEventArgs = s_lst[s_lst.Count - 1];
                        s_lst.RemoveAt(s_lst.Count - 1);
                    }
                }
                if (senderSocketAsyncEventArgs == null)
                {
                    senderSocketAsyncEventArgs = new SocketAsyncEventArgs();
                    senderSocketAsyncEventArgs.UserToken = this.clientSocket;
                    senderSocketAsyncEventArgs.RemoteEndPoint = this.clientSocket.RemoteEndPoint;
                    if (handler == null)
                    {
                        handler = delegate(object sender, SocketAsyncEventArgs _e)
                        {
                            lock (s_lst)
                            {
                                s_lst.Add(senderSocketAsyncEventArgs);
                            }
                        };
                    }
                    senderSocketAsyncEventArgs.Completed += handler;
                }
                senderSocketAsyncEventArgs.SetBuffer(buffer, 0, buffer.Length);
                this.clientSocket.SendAsync(senderSocketAsyncEventArgs);
            }
            else
            {
                this.connected = false;
            }
        }
        /// <summary>
        /// 监听服务端
        /// </summary>
        public void Listen()
        {
            if (this.connected && this.clientSocket != null)
            {
                try
                {
                    (lisnterSocketAsyncEventArgs.UserToken as Socket).ReceiveAsync(lisnterSocketAsyncEventArgs);
                }
                catch (Exception)
                {
                }
            }
        }
        /// <summary>
        /// 断开连接
        /// </summary>
        /// <returns></returns>
        private int Disconnect()
        {
            int res = 0;
            try
            {
                this.clientSocket.Shutdown(SocketShutdown.Both);
            }
            catch (Exception)
            {
            }
            try
            {
                this.clientSocket.Close();
            }
            catch (Exception)
            {
            }
            this.connected = false;
            return res;
        }
        /// <summary>
        /// 数据接受
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnReceive(object sender, SocketAsyncEventArgs e)
        {
            if (e.BytesTransferred == 0)
            {
                try
                {
                    this.clientSocket.Shutdown(SocketShutdown.Both);
                }
                catch (Exception)
                {
                }
                finally
                {
                    if (this.clientSocket.Connected)
                    {
                        this.clientSocket.Close();
                    }
                }
                byte[] info = new Byte[] { 0 };
                this.OnMsgReceived(info);
            }
            else
            {
                byte[] buffer = new byte[e.BytesTransferred];
                for (int i = 0; i < e.BytesTransferred; i++)
                {
                    buffer[i] = e.Buffer[i];
                }
                this.OnMsgReceived(buffer);
                Listen();
            }
        }
        /// <summary>
        /// 接受完成
        /// </summary>
        /// <param name="info"></param>
        private void SimaticSocketClient_OnMsgReceived(byte[] info)
        {
            if (info[0] != 0)
            {
                if (this.SendOrRecv == 1)
                {
                    this.SendDataPro = info;
                    this.SendOrRecv = 0;
                }
                else if (this.SendOrRecv == 2)
                {
                    this.SendData = info;
                    this.SendOrRecv = 0;
                }
                else if (this.SendOrRecv == 3)
                {
                    this.RecvDataPro = info;
                    this.SendOrRecv = 0;
                }
                else if (this.SendOrRecv == 4)
                {
                    this.RecvData = info;
                    this.SendOrRecv = 0;
                }
            }
            else
            {
                if (this.SendOrRecv == 1)
                {
                    this.SendDataPro = new Byte[] { 0 };
                    this.SendOrRecv = 0;
                }
                else if (this.SendOrRecv == 2)
                {
                    this.SendData = new Byte[] { 0 };
                    this.SendOrRecv = 0;
                }
                else if (this.SendOrRecv == 3)
                {
                    this.RecvDataPro = new Byte[] { 0 };
                    this.SendOrRecv = 0;
                }
                else if (this.SendOrRecv == 4)
                {
                    this.RecvData = new Byte[] { 0 };
                    this.SendOrRecv = 0;
                }
            }
        }
        /// <summary>
        /// 建立连接的方法
        /// </summary>
        /// <returns></returns>
        public bool OpenLinkPLC()
        {
            bool flag = false;
            this.StartListen += new StartListeHandler(SimaticSocketClient_StartListen);
            this.OnMsgReceived += new ReceiveMsgHandler(SimaticSocketClient_OnMsgReceived);
            flag = this.Connect();
            if (!flag)
            {
                return flag;
            }
            return true;
        }

        /// <summary>
        /// 关闭连接的方法
        /// </summary>
        /// <returns></returns>
        public int CloseLinkPLC()
        {
            return this.Disconnect();
        }
        /// <summary>
        /// 监听的方法
        /// </summary>
        private void SimaticSocketClient_StartListen()
        {
            this.Listen();
        }

        #region 

        public int ReadVM(int stationNO)
        {
            int flag = -1;
            Byte[] data = new Byte[6];
            data[0] = 0x3A;
            data[1] = (Byte)stationNO;
            data[2] = 0x43;
            data[3] = 0x01;
            data[4] = (Byte)((data[1]+data[2]+data[3])%256);
            data[5] = 0x0A;
          
            this.SendOrRecv = 3;
            int numPro = 0;
            this.Send(data);
            while (this.SendOrRecv != 0 && numPro < 500)
            {
                Thread.Sleep(1);
                numPro++;
            }
            if (numPro < 500)
            {
                if (this.RecvDataPro[4] == stationNO && this.RecvDataPro.Length == 12)
                {
                    flag = 0;

                    string str = "select [WorkTaskID] from [TakeTask] where [TaskStatus]=0 order by StartTime";
                    mainfrm.ds_WorkTask0.Clear();
                    mainfrm.ds_WorkTask0 = db.connDt(str).Copy();
                    db.ConnClosed();

                    if (stationNO >= 1 && stationNO <= 4)
                    {
                        if (this.RecvDataPro[5] > 0 || this.RecvDataPro[6] > 0)
                        {
                            Frm_A18164.btn_wireless[stationNO - 1,0] = 1;
                        }
                    }
                    else if (stationNO >= 5 && stationNO <= 8)
                    {
                        if (this.RecvDataPro[5] > 0 || this.RecvDataPro[6] > 0)
                        {
                            if (this.RecvDataPro[5] > 0 )
                            {
                                Frm_A18164.btn_wireless[stationNO - 1, 0] = 1;
                                Frm_A18164.btn_wireless[stationNO - 1, 1] = 0;
                            }
                            if ( this.RecvDataPro[6] > 0)
                            {
                                Frm_A18164.btn_wireless[stationNO - 1, 0] = 0;
                                Frm_A18164.btn_wireless[stationNO - 1, 1] = 1;
                            }
                        }
                    }
                    else if (stationNO >= 9 && stationNO <= 10)
                    {
                        if (this.RecvDataPro[5] > 0 || this.RecvDataPro[6] > 0)
                        {
                            if (this.RecvDataPro[5] > 0)
                            {
                                Frm_A18164.btn_wireless[stationNO - 1, 0] = 1;
                                //Frm_A18164.btn_wireless[stationNO - 1, 1] = 0;
                            }
                            if (this.RecvDataPro[6] > 0)
                            {
                                //Frm_A18164.btn_wireless[stationNO - 1, 0] = 0;
                                Frm_A18164.btn_wireless[stationNO - 1, 1] = 1;
                            }
                        }
                    }
                    else if (stationNO >= 11 && stationNO <= 12)
                    {
                        if (this.RecvDataPro[5] > 0 || this.RecvDataPro[6] > 0)
                        {
                            if (this.RecvDataPro[5] > 0)
                            {
                                Frm_A18164.btn_wireless[stationNO - 1, 0] = 1;
                                Frm_A18164.btn_wireless[stationNO - 1, 1] = 0;
                            }
                            if (this.RecvDataPro[6] > 0)
                            {
                                Frm_A18164.btn_wireless[stationNO - 1, 0] = 0;
                                Frm_A18164.btn_wireless[stationNO - 1, 1] = 1;
                            }
                        }
                    }
                }
            }
            this.SendOrRecv = 0;
            return flag;
        }

        public int WriteVM(int stationNO, int value1, int value2)
        {
            int flag = -1;
            Byte[] data = new Byte[12];
            data[0] = 0x3A;
            data[1] = (Byte)stationNO;
            data[2] = 0x63;
            data[3] = 0x06;
            data[4] = 0x01;
            data[5] = (byte)value1;
            data[6] = (byte)value2;
            data[7] = 0x00;
            data[8] = 0x00;
            data[9] = 0x00;
            data[10] = (Byte)((data[1] + data[2] + data[3] + data[4] + data[5] + data[6] + data[7] + data[8] + data[9]) % 256);
            data[11] = 0x0A;

            this.SendOrRecv = 3;
            int numPro = 0;
            this.Send(data);
            while (this.SendOrRecv != 0 && numPro < 500)
            {
                Thread.Sleep(1);
                numPro++;
            }
            if (numPro < 500)
            {
                if (this.RecvDataPro[3] == stationNO && this.RecvDataPro.Length == 6)
                {
                    flag = 0;
                }
            }
            this.SendOrRecv = 0;
            return flag;
        }
        #endregion
       /// <summary>
       /// 插入呼叫工位号
       /// </summary>
       /// <param name="WorkID">呼叫工位号</param>
        public static void WorkTask_Insert(int WorkID)
        {
            lock (obj_workTask_insert)
            {
                workTask_Insert(WorkID);
            }
        }
        private static object obj_workTask_insert = new object();
        public static void workTask_Insert(int WorkID)
        {
            string strsql = "";
            strsql = "insert into [TakeTask](WorkTaskID,TaskStatus,StartTime) values(" + WorkID + ",0,'" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + "')";
            Command(strsql);
        }
        private static SqlConnection sqlconn = null;
        private static SqlCommand command = null;
        private static object obj_cmd = new object();
        private static void Command(string sqlstr)
        {
            lock (obj_cmd)
            {
                try
                {
                    if (sqlconn == null)
                    {
                        ConnectStr = Cls.Param.connectStr;
                        sqlconn = new SqlConnection(ConnectStr);
                    }
                    if (sqlconn.State != ConnectionState.Open)
                    {
                        sqlconn.Open();
                    }
                    command = new SqlCommand(sqlstr, sqlconn);
                    command.ExecuteNonQuery();
                    sqlconn.Close();
                }
                catch (Exception err)
                {
                    WriteTxt.SaveLog1("SQL：" + sqlstr + " , \r\n" + err.Message + " \r\n FunctionName：Command", "Command_Log", "系统日志");
                }
            }
        }

    }
}