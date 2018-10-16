using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using Microsoft.SharePoint.Client;
using System.Data;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Worksheet = DocumentFormat.OpenXml.Spreadsheet.Worksheet;
using Sheets = DocumentFormat.OpenXml.Spreadsheet.Sheets;
using Microsoft.Office.Interop.Excel;
using Workbook = Microsoft.Office.Interop.Excel.Workbook;
using System.Runtime.InteropServices;
using Field = Microsoft.SharePoint.Client.Field;

namespace SharePointCSOMAssessment
{
    class SharePointListLibrary : Exception
    {
        public static System.Data.DataTable ExcelDataTable;
        public static string DiskFilePath = string.Empty; 
        static void Main(string[] args)
        {
            try
            {
                Console.WriteLine("Enter User Email : ");
                string userName = Console.ReadLine();                   // "dharanendra.sheetal@acuvate.com";
                Console.WriteLine("Enter your password.");
                //SecureString sec = new SecureString();

                SecureString Password = GetPassword();

                //Url with site name
                using (var ClientContext = new ClientContext(ApplicationConfiguration.ApplicationUrl))
                {
                    ClientContext.Credentials = new SharePointOnlineCredentials(userName, Password);

                    try
                    {
                        GetExcelFileDetails(ClientContext);
                        UploadFilesAndData(ClientContext, ExcelDataTable);
                        UploadFileToSP(ClientContext);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("Exception caught : " + e.Message);
                        ErrorWriteToLog.WriteToLogFile(e);
                    }
                }
            }
            catch (Exception exe)
            {
                Console.WriteLine("Exception caught : " + exe.Message);
                ErrorWriteToLog.WriteToLogFile(exe);
            }

            Console.ReadKey();
        }

        //Get Excel file Details
        public static void GetExcelFileDetails(ClientContext ClientContext)
        {
            List Empoyeelist = ClientContext.Web.Lists.GetByTitle("UserDocuments");
            CamlQuery CamlQuery1 = new CamlQuery();
            CamlQuery1.ViewXml = "<View><RowLimit></RowLimit></View>";

            ListItemCollection EmpCollection = Empoyeelist.GetItems(CamlQuery1);
            ClientContext.Load(EmpCollection);
            ClientContext.ExecuteQuery();

            Microsoft.SharePoint.Client.File ExcelFile = EmpCollection[0].File;
            ClientContext.Load(ExcelFile);
            ClientContext.ExecuteQuery();
            var FilePath1 = EmpCollection[0].File.ServerRelativeUrl;
            var FileInfo1 = Microsoft.SharePoint.Client.File.OpenBinaryDirect(ClientContext, FilePath1);

            if (ExcelFile != null)
            {
              //  DiskFilePath = LocalFilePath + EmpCollection[0].File.Name;
                try
                {
                    var fileName = Path.Combine(ApplicationConfiguration.LocalFilePath+DiskFilePath, (string)EmpCollection[0].File.Name);
                    DiskFilePath = EmpCollection[0].File.Name;
                    if (System.IO.File.Exists(fileName))
                    {
                        System.IO.File.Delete(fileName);
                    }

                    /****************Creates File in the specified path*****************/
                    using (var FileStream1 = System.IO.File.Create(fileName))
                    {
                        FileInfo1.Stream.CopyTo(FileStream1);
                        FileInfo1.Stream.Close();
                        FileStream1.Dispose();
                    }
                }
                catch (Exception exc)
                {
                    Console.WriteLine("Exception exc : " + exc.Message);
                    ErrorWriteToLog.WriteToLogFile(exc);
                }
            }

            string StrErrorMsg = string.Empty;

            /*************************DataSet Different Approcach***************************************/

            /*************************DataSet Different Approcach***************************************/
            try
            {
                ExcelDataTable = new System.Data.DataTable("ExcelFileDataTable");

                ClientResult<System.IO.Stream> Data = ExcelFile.OpenBinaryStream();
                ClientContext.Load(ExcelFile);
                ClientContext.ExecuteQuery();
                using (System.IO.MemoryStream mStream = new System.IO.MemoryStream())
                {
                    if (Data != null)
                    {
                        Data.Value.CopyTo(mStream);
                        using (SpreadsheetDocument Document1 = SpreadsheetDocument.Open(mStream, false))
                        {
                            IEnumerable<Sheet> Sheets1 = Document1.WorkbookPart.Workbook.GetFirstChild<Sheets>().Elements<Sheet>();
                            string RelationshipId = Sheets1.First().Id.Value;
                            WorksheetPart WorksheetPart1 = (WorksheetPart)Document1.WorkbookPart.GetPartById(RelationshipId);
                            Worksheet WorkSheet1 = WorksheetPart1.Worksheet;
                            SheetData SheetData1 = WorkSheet1.GetFirstChild<SheetData>();
                            IEnumerable<Row> Rows = SheetData1.Descendants<Row>();
                            foreach (Cell Cell1 in Rows.ElementAt(0))
                            {
                                string StrCellValue = GetCellValue(ClientContext, Document1, Cell1);
                                ExcelDataTable.Columns.Add(StrCellValue);
                            }
                            foreach (Row RowLoop in Rows)
                            {
                                if (RowLoop != null)
                                {
                                    DataRow DataRow1 = ExcelDataTable.NewRow();
                                    for (int iterator = 0; iterator < RowLoop.Descendants<Cell>().Count(); iterator++)
                                    {
                                        DataRow1[iterator] = GetCellValue(ClientContext, Document1, RowLoop.Descendants<Cell>().ElementAt(iterator));
                                    }
                                    ExcelDataTable.Rows.Add(DataRow1);
                                }
                            }
                            ExcelDataTable.Rows.RemoveAt(0);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception exx " + e);
                ErrorWriteToLog.WriteToLogFile(e);
            }
        }

        /******************Gets Value from each cell of excel file*****************************/
        private static string GetCellValue(ClientContext clientContext, SpreadsheetDocument document, Cell cell)
        {
            string StrErrorMsg = string.Empty;
            string CellValue = string.Empty;
            try
            {
                if (cell != null)
                {
                    SharedStringTablePart StringTablePart = document.WorkbookPart.SharedStringTablePart;
                    if (cell.CellValue != null)
                    {
                        CellValue = cell.CellValue.InnerXml;
                        if (cell.DataType != null && cell.DataType.Value == CellValues.SharedString)
                        {
                            if (StringTablePart.SharedStringTable.ChildElements[Int32.Parse(CellValue)] != null)
                            {
                                return StringTablePart.SharedStringTable.ChildElements[Int32.Parse(CellValue)].InnerText;
                            }
                        }
                        else
                        {
                            return CellValue;
                        }
                    }
                }

                return string.Empty;
            }
            catch (Exception e)
            {
                StrErrorMsg = e.Message;
                ErrorWriteToLog.WriteToLogFile(e);
                throw;
            }
        }

        /***********************Uploading Data and Files in Specific List Library*******************/
        public static void UploadFilesAndData(ClientContext clientContext, System.Data.DataTable dataTable)
        {
            Application App1 = new Application();
            //if (System.IO.File.Exists(@"D:\FileUploadData.xlsx"))
            //{
            //    Console.WriteLine("Exists file");
            //}

            Workbook WorkBook1 = (Microsoft.Office.Interop.Excel.Workbook)(App1.Workbooks._Open(ApplicationConfiguration.LocalFilePath+DiskFilePath, System.Reflection.Missing.Value,
            System.Reflection.Missing.Value, System.Reflection.Missing.Value, System.Reflection.Missing.Value,
            System.Reflection.Missing.Value, System.Reflection.Missing.Value, System.Reflection.Missing.Value,
            System.Reflection.Missing.Value, System.Reflection.Missing.Value, System.Reflection.Missing.Value,
            System.Reflection.Missing.Value, System.Reflection.Missing.Value));

            //int NumberOfWorkbooks = App1.Workbooks.Count;
            Microsoft.Office.Interop.Excel.Worksheet Sheet1 = (Microsoft.Office.Interop.Excel.Worksheet)WorkBook1.Worksheets[1];
            //int NumberOfSheets = WorkBook1.Worksheets.Count;

            try
            {
                if (dataTable.Rows.Count > 0)
                {
                    Console.WriteLine("-------------------Uploading file--------------------");

                    List ExcelFile = clientContext.Web.Lists.GetByTitle("FileUpload");
                    clientContext.Load(ExcelFile);
                    clientContext.ExecuteQuery();

                    Console.WriteLine("List name " + ExcelFile.Title + " desc :" + ExcelFile.Description);

                    /**************Get All Users of Site**************/
                    UserCollection SiteUsers = clientContext.Web.SiteUsers;
                    clientContext.Load(SiteUsers);
                    clientContext.ExecuteQuery();

                    for (int Count = 0; Count < dataTable.Rows.Count; Count++)
                    {
                        try
                        {
                            if (Count > -1)
                            {
                                string LocalFilePath = dataTable.Rows[Count]["FilePath"].ToString();
                                string StatusList = dataTable.Rows[Count]["Status"].ToString();
                                string CreatedByEmail = dataTable.Rows[Count]["Created By"].ToString();
                                string Department = dataTable.Rows[Count]["Dept"].ToString();
                                string UploadStatus = dataTable.Rows[Count]["Upload Status"].ToString();
                                string Reason = dataTable.Rows[Count]["Reason"].ToString();
                                long SizeOfFile = new System.IO.FileInfo(LocalFilePath.Replace(@"\\", @"\")).Length;

                                /****************Get All Users for that site*************/
                                User CreatedUserObj = SiteUsers.GetByEmail(CreatedByEmail);
                                clientContext.Load(CreatedUserObj);
                                clientContext.ExecuteQuery();

                                /****************Get All Departments from Department List and filter the data*************/
                                List DepartmentList = clientContext.Web.Lists.GetByTitle("Department");
                                clientContext.Load(DepartmentList);
                                clientContext.ExecuteQuery();

                                CamlQuery DepartmentQuery = new CamlQuery();
                                DepartmentQuery.ViewXml = "<View><Query><Where><Eq><FieldRef Name='Department'/><Value Type = 'Text'>" + Department.Trim() + "</Value></Eq></Where></Query><RowLimit></RowLimit></View> ";

                                //var fs = new FileStream(filepath, FileMode.Open);
                                try
                                {
                                    ListItemCollection GetDepartmentByName = DepartmentList.GetItems(DepartmentQuery);
                                    clientContext.Load(GetDepartmentByName);
                                    clientContext.ExecuteQuery();
                                    if (GetDepartmentByName != null && GetDepartmentByName.Count > 0)
                                    {
                                        if (SizeOfFile > 100 && SizeOfFile < 2097150)
                                        {
                                            FileCreationInformation FileToUpload = new FileCreationInformation();
                                            FileToUpload.Content = System.IO.File.ReadAllBytes(LocalFilePath.Replace(@"\\", @"\"));
                                            FileToUpload.Overwrite = true;
                                            FileToUpload.Url = Path.Combine("FileUpload/", Path.GetFileName(LocalFilePath.Replace(@"\\", @"\")));
                                            Microsoft.SharePoint.Client.File UploadFile = ExcelFile.RootFolder.Files.Add(FileToUpload);

                                            clientContext.Load(UploadFile);
                                            clientContext.ExecuteQuery();

                                            ListItem UploadItem = UploadFile.ListItemAllFields;

                                            Field Choice = ExcelFile.Fields.GetByInternalNameOrTitle("Status");
                                            clientContext.Load(Choice);
                                            clientContext.ExecuteQuery();
                                            FieldChoice StatusFieldChoice = clientContext.CastTo<FieldChoice>(Choice);
                                            clientContext.Load(StatusFieldChoice);
                                            clientContext.ExecuteQuery();
                                            string[] StatusArray = StatusList.Split(',');
                                            string StatusPutSelectedValue = string.Empty;
                                            for (int statusCount = 0; statusCount < StatusArray.Length; statusCount++)
                                            {
                                                if (StatusFieldChoice.Choices.Contains(StatusArray[statusCount]))
                                                {
                                                    if (statusCount == StatusArray.Length - 1)
                                                    {
                                                        StatusPutSelectedValue += StatusArray[statusCount];
                                                    }
                                                    else
                                                    {
                                                        StatusPutSelectedValue += StatusArray[statusCount] + ";";
                                                    }
                                                }
                                            }

                                            UploadItem["CreatedBy"] = CreatedUserObj.Title;
                                            UploadItem["SizeOfFile"] = SizeOfFile;
                                            UploadItem["FileType"] = Path.GetExtension(LocalFilePath.Replace(@"\\", @"\"));
                                            UploadItem["Status"] = StatusPutSelectedValue;
                                            UploadItem["Dept"] = GetDepartmentByName[0].Id.ToString(); ;

                                            UploadItem.Update();
                                            clientContext.ExecuteQuery();
                                            Sheet1.Cells[Count + 2, 5] = "Success";
                                            Sheet1.Cells[Count + 2, 6] = "N/A";
                                        }
                                        else
                                        {
                                            Console.WriteLine("File : " + Path.GetFileName(LocalFilePath.Replace(@"\\", @"\")) + " could not be uploaded since file size is not in range");
                                            Sheet1.Cells[Count + 2, 5] = "Error";
                                            Sheet1.Cells[Count + 2, 6] = "File Size Exceeds Specified Range";
                                            /*************Create Custom Exception Object**********/
                                            ApplicationCustomException FileSizeException = new ApplicationCustomException("File Size Exceeds Specified Range");
                                            ErrorWriteToLog.WriteToLogFile(FileSizeException);
                                        }
                                    }
                                    else
                                    {
                                        Console.WriteLine("Department Not Found in Department List");
                                        Sheet1.Cells[Count + 2, 5] = "Error";
                                        Sheet1.Cells[Count + 2, 6] = "Department Not Found in Department List";
                                        /*************Create Custom Exception Object**********/
                                        ApplicationCustomException DepartmentNotFoundException = new ApplicationCustomException("File Size Exceeds Specified Range");
                                        ErrorWriteToLog.WriteToLogFile(DepartmentNotFoundException);
                                    }
                                }
                                catch (Exception exe)
                                {
                                    Console.WriteLine("Exception : " + exe.Message);
                                    ErrorWriteToLog.WriteToLogFile(exe);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("Exception : " + ex);
                            Sheet1.Cells[Count + 2, 5] = "Error";
                            Sheet1.Cells[Count + 2, 6] = ex.Message;
                            ErrorWriteToLog.WriteToLogFile(ex);
                        }
                    }
                }
            }
            catch (Exception ee)
            {
                Console.WriteLine("Exception :" + ee.Message);
                ErrorWriteToLog.WriteToLogFile(ee);
            }

            WorkBook1.Save();

            WorkBook1.Close(true, ApplicationConfiguration.LocalFilePath+DiskFilePath, System.Reflection.Missing.Value);
            App1.Quit();
            System.Runtime.InteropServices.Marshal.ReleaseComObject(App1);

            Marshal.ReleaseComObject(Sheet1);
            Marshal.ReleaseComObject(WorkBook1);
            Marshal.ReleaseComObject(App1);
        }

        /******************Upload Status File******************/
        public static void UploadFileToSP(ClientContext clientContext)
        {
            try
            {
                Console.WriteLine("---------------Uploading file to Share Point--------------");
                var NewLocalUpdatedFile = ApplicationConfiguration.LocalFilePath+ DiskFilePath;

                FileCreationInformation NewLocalUpdatedFileInfo = new FileCreationInformation();
                NewLocalUpdatedFileInfo.Content = System.IO.File.ReadAllBytes(NewLocalUpdatedFile);
                NewLocalUpdatedFileInfo.Overwrite = true;
                NewLocalUpdatedFileInfo.Url = Path.Combine("UserDocuments/", Path.GetFileName(NewLocalUpdatedFile));

                List ExcelFileLibrary = clientContext.Web.Lists.GetByTitle("UserDocuments");
                ExcelFileLibrary.RootFolder.Files.Add(NewLocalUpdatedFileInfo);

                clientContext.ExecuteQuery();
            }
            catch (Exception e)
            {
                ErrorWriteToLog.WriteToLogFile(e);
                throw;
            }
        }

        //Password Secure String
        private static SecureString GetPassword()
        {
            ConsoleKeyInfo Info;
            //Get the user's password as a SecureString  
            SecureString SecurePassword = new SecureString();
            do
            {
                Info = Console.ReadKey(true);
                if (Info.Key != ConsoleKey.Enter)
                {
                    SecurePassword.AppendChar(Info.KeyChar);
                }
            }
            while (Info.Key != ConsoleKey.Enter);
            return SecurePassword;
        }
    }
}
