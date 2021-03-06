﻿using System;
using System.Management;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Threading;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.IO;
using System.IO.Ports;
using CrystalDecisions.CrystalReports.Engine;
using CrystalDecisions.Shared;
using System.Drawing.Printing;

namespace PAutoPrintReport
{

    public class PrintProcessWeight : IDisposable
    {
        #region Constant, Struct and Enum
        const int offsetFC01 = 1;
        const int offsetFC02 = 100001;
        const int offsetFC03 = 400001;
        const int offsetFC04 = 300001;
        const int offsetFC05 = 1;
        const int offsetFC06 = 400001;
        const ushort offsetFC15 = 1;
        const int offsetFC16 = 400001;
        int ID_write = -1;
        private string LoadHeader_NO;
        private string out_ReportID;
        private string out_ReportPath;
        private string out_PrinterName;
        private string out_Parametername;
        private string out_ParameternameGovProject ;
        private string out_ReportPathGovProject ;
        private string out_PrinterNameGovProject;
               
        struct _PrintBuffer
        {
            public int Id;
            public string ReportID;
            public string ReportName;
            public string ReportPath;
            public string PrinterID;
            public string PrinterName;
            public string ParamterName;
            public bool AutoPrint;
            public bool IsEnabled;
            public DateTime TimeStamp;
            public int PrintStatus;
            public string LoadHeader_NO;
        }

        enum _PrintStepProcessWeight : int
        {
            InitialReport=0,
            CheckStatusReport=1,
            LoadReport=10,
            UpdateStatus=11,
            ChangeProcess=30
        } 
        #endregion

       frmMain fMain;
       _PrintBuffer[] PrintBuffer;
        _PrintStepProcessWeight PrintStepProcessWeight;

        int processId=1;
        //int atgAddress;
        DateTime chkResponse;

        #region Construct and Deconstruct
        private bool IsDisposed = false;
        
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
            //mRunn = false;
        }

        protected void Dispose(bool Diposing)
        {
            if (!IsDisposed)
            {
                if (Diposing)
                {
                    //Clean Up managed resources
                    thrShutdown = true; 
                }
                //Clean up unmanaged resources
            }
            IsDisposed = true;
        }
        
        public PrintProcessWeight(frmMain pFrm)
        {
            fMain = pFrm;
        }

        public PrintProcessWeight(frmMain pFrm, int pId)
        {
            fMain = pFrm;
            processId = pId;  
        }

        ~PrintProcessWeight()
        { }
        #endregion

        #region Class Events
        public delegate void ATGEventsHaneler(object pSender, string pEventMsg);
        string logFileName;

        void RaiseEvents(string pSender, string pMsg)
        {
            string vMsg = DateTime.Now + ">[" + pSender + "]" + pMsg;
            logFileName = "Auto Print Weight LLTLB";
            try
            {
                fMain.AddListBox = "" + logFileName + ">" + vMsg;
                //fMain.LogFile.WriteLog(logFileName, vMsg);
            }
            catch (Exception exp)
            { }
        }

        void RaiseEvents(string pMsg)
        {
            logFileName = "Auto Print Weight LLTLB";
            try
            {
                fMain.AddListBox = DateTime.Now + "> " + pMsg;
                //fMain.LogFile.WriteLog(logFileName, vMsg);
            }
            catch (Exception exp)
            { }
        }
        #endregion

        #region Thread
        bool thrConnect;
        bool thrShutdown;
        bool thrRunning;

        Thread thrMain;

        public void StartThread()
        {
            System.Threading.Thread.Sleep(1000);
            thrMain = new Thread(this.RunProcess);
            thrMain.Name = processId.ToString() + "thrPrintLoadingReport";
            thrMain.Start();
        }

        public void StopThread()
        {
            thrShutdown = true;
        }

        private void RunProcess()
        {
            thrRunning = true;
            thrShutdown = false;
            PrintStepProcessWeight = _PrintStepProcessWeight.InitialReport;
            chkResponse = DateTime.Now;
            while (thrRunning)
            {
                try
                {
                    if (thrShutdown)
                        return;
                    switch (PrintStepProcessWeight)
                    {
                        case _PrintStepProcessWeight.InitialReport:
                            if (GetConfigReport())
                            {
                                GetConfigReporGovProjectAttach();
                                PrintStepProcessWeight = _PrintStepProcessWeight.CheckStatusReport;
                            }
                            break;
                        case _PrintStepProcessWeight.CheckStatusReport:
                            //if (CheckPrinterOnline(out_PrinterName))
                            if (CheckStatusReport())
                            {
                                
                                PrintStepProcessWeight = _PrintStepProcessWeight.LoadReport;       //connect via Kepware OPC Server
                            }
                            break;
                        case _PrintStepProcessWeight.LoadReport:
                            if (LoadReportWeight())
                            {
                                //PrintGovProjectAttach();
                                UpdateToDatabase(LoadHeader_NO);
                                PrintStepProcessWeight = _PrintStepProcessWeight.ChangeProcess; 
                            }
                            else
                            {
                                PrintStepProcessWeight = _PrintStepProcessWeight.CheckStatusReport;
                            }
                            break;
                        //case _PrintStepProcessWeight.UpdateStatus:
                        //    UpdateToDatabase(LoadHeader_NO);
                           
                        //    break;
                        case _PrintStepProcessWeight.ChangeProcess:
                            PrintStepProcessWeight = _PrintStepProcessWeight.CheckStatusReport;
                            break;
                    }
                    Thread.Sleep(1000);
                }
                catch (Exception exp)
                { 
                    RaiseEvents(exp.Message + "[source>" + exp.Source + "]");
                    Thread.Sleep(3000);
                }
                //finally
                //{
                //    mShutdown = true;
                //    mRunning = false;
                //}
                Thread.Sleep(500);
            }
        }
        #endregion

        #region define Enum String Value
        enum _DataType
        {
            [EnumValue("BOOLEAN")]
            BOOLEAN = 1,
            [EnumValue("BYTE")]
            BYTE = 2,
            [EnumValue("SHORT INT")]
            SHORT_INT = 3,
            [EnumValue("LONG INT")]
            LONG_INT = 4,
            [EnumValue("FLOAT")]
            FLOAT = 5,
            [EnumValue("STRING")]
            STRING = 6,
        }
        class EnumValue : System.Attribute
        {
            private string _value;
            public EnumValue(string value)
            {
                _value = value;
            }
            public string Value
            {
                get { return _value; }
            }
        }
        static class EnumString
        {
            public static string GetStringValue(Enum value)
            {
                string output = null;
                Type type = value.GetType();
                System.Reflection.FieldInfo fi = type.GetField(value.ToString());
                EnumValue[] attrs = fi.GetCustomAttributes(typeof(EnumValue), false) as EnumValue[];
                if (attrs.Length > 0)
                {
                    output = attrs[0].Value;
                }
                return output;
            }
        } 
        #endregion


      
        #region Change process or comport
        void ChangeProcess()
        {
            Thread.Sleep(3000);
            bool vRet = false;
           
            var vDiff = (DateTime.Now - chkResponse).TotalMinutes;
            if ((vRet) && (vDiff >= 1))
            {
                chkResponse = DateTime.Now;
                thrShutdown = true;
                //ChangeComport();
                RaiseEvents("Communication changed.");
                fMain.ChangeProcess();
            }   
        }

        public bool IsThreadAlive
        {
            get { return thrMain.IsAlive; }
        }
        bool CheckStatusReport()
        {
            DateTime dt_Date = DateTime.Today;
            //DateTime dt_Date = Convert.ToDateTime("10/06/2016");
            string strSQL = "select t.*" +
                            " from tas.OIL_LOAD_HEADERS t " +
                            " where to_date(t.UPDATE_DATE) = to_date('" + dt_Date.ToString("dd/MM/yyyy") + "', 'dd/MM/yyyy')" +
                            " and t.LOAD_STATUS in (71)" +
                             "and t.is_weight_process=1" +
                          // " and t.PRINT_AUTO = 1" +
                            " and t.PRINT_STATUS <> 3" +
                            " order by t.UPDATE_DATE";
            DataSet vDataset = null;
            DataTable dt;
            bool vRet = false;
            try
            {
                if (fMain.OraDb.OpenDyns(strSQL, "TableName", ref vDataset))
                {
                    dt = vDataset.Tables["TableName"];
                    if (dt != null && dt.Rows.Count != 0)
                    {
                        LoadHeader_NO = dt.Rows[0]["LOAD_HEADER_NO"].ToString();
                        vRet = true;
                    }
                }
            }
            catch (Exception exp)
            { fMain.LogFile.WriteErrLog("[Print Weight, Check status loading] " + exp.Message); }
            vDataset = null;
            dt = null;
            return vRet;
        }
        private void  PrintGovProjectAttach()//ปริ้นเอกสารแแนบกรมทาง
        {
            DataSet DatasetGov = null;
            DataTable dtGov;
            string vCheck;
            //Thread.Sleep(1000);
             string strSQLGov = "select t.*" +
                                " from RPT.DAILY_LOADING_DETAIL  t " + 
                                " where t.LOAD_HEADER_NO=" + LoadHeader_NO;
             if (fMain.OraDb.OpenDyns(strSQLGov, "table_name", ref DatasetGov))
             {
                 dtGov = DatasetGov.Tables["table_name"];
                 vCheck = dtGov.Rows[0]["GOV_PROJECT"].ToString();
                 if (vCheck.Equals("1"))
                 {
                     //if (CheckPrinterOnline(out_PrinterNameGovProject))
                     ////{
                         string strSQL = "select t.*" +
                                         " from rpt.VIEW_GOV_PROJECT_ATTACHAS t " + 
                                         " where t.LOAD_HEADER_NO=" + LoadHeader_NO;
                         DataSet vDataset = null;
                         DataTable dt;
                         try
                         {
                             if (fMain.OraDb.OpenDyns(strSQL, "VIEW_GOV_PROJECT_ATTACHAS", ref vDataset))
                             {
                                 dt = vDataset.Tables["VIEW_GOV_PROJECT_ATTACHAS"];
                                 if (dt != null && dt.Rows.Count != 0)
                                 {
                                     ReportDocument cr = new ReportDocument();
                                     string reportPath = fMain.App_Path + "\\Report File\\" + out_ReportPathGovProject;
                                     RaiseEvents(reportPath);
                                     if (!File.Exists(reportPath))
                                     {
                                         fMain.AddListBox = "The specified report does not exist.";
                                     }
                                     Application.DoEvents();
                                     cr.Load(reportPath);
                                     cr.Database.Tables["VIEW_GOV_PROJECT_ATTACHAS"].SetDataSource((DataTable)dt);
                                     cr.SetDatabaseLogon("tas", "tam");
                                     cr.SetParameterValue(out_ParameternameGovProject, LoadHeader_NO);
                                     cr.PrintOptions.PrinterName = out_PrinterNameGovProject;
                                     cr.PrintToPrinter(1, false, 0, 0);
                                     cr.Dispose();
                                     RaiseEvents("Print เอกสารแนบกรมทาง " + LoadHeader_NO);
                                     RaiseEvents(reportPath);
                                 }
                             }

                             Thread.Sleep(2000);
                         }
                         catch (Exception exp)
                         { 
                         RaiseEvents("[Print เอกสารแนบกรมทาง] " + exp.Message);
                         PrintStepProcessWeight =  _PrintStepProcessWeight.InitialReport;
                         }
                         vDataset = null;
                         dt = null;
                     //}
                 }
             }

        }
        private bool LoadReportWeight()
        {
            bool vRet = false;
            Thread.Sleep(2000);
            PrintGovProjectAttach();
            //if (CheckPrinterOnline(out_PrinterName))
            //{
            string strSQL = "select t.*" +
                                " from rpt.VIEW_DELIV_HEADER t " + //VIEW_DATA_WEIGHT
                                " where t.LOAD_HEADER_NO=" + LoadHeader_NO;

                DataSet vDataset = null;
                DataTable dt;
                try
                {
                    if (fMain.OraDb.OpenDyns(strSQL, "VIEW_DELIV_HEADER", ref vDataset))
                    {
                        dt = vDataset.Tables["VIEW_DELIV_HEADER"];
                        if (dt != null && dt.Rows.Count != 0)
                        {
                            ReportDocument cr = new ReportDocument();
                            string reportPath = fMain.App_Path + "\\Report File\\" + out_ReportPath;
                            RaiseEvents(reportPath);
                            if (!File.Exists(reportPath))
                            {
                                fMain.AddListBox = "The specified report does not exist.";
                            }
                            Application.DoEvents();
                            cr.Load(reportPath);
                            cr.Database.Tables["VIEW_DELIV_HEADER"].SetDataSource((DataTable)dt);
                            cr.SetDatabaseLogon("tas", "tam");
                            //cr.SetDataSource((DataTable)dt);
                            cr.SetParameterValue(out_Parametername, LoadHeader_NO);
                            cr.PrintOptions.PrinterName = out_PrinterName;
                            cr.PrintToPrinter(1, false, 0, 0);
                            cr.Dispose();
                            RaiseEvents("Print Report Loading NO " + LoadHeader_NO);
                            RaiseEvents(reportPath);
                            vRet = true;
                        }
                    }

                    Thread.Sleep(2000);
                }
                catch (Exception exp)
                { 
                    RaiseEvents("[Print Loading, Load data report] " + exp.Message);
                    PrintStepProcessWeight = _PrintStepProcessWeight.InitialReport;
                }
                vDataset = null;
                dt = null;
           // }
            return vRet;
        }

        void UpdateToDatabase(string p_LH_NO)
        {
            string vSQL = "update tas.OIL_LOAD_HEADERS t set ";

            vSQL += " t.PRINT_STATUS=3 where t.LOAD_HEADER_NO='" + p_LH_NO + "'";
            fMain.OraDb.ExecuteSQL(vSQL);
            RaiseEvents("Update Print Status = 3 of Load Header NO: " + p_LH_NO);
        }
        private bool GetConfigReport()
        {
        
                out_ReportID = "52010048";
            string strSQL = "select t.*, rt.*" +
                                    " from tas.VIEW_REPORT_PARA_CONFIG t, tas.PRINTER_TAS rt " +
                                    " where t.PRINTER_ID= rt.PRINTER_ID" +
                                    " and t.Report_ID= " + out_ReportID;
            DataSet vDataset = null;
            DataTable dt;
            bool vRet = false;
            try
            {
                if (fMain.OraDb.OpenDyns(strSQL, "TableName", ref vDataset))
                {
                    dt = vDataset.Tables["TableName"];
                    if (dt != null && dt.Rows.Count != 0)
                    {
                        out_Parametername = dt.Rows[0]["PARAMETER_NAME"].ToString();
                        out_ReportPath = dt.Rows[0]["REPORT_PATH"].ToString();
                        out_PrinterName = dt.Rows[0]["PRINTER_NAME"].ToString();
                        vRet = true;
                    }
                } 
            }
            catch (Exception exp)
            { fMain.LogFile.WriteErrLog("[Print Weight, Get config report] " + exp.Message); }
            vDataset = null;
            dt = null;
            return vRet;
        }
        private bool GetConfigReporGovProjectAttach()
        {
            out_ReportID = "52010058";
            string strSQL = "select t.*, rt.*" +
                                    " from tas.VIEW_REPORT_PARA_CONFIG t, tas.PRINTER_TAS rt " +
                                    " where t.PRINTER_ID= rt.PRINTER_ID" +
                                    " and t.Report_ID= " + out_ReportID;
            DataSet vDataset = null;
            DataTable dt;
            bool vRet = false;
            try
            {
                if (fMain.OraDb.OpenDyns(strSQL, "TableName", ref vDataset))
                {
                    dt = vDataset.Tables["TableName"];
                    if (dt != null && dt.Rows.Count != 0)
                    {
                        out_ParameternameGovProject = dt.Rows[0]["PARAMETER_NAME"].ToString();
                        out_ReportPathGovProject = dt.Rows[0]["REPORT_PATH"].ToString();
                        out_PrinterNameGovProject = dt.Rows[0]["PRINTER_NAME"].ToString();
                        vRet = true;
                    }
                }
            }
            catch (Exception exp)
            { fMain.LogFile.WriteErrLog("[Print Weight, Get config report] " + exp.Message); }
            vDataset = null;
            dt = null;
            return vRet;
        }
        private bool CheckPrinter()
        {
            bool online = false;

            if (out_PrinterName!="")
            {
                PrintDocument printDocument = new PrintDocument();
                printDocument.PrinterSettings.PrinterName = out_PrinterName;
                online = printDocument.PrinterSettings.IsValid;
            }
            else
                online = false;

            return online;
        }
        public bool CheckPrinterOnline(string printerToCheck)
        {
            // Set management scope
            ManagementScope scope = new ManagementScope(@"\root\cimv2");
            scope.Connect();

            // Select Printers from WMI Object Collections
            string query = "SELECT * FROM Win32_Printer WHERE Name='" + printerToCheck + "'";
            ManagementObjectSearcher searcher = new ManagementObjectSearcher(query);


            bool IsReady = false;
            string printerName = "";
            foreach (ManagementObject printer in searcher.Get()) 
             {
                 printerName = printer["Name"].ToString();
                 if (string.Equals(printerName, printerToCheck, StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine("Printer = " + printer["Name"]);
                    if (printer["WorkOffline"].ToString().ToLower().Equals("true"))
                    {
                        // printer is offline by user
                        RaiseEvents("Your Plug-N-Play printer is not Connected.");
                    }
                    else
                    {
                        // printer is online
                        IsReady = true;
                        break;
                    }
                }
             }
            return IsReady;
        }
        bool InitialDataBuffer()
        {
            bool vRet = false;
            string vMsg;
            string strSQL = "select" +
                            " t.REPORT_ID,t.REPORT_NAME,t.REPORT_PATH, t.PRINTER_ID, t.STATUS_PRINT, t.IS_ENABLED" +
                            " from tas.REPORT_SETTING t " +
                            " order by t.REPORT_ID";

            DataSet vDataset = null;
            DataTable dt;
            if (fMain.OraDb.OpenDyns(strSQL, "TableName", ref vDataset))
            {
                dt = vDataset.Tables["TableName"];
                PrintBuffer = new _PrintBuffer[dt.Rows.Count];
                for (int i = 0; i < dt.Rows.Count; i++)
                {
                    PrintBuffer[i].ReportID = dt.Rows[i]["REPORT_ID"].ToString();
                    PrintBuffer[i].ReportName = dt.Rows[i]["REPORT_NAME"].ToString();
                    PrintBuffer[i].ReportPath = dt.Rows[i]["REPORT_PATH"].ToString();
                    PrintBuffer[i].PrinterID = dt.Rows[i]["PRINTER_ID"].ToString();
                    PrintBuffer[i].PrintStatus = Convert.ToInt16(dt.Rows[i]["STATUS_PRINT"].ToString());
                    PrintBuffer[i].IsEnabled = Convert.ToBoolean(dt.Rows[i]["IS_ENABLED"].ToString());

                    vMsg = string.Format("Initial {0}, {1}, address={2} Data type={3}.",
                                          (i + 1).ToString(),
                                          PrintBuffer[i].ReportID,
                                          PrintBuffer[i].ReportName,
                                          PrintBuffer[i].PrintStatus);
                    RaiseEvents(vMsg);
                    vRet = true;
                }
            }
            return vRet;
        }
        #endregion
    }
}
