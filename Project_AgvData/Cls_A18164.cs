using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.NetworkInformation;
using System.Data;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using Crossing;
using System.Data.SqlClient;
using Agv_Info;
using Siemens_S200_Serial; //2
using Siemens_net_Protocol;//3
using SCM_Protocol;        //4 
using Omron_Serial_Fins;   //1
using Omron_Serial_Hostlink;//0
using s7api.net;


namespace AGVSystem
{
    public class Cls_A18164
    {
        static Thread[] th_getData;
        public static DataSet ds_mileage = new DataSet();
        public DataSet ds_Mileage
        {
            get { return ds_mileage; }
            set { ds_mileage = value; }
        }


        static string connectStr = "";
      
        static bool startFlag = false;
        /// <summary>
        /// AGV调度启动标志
        /// </summary>
        public bool StartFlag
        {
            get { return startFlag; }
            set { startFlag = value; }
        }

        static int closedFlag = -1;
        /// <summary>
        /// AGV调度关闭标志
        /// </summary>
        public int ClosedFlag
        {
            get { return closedFlag; }
            set { closedFlag = value; }
        }


        public Thread[] th_GetData
        {
            get { return th_getData; }
            set { th_getData = value; }
        }

        public void AGV_start(object agvinfo)
        {
            agv newagv = new agv();
            Cls.AgvInfo temp = (Cls.AgvInfo)agvinfo;
            th_GetData[temp.Agv_ID - 1] = new Thread(newagv.AGV_Data);
            th_GetData[temp.Agv_ID - 1].IsBackground = true;
            th_GetData[temp.Agv_ID - 1].Start(agvinfo);
        }




        public class agv
        {
            public void AGV_Data(object agvinfo)
            {
                //bool task_flag = false; //标记任务是否分配
                Cls.AgvInfo temp = (Cls.AgvInfo)agvinfo;
                int location_temp = 0;
                int location_value = 0;
                //int robot_location = 0, agv_reset = 0, agv_warn = 0;
                byte[] param_info = { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
                
                int errorcount = 0;
                int t = 0, t_temp = 0 ;
                Ping pingAgv = new Ping();
                Soc_Client Link_To_PLC = new Soc_Client(temp.IP, temp.Port);
                temp.Soc = Link_To_PLC.clientSocket;
                string[] warn_info = { "", "", "", "", "", "", "", "" };
                int warn_level = 1;
                closedFlag = -1;

                
                while (true)
                {
                    if (mainfrm.Closed)
                    {
                        return;
                    }
                    if (!temp.Enable)
                    {
                        Thread.Sleep(3000);
                        continue;
                    }
                    
                    if (!startFlag)
                    {
                        Thread.Sleep(500);
                        continue;
                    }
                    try
                    {
                        if (pingAgv.Send(temp.IP, 150).Status == IPStatus.Success)
                        {
                            //来到这里，表示PING成功。
                            if (!temp.bPlcLinked)
                            {
                                if (Link_To_PLC.OpenLinkPLC())
                                {   //来到这里，表示链路成功打开
                                    temp.bPlcLinked = true;
                                }
                                else
                                {   //来到这里，表示要重新建立链路
                                    temp.bPlcLinked = false;
                                    Link_To_PLC.CloseLinkPLC();
                                    Link_To_PLC = new Soc_Client(temp.IP, temp.Port);
                                    temp.Soc = Link_To_PLC.clientSocket;
                                    errorcount = 0;
                                }
                            }
                            else
                            {
                                for (byte i = 0; i < 15; i++)
                                {
                                    param_info[i] = 0;
                                }
                                //来到这里，表示PLC的链路已经打开，可以直接读写数据
                                if (0 != Link_To_PLC.ReadInfo(out param_info))
                                {
                                    //来到这里，表示读数据失败    
                                    errorcount--;

                                    if (Math.Abs(errorcount) >= 5)
                                    {
                                        //来到这里，表示一再失败，要重新建立链路                                    
                                        temp.bPlcLinked = false;
                                        errorcount = 0;
                                        Link_To_PLC.CloseLinkPLC();
                                        Link_To_PLC = new Soc_Client(temp.IP, temp.Port);
                                        temp.Soc = Link_To_PLC.clientSocket;
                                        temp.Warn_ID = "1000000000000000";
                                        temp.Warn_Level = 4;
                                        temp.Warn_info = "网络异常";
                                        Func.db_conn.Command("INSERT INTO [Net_Log] ([dt] ,[AGV_ID] ,[Type]) VALUES  ('" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + "','" + temp.Agv_ID + "','Read Fail')");
                                    }
                                }
                                else
                                {
                                    location_temp = Func.GetLandMarkId(temp.AssemblyLine, param_info[3] + param_info[2] * 256);
                                    if (param_info[3] + param_info[2] * 256 == 113)
                                    {
                                        Thread.Sleep(1000);
                                        continue;
                                    }
                                    if (temp.Run_Direction != param_info[9])
                                    {
                                        //运动方向
                                        temp.Run_Direction = param_info[9];
                                        if (location_temp == 100 || location_temp == 102 ||
                                           location_temp == 104 || location_temp == 106 ||
                                           location_temp == 132 || location_temp == 134 ||
                                           location_temp == 136 || location_temp == 138 ||
                                           location_temp == 140 || location_temp == 142 ||
                                           location_temp == 144 || location_temp == 146 || location_temp == 148 ||
                                           //--new
                                           location_temp == 164 || location_temp == 166 ||
                                           location_temp == 168 || location_temp == 170 ||
                                           location_temp == 182 || location_temp == 184 ||
                                           location_temp == 186 || location_temp == 188 || location_temp == 162)
                                        //--new-end
                                        {
                                            temp.Cross_Location_Go = 0;

                                        }
                                    }
                                    //用temp.bangstyle表征牵引棒状态：0x01上升，0x02下降
                                    if (temp.bangstyle != param_info[12])
                                    {
                                        temp.bangstyle = param_info[12];
                                    }

                                    if (temp.Run_Status != param_info[10])
                                        {
                                            temp.Run_Status = param_info[10];
                                        }
                                        if (temp.Voltage != param_info[6] + param_info[7] / 100)
                                        {
                                            //电压
                                            temp.Voltage = param_info[6] + Convert.ToSingle(param_info[7] / 100.0);
                                        }

                                        if (temp.Location_Display != location_temp)
                                        {
                                            //地标信息
                                            temp.Location_Display = location_temp;
                                            Frm_A18164.frm.Invoke(Frm_A18164.frm.dataset_Set, temp.Agv_ID - 1, 1, location_temp.ToString(), "agv_info");

                                            //记录充电
                                            Func.Change_Rec(temp.Agv_ID, location_temp, temp.Location_Display, temp.Read_dt);
                                            temp.Read_dt = DateTime.Now;
                                            Func.db_conn.Command("INSERT INTO [Location_Log] ([Dt] ,[Agv_ID] ,[Loc_ID] ,[Value] ,[Loc_ID2]  ,[Value2],[Value3])  VALUES ('" +
                                                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + "'," + temp.Agv_ID + "," + location_temp + "," + location_value + "," + 0 + "," + 0 + "," + 0 + ")");
                                        }

                                        #region 报警信息
                                        if (temp.Warn_ID != Convert.ToString(param_info[5], 2).PadLeft(8, '0') +
                                                                                      Convert.ToString(param_info[4], 2).PadLeft(8, '0'))
                                        {
                                            temp.Warn_ID = Convert.ToString(param_info[5], 2).PadLeft(8, '0') +
                                                           Convert.ToString(param_info[4], 2).PadLeft(8, '0');

                                            WarnInfo(temp.Warn_ID, out warn_info, out warn_level);
                                            Frm_A18164.frm.Invoke(Frm_A18164.frm.dataset_Set, temp.Agv_ID - 1, 2, temp.Warn_ID, "agv_info");
                                            temp.Warn_info = "";
                                            temp.Warn_Level = warn_level;
                                            if (warn_level == 1)
                                            {
                                                temp.Warn_info = "正常";
                                                Func.Agv_Alarm_Insert(temp.Agv_ID, temp.AssemblyName, temp.AssemblyLine.ToString(), temp.Internal_ID, "正常", temp.Agv_ID);
                                            }
                                            else
                                            {
                                                for (byte i = 0; i < 8; i++)
                                                {
                                                    if (warn_info[i] != "")
                                                    {
                                                        temp.Warn_info = temp.Warn_info + warn_info[i] + "\r\n";
                                                        Func.Agv_Alarm_Insert(0, temp.AssemblyName, temp.AssemblyLine.ToString(), temp.Internal_ID, warn_info[i], temp.Agv_ID);
                                                    }
                                                }
                                            }
                                        }
                                    #endregion
                                    //--new

                                    if (location_temp == 182 ||
                                            location_temp == 183 ||
                                            location_temp == 184 || location_temp == 185)
                                    {
                                        if (Frm_A18164.btn_wireless[10, 0] == 1 || Frm_A18164.btn_wireless[10, 1] == 1)
                                        {
                                            if (temp.Task_ID == 14)
                                            {
                                                Frm_A18164.btn_wireless[10, 0] = 0;
                                                temp.Task_ID = 0;
                                                temp.Task_Temp = 0;
                                                temp.Task_orgin = 0;
                                            }
                                            else if (temp.Task_ID == 15)
                                            {
                                                Frm_A18164.btn_wireless[10, 1] = 0;
                                                temp.Task_ID = 0;
                                                temp.Task_Temp = 0;
                                                temp.Task_orgin = 0;
                                            }
                                        }
                                    }

                                        if (location_temp == 186 ||
                                            location_temp == 187 ||
                                            location_temp == 188 || location_temp == 189)
                                        {
                                        if (Frm_A18164.btn_wireless[11, 0] == 1 || Frm_A18164.btn_wireless[11, 1] == 1)
                                        {
                                            if (temp.Task_ID == 18)
                                            {
                                                Frm_A18164.btn_wireless[11, 0] = 0;
                                                temp.Task_ID = 0;
                                                temp.Task_Temp = 0;
                                                temp.Task_orgin = 0;
                                            }
                                            else if (temp.Task_ID == 19)
                                            {
                                                Frm_A18164.btn_wireless[11, 1] = 0;
                                                temp.Task_ID = 0;
                                                temp.Task_Temp = 0;
                                                temp.Task_orgin = 0;
                                            }
                                        }
                                        }
                                    //--new-end

                                    if (location_temp == 144 ||
                                           location_temp == 145 ||
                                           location_temp == 146 || location_temp == 147)
                                    {
                                        if (Frm_A18164.btn_wireless[7, 0] == 1 || Frm_A18164.btn_wireless[7, 1] == 1)
                                        {
                                            Frm_A18164.btn_wireless[7, 0] = 0;
                                            Frm_A18164.btn_wireless[7, 1] = 0;
                                            if (temp.Task_ID == 8)
                                            {
                                                temp.Task_ID = 0;
                                                temp.Task_Temp = 0;
                                            }
                                        }
                                    } 
                                    if (location_temp == 140 ||
                                            location_temp == 141 ||
                                            location_temp == 142 || location_temp == 143)
                                        {
                                            if (Frm_A18164.btn_wireless[6, 0] == 1 || Frm_A18164.btn_wireless[6, 1] == 1)
                                            {
                                                Frm_A18164.btn_wireless[6, 0] = 0;
                                                Frm_A18164.btn_wireless[6, 1] = 0;
                                                if (temp.Task_ID == 7)
                                                {
                                                    temp.Task_ID = 0;
                                                    temp.Task_Temp = 0;
                                                }
                                            }
                                        }

                                        if (location_temp == 136 ||
                                            location_temp == 137 ||
                                            location_temp == 138 || location_temp == 139)
                                        {
                                            if (Frm_A18164.btn_wireless[5, 0] == 1 || Frm_A18164.btn_wireless[5, 1] == 1)
                                            {
                                                Frm_A18164.btn_wireless[5, 0] = 0;
                                                Frm_A18164.btn_wireless[5, 1] = 0;
                                                if (temp.Task_ID == 6)
                                                {
                                                    temp.Task_ID = 0;
                                                    temp.Task_Temp = 0;
                                                }
                                            }
                                        }

                                        if (location_temp == 132 ||
                                            location_temp == 133 ||
                                            location_temp == 134 || location_temp == 135)
                                        {
                                            if (Frm_A18164.btn_wireless[4, 0] == 1 || Frm_A18164.btn_wireless[4, 1] == 1)
                                            {
                                                Frm_A18164.btn_wireless[4, 0] = 0;
                                                Frm_A18164.btn_wireless[4, 1] = 0;
                                                if (temp.Task_ID == 5)
                                                {
                                                    temp.Task_ID = 0;
                                                    temp.Task_Temp = 0;
                                                }
                                            }
                                        }

                                        if ((temp.Task_ID == 1 || temp.Task_ID == 2) &&
                                            (temp.Location_Display == 100 || temp.Location_Display == 101))
                                        {
                                            if (Frm_A18164.btn_wireless[0, 0] == 1 || Frm_A18164.btn_wireless[0, 1] == 1)
                                            {
                                                Frm_A18164.btn_wireless[0, 0] = 0;
                                                Frm_A18164.btn_wireless[0, 1] = 0;
                                            }
                                        }

                                        if ((temp.Task_ID == 3 || temp.Task_ID == 4) &&
                                            (temp.Location_Display == 102 || temp.Location_Display == 103))
                                        {
                                            if (Frm_A18164.btn_wireless[1, 0] == 1 || Frm_A18164.btn_wireless[1, 1] == 1)
                                            {
                                                Frm_A18164.btn_wireless[1, 0] = 0;
                                                Frm_A18164.btn_wireless[1, 1] = 0;
                                            }
                                        }

                                        if ((temp.Task_ID == 5 || temp.Task_ID == 6) &&
                                            (temp.Location_Display == 104 || temp.Location_Display == 105))
                                        {
                                            if (Frm_A18164.btn_wireless[2, 0] == 1 || Frm_A18164.btn_wireless[2, 1] == 1)
                                            {
                                                Frm_A18164.btn_wireless[2, 0] = 0;
                                                Frm_A18164.btn_wireless[2, 1] = 0;
                                            }
                                        }

                                        if ((temp.Task_ID == 7 || temp.Task_ID == 8) &&
                                            (temp.Location_Display == 106 || temp.Location_Display == 107))
                                        {
                                            if (Frm_A18164.btn_wireless[3, 0] == 1 || Frm_A18164.btn_wireless[3, 1] == 1)
                                            {
                                                Frm_A18164.btn_wireless[3, 0] = 0;
                                                Frm_A18164.btn_wireless[3, 1] = 0;
                                            }
                                        }
                                    //-new
                                    if ((temp.Task_ID == 14 || temp.Task_ID == 15) && temp.Task_orgin==0 &&
                                            (temp.Location_Display == 164 || temp.Location_Display == 165))
                                    {
                                        if (Frm_A18164.btn_wireless[9, 0] == 1)
                                        {
                                            Frm_A18164.btn_wireless[9, 0] = 0;
                                        }
                                    }
                                    else if ((temp.Task_ID == 14 || temp.Task_ID == 15) && temp.Task_orgin == 0 &&
                                            (temp.Location_Display == 166|| temp.Location_Display == 167))
                                    {
                                        if (Frm_A18164.btn_wireless[9, 1] == 1)
                                        {
                                            Frm_A18164.btn_wireless[9, 1] = 0;
                                        }
                                    }

                                    if ((temp.Task_ID == 18 || temp.Task_ID == 19) && temp.Task_orgin == 0 &&
                                            (temp.Location_Display == 168 || temp.Location_Display == 169))
                                    {
                                        if (Frm_A18164.btn_wireless[8, 0] == 1)
                                        {
                                            Frm_A18164.btn_wireless[8, 0] = 0;
                                        }
                                    }
                                    else if ((temp.Task_ID == 18 || temp.Task_ID == 19) && temp.Task_orgin == 0 &&
                                            (temp.Location_Display == 170 || temp.Location_Display == 171))
                                    {
                                        if (Frm_A18164.btn_wireless[8, 1] == 1)
                                        {
                                            Frm_A18164.btn_wireless[8, 1] = 0;
                                        }
                                    }

                                        //-end
                                        if ((temp.Task_ID == 10 || temp.Task_ID == 10) &&
                                        (temp.Location_Display == 150 || temp.Location_Display == 148))
                                        {
                                            temp.Task_ID = 0;
                                            temp.Task_Temp = 0;
                                        }
                                        //new
                                        if ((temp.Task_ID == 11 || temp.Task_ID == 11) &&
                                            (temp.Location_Display == 152 || temp.Location_Display == 162))
                                        {
                                            temp.Task_ID = 0;
                                            temp.Task_Temp = 0;
                                        }
                                        //-end
                                        if (temp.Cross_Location_Go != location_temp && !mainfrm.All_Agv_Stop && !mainfrm.All_Agv_Close)
                                        {
                                        if (location_temp == 132 ||
                                            location_temp == 134 ||
                                            location_temp == 136 ||
                                            location_temp == 138 ||
                                            location_temp == 140 ||
                                            location_temp == 142 ||
                                            location_temp == 144 ||
                                            location_temp == 146 ||
                                            //-new
                                            location_temp == 182 ||
                                            location_temp == 184 ||
                                            location_temp == 186 ||
                                            location_temp == 188)//-end
                                        {
                                            if (temp.Task_ID > 0 || temp.Task_Temp > 0)
                                            {
                                                temp.Task_ID = 0;
                                                temp.Task_Temp = 0;
                                            }
                                            if (temp.Run_Direction == 2)
                                            {
                                                Crossing.Crossing.BlockCross(temp.AssemblyLine, 0, temp.Agv_ID, mainfrm.ds_CrossInfo);
                                                Func.CrossInfo_Insert(temp.AssemblyName, temp.AssemblyLine.ToString(), temp.Internal_ID, location_temp, "复位路口", temp.Agv_ID, location_value);
                                                temp.go_info = "复位路口，位置：" + location_temp.ToString();
                                                Thread.Sleep(2000);
                                            }
                                            else
                                            {
                                                if (temp.Run_Status == 2)
                                                {
                                                    if (1 == Crossing.Crossing.BlockCross(temp.AssemblyLine, temp.Location_Display, temp.Agv_ID, mainfrm.ds_CrossInfo))
                                                    {
                                                        if (Link_To_PLC.GoSingle(out param_info) == 0)
                                                        {
                                                            //temp.Cross_Location_Go = temp.Location_Display;
                                                            Func.CrossInfo_Insert(temp.AssemblyName, temp.AssemblyLine.ToString(), temp.Internal_ID, location_temp, "已放行", temp.Agv_ID, location_value);
                                                            temp.go_info = "已放行，位置：" + location_temp.ToString();
                                                        }
                                                    }
                                                }
                                            }
                                        }

                                        else if (location_temp == 109 || location_temp == 111)
                                        {
                                            if (loc_agv(temp.Run_Direction))
                                            {
                                                Crossing.Crossing.BlockCross(temp.AssemblyLine, temp.Location_Display, temp.Agv_ID, mainfrm.ds_CrossInfo);
                                                temp.Cross_Location_Go = temp.Location_Display;
                                                Func.CrossInfo_Insert(temp.AssemblyName, temp.AssemblyLine.ToString(), temp.Internal_ID, location_temp, "复位路口", temp.Agv_ID, location_value);
                                                temp.go_info = "复位路口，位置：" + location_temp.ToString();
                                            }
                                        }
                                        //new
                                        else if (location_temp == 159 || location_temp == 123)
                                        {

                                            Crossing.Crossing.BlockCross(temp.AssemblyLine, temp.Location_Display, temp.Agv_ID, mainfrm.ds_CrossInfo);
                                            temp.Cross_Location_Go = temp.Location_Display;
                                            Func.CrossInfo_Insert(temp.AssemblyName, temp.AssemblyLine.ToString(), temp.Internal_ID, location_temp, "复位路口", temp.Agv_ID, location_value);
                                            temp.go_info = "复位路口，位置：" + location_temp.ToString();

                                        }
                                        //-new-end
                                        //else if (location_temp == 81 || location_temp == 83)
                                        //{
                                        //    if (loc_agv(temp.Run_Direction))
                                        //    {
                                        //        Crossing.Crossing.BlockCross(temp.AssemblyLine, temp.Location_Display, temp.Agv_ID, mainfrm.ds_CrossInfo);
                                        //        temp.Cross_Location_Go = temp.Location_Display;
                                        //        Func.CrossInfo_Insert(temp.AssemblyName, temp.AssemblyLine.ToString(), temp.Internal_ID, location_temp, "复位路口", temp.Agv_ID, location_value);
                                        //        temp.go_info = "复位路口，位置：" + location_temp.ToString();
                                        //    }
                                        //}
                                        //-new-end
                                        else if (location_temp == 148 )
                                        {
                                            if (temp.Run_Direction == 2)
                                            {
                                                Crossing.Crossing.BlockCross(temp.AssemblyLine, 0, temp.Agv_ID, mainfrm.ds_CrossInfo);
                                                //Func.CrossInfo_Insert(temp.AssemblyName, temp.AssemblyLine.ToString(), temp.Internal_ID, location_temp, "复位路口", temp.Agv_ID, location_value);
                                                temp.go_info = "复位路口，位置：" + location_temp.ToString();
                                                //temp.Cross_Location_Go = temp.Location_Display;
                                            }
                                            else if (temp.Run_Direction == 1)
                                            {
                                                //获取任务                                   
                                                if (temp.Task_ID == 0 && temp.Task_Temp == 0)
                                                {
                                                    int task_id = task_agv();
                                                    if (task_id > 0)
                                                    {
                                                        int agv_id = current_agv(task_agv());
                                                        if (agv_id == 0 || agv_id == temp.Agv_ID)
                                                        {
                                                            temp.Task_ID = task_id;
                                                        }
                                                    }
                                                }
                                                //执行任务
                                                if (temp.Task_ID > 0 && temp.Task_Temp == 0)
                                                {
                                                    if (Crossing.Crossing.BlockCross(temp.AssemblyLine, temp.Location_Display, temp.Agv_ID, mainfrm.ds_CrossInfo) == 1)
                                                    {
                                                        if (Link_To_PLC.WriteInfo(temp.Task_ID, out param_info) == 0)
                                                        {
                                                            if (Link_To_PLC.GoSingle(out param_info) == 0)
                                                            {
                                                                temp.Task_Temp = temp.Task_ID;
                                                                Func.CrossInfo_Insert(temp.AssemblyName, temp.AssemblyLine.ToString(), temp.Internal_ID, location_temp, "目标位置：" + temp.Task_ID.ToString(), temp.Agv_ID, location_value);
                                                                temp.Cross_Location_Go = location_temp;
                                                                temp.go_info = "已放行，目标：工位" + temp.Task_ID.ToString() + "#";
                                                            }
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                        else if (location_temp == 162)
                                        {
                                            if (temp.Run_Direction == 1)
                                            {
                                                Crossing.Crossing.BlockCross(temp.AssemblyLine, 0, temp.Agv_ID, mainfrm.ds_CrossInfo);
                                                //Func.CrossInfo_Insert(temp.AssemblyName, temp.AssemblyLine.ToString(), temp.Internal_ID, location_temp, "复位路口", temp.Agv_ID, location_value);
                                                temp.go_info = "复位路口，位置：" + location_temp.ToString();
                                                //temp.Cross_Location_Go = temp.Location_Display;
                                            }
                                            else if (temp.Run_Direction == 2)
                                            {
                                                //获取任务                                   
                                                if (temp.Task_ID == 0 && temp.Task_Temp == 0)
                                                {
                                                    int task_id = task_agv();
                                                    if (task_id > 0)
                                                    {
                                                        int agv_id = current_agv(task_agv());
                                                        if ( agv_id == temp.Agv_ID)
                                                        {
                                                            temp.Task_ID = task_id;
                                                        }
                                                    }
                                                }
                                                //执行任务
                                                if (temp.Task_ID > 0 && temp.Task_Temp == 0)
                                                {
                                                    //if (Crossing.Crossing.BlockCross(temp.AssemblyLine, temp.Location_Display, temp.Agv_ID, mainfrm.ds_CrossInfo) == 1)
                                                    //{
                                                        if (temp.Task_orgin   == 0)
                                                        {
                                                            if (Link_To_PLC.WriteInfo(temp.Task_ID, out param_info) == 0)
                                                            {
                                                                if (Link_To_PLC.GoSingle(out param_info) == 0)
                                                                {
                                                                    temp.Task_Temp = temp.Task_ID;
                                                                    Func.CrossInfo_Insert(temp.AssemblyName, temp.AssemblyLine.ToString(), temp.Internal_ID, location_temp, "目标位置：" + temp.Task_ID.ToString(), temp.Agv_ID, location_value);
                                                                temp.Cross_Location_Go = location_temp;
                                                                temp.go_info = "已放行，目标：工位" + temp.Task_ID.ToString() + "#";
                                                                }
                                                            }
                                                        }
                                                        else
                                                        {
                                                            if (Link_To_PLC.WriteInfo(temp.Task_orgin, out param_info) == 0)
                                                            {
                                                                if (Link_To_PLC.GoSingle(out param_info) == 0)
                                                                {
                                                                    Func.CrossInfo_Insert(temp.AssemblyName, temp.AssemblyLine.ToString(), temp.Internal_ID, location_temp, "先到上料呼叫点，目标位置：" + temp.Task_orgin.ToString(), temp.Agv_ID, location_value);
                                                                temp.Cross_Location_Go = location_temp;
                                                                temp.go_info = "已放行，目标：工位" + temp.Task_orgin.ToString() + "#";
                                                                    temp.Task_orgin = 0;
                                                                }
                                                            }
                                                        }


                                                    //}
                                                }
                                            }
                                        }
                                        else if (location_temp == 202)
                                        {
                                           
                                            if (Func.AgvInfo[2].Run_Direction == 2)
                                            {
                                                if (loc_agv2() == false)
                                                {
                                                    t = Crossing.Crossing.BlockCross(temp.AssemblyLine, temp.Location_Display, temp.Agv_ID, mainfrm.ds_CrossInfo);
                                                    if (t == 1)
                                                    {
                                                        if (Link_To_PLC.GoSingle(out param_info) == 0)
                                                        {
                                                            temp.Task_Temp = temp.Task_ID;
                                                            Func.CrossInfo_Insert(temp.AssemblyName, temp.AssemblyLine.ToString(), temp.Internal_ID, location_temp, "已放行", temp.Agv_ID, location_value);
                                                            temp.Cross_Location_Go = location_temp;
                                                            temp.go_info = "已放行";
                                                        }
                                                    }
                                                    else if (t < 0)
                                                    {
                                                        temp.go_info = "AGV-" + t.ToString() + "占用路口！";
                                                    }
                                                }
                                            }
                                            // new
                                            else if (Func.AgvInfo[2].Run_Direction == 1)
                                            {
                                                t = Crossing.Crossing.BlockCross(temp.AssemblyLine,0, temp.Agv_ID, mainfrm.ds_CrossInfo);//强制复位
                                                if (t == 2)
                                                {
                                                    if (Link_To_PLC.GoSingle(out param_info) == 0)
                                                    {
                                                        temp.Task_Temp = temp.Task_ID;
                                                        Func.CrossInfo_Insert(temp.AssemblyName, temp.AssemblyLine.ToString(), temp.Internal_ID, location_temp, "已强制复位路口并放行", temp.Agv_ID, location_value);
                                                        temp.Cross_Location_Go = location_temp;
                                                        temp.go_info = "已放行";
                                                    }
                                                }
                                            }
                                            //new -end
                                        }
                                        else if (location_temp == 204)
                                        {
                                            if (Func.AgvInfo[2].Run_Direction == 1)
                                            {
                                                if (loc_agv2() == false)
                                                {
                                                    t = Crossing.Crossing.BlockCross(temp.AssemblyLine, temp.Location_Display, temp.Agv_ID, mainfrm.ds_CrossInfo);
                                                    if (t == 1)
                                                    {
                                                        if (Link_To_PLC.GoSingle(out param_info) == 0)
                                                        {
                                                            temp.Task_Temp = temp.Task_ID;
                                                            Func.CrossInfo_Insert(temp.AssemblyName, temp.AssemblyLine.ToString(), temp.Internal_ID, location_temp, "已放行", temp.Agv_ID, location_value);
                                                            temp.Cross_Location_Go = location_temp;
                                                            temp.go_info = "已放行";
                                                        }
                                                    }
                                                    else if (t < 0)
                                                    {
                                                        temp.go_info = "AGV-" + t.ToString() + "占用路口！";
                                                    }
                                                }
                                            }
                                            // new
                                            else if (Func.AgvInfo[2].Run_Direction == 2)
                                            {
                                                t = Crossing.Crossing.BlockCross(temp.AssemblyLine, 0, temp.Agv_ID, mainfrm.ds_CrossInfo);//强制复位
                                                if (t == 2)
                                                {
                                                    if (Link_To_PLC.GoSingle(out param_info) == 0)
                                                    {
                                                        temp.Task_Temp = temp.Task_ID;
                                                        Func.CrossInfo_Insert(temp.AssemblyName, temp.AssemblyLine.ToString(), temp.Internal_ID, location_temp, "已强制复位路口并放行", temp.Agv_ID, location_value);
                                                        temp.Cross_Location_Go = location_temp;
                                                        temp.go_info = "已放行";
                                                    }
                                                }
                                            }
                                            //new -end
                                        }
                                        else if (location_temp == 100 ||
                                            location_temp == 102 ||
                                            location_temp == 104 ||
                                            location_temp == 106 ||
                                            //-new
                                            location_temp == 164 ||
                                            location_temp == 166 ||
                                            location_temp == 168 ||
                                            location_temp == 170)//-end
                                        {
                                            if (temp.Run_Direction == 1)
                                            {
                                                Crossing.Crossing.BlockCross(temp.AssemblyLine, 0, temp.Agv_ID, mainfrm.ds_CrossInfo);
                                                Func.CrossInfo_Insert(temp.AssemblyName, temp.AssemblyLine.ToString(), temp.Internal_ID, location_temp, "复位路口", temp.Agv_ID, location_value);
                                                temp.go_info = "复位路口，位置：" + location_temp.ToString();
                                                temp.Cross_Location_Go = temp.Location_Display;
                                            }
                                            else if (temp.Run_Direction == 2)
                                            {
                                                //获取任务                                   
                                                if (temp.Task_ID == 0 && temp.Task_Temp == 0)
                                                {
                                                    if (temp.Voltage < 23.4)
                                                    {
                                                        if (temp.Agv_ID == 1 || temp.Agv_ID == 2)
                                                        {
                                                            if (Func.AgvInfo[0].Location_Display != 150 && Func.AgvInfo[0].Location_Display != 148 &&
                                                             Func.AgvInfo[0].Task_ID != 10 && Func.AgvInfo[1].Task_ID != 10 &&
                                                             Func.AgvInfo[1].Location_Display != 150 && Func.AgvInfo[1].Location_Display != 148)
                                                            {
                                                                temp.Task_ID = 10;
                                                            }
                                                        }
                                                        if (temp.Agv_ID == 3 || temp.Agv_ID == 4)
                                                        {
                                                            if (Func.AgvInfo[2].Location_Display != 162 && Func.AgvInfo[2].Location_Display != 152 &&
                                                             Func.AgvInfo[2].Task_ID != 11 && Func.AgvInfo[3].Task_ID != 11 &&
                                                             Func.AgvInfo[3].Location_Display != 162 && Func.AgvInfo[3].Location_Display != 152
                                                              )
                                                            {
                                                                temp.Task_ID = 11;
                                                            }
                                                        }
                                                    }
                                                    else
                                                    {
                                                        int task_id = task_agv();
                                                        if (task_id > 0)
                                                        {
                                                            int agv_id = current_agv(task_agv());
                                                            if (temp.Agv_ID <= 2)
                                                            {
                                                                if (agv_id == 0 || agv_id == temp.Agv_ID)
                                                                {
                                                                    temp.Task_ID = task_id;
                                                                }
                                                            }
                                                            else if (temp.Agv_ID > 2 && temp.Agv_ID < 5)
                                                            {
                                                                if (agv_id == temp.Agv_ID)
                                                                {
                                                                    temp.Task_ID = task_id;
                                                                }
                                                            }

                                                        }
                                                    }
                                                }
                                                //执行任务
                                                if (temp.Task_ID > 0 && temp.Task_Temp == 0)
                                                {
                                                    if (temp.Run_Status == 2)
                                                    {
                                                        if (Crossing.Crossing.BlockCross(temp.AssemblyLine, temp.Location_Display, temp.Agv_ID, mainfrm.ds_CrossInfo) == 1)
                                                        {
                                                            if (temp.Task_orgin == 0)
                                                            {
                                                                if (Link_To_PLC.WriteInfo(temp.Task_ID, out param_info) == 0)
                                                                {
                                                                    if (Link_To_PLC.GoSingle(out param_info) == 0)
                                                                    {
                                                                        temp.Task_Temp = temp.Task_ID;
                                                                        Func.CrossInfo_Insert(temp.AssemblyName, temp.AssemblyLine.ToString(), temp.Internal_ID, location_temp, "目标位置：" + temp.Task_ID.ToString(), temp.Agv_ID, location_value);
                                                                        //temp.Cross_Location_Go = location_temp;
                                                                        temp.go_info = "已放行，目标：工位" + temp.Task_ID.ToString() + "#";
                                                                    }
                                                                }
                                                            }
                                                            else
                                                            {
                                                                if (Link_To_PLC.WriteInfo(temp.Task_orgin, out param_info) == 0)
                                                                {
                                                                    if (Link_To_PLC.GoSingle(out param_info) == 0)
                                                                    {
                                                                        Func.CrossInfo_Insert(temp.AssemblyName, temp.AssemblyLine.ToString(), temp.Internal_ID, location_temp, "先到上料呼叫点，目标位置：" + temp.Task_orgin.ToString(), temp.Agv_ID, location_value);
                                                                        //temp.Cross_Location_Go = location_temp;
                                                                        temp.go_info = "已放行，目标：工位" + temp.Task_orgin.ToString() + "#";
                                                                        temp.Task_orgin = 0;
                                                                    }
                                                                }
                                                            }


                                                        }
                                                    }
                                                }
                                                else if (temp.Task_ID > 0 && temp.Task_Temp == temp.Task_ID)
                                                {
                                                    if (temp.Run_Status == 2)
                                                    {
                                                        if (Crossing.Crossing.BlockCross(temp.AssemblyLine, temp.Location_Display, temp.Agv_ID, mainfrm.ds_CrossInfo) == 1)
                                                        {
                                                            if (Link_To_PLC.GoSingle(out param_info) == 0)
                                                            {
                                                                temp.Task_Temp = temp.Task_ID;
                                                                Func.CrossInfo_Insert(temp.AssemblyName, temp.AssemblyLine.ToString(), temp.Internal_ID, location_temp, "目标位置：" + temp.Task_ID.ToString(), temp.Agv_ID, location_value);
                                                                //temp.Cross_Location_Go = location_temp;
                                                                temp.go_info = "已放行，目标：工位" + temp.Task_ID.ToString() + "#";
                                                            }
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                        else
                                        {
                                            Crossing.Crossing.BlockCross(temp.AssemblyLine, 0, temp.Agv_ID, mainfrm.ds_CrossInfo);
                                            Func.CrossInfo_Insert(temp.AssemblyName, temp.AssemblyLine.ToString(), temp.Internal_ID, location_temp, "复位路口", temp.Agv_ID, location_value);
                                            temp.go_info = "复位路口，位置：" + location_temp.ToString();
                                            temp.Cross_Location_Go = temp.Location_Display;
                                        }
                                        }
                                    }
                                    if (Math.Abs(errorcount) > 0)
                                    {
                                        errorcount = 0;
                                    }

                                }
                            }                        
                        else
                        {
                            if (temp.bPlcLinked)
                            {
                                errorcount--;
                                if (Math.Abs(errorcount) > 50)
                                {
                                    temp.Warn_ID = "1000000000000000";
                                    temp.Warn_Level = 4;
                                    temp.Warn_info = "网络异常";
                                    errorcount = 0;
                                    temp.bPlcLinked = false;
                                    Link_To_PLC.CloseLinkPLC();
                                }
                            }
                        }
                    }
                    catch (Exception Err)
                    {
                        if (Err.TargetSite.Name.ToString() == "ConnectAsync")
                        {
                            temp.Warn_ID = "1000000000000000";
                            temp.Warn_Level = 4;
                            temp.Warn_info = "网络异常";
                            errorcount = 0;
                            temp.bPlcLinked = false;
                            Link_To_PLC.CloseLinkPLC();
                            Link_To_PLC = new Soc_Client(temp.IP, temp.Port);
                            temp.Soc = Link_To_PLC.clientSocket;
                        }
                        if (temp.bPlcLinked)
                        {
                            errorcount--;
                            if (Math.Abs(errorcount) > 50)
                            {
                                temp.Warn_ID = "1000000000000000";
                                temp.Warn_Level = 4;
                                temp.Warn_info = "网络异常";
                                errorcount = 0;
                                temp.bPlcLinked = false;
                                Link_To_PLC.CloseLinkPLC();
                            }
                        }
                        Thread.Sleep(1000);
                    }
                    Thread.Sleep(600);
                }
            }
        }//class agv

        private static int current_agv(int task_id)
        {
            int agv_id = 0;
            for (int i = 0; i < Func.AgvInfo.Length; i++)
            {
                if (task_id == 1 || task_id == 2)
                {
                    if (Func.AgvInfo[i].Location_Display == 100 || Func.AgvInfo[i].Location_Display == 101)
                        agv_id = i + 1;
                }
                else if (task_id == 3 || task_id == 4)
                {
                    if (Func.AgvInfo[i].Location_Display == 102 || Func.AgvInfo[i].Location_Display == 103)
                        agv_id = i + 1;
                }
                else if (task_id == 5 || task_id == 6)
                {
                    if (Func.AgvInfo[i].Location_Display == 104 || Func.AgvInfo[i].Location_Display == 105)
                        agv_id = i + 1;
                }
                else if (task_id == 7 || task_id == 8)
                {
                    if (Func.AgvInfo[i].Location_Display == 106 || Func.AgvInfo[i].Location_Display == 107)
                        agv_id = i + 1;
                }
                /*else if(task_id == 11)
                {
                    if (Func.AgvInfo[i].Location_Display == 152 || Func.AgvInfo[i].Location_Display == 162)
                        agv_id = i + 1;
                }*/
                else if (task_id == 10)
                {
                    if (Func.AgvInfo[i].Location_Display == 150 || Func.AgvInfo[i].Location_Display == 148)
                        agv_id = i + 1;
                }
                //-new
                else if (task_id == 14||task_id == 15)
                {
                    if (Func.AgvInfo[3].Location_Display == 166 || Func.AgvInfo[3].Location_Display == 167||
                        Func.AgvInfo[3].Location_Display == 164 || Func.AgvInfo[3].Location_Display == 165 || Func.AgvInfo[3].Location_Display == 162)
                        agv_id = 4;
                }
                else if (task_id == 18 || task_id == 19)
                {
                    if (Func.AgvInfo[2].Location_Display == 168 || Func.AgvInfo[2].Location_Display == 169 ||
                        Func.AgvInfo[2].Location_Display == 170 || Func.AgvInfo[2].Location_Display == 171 || Func.AgvInfo[2].Location_Display == 162)
                        agv_id = 3;
                }
                //-end
            }
            return agv_id;
        }
        private static int task_agv()
        {
            int res = 0;
            if (Frm_A18164.btn_wireless[0, 0] == 1 || Frm_A18164.btn_wireless[0, 1] == 1) 
            {
                if (Frm_A18164.btn_wireless[4, 0] == 1)
                {
                    if (Func.AgvInfo[0].Task_ID != 1 && Func.AgvInfo[1].Task_ID != 1)
                        return  res = 1;
                }
                else if (Frm_A18164.btn_wireless[4, 1] == 1)
                {
                    if (Func.AgvInfo[0].Task_ID != 2 && Func.AgvInfo[1].Task_ID != 2)
                        return res = 2;
                }
            }

            if (Frm_A18164.btn_wireless[1, 0] == 1 || Frm_A18164.btn_wireless[1, 1] == 1)
            {
                if (Frm_A18164.btn_wireless[5, 0] == 1)
                {
                    if (Func.AgvInfo[0].Task_ID != 3 && Func.AgvInfo[1].Task_ID != 3)
                        return res = 3;
                }
                else if (Frm_A18164.btn_wireless[5, 1] == 1)
                {
                    if (Func.AgvInfo[0].Task_ID != 4 && Func.AgvInfo[1].Task_ID != 4)
                        return res = 4;
                }
            }

            if (Frm_A18164.btn_wireless[2, 0] == 1 || Frm_A18164.btn_wireless[2, 1] == 1)
            {
                if (Frm_A18164.btn_wireless[6, 0] == 1)
                {
                    if (Func.AgvInfo[0].Task_ID != 5 && Func.AgvInfo[1].Task_ID != 5)
                        return res = 5;
                }
                else if (Frm_A18164.btn_wireless[6, 1] == 1)
                {
                    if (Func.AgvInfo[0].Task_ID != 6 && Func.AgvInfo[1].Task_ID != 6)
                        return res = 6;
                }
            }

            if (Frm_A18164.btn_wireless[3, 0] == 1 || Frm_A18164.btn_wireless[3, 1] == 1)
            {
                if (Frm_A18164.btn_wireless[7, 0] == 1)
                {
                    if (Func.AgvInfo[0].Task_ID != 7 && Func.AgvInfo[1].Task_ID != 7)
                        return res = 7;
                }
                else if (Frm_A18164.btn_wireless[7, 1] == 1)
                {
                    if (Func.AgvInfo[0].Task_ID != 8 && Func.AgvInfo[1].Task_ID != 8)
                        return res = 8;
                }
            }
            //new  16(16工位到18),17(16工位到19)，18(17工位到18),19(17工位到19)
            if (Frm_A18164.btn_wireless[8, 0] == 1 || Frm_A18164.btn_wireless[8, 1] == 1)
            {
                if (Frm_A18164.btn_wireless[8, 0] == 1)
                {
                    if (Frm_A18164.btn_wireless[11, 0] == 1)
                    {
                        if (Func.AgvInfo[2].Task_ID != 18 && (Func.AgvInfo[2].Location_Display == 168 || Func.AgvInfo[2].Location_Display == 169))
                        {
                            Func.AgvInfo[2].Task_orgin = 16;
                            return res = 18;
                        }
                        else if (Func.AgvInfo[2].Task_ID != 18 && (Func.AgvInfo[2].Location_Display == 170 || Func.AgvInfo[2].Location_Display == 171 || Func.AgvInfo[2].Location_Display == 162))
                        {
                            Func.AgvInfo[2].Task_orgin = 16;
                            return res = 18;
                        }
                    }
                    else if (Frm_A18164.btn_wireless[11, 1] == 1)
                    {
                        if (Func.AgvInfo[2].Task_ID != 19 && (Func.AgvInfo[2].Location_Display == 168 || Func.AgvInfo[2].Location_Display == 169))
                        {
                            Func.AgvInfo[2].Task_orgin = 16;
                            return res = 19;
                        }
                        else if (Func.AgvInfo[2].Task_ID != 19 && (Func.AgvInfo[2].Location_Display == 170 || Func.AgvInfo[2].Location_Display == 171 || Func.AgvInfo[2].Location_Display == 162))
                        {
                            Func.AgvInfo[2].Task_orgin = 16;
                            return res = 19;
                        }
                    }
                }
                if (Frm_A18164.btn_wireless[8, 1] == 1)
                {
                    if (Frm_A18164.btn_wireless[11, 0] == 1)
                    {
                        if (Func.AgvInfo[2].Task_ID != 18 && (Func.AgvInfo[2].Location_Display == 170 || Func.AgvInfo[2].Location_Display == 171))
                        {
                            Func.AgvInfo[2].Task_orgin = 17;
                            return res = 18;
                        }
                        else if (Func.AgvInfo[2].Task_ID != 18 && (Func.AgvInfo[2].Location_Display == 168 || Func.AgvInfo[2].Location_Display == 169 || Func.AgvInfo[2].Location_Display == 162))
                        {
                            Func.AgvInfo[2].Task_orgin = 17;
                            return res = 18;
                        }
                    }
                    else if (Frm_A18164.btn_wireless[11, 1] == 1)
                    {
                        if (Func.AgvInfo[2].Task_ID != 19 && (Func.AgvInfo[2].Location_Display == 170 || Func.AgvInfo[2].Location_Display == 171))
                        {
                            Func.AgvInfo[2].Task_orgin = 17;
                            return res = 19;
                        }
                        else if (Func.AgvInfo[2].Task_ID != 19 && (Func.AgvInfo[2].Location_Display == 168 || Func.AgvInfo[2].Location_Display == 169 || Func.AgvInfo[2].Location_Display == 162))
                        {
                            Func.AgvInfo[2].Task_orgin = 17;
                            return res = 19;
                        }
                    }
                }
            }

            if (Frm_A18164.btn_wireless[9, 0] == 1 || Frm_A18164.btn_wireless[9, 1] == 1)
            {
                if (Frm_A18164.btn_wireless[9, 0] == 1)
                {
                    if (Frm_A18164.btn_wireless[10, 0] == 1)
                    {
                        if (Func.AgvInfo[3].Task_ID != 14 && (Func.AgvInfo[3].Location_Display == 164 || Func.AgvInfo[3].Location_Display == 165))

                        {
                            Func.AgvInfo[3].Task_orgin = 12;
                            return res = 14;
                        } 
                        else if (Func.AgvInfo[3].Task_ID != 14 && (Func.AgvInfo[3].Location_Display == 166 || Func.AgvInfo[3].Location_Display == 167 || Func.AgvInfo[3].Location_Display == 162))
                        {
                            Func.AgvInfo[3].Task_orgin = 12;
                            return res = 14;
                        }
                    }
                    else if (Frm_A18164.btn_wireless[10, 1] == 1)
                    {
                        if (Func.AgvInfo[3].Task_ID != 15 && (Func.AgvInfo[3].Location_Display == 164 || Func.AgvInfo[3].Location_Display == 165))
                        {
                            Func.AgvInfo[3].Task_orgin = 12;
                            return res = 15;
                        }
                        else if (Func.AgvInfo[3].Task_ID != 15 && (Func.AgvInfo[3].Location_Display == 166 || Func.AgvInfo[3].Location_Display == 167 || Func.AgvInfo[3].Location_Display == 162))
                        {
                            Func.AgvInfo[3].Task_orgin = 12;
                            return res = 15;
                        }
                    }
                }
                if (Frm_A18164.btn_wireless[9, 1] == 1)
                {
                    if (Frm_A18164.btn_wireless[10, 0] == 1)
                    {
                        if (Func.AgvInfo[3].Task_ID != 14 && (Func.AgvInfo[3].Location_Display == 166 || Func.AgvInfo[3].Location_Display == 167))
                        {
                            Func.AgvInfo[3].Task_orgin = 13;
                            return res = 14;
                        }
                        else if (Func.AgvInfo[3].Task_ID != 14 && (Func.AgvInfo[3].Location_Display == 164 || Func.AgvInfo[3].Location_Display == 165 || Func.AgvInfo[3].Location_Display == 162))
                        {
                            Func.AgvInfo[3].Task_orgin = 13;
                            return res = 14;
                        }
                    }
                    else if (Frm_A18164.btn_wireless[10, 1] == 1)
                    {
                        if (Func.AgvInfo[3].Task_ID != 15 && (Func.AgvInfo[3].Location_Display == 166 || Func.AgvInfo[3].Location_Display == 167))
                        {
                            Func.AgvInfo[3].Task_orgin = 13;
                            return res = 15;
                        }
                        else if (Func.AgvInfo[3].Task_ID != 15 && (Func.AgvInfo[3].Location_Display == 164 || Func.AgvInfo[3].Location_Display == 165 || Func.AgvInfo[3].Location_Display == 162))
                        {
                            Func.AgvInfo[3].Task_orgin = 13;
                            return res = 15;
                        }
                    }
                }
            }
            //-end
            return res;
        }

        private static bool loc_agv(int drect)
        {
            bool res = false;
            if ( drect == 1)
            {
                for (int i = 0; i < Func.AgvInfo.Length; i++)
                {
                    if (Func.AgvInfo[i].Location_Display == 132 ||
                        Func.AgvInfo[i].Location_Display == 134 ||
                        Func.AgvInfo[i].Location_Display == 136 ||
                        Func.AgvInfo[i].Location_Display == 138 ||
                        Func.AgvInfo[i].Location_Display == 140 ||
                        Func.AgvInfo[i].Location_Display == 142 ||
                        Func.AgvInfo[i].Location_Display == 144 || 
                        Func.AgvInfo[i].Location_Display == 146
                        /*
                        Func.AgvInfo[i].Location_Display == 182 ||
                        Func.AgvInfo[i].Location_Display == 184 ||
                        Func.AgvInfo[i].Location_Display == 186 ||
                        Func.AgvInfo[i].Location_Display == 188 */ )
                       
                    {
                        res = true;
                    }
                }
            }
            else if ( drect == 2)
            {
                for (int i = 0; i < Func.AgvInfo.Length; i++)
                {
                    if (Func.AgvInfo[i].Location_Display == 102 ||
                        Func.AgvInfo[i].Location_Display == 104 ||
                        Func.AgvInfo[i].Location_Display == 106 ||
                        Func.AgvInfo[i].Location_Display == 100 || 
                        Func.AgvInfo[i].Location_Display == 148 
                        /*
                        Func.AgvInfo[i].Location_Display == 162 ||
                        Func.AgvInfo[i].Location_Display == 164 ||
                        Func.AgvInfo[i].Location_Display == 166 ||
                        Func.AgvInfo[i].Location_Display == 168 ||
                        Func.AgvInfo[i].Location_Display == 170*/ )
                        
                    {
                        res = true;
                    }
                }
            }    
            return res;
        }

        private static bool loc_agv2()
        {
            bool res = false;
            for (int i = 0; i < Func.AgvInfo.Length; i++)
            {
                if (
                    //-new
                    Func.AgvInfo[i].Location_Display == 132 ||
                    Func.AgvInfo[i].Location_Display == 133 ||
                    Func.AgvInfo[i].Location_Display == 134 ||
                    Func.AgvInfo[i].Location_Display == 135 ||
                    Func.AgvInfo[i].Location_Display == 136 ||
                    Func.AgvInfo[i].Location_Display == 137 ||
                    Func.AgvInfo[i].Location_Display == 138 ||
                    Func.AgvInfo[i].Location_Display == 139 ||
                    Func.AgvInfo[i].Location_Display == 140 ||
                    Func.AgvInfo[i].Location_Display == 141 ||
                    Func.AgvInfo[i].Location_Display == 142 ||
                    Func.AgvInfo[i].Location_Display == 143 ||
                    Func.AgvInfo[i].Location_Display == 144 ||
                    Func.AgvInfo[i].Location_Display == 145 ||
                    Func.AgvInfo[i].Location_Display == 146 ||
                    Func.AgvInfo[i].Location_Display == 147 ||
                    (Func.AgvInfo[i].Location_Display == 109 && Func.AgvInfo[i].Run_Direction == 2) ||
                    (Func.AgvInfo[i].Location_Display == 111 && Func.AgvInfo[i].Run_Direction == 2))
                //-end
                {
                    res = true;
                }
            }
            return res;
        }

        private static byte Agv_Count_Area(int worktype, int[] landmark)
        {
            byte count = 0;
            for (byte i = 0; i < Func.AgvInfo.Length; i++)
            {
                if (Func.AgvInfo[i].WorkType == worktype)
                {
                    for (byte j = 0; j < landmark.Length; j++)
                    {
                        if (Func.AgvInfo[i].Location_Display == landmark[j])
                            count++;
                    }
                }
            }
            return count;
        }


        //本函数为AGV的失联计数器。负几表示失联几次
        private static object SEM_LOST_CONNECT = new object();
        private static void LostConnect(ref int iStatus)    //AGV的状态
        {
            lock (SEM_LOST_CONNECT)
            {
                if (iStatus >= 0)
                    iStatus = -1;
                else
                    iStatus--;
            }
        }

        private static void WarnInfo(string str, out string[] info, out int level)
        {
            int count = 0;
            info = new string[8];
            level = 1;
            for (byte i = 0; i < 8; i++)
            {
                info[i] = "";
            }
            if (str == "0000000000000000")
            {
                info[0] = "正常";
                return;
            }

            for (byte i = 0; i < 64; i++)
            {
                if (str.Substring(63 - i, 1) == "1")
                {
                    if (mainfrm.ds_WarnList.Tables[0].Rows[i][2].ToString() != "0")
                    {
                        info[count] = mainfrm.ds_WarnList.Tables[0].Rows[i][1].ToString();
                        if (level < Convert.ToInt16(mainfrm.ds_WarnList.Tables[0].Rows[i][2].ToString()))
                        {
                            level = Convert.ToInt16(mainfrm.ds_WarnList.Tables[0].Rows[i][2].ToString());
                        }
                        count++;
                    }
                    if (count >= 8)
                        return;
                }
            }
        }

        private static object obj_GetMileage = new object();
        public static float GetMileage(int point1, int point2)
        {
            lock (obj_GetMileage)
            {
                float mileage = 0;
                int t = ds_mileage.Tables[0].Rows.Count;
                DataView rowfilter = new DataView(ds_mileage.Tables[0]);
                rowfilter.RowFilter = "(Start_Point=" + point1 + " or Start_Point=" + point2 + ") and (End_Point=  " + point1 + " or End_Point=  " + point2 + ")";
                rowfilter.RowStateFilter = DataViewRowState.OriginalRows;
                DataTable dt = rowfilter.ToTable();
                int t1 = dt.Rows.Count;
                try
                {
                    mileage = Convert.ToSingle(dt.Rows[0]["Distance"].ToString());
                }
                catch
                {
                }
                return mileage;
            }
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
                        connectStr = Cls.Param.connectStr;
                        sqlconn = new SqlConnection(connectStr);
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


