using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DBBackupLib
{
    public class Logger
    {
        public static string ExceptionLogFilename { get; private set; } = "ExceptionLog.txt";

        public static string CurrentDateTimeString { get { return DateTime.Now.ToString("dd-MM-yyyy HH:mm:ss.ffff"); } }

        public static readonly string LogTextTemplate = "";

        public static readonly int NoOfBytesToReadForLastLog = 512;

        public static Task LastLogNumberTask{ get; private set;}

        public static int CurrentLogNumber { get; private set; }

        public static Exception LoggerException { get; private set; }

        public static List<Task> WriteLogTasks { get; private set; }

        static Logger() {
            LogTextTemplate = "************** Exception Log {0} - {1} ***********************\n{2}\n********************* End of Log {0} ********************\n";
            WriteLogTasks = new List<Task>();
            if (!File.Exists(ExceptionLogFilename))
                File.Create(ExceptionLogFilename);
            if (IsInited)
                return;
            LastLogNumberTask = Init();            
        }

        public static bool IsInited { get; private set; } = false;

        public async static Task Init() {
            if (IsInited)
                return;
            IsInited = true;
            await GetLastLogNumber();

            foreach (Task t in WriteLogTasks)
                if (t.Status == TaskStatus.Created)
                    t.Start();
                else if (t.Status == TaskStatus.Faulted || t.Status == TaskStatus.Canceled)
                    LoggerException = t.Exception;
        }

        public static async Task LogException(Exception exception) {
            if (!LastLogNumberTask.Wait(100))
                WriteLogTasks.Add(new Task(async () => await LogExceptionAsync(exception)));
            else
                await LogExceptionAsync(exception);
        }

        static async Task LogExceptionAsync(Exception exception) {

            string exceptionLog = exception.Message;
            exceptionLog += "\n\t\t%%%%%%% Stack Trace %%%%%\n" + exception.StackTrace + "\n\t\t%%%%%% End of Stack Trace %%%%%%";
            while (exception.InnerException != null)
            {
                exception = exception.InnerException;
                exceptionLog += string.Format("\n\n\t\t*******Inner Exception********{0}\n\n\t\t*********End of Inner Exception********", exception);
                exceptionLog += "\n\t\t%%%%%%% Stack Trace %%%%%\n" + exception.StackTrace + "\n\t\t%%%%%% End of Stack Trace %%%%%%\n";
            }            
            try
            {
                string log = string.Format(LogTextTemplate, ++CurrentLogNumber, CurrentDateTimeString, exceptionLog);
                await File.AppendAllTextAsync(ExceptionLogFilename, log, Encoding.Default);
            }
            catch (Exception ex) {
                LoggerException = ex;
            }
        }

        static async Task<int> GetLastLogNumber()
        {
            FileStream fs = File.OpenRead(ExceptionLogFilename);
            CurrentLogNumber = 0;
            if (fs.Length > "Exception Log".Length)
            {
                long noOfbytesSeeked = fs.Seek(0, SeekOrigin.End);
                CurrentLogNumber = await GetLastLogNumberFromLogFile(fs);
            }
            fs.Dispose();
            return CurrentLogNumber;
        }

        static async Task<int>/*int*/ GetLastLogNumberFromLogFile(FileStream fs) {
            if (fs == null)
                return -1;

            byte[] readBuffer = new byte[NoOfBytesToReadForLastLog];
            long fsLastPosition = fs.Position;
            fs.Seek(-(fs.Position > NoOfBytesToReadForLastLog ? NoOfBytesToReadForLastLog : fs.Position), SeekOrigin.Current);
            int noOfBytesRead = await fs.ReadAsync(readBuffer, 0, NoOfBytesToReadForLastLog);
                 //fs.Read(readBuffer, 0, NoOfBytesToReadForLastLog);
            if (noOfBytesRead < NoOfBytesToReadForLastLog && fs.Length > NoOfBytesToReadForLastLog && fsLastPosition > NoOfBytesToReadForLastLog)
                throw new Exception("Error reading log file - " + fs.Name);

            string logString = Encoding.Default.GetString(readBuffer);
            int indexOfLogNo = logString.LastIndexOf("Exception Log ");
            if (indexOfLogNo == -1)
            {
                fs.Seek(-noOfBytesRead, SeekOrigin.Current);
                fs.ReadTimeout = 1000 * 60;
                return GetLastLogNumberFromLogFile(fs).Result;
            }
            char cNo;
            string logNoString = "";indexOfLogNo += "Exception Log ".Length;
            while ((!char.IsWhiteSpace(cNo = logString[indexOfLogNo++])))
                logNoString += cNo;
            int logNo = int.Parse(logNoString);
            return logNo;
        }

    }
}
