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


namespace AGVSystem
{
    public class Cls_A18164Btn
    {
        public  void Btn_Data(object ID)
        {
            Ping pingAgv = new Ping();
            int errCount = 0;
            int id = (int)ID;
            string strip = "192.168.127." + (30+id).ToString();
            bool bPlcLinked = false;
            Cls_A18164BtnClient Link_To_PLC = new Cls_A18164BtnClient(strip);
            ClsDBConn_sql db = new ClsDBConn_sql();

            while (true)
            {
                try
                {
                    if (pingAgv.Send(strip, 150).Status == IPStatus.Success)
                    {
                        //来到这里，表示PING成功。
                        if (!bPlcLinked)
                        {
                            if (Link_To_PLC.OpenLinkPLC())
                            {   //来到这里，表示链路成功打开
                                bPlcLinked = true;
                            }
                            else
                            {
                                Frm_A18164.btn_wireless[id - 1, 2] = 0;
                                //Frm_A18164.Pic_Single[id - 1].BackgroundImage = Properties.Resources.hui;
                                //来到这里，表示要重新建立链路
                                bPlcLinked = false;
                                Link_To_PLC.CloseLinkPLC();
                                Link_To_PLC = new Cls_A18164BtnClient(strip);
                            }
                        }
                        else
                        {
                            //来到这里，表示PLC的链路已经打开，可以直接读写数据
                            if (Link_To_PLC.ReadVM(id) != 0)
                            {
                                //来到这里，表示读数据失败  
                                errCount++;

                                if (errCount >= 3)
                                {
                                    Frm_A18164.btn_wireless[id - 1, 1] = 0;
                                    Frm_A18164.btn_wireless[id - 1, 0] = 0;
                                    //Frm_A18164.Pic_Single[id - 1].BackgroundImage = Properties.Resources.hui;
                                    //来到这里，表示一再失败，要重新建立链路                                    
                                    bPlcLinked = false;
                                    errCount = 0;
                                    Link_To_PLC.CloseLinkPLC();
                                    Link_To_PLC = new Cls_A18164BtnClient(strip);
                                }
                            }
                            else
                            {
                                Frm_A18164.btn_wireless[id - 1, 2] = 1;
                                if (errCount > 0)
                                {
                                    errCount = 0;
                                }
                                Link_To_PLC.WriteVM(id, Frm_A18164.btn_wireless[id-1,0], Frm_A18164.btn_wireless[id - 1,1]);
                                //for (int i = 4; i < 8; i++)
                                //{
                                //    Frm_A18164.btn_wireless[i, 0] = 0;
                                //    Frm_A18164.btn_wireless[i, 1] = 0;
                                //}
                                //string str = "select [WorkTaskID] from [TakeTask] where [TaskStatus]=0 or [TaskStatus]=1 order by StartTime";
                                //mainfrm.ds_WorkTask.Clear();
                                //mainfrm.ds_WorkTask = db.connDt(str).Copy();
                                //db.ConnClosed();

                                //for (int i = 0; i < mainfrm.ds_WorkTask.Tables[0].Rows.Count; i++)
                                //{
                                //    switch (int.Parse(mainfrm.ds_WorkTask.Tables[0].Rows[i]["WorkTaskID"].ToString()))
                                //    {
                                //        case 1: Frm_A18164.btn_wireless[4, 1] = 1;break;
                                //        case 2: Frm_A18164.btn_wireless[4, 0] = 1; break;
                                //        case 3: Frm_A18164.btn_wireless[5, 1] = 1; break;
                                //        case 4: Frm_A18164.btn_wireless[5, 0] = 1; break;
                                //        case 5: Frm_A18164.btn_wireless[6, 0] = 1; break;
                                //        case 6: Frm_A18164.btn_wireless[6, 1] = 1; break;
                                //        case 7: Frm_A18164.btn_wireless[7, 1] = 1; break;
                                //        default: Frm_A18164.btn_wireless[7, 0] = 1; break;
                                //    }
                                //}

                                //if (id >= 1 && id <= 4)
                                //{
                                //    if (Frm_A18164.btn_wireless[id - 1, 0] == 1)
                                //    {
                                //        if (Link_To_PLC.WriteVM(id, 1, 1) == 0)
                                //        {
                                //            //接到按钮信号
                                //        }
                                //    }
                                //    else
                                //    {
                                //        if (Link_To_PLC.WriteVM(id, 0, 1) == 0)
                                //        {
                                //            //取消信号指示
                                //        }
                                //    }
                                //}
                                //else if (id >= 5 && id <= 8)
                                //{
                                //    if (Frm_A18164.btn_wireless[id-1, 0] == 1)
                                //    {
                                //        if (Link_To_PLC.WriteVM(id, 1, 0) == 0)
                                //        {
                                //            //1号工位当前有任务
                                //        }
                                //    }

                                //    else  if (Frm_A18164.btn_wireless[id - 1, 1] == 1)
                                //    {
                                //        if (Link_To_PLC.WriteVM(id, 0, 1) == 0)
                                //        {
                                //            //2号工位当前有任务
                                //        }
                                //    }
                                //    else
                                //    {
                                //        if (Link_To_PLC.WriteVM(id, 0, 0) == 0)
                                //        {
                                //            //1号工位当前有任务
                                //        }
                                //    }

                                //}
                            }
                        }
                    }
                    else
                    {
                        if (bPlcLinked)
                        {
                            errCount++;
                            if (errCount > 5)
                            {
                                Frm_A18164.btn_wireless[id-1, 2] = 0;
                                errCount = 0;
                                bPlcLinked = false;
                                Link_To_PLC.CloseLinkPLC();
                                Link_To_PLC = new Cls_A18164BtnClient(strip);
                            }
                        }
                    }
                }
                catch
                {
                    if (bPlcLinked)
                    {
                        errCount++;
                        if (errCount > 5)
                        {
                            Frm_A18164.btn_wireless[id-1, 2] = 0;
                            //Frm_A18164.Pic_Single[id - 1].BackgroundImage = Properties.Resources.hui;
                            errCount = 0;
                            bPlcLinked = false;
                            Link_To_PLC.CloseLinkPLC();
                            Link_To_PLC = new Cls_A18164BtnClient(strip);
                        }
                    }
                    Thread.Sleep(1000);
                }
                Thread.Sleep(500);
            }   

        }
    }
}
