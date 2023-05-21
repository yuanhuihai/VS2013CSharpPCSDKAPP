using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
//using System.Threading.Tasks;
using System.Windows.Forms;
using ABB.Robotics.Controllers;
using ABB.Robotics.Controllers.Discovery;
using ABB.Robotics.Controllers.RapidDomain;
using ABB.Robotics.Controllers.IOSystemDomain;
using ABB.Robotics.Controllers.EventLogDomain;
using ABB.Robotics.Controllers.MotionDomain;
using ABB.Robotics.Controllers.Messaging;

namespace VS2013CSharpPCSDKAPP
{
    public partial class Form1 : Form
    {
        #region variable declaration

        private NetworkScanner scanner = null;
        private Controller controller = null;
        private Task[] tasks = null;
        private NetworkWatcher networkwatcher = null;

        //I/O System
        private Signal mDI1, mDO1,mAI1,mAO1;

        //Rapid
        private ABB.Robotics.Controllers.RapidDomain.RapidData rd_bool1, rd_num1, 
                                                          rd_pose1, rd_numArray1;

        //Event Log
        private ABB.Robotics.Controllers.EventLogDomain.EventLog eLog1;

        //Messaging
        private IpcQueue tRob1Queue,myQueue;
        private IpcMessage sendMessage, recMessage;

        #endregion

        #region Form1

        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            this.scanner = new NetworkScanner();
            this.scanner.Scan();
            ControllerInfoCollection controllers = scanner.Controllers;
            ListViewItem item = null;
            foreach (ControllerInfo controllerInfo in controllers)
            {
                item = new ListViewItem(controllerInfo.IPAddress.ToString());
                item.SubItems.Add(controllerInfo.Id);
                item.SubItems.Add(controllerInfo.Availability.ToString());
                item.SubItems.Add(controllerInfo.IsVirtual.ToString());
                item.SubItems.Add(controllerInfo.SystemName);
                item.SubItems.Add(controllerInfo.Version.ToString());
                item.SubItems.Add(controllerInfo.ControllerName);
                this.listView1.Items.Add(item);
                item.Tag = controllerInfo;
            }

            this.networkwatcher = new NetworkWatcher(scanner.Controllers);
            this.networkwatcher.Found += new EventHandler<NetworkWatcherEventArgs>(HandleFoundEvent);
            this.networkwatcher.Lost += new EventHandler<NetworkWatcherEventArgs>(HandleLostEvent);
            this.networkwatcher.EnableRaisingEvents = true;
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            try
            {
                DialogResult result = MessageBox.Show("您确定要关闭主程序吗？", "主程序关闭", MessageBoxButtons.OKCancel);
                if (result == DialogResult.OK)
                {
                    if (networkwatcher != null)
                    {
                        networkwatcher.Found -= new EventHandler<NetworkWatcherEventArgs>(HandleFoundEvent);
                        networkwatcher.Lost -= new EventHandler<NetworkWatcherEventArgs>(HandleLostEvent);
                        networkwatcher = null;
                    }
                    if (controller != null)
                    {
                        if (controller.Connected)
                        {
                            controller.Logoff();
                        }
                        controller.Dispose();
                        controller = null;
                    }
                    e.Cancel = false;
                }
                else
                {
                    e.Cancel = true;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        #endregion

        #region Found/LostEvent

        void HandleFoundEvent(object sender, NetworkWatcherEventArgs e)
        {
            this.Invoke(new
            EventHandler<NetworkWatcherEventArgs>(AddControllerToListView),
            new Object[] { this, e });
        }
        private void AddControllerToListView(object sender, NetworkWatcherEventArgs e)
        {
            ControllerInfo controllerInfo = e.Controller; 
            ListViewItem item = new ListViewItem(controllerInfo.IPAddress.ToString());
            item.SubItems.Add(controllerInfo.Id); 
            item.SubItems.Add(controllerInfo.Availability.ToString()); 
            item.SubItems.Add(controllerInfo.IsVirtual.ToString()); 
            item.SubItems.Add(controllerInfo.SystemName); 
            item.SubItems.Add(controllerInfo.Version.ToString()); 
            item.SubItems.Add(controllerInfo.ControllerName);
            this.listView1.Items.Add(item); item.Tag = controllerInfo;
        }

        void HandleLostEvent(object sender, NetworkWatcherEventArgs e)
        {
            this.Invoke(new EventHandler<NetworkWatcherEventArgs>(RemoveControllerFromListView), new object[] { sender, e });
        }
        private void RemoveControllerFromListView(object sender, NetworkWatcherEventArgs e)
        {
            foreach (ListViewItem item in this.listView1.Items)
            {
                if ((ControllerInfo)item.Tag == e.Controller)
                {
                    this.listView1.Items.Remove(item);
                    break;
                }
            }
        }

        #endregion

        #region Logon&StartRapidProgram

        private void listView1_DoubleClick(object sender, EventArgs e)
        {
            try
            {
                ListViewItem item = this.listView1.SelectedItems[0];
                if (item.Tag != null)
                {
                    ControllerInfo controllerInfo = (ControllerInfo)item.Tag;
                    if (controllerInfo.Availability == Availability.Available)
                    {
                        if (this.controller != null)
                        {
                            this.controller.Logoff();
                            this.controller.Dispose();
                            this.controller = null;
                        }
                        this.controller = ControllerFactory.CreateFrom(controllerInfo);
                        this.controller.Logon(UserInfo.DefaultUser);
                        //如果连接成功，则双击行变蓝
                        if (controller.Connected)
                        {
                            item.Selected = false;
                            item.ForeColor = Color.Blue;

                            controller.ConnectionChanged += new EventHandler<ConnectionChangedEventArgs>(controller_ConnectionChanged);
                            // PC变量与对应的机器人变量映射，并注册事件
                            // I/O System
                            mDI1 = controller.IOSystem.GetSignal("DI1");
                            mDI1.Changed += new EventHandler<SignalChangedEventArgs>(mDI1_Changed);
                            mDO1 = controller.IOSystem.GetSignal("DO1");
                            mDO1.Changed += new EventHandler<SignalChangedEventArgs>(mDO1_Changed);
                            mAI1 = controller.IOSystem.GetSignal("AI1");
                            mAI1.Changed += new EventHandler<SignalChangedEventArgs>(mAI1_Changed);
                            mAO1 = controller.IOSystem.GetSignal("AO1");
                            mAO1.Changed += new EventHandler<SignalChangedEventArgs>(mAO1_Changed);
                            //Rapid
                            rd_bool1 = controller.Rapid.GetRapidData("T_ROB1", "Data", "bool1");
                            rd_bool1.ValueChanged += new EventHandler<DataValueChangedEventArgs>(rd_bool1_ValueChanged);
                            rd_num1 = controller.Rapid.GetRapidData("T_ROB1", "Data", "num1");
                            rd_num1.ValueChanged += new EventHandler<DataValueChangedEventArgs>(rd_num1_ValueChanged);
                            rd_pose1 = controller.Rapid.GetRapidData("T_ROB1", "Data", "pose1");
                            rd_pose1.ValueChanged += new EventHandler<DataValueChangedEventArgs>(rd_pose1_ValueChanged);
                            rd_numArray1 = controller.Rapid.GetRapidData("T_ROB1", "Data", "numArray1");
                            rd_numArray1.ValueChanged += new EventHandler<DataValueChangedEventArgs>(rd_numArray1_ValueChanged);
                            //Event Log
                            eLog1 = controller.EventLog;
                            eLog1.MessageWritten += new EventHandler<MessageWrittenEventArgs>(eLog1_MessageWritten);
                            //Messaging
                            //get T_ROB1 queue to send msgs to RAPID task
                            tRob1Queue = controller.Ipc.GetQueue("RMQ_T_ROB1");
                            //create my own PC SDK queue to receive msgs 
                            if (!controller.Ipc.Exists("RAB_Q"))
                            {
                                myQueue = controller.Ipc.CreateQueue("PC_SDK_Q", 5, Ipc.IPC_MAXMSGSIZE);
                                myQueue = controller.Ipc.GetQueue("PC_SDK_Q");
                            }
                            //Create IpcMessage objects for sending and receiving
                            sendMessage = new IpcMessage();
                            recMessage = new IpcMessage();
                        }
                        else
                        {
                            item.Selected = false;
                            item.ForeColor = Color.Black;
                        }
                    }
                    else
                    {
                        MessageBox.Show("Selected controller not available.");
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        private void controller_ConnectionChanged(object sender, ConnectionChangedEventArgs e)
        {
            this.Invoke(new EventHandler<ConnectionChangedEventArgs>(HandleConnectionChangedEvent), new object[] { this, e });
        }
        private void HandleConnectionChangedEvent(object sender, ConnectionChangedEventArgs e)
        {
            try
            {
                if (!e.Connected)
                {
                    this.controller.ConnectionChanged -= new EventHandler<ConnectionChangedEventArgs>(controller_ConnectionChanged);
                    mDI1.Changed -= new EventHandler<SignalChangedEventArgs>(mDI1_Changed);
                    mDO1.Changed -= new EventHandler<SignalChangedEventArgs>(mDO1_Changed);
                    mAI1.Changed -= new EventHandler<SignalChangedEventArgs>(mAI1_Changed);
                    mAO1.Changed -= new EventHandler<SignalChangedEventArgs>(mAO1_Changed);
                    rd_bool1.ValueChanged -= new EventHandler<DataValueChangedEventArgs>(rd_bool1_ValueChanged);
                    rd_num1.ValueChanged -= new EventHandler<DataValueChangedEventArgs>(rd_num1_ValueChanged);
                    rd_pose1.ValueChanged -= new EventHandler<DataValueChangedEventArgs>(rd_pose1_ValueChanged);
                    rd_numArray1.ValueChanged -= new EventHandler<DataValueChangedEventArgs>(rd_numArray1_ValueChanged);
                    eLog1.MessageWritten -= new EventHandler<MessageWrittenEventArgs>(eLog1_MessageWritten); 
                }
            }
            catch (Exception)
            {
            }
        }

        private void btnStartRapidProgram_Click(object sender, EventArgs e)
        {
            try
            {
                if (controller.OperatingMode == ControllerOperatingMode.Auto)
                {
                    tasks = controller.Rapid.GetTasks();
                    using (Mastership m = Mastership.Request(controller.Rapid))
                    {
                        tasks[0].SetProgramPointer("MainModule", "main");
                        //Perform operation
                        controller.Rapid.Start();
                    }
                }
                else
                {
                    MessageBox.Show("Automatic mode is required to start execution from a remote client.");
                }
            }
            catch (System.InvalidOperationException ex)
            {
                MessageBox.Show("Mastership is held by another client." + ex.Message);
            }
            catch (System.Exception ex)
            {
                MessageBox.Show("Unexpected error occurred: " + ex.Message);
            }
        }

        #endregion

        #region I/O System

        private void mDI1_Changed(object sender, SignalChangedEventArgs e)
        {
            this.Invoke(new EventHandler<SignalChangedEventArgs>(Update_mDI1), 
            new object[] { this, e });
        }
        private void Update_mDI1(object sender, SignalChangedEventArgs e)
        {
            if (e.NewSignalState.Value == 1)
                cBDI1.Checked = true;
            else
                cBDI1.Checked = false;
        }

        private void mDO1_Changed(object sender, SignalChangedEventArgs e)
        {
            this.Invoke(new EventHandler<SignalChangedEventArgs>(Update_mDO1), new object[] { this, e });
        }
        private void Update_mDO1(object sender, SignalChangedEventArgs e)
        {
            if (e.NewSignalState.Value == 1)
                cBDO1.Checked = true;
            else
                cBDO1.Checked = false;
        }

        private void btnDI1_Click(object sender, EventArgs e)
        {
            try
            {
                if (controller != null && controller.Connected)
                {
                    if (btnDI1.Text == "Set_DI1")
                    {
                        mDI1.Value = 1;
                        //((DigitalSignal)mDI1).Set(); //也可以这样使用
                        btnDI1.Text = "Reset_DI1";
                    }
                    else
                    {
                        mDI1.Value = 0;
                        //((DigitalSignal)mDI1).Reset(); //也可以这样使用
                        btnDI1.Text = "Set_DI1";
                    }
                }
                else
                {
                    MessageBox.Show("控制器没有连接！");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        private void btnDO1_Click(object sender, EventArgs e)
        {
            try
            {
                if (controller != null && controller.Connected)
                {
                    if (btnDO1.Text == "Set_DO1")
                    {
                        mDO1.Value = 1;
                        //((DigitalSignal)mDO1).Set(); //也可以这样使用
                        btnDO1.Text = "Reset_DO1";
                    }
                    else
                    {
                        mDO1.Value = 0;
                        //((DigitalSignal)mDO1).Reset(); //也可以这样使用
                        btnDO1.Text = "Set_DO1";
                    }
                }
                else
                {
                    MessageBox.Show("控制器没有连接！");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        private void mAI1_Changed(object sender, SignalChangedEventArgs e)
        {
            this.Invoke(new EventHandler<SignalChangedEventArgs>(Update_mAI1), new object[] { this, e });
        }
        private void Update_mAI1(object sender, SignalChangedEventArgs e)
        {
            lbl_AI1.Text = Convert.ToString(e.NewSignalState.Value);
        }

        private void mAO1_Changed(object sender, SignalChangedEventArgs e)
        {
            this.Invoke(new EventHandler<SignalChangedEventArgs>(Update_mAO1), new object[] { this, e });
        }
        private void Update_mAO1(object sender, SignalChangedEventArgs e)
        {
            lbl_AO1.Text = Convert.ToString(e.NewSignalState.Value);
        }

        private void btnSetAI1_Click(object sender, EventArgs e)
        {
            try
            {
                if (controller != null && controller.Connected)
                {
                    float fAI1 = Convert.ToSingle(nAI1.Value);
                    mAI1.Value = fAI1;
                }
                else
                {
                    MessageBox.Show("控制器没有连接！");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        private void btnSetAO1_Click(object sender, EventArgs e)
        {
            try
            {
                if (controller != null && controller.Connected)
                {
                    float fAO1 = Convert.ToSingle(nAO1.Value);
                    mAO1.Value = fAO1;
                }
                else
                {
                    MessageBox.Show("控制器没有连接！");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        #endregion

        #region Rapid

        private void rd_bool1_ValueChanged(object sender, DataValueChangedEventArgs e)
        {
            this.Invoke(new EventHandler<DataValueChangedEventArgs>(Update_rd_bool1), new object[] { this, e });
        }
        private void Update_rd_bool1(object sender, DataValueChangedEventArgs e)
        {
            if (rd_bool1.Value is Bool )
            {
                Bool rapidBool = (Bool)rd_bool1.Value;
                cBbool1.Checked = rapidBool.Value;
            }
        }
        private void btnSet_bool1_Click(object sender, EventArgs e)
        {
            try
            {
                if (controller != null && controller.Connected)
                {
                    Bool mBool = new Bool();
                    if (btnSet_bool1.Text == "Set_bool1")
                    {
                        mBool.Value = true;
                        using (Mastership m = Mastership.Request(controller.Rapid))
                        { rd_bool1.Value = mBool; }
                        btnSet_bool1.Text = "Reset_bool1";
                    }
                    else
                    {
                        mBool.Value = false;
                        using (Mastership m = Mastership.Request(controller.Rapid))
                        { rd_bool1.Value = mBool; }
                        btnSet_bool1.Text = "Set_bool1";
                    }
                }
                else
                {
                    MessageBox.Show("控制器没有连接！");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        private void rd_num1_ValueChanged(object sender, DataValueChangedEventArgs e)
        {
            this.Invoke(new EventHandler<DataValueChangedEventArgs>(Update_rd_num1), 
            new object[] { this, e });
        }
        private void Update_rd_num1(object sender, DataValueChangedEventArgs e)
        {
            if (rd_num1.Value is Num)
            {
                Num rapidNum = (Num)rd_num1.Value;
                lbl_num1.Text = Convert.ToString(rapidNum.Value);
            }
        }
        private void btnSet_num1_Click(object sender, EventArgs e)
        {
            try
            {
                if (controller != null && controller.Connected)
                {
                    Num mNum=new Num();
                    mNum.Value = Convert.ToDouble(n_num1.Value);
                    using (Mastership m = Mastership.Request(controller.Rapid))
                    { rd_num1.Value = mNum; }
                }
                else
                {
                    MessageBox.Show("控制器没有连接！");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        private void rd_pose1_ValueChanged(object sender, DataValueChangedEventArgs e)
        {
            this.Invoke(new EventHandler<DataValueChangedEventArgs>(Update_rd_pose1), new object[] { this, e });
        }
        private void Update_rd_pose1(object sender, DataValueChangedEventArgs e)
        {
            if (rd_pose1.Value is Pose)
            {
                Pose mPose = (Pose)rd_pose1.Value;
                lbl_pose1.Text = mPose.ToString();
            }
        }
        private void btnSetPose1_Click(object sender, EventArgs e)
        {
            try
            {
                if (controller != null && controller.Connected)
                {
                    Pose mPose=new Pose();
                    string trans = "[" + nx.Value.ToString() + "," + ny.Value.ToString() + "," 
                                 + nz.Value.ToString() + "]";
                    mPose.Trans.FillFromString2(trans);
                    string rot = "[" + q1.Value.ToString() + "," + q2.Value.ToString() + "," 
                               + q3.Value.ToString() + "," + q4.Value.ToString() + "]";
                    mPose.Rot.FillFromString2(rot);
                    using (Mastership m = Mastership.Request(controller.Rapid))
                    { rd_pose1.Value = mPose; }
                }
                else
                {
                    MessageBox.Show("控制器没有连接！");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        private void rd_numArray1_ValueChanged(object sender, DataValueChangedEventArgs e)
        {
            this.Invoke(new EventHandler<DataValueChangedEventArgs>(Update_rd_numArray1), new object[] { this, e });
        }
        private void Update_rd_numArray1(object sender, DataValueChangedEventArgs e)
        {
            if (rd_numArray1.IsArray)
            {
                lbl_numArray1.Text = rd_numArray1.Value.ToString();
            }
        }
        private void nArrayIndex_ValueChanged(object sender, EventArgs e)
        {
            try
            {
                btnSetNumArray.Text = "Set_numArray1{" + (int)nArrayIndex.Value + "}";
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }
        private void btnSetNumArray_Click(object sender, EventArgs e)
        {
            try
            {
                if (controller != null && controller.Connected)
                {
                    Num nNum = new Num();
                    nNum.Value =(double)n_Value.Value;              
                    using (Mastership m = Mastership.Request(controller.Rapid))
                    { rd_numArray1.WriteItem(nNum, (int)nArrayIndex.Value); }
                }
                else
                {
                    MessageBox.Show("控制器没有连接！");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        #endregion

        #region EventLog

        private void eLog1_MessageWritten(object sender,MessageWrittenEventArgs e)
        {
            this.Invoke(new EventHandler<MessageWrittenEventArgs>
            (Update_eLog1_MessageWritten), new object[] { this, e });
        }
        private void Update_eLog1_MessageWritten(object sender, MessageWrittenEventArgs e)
        {
            ListViewItem item = null;
            EventLogCategory cat = eLog1.GetCategory(CategoryType.Common);
            foreach (EventLogMessage elm in cat.Messages)
            {
                item = new ListViewItem(elm.Title);
                item.SubItems.Add(elm.Timestamp.ToString());
                item.SubItems.Add(elm.Type.ToString());
                listView2.Items.Add(item);
                item.Tag = elm;
            }
        }

        #endregion

        #region Motion

        private void btnGetMotionInfo_Click(object sender, EventArgs e)
        {
            try
            {
                MotionSystem ms1;
                if (controller != null && controller.Connected)
                {
                    ms1 = controller.MotionSystem;
                    listBox1.Items.Add("name: " + ms1.Name);
                    listBox1.Items.Add("speed: " + ms1.SpeedRatio.ToString());
                    listBox1.Items.Add("ActiveMechanicalUnit->Name: "
                          + ms1.ActiveMechanicalUnit.Name);
                    listBox1.Items.Add("ActiveMechanicalUnit->Model: "
                          + ms1.ActiveMechanicalUnit.Model);
                    listBox1.Items.Add("ActiveMechanicalUnit->NumberOfAxes: "
                          + ms1.ActiveMechanicalUnit.NumberOfAxes.ToString());
                    listBox1.Items.Add("ActiveMechanicalUnit->SerialNumber: "
                          + ms1.ActiveMechanicalUnit.SerialNumber);
                    RobTarget aRobTarget = ms1.ActiveMechanicalUnit.
                                           GetPosition(CoordinateSystemType.World);
                    listBox1.Items.Add("RobTarget: " + aRobTarget.ToString());
                    JointTarget aJointTarget = ms1.ActiveMechanicalUnit.GetPosition();
                    listBox1.Items.Add("JointTarget: " + aJointTarget.ToString());
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        #endregion

        #region Messaging

        private void btnSendMessage_Click(object sender, EventArgs e)
        {
            try
            {
                if (controller != null && controller.Connected)
                {
                    SendMessage(true);
                    CheckReturnMsg();
                }
                else
                {
                    MessageBox.Show("控制器没有连接！");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }

        }
        public void SendMessage(bool boolMsg)
        {
            try
            {
                byte[] data = null;
                //Create message data 
                if (boolMsg)
                {
                    data = new UTF8Encoding().GetBytes("bool;TRUE");
                }
                else
                {
                    data = new UTF8Encoding().GetBytes("bool;FALSE");
                }
                //Place data and sender information in message
                sendMessage.SetData(data);
                sendMessage.Sender = myQueue.QueueId;
                //Send message to the RAPID queue
                tRob1Queue.Send(sendMessage);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }
        private void CheckReturnMsg()
        {
            try
            {
                IpcReturnType ret = IpcReturnType.Timeout;
                string answer = string.Empty;
                int timeout = 5000;
                //Check for msg in the PC SDK queue
                ret = myQueue.Receive(timeout, recMessage);
                if (ret == IpcReturnType.OK)
                {
                    //convert msg data to string
                    answer = new UTF8Encoding().GetString(recMessage.Data);
                    MessageBox.Show(answer);
                    //MessageBox should show: string;"Acknowledged"
                }
                else
                {
                    MessageBox.Show("Timeout!");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        #endregion

    }
}
