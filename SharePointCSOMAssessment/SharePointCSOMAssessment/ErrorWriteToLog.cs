using System;
using System.IO;

namespace SharePointCSOMAssessment
{
    class ErrorWriteToLog
    {
        static public void WriteToLogFile(Exception e)
        {
            string ErrorString = "-- " + DateTime.Now + Environment.NewLine + e.StackTrace + Environment.NewLine + e.Message + Environment.NewLine + Environment.NewLine + Environment.NewLine;
            string FilePath = @"D:\ErrorLogFile.txt";
            
           // Console.WriteLine("Exists :" + File.Exists(FilePath));
            File.AppendAllText(FilePath, ErrorString);
        }
    }
}
