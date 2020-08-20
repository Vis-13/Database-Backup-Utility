using Google;
using Google.Apis.Upload;
using Google.Apis.Util;
using Microsoft.VisualBasic.CompilerServices;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Reactive.Bindings;
using Reactive.Bindings.ObjectExtensions;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data.Common;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reactive.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.ComTypes;
using System.Security.RightsManagement;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;

namespace DBBackupLib
{
    public class MainViewModel : NotifyPropertyChanged, IMainViewModel, IDisposable
    {
        public string BackupLogFile { get; private set; } = "BackupLog.txt";

        public string DeleteLocalAndGDriveBackupFilesLogFile { get; private set; } = "DeleteBackupFilesErroLog.txt";

        public const int DeleteLocalAndGDriveFilesIntervalInMins = 5;
        string _cstringDBToBackup = "Data Source=.;Initial Catalog=gfunjoker;Integrated Security=True";
        public string CStringDBToBackup { get { return _cstringDBToBackup; } set { Set(ref _cstringDBToBackup, value); } }

        string _databaseNameToBackup = "gfunjoker";
        public string DatabaseNameToBackup { get { return _databaseNameToBackup; } set { Set(ref _databaseNameToBackup, value); } }

        string _backupFolderPath;
        public string BackupFolderPath { get { return _backupFolderPath; } set { Set(ref _backupFolderPath, value); } }

        string _backupQuery = "BACKUP DATABASE [{0}] TO DISK = '{1}'";

        int _backupIntervalInMins = 10;
        public int BackupIntervalInMins { get { return _backupIntervalInMins; } set { if (Set(ref _backupIntervalInMins, value)) BackUpDatabaseInIntervals(); } }

        public int BackupIntervalInMilliSeconds { get { return (int)TimeSpan.FromMinutes(_backupIntervalInMins).TotalMilliseconds; } }

        public ObservableCollection<CString> CStringsDBToBackup { get; set; } = new ObservableCollection<CString>();

        CString _selectedCStringDBToBackup = null;
        public CString SelectedCStringDBToBackup { get { return _selectedCStringDBToBackup; } set { Set(ref _selectedCStringDBToBackup, value); } }

        public ReactiveProperty<bool> IsProcessing { get; private set; } = new ReactiveProperty<bool>(false);

        public ICommand BackupCommand { get; }

        public ICommand SetBackupIntervalCommand { get; }

        public ICommand RemoveConnectionStringCommand { get; }

        public ICommand AddConnectionStringCommand { get; }

        public ICommand UpdateConnectionStringCommand { get; }

        public ICommand CleanUpLocalAndGDriveCommand { get; }

        public string CurrentDateTimeString { get { return DateTime.Now.ToString("dd-MM-yyyy_HH-mm-ss"); } }

        public string CurrentDTFileAppend { get { return DateTime.Now.ToString("dd-HH-mm-ss"); } }// day, mins, secs

        public ReactiveProperty<bool> HasExceptionOccuredWhileBackingup { get; private set; } = new ReactiveProperty<bool>(false);

        public ReactiveProperty<bool> BackupUploadedSuccessfully { get; private set; } = new ReactiveProperty<bool>(false);

        public ReactiveProperty<string> LastBackupExceptionMsg { get; private set; } = new ReactiveProperty<string>();

        public string LastGDriveUploadId = "";
        public string CurrentGDriveUploadId = "";
        Dictionary<string, string> _fileAppAttributes = new Dictionary<string, string>();

        List<FileToUpload> LastUploadedFiles = new List<FileToUpload>();

        ObservableCollection<Task<FileToUpload>> LatestUploadedFiles = new ObservableCollection<Task<FileToUpload>>();

        ObservableCollection<FileToUpload> RecentFilesToUpload = new ObservableCollection<FileToUpload>();


        bool _isDeletingPreviousHoursBackupFiles = false;
        public bool IsDeletingPreviousHoursBackupFiles { get { return _isDeletingPreviousHoursBackupFiles; } set { Set(ref _isDeletingPreviousHoursBackupFiles, value); } }

        public MainViewModel() {
            string exFilename = Logger.ExceptionLogFilename;            
            _backupFolderPath = ServiceProvider.TempBackupDir;
            Task.Run(() => DeleteYesterdaysFiles(_backupFolderPath, DateTime.Now.AddDays(-1)));
            BackupCommand = new[] { IsProcessing, HasExceptionOccuredWhileBackingup }.CombineLatest(M => !M[0] && !M[1]).ToReactiveCommand().WithSubscribe(StartDatabaseBackup);
            SetBackupIntervalCommand = new[] { IsProcessing }.CombineLatest(M => !M[0]).ToReactiveCommand<string>().WithSubscribe(SetBackupInervals);
            RemoveConnectionStringCommand = new[] { IsProcessing }.CombineLatest(M => !M[0]).ToReactiveCommand<CString>().WithSubscribe(RemoveConnectionString);
            AddConnectionStringCommand = new[] { IsProcessing }.CombineLatest(M => !M[0]).ToReactiveCommand<string>().WithSubscribe(AddConnectionString);
            CleanUpLocalAndGDriveCommand = new[] { IsProcessing }.CombineLatest(M => !M[0]).ToReactiveCommand().WithSubscribe(() => Last5DeleteFilesTimer(false));
            CStringsDBToBackup = CString.AddInit(true);
            _fileAppAttributes.Add("UpId", "");
            Task.Run(() => LastUploadedFiles = FileToUpload.GetFileToUploadListFromSettings());            
            Task.Run(() => Last5DeleteFilesTimer(null));
        }

        //public void UploadFile(string tempFilePath, string currentTimeString, string databaseName) {
        //    string uploadFilePath = //Directory.GetFiles(_backupFolderPath).FirstOrDefault(); 
        //                            Path.Combine(_backupFolderPath, databaseName + "_backup_" + currentTimeString + ".zip");

        //    //try
        //    //{
        //        ZipFile.CreateFromDirectory(tempFilePath, uploadFilePath);
        //    //}
        //    //catch (Exception ex) { 

        //    //}
        //    //IUploadProgress uploadProgress = GoogleApis.UploadFile(uploadFilePath, _fileAppAttributes).Result;
        //    Google.Apis.Drive.v3.Data.File responseFile = null;
        //    IUploadProgress uploadProgress = GoogleApis.UploadFile(uploadFilePath, ref responseFile,  _fileAppAttributes);
        //    if (uploadProgress.Status == UploadStatus.Failed)
        //        throw uploadProgress.Exception;
        //    Directory.Delete(tempFilePath, true);
        //    //LastUploadedFiles.Add(responseFile.Id);
        //}

        public void StartDatabaseBackup() {
            Task t = Task.Run(() => {
                BackUpDatabase();
                BackUpDatabaseInIntervals();
            });
            while (!t.Wait(10))
                System.Windows.Forms.Application.DoEvents();
        }

        public void SetBackupInervals(string txtInterval)
        {
            BackupIntervalInMins = int.Parse(txtInterval);
        }

        private void RemoveConnectionString(CString index)
        {
            CStringsDBToBackup.Remove((CString)index);
        }

        private void AddConnectionString(string connectionString)
        {
            CString.Add(connectionString);
        }

        public void BackUpDatabase() {

            //while (IsDeletingLastHoursBackupFiles) { 
            //    Thread.Sleep(1000);
            //    System.Windows.Forms.Application.DoEvents();
            //}
            HasExceptionOccuredWhileBackingup.Value = false;
            BackupUploadedSuccessfully.Value = false;
            IsProcessing.Value = true;
            //LastBackupExceptionMsg.Value = "";
            CurrentGDriveUploadId = DateTime.Now.ToString("dd-HH:mm:ss.fffff");
            _fileAppAttributes["UpId"] = CurrentGDriveUploadId;
            //LastUploadedFiles.Clear();
            string currentTimeString = CurrentDTFileAppend;
            string backupTaskLog1 = "Backing Up Databases Begins " + CurrentDateTimeString + "\n{0}\nBacking Up Databases Ends\n";
            string backupTaskLog = "Started\n{0}";
            FileToUpload ftu = null;
            foreach (CString cString in CStringsDBToBackup)
                try
                {
                    SelectedCStringDBToBackup = cString;
                    backupTaskLog = $"******** Log for {SelectedCStringDBToBackup.Database} ************\nBacking up using SqlClient....\n";
                    cString.IsProcessing.Value = true;
                    CStringDBToBackup = cString.SqlConnectionString;
                    DatabaseNameToBackup = cString.Database;
                    SqlConnection sqlConnection = new SqlConnection(_cstringDBToBackup);
                    sqlConnection.Open();
                    if (!Directory.Exists(_backupFolderPath))
                        Directory.CreateDirectory(_backupFolderPath);
                    DirectoryInfo di = Directory.CreateDirectory(_backupFolderPath + "\\" + _databaseNameToBackup + currentTimeString);
                    string tempBackupFilePath = Path.Combine(di.FullName, _databaseNameToBackup + ".bak");
                    string backupQuery = string.Format(_backupQuery, DatabaseNameToBackup, tempBackupFilePath);
                    using (SqlCommand cmd = new SqlCommand(backupQuery, sqlConnection))
                        cmd.ExecuteNonQuery();
                    sqlConnection.Dispose();
                    //UploadFile(di.FullName, currentTimeString, cString.Database);
                    backupTaskLog += $"Backed up database to {tempBackupFilePath}.....\nUploading {di.FullName} Backup File....\n";
                    Task<FileToUpload> fileToUploadTask = FileToUpload.UploadFile(di.FullName, cString, cString.Database + "_backup_" + currentTimeString + ".zip");
                    LatestUploadedFiles.Add(fileToUploadTask);
                    while (!fileToUploadTask.Wait(100))
                        Thread.Sleep(1000);
                    if (fileToUploadTask.Status == TaskStatus.Faulted)
                        throw fileToUploadTask.Exception;
                    else
                    {
                        ftu = fileToUploadTask.Result;
                        if (ftu.IsFaulted && !ftu.IsZipUploaded)
                        {
                            backupTaskLog += "FileToUpload Object:\n" + JsonConvert.SerializeObject(ftu) + "\n";
                            throw ftu.Error;
                        }
                        else if (ftu.IsZipUploaded)
                        {
                            backupTaskLog += $"Uploaded file {ftu.LocalFilePath} to gdrive with Id: {ftu.UploadedFileId}!\n";
                        }
                        lock (RecentFilesToUpload)
                            RecentFilesToUpload.Add(ftu);
                    }
                    cString.IsProcessing.Value = false;
                }
                catch (Exception ex)
                {
                    backupTaskLog += $"Error: \n{ex.ToString()}\n\n";
                    cString.IsProcessing.Value = false;
                    cString.HasErrors = true;
                    SetException(ex);
                }
                finally {
                    backupTaskLog1 = string.Format(backupTaskLog1, backupTaskLog + $"\n******** Log for { SelectedCStringDBToBackup.Database }" + " Ends *************\n\n{0}");
                }
            IsProcessing.Value = false;
            if (!HasExceptionOccuredWhileBackingup.Value)
            {
                BackupUploadedSuccessfully.Value = true;
                LastUploadedFiles = LatestUploadedFiles.Select(lt => lt.Result).ToList();
                LatestUploadedFiles = new ObservableCollection<Task<FileToUpload>>();
                string backupTaskLogToWrite = backupTaskLog1;
                //t.ContinueWith(t => { if (t.IsFaulted) backupTaskLogToWrite = string.Format(backupTaskLogToWrite, t.Exception); 
                //    File.AppendAllText(BackupLogFile, backupTaskLogToWrite); });
                lock (RecentFilesToUpload)
                    File.AppendAllText(BackupLogFile, backupTaskLogToWrite);
            }
            else
            {
                SaveFaultedUploadsList();
            }
            IsDeletingPreviousHoursBackupFiles = true;
            Task.Run(DeleteLastBackedUpFilesOnLocalAndGDrive).Wait(10);
        }

        void SetException(Exception ex) {
            IsProcessing.Value = false;
            HasExceptionOccuredWhileBackingup.Value = true;
            BackupUploadedSuccessfully.Value = false;
            File.WriteAllText("LastException.txt", ex.ToString());
            Logger.LogException(ex);
            LastBackupExceptionMsg.Value = "There was an error Backing Up the Database to GDrive. Please check the logs!";
        }

        void SaveFaultedUploadsList() {
            string jsonFaultedUploads = JsonConvert.SerializeObject(LatestUploadedFiles.Where(l => l.Result.IsFaulted).ToArray());
            File.WriteAllText("Faulted Uploads - " + CurrentDateTimeString + ".json", jsonFaultedUploads.Length <= "[]".Length ? "No faulted Uploads. Please check other logs!" : jsonFaultedUploads);
        }

        Timer _timer = null;
        public void BackUpDatabaseInIntervals()
        {
            bool isTimmerChanged = false;
            if (_timer == null)
            {
                _timer = new Timer(_timer_DurationElapsed, this, TimeSpan.FromMilliseconds(BackupIntervalInMilliSeconds), Timeout.InfiniteTimeSpan);
            }
            else if (!IsProcessing.Value)
            {
                isTimmerChanged = _timer.Change(TimeSpan.FromMilliseconds(BackupIntervalInMilliSeconds), Timeout.InfiniteTimeSpan);
            }
        }

        void _timer_DurationElapsed(object state) {
            if (!IsInScheduledHours())
                return;
            IsProcessing.Value = true;
            BackUpDatabase();
            IsProcessing.Value = false;
            _timer.Change(TimeSpan.FromMilliseconds(BackupIntervalInMilliSeconds), Timeout.InfiniteTimeSpan);
        }

        public bool IsInScheduledHours() {
            return (DateTime.Now.Hour >= 0 && DateTime.Now.AddSeconds(-2).Hour < 24);
        }

        public void DeleteLastBackedUpFilesOnLocalAndGDrive() {
            try
            {
                IsDeletingPreviousHoursBackupFiles = true;
                lock (RecentFilesToUpload)
                {
                    List<FileToUpload> filesToUpload = RecentFilesToUpload.Where(rft => rft.IsZipUploaded && rft.CreatedOn <= DateTime.Now.Subtract(TimeSpan.FromMinutes(DeleteLocalAndGDriveFilesIntervalInMins))).ToList();//.OrderBy(o => o.CreatedOn).Take(5).ToList();
                    filesToUpload.ForEach(f => { DeleteLocalAndGDriveFile(f); RecentFilesToUpload.Remove(f); });
                }
                Task<string> t = null;
                (t = Task.Run(() => GoogleApis.EmptyTrashFolder()).ContinueWith(t => { IsDeletingPreviousHoursBackupFiles = false; return t.Result; })).Wait(1);
            }
            catch (Exception ex)
            {
                IsDeletingPreviousHoursBackupFiles = false;
                throw;
            }            
        }

        public void DeleteLocalAndGDriveFile(FileToUpload fileToUpload) {
            string logChars = $"!@~{DateTime.Now} : {{0}}#$%\n";
            string deleteLocalAndGDriveBackupFilesLog = "";
            try
            {
                if (string.IsNullOrWhiteSpace(fileToUpload.LocalFilePath) || string.IsNullOrWhiteSpace(fileToUpload.UploadedFileId))
                    deleteLocalAndGDriveBackupFilesLog += string.Format(logChars, $"Local or GDrive file path not found -> Local: {fileToUpload.LocalFilePath}\t GDrive: {fileToUpload.UploadedFileId}\n{{0}}");

                if (!File.Exists(fileToUpload.LocalFilePath))
                    deleteLocalAndGDriveBackupFilesLog += string.Format(logChars, $"File not found: {fileToUpload.LocalFilePath}\n{{0}}");
                else
                    Task.Run(() => { try { fileToUpload.DeleteFromLocal(); } catch (Exception ex) { deleteLocalAndGDriveBackupFilesLog += string.Format(logChars, "\n" + ex + "\n\n{{0}}"); } })
                        .Wait(10);
                //string deleteFromGDriveResult = "";
                if (!fileToUpload.IsGDriveZipDeleted)
                {
                    fileToUpload.DeleteFromGDrive(null);
                    fileToUpload.DeleteFromGDriveTask.ContinueWith(t =>
                    {
                        if (t.IsFaulted)
                            deleteLocalAndGDriveBackupFilesLog += string.Format(logChars, $"Error: {fileToUpload.Error}\n\n{{0}}");
                        if (!string.IsNullOrWhiteSpace(t.Result))
                            deleteLocalAndGDriveBackupFilesLog += string.Format(logChars, $"Error: {t.Result}\n\n{{0}}");
                        else
                            lock (RecentFilesToUpload)
                                File.AppendAllText(BackupLogFile, string.Format(logChars, $"GDrive file: {Path.GetFileName(fileToUpload.LocalFilePath)}, Id: {fileToUpload.UploadedFileId} deleted successfully!\n{{0}}"));
                        if (!string.IsNullOrWhiteSpace(deleteLocalAndGDriveBackupFilesLog))
                            File.AppendAllText(DeleteLocalAndGDriveBackupFilesLogFile, deleteLocalAndGDriveBackupFilesLog);

                    }).Wait(100);
                }
                else
                {
                    deleteLocalAndGDriveBackupFilesLog += string.Format(logChars, $"GDrive file: {fileToUpload.UploadedFileId} already deleted!\n{{0}}");
                }
            }
            catch (Exception ex) {
                File.AppendAllText(DeleteLocalAndGDriveBackupFilesLogFile, string.Format(logChars, "\n" + ex + "\n\n"));
            }
            if (!string.IsNullOrWhiteSpace(deleteLocalAndGDriveBackupFilesLog))
                File.AppendAllText(DeleteLocalAndGDriveBackupFilesLogFile, deleteLocalAndGDriveBackupFilesLog);
            //lock (RecentFilesToUpload)            
            //    File.AppendAllText(BackupLogFile, );
        }

        public static void DeleteYesterdaysFiles(string pathToDeleteContaintsOfYesterday, DateTime? timeBeforeWhichToDelete = null)
        {
            timeBeforeWhichToDelete = timeBeforeWhichToDelete.HasValue ? timeBeforeWhichToDelete : DateTime.Now.Date;
            IEnumerable<string> directoriesDeleted = Directory.GetDirectories(pathToDeleteContaintsOfYesterday).Where(d => Directory.GetCreationTime(d) < timeBeforeWhichToDelete)
                .Select(dirname => { Directory.Delete(dirname, true); return dirname; }).ToList();
            IEnumerable<string> files = Directory.GetFiles(pathToDeleteContaintsOfYesterday, "*_backup_*.zip").Where(f => File.GetCreationTime(f) < DateTime.Now.Date)
                .Select(filename => { File.Delete(filename); return filename; }).ToList();
            //File.WriteAllText()
        }

        public void DeleteYesterdaysLocalAndGDriveFilesIfNotNeeded(DateTime? logStartTime =null) {
            //($"createdTime < '{DateTime.Now.AddDays(-1).Date.ToString("yyyy-MM-dd")}' and {GFolderId} in parents");
            IsDeletingPreviousHoursBackupFiles = true;
            try
            {
                logStartTime = logStartTime.HasValue ? logStartTime : DateTime.Now;
                string logChars = $"!@~{logStartTime} : {{0}}#$%\n";
                string errorLog = "";
                string backupLog = "";
                Task.Run(() =>
                {
                    try { DeleteYesterdaysFiles(BackupFolderPath, DateTime.Now.AddHours(-12)); backupLog += string.Format(logChars, $"'DeleteYesterdaysFiles(BackupFolderPath....)' called to delete older files in {BackupFolderPath}"); }
                    catch (Exception ex) { errorLog += string.Format(logChars, $"Error deleting in 'DeleteYesterdaysFiles(BackupFolderPath....)'\n{ex}\n\n{{0}}"); }
                }).Wait(10);
                List<Task<string>> deleteTasks = new List<Task<string>>();
                Google.Apis.Drive.v3.Data.FileList fileList = GoogleApis.GetYesterdaysFiles($"'{GoogleApis.GFolderId}' in parents");
                foreach (CString cString in CString.CSStrings)
                {
                    List<Google.Apis.Drive.v3.Data.File> filesOfYesterday = fileList.Files.Where(f => f.Name.Contains(cString.Database + "_backup_") && f.CreatedTime < DateTime.Now.AddHours(-1)).ToList();
                    int todaysFileCount = fileList.Files.Where(f => f.Name.Contains(cString.Database + "_backup_") && f.CreatedTime >= DateTime.Now.AddHours(-1)).Count();
                    if (todaysFileCount > 5)
                        foreach (Google.Apis.Drive.v3.Data.File fileOfYesterday in filesOfYesterday)
                        {
                            Task<string> t = Task.Run(() => GoogleApis.DeleteFile(cString.Database, fileOfYesterday.Id)).ContinueWith(t =>
                            {
                                string filenameIdString = $" : yesterdays file({fileOfYesterday.Name}, Id: {fileOfYesterday.Id})";
                                string result = "Error";
                                if (t.IsFaulted)
                                    errorLog += string.Format(logChars, cString.Database + $"{filenameIdString} could not be delete. Please check on Gdrive.\n{{0}}");
                                else if (t.IsCanceled)
                                    errorLog += string.Format(logChars, cString.Database + $"{filenameIdString} could not be delete. The task was canceled. Please check on Gdrive.\n{{0}}");
                                else
                                {
                                    backupLog += string.Format(logChars, cString.Database + $"{filenameIdString} created on {fileOfYesterday.CreatedTime} deleted Successfully.\n{{0}}");
                                    result = t.Result;
                                }
                                return result;
                            });
                            t.Wait(1);
                            deleteTasks.Add(t);
                        }
                }
                deleteTasks.ForEach(t => { if (!t.IsCompleted) t.Wait(); });
                if (!string.IsNullOrWhiteSpace(errorLog))
                    File.AppendAllText("DeleteYesterdaysLocalAndGDriveFilesIfNotNeededErrorLogs.txt", errorLog);
                if (!string.IsNullOrWhiteSpace(backupLog))
                    lock (RecentFilesToUpload)
                        File.AppendAllText(BackupLogFile, backupLog);
            }
            catch (Exception ex)
            {
                throw;
            }
            finally {
                int noOfSecsToCloseCleanUpDialog = 12*1000;
                Thread.Sleep((noOfSecsToCloseCleanUpDialog - cleanUpStopWatch.ElapsedMilliseconds > 1000) ? noOfSecsToCloseCleanUpDialog - (int)cleanUpStopWatch.ElapsedMilliseconds : 0);
                cleanUpStopWatch.Stop();
                IsDeletingPreviousHoursBackupFiles = false;                
            }
        }

        Timer _last5DeleteFilesTimer = null;
        public DateTime Last5CleanUpStartTime { get; private set; } = DateTime.MinValue;
        public DateTime Last5CleanUpEndTime { get; private set; } = DateTime.MinValue;
        public const string Last5CleanUpLogFile = "Last5CleanUp.txt";
        Stopwatch cleanUpStopWatch = new Stopwatch();
        private void Last5DeleteFilesTimer(object restartTimmer)
        {
            if (_last5DeleteFilesTimer == null)
                _last5DeleteFilesTimer = new Timer(Last5DeleteFilesTimer, true, (int)TimeSpan.FromHours(1).TotalMilliseconds, Timeout.Infinite);
            else if(restartTimmer is bool && (bool)restartTimmer)
            {
                bool hasTimerDurationChanged = _last5DeleteFilesTimer.Change((int)TimeSpan.FromHours(1).TotalMilliseconds, Timeout.Infinite);
            }
            Last5CleanUpStartTime = DateTime.Now;
            string logChars = $"!@~{Last5CleanUpStartTime} : {{0}}#$%\n";
            while (IsProcessing.Value)
                Thread.Sleep(1000);
            try
            {
                cleanUpStopWatch.Restart();
                DeleteYesterdaysLocalAndGDriveFilesIfNotNeeded();
                Last5CleanUpEndTime = DateTime.Now;
                File.AppendAllText(Last5CleanUpLogFile, string.Format(logChars, $"\nLast 5 clean up started.... ended at {Last5CleanUpEndTime}\n"));                
                
            } catch (Exception ex) {
                File.AppendAllText(Last5CleanUpLogFile, string.Format(logChars, $"\nLast 5 clean up started.... errored: \n{ex}\n\n"));
                Last5CleanUpEndTime = DateTime.Now;
            }           
        }

        public void Dispose()
        {
            FileToUpload.SaveFileToUploadList(LastUploadedFiles);
        }

    }

    public class NotifyPropertyChanged : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected void RaisePropertyChanged(string PropertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(PropertyName));
        }

        protected void OnPropertyChanged([CallerMemberName] string PropertyName = null)
        {
            RaisePropertyChanged(PropertyName);
        }

        protected void RaiseAllChanged()
        {
            RaisePropertyChanged("");
        }

        protected bool Set<T>(ref T Field, T Value, [CallerMemberName] string PropertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(Field, Value))
                return false;

            Field = Value;

            RaisePropertyChanged(PropertyName);

            return true;
        }
    }

    public interface IMainViewModel {

        string CStringDBToBackup { get; set; }

        string BackupFolderPath { get; set; }

        string DatabaseNameToBackup { get; set; }

        void Dispose();
    }

    public class CString : NotifyPropertyChanged {

        public static int CurrentIndex { get; private set; }

        public const string ICString = "Initial Catalog=";

        int _index;
        public int Index { get { return _index; } private set { Set(ref _index, value); } }

        public ReactiveProperty<bool> IsProcessing { get; set; } = new ReactiveProperty<bool>(false);

        bool _hasErrors = false;
        public bool HasErrors { get { return _hasErrors; } set {
                Set(ref _hasErrors, value); } }

        string _sqlConnectionString = "";
        public string SqlConnectionString { get { return _sqlConnectionString; } set { Set(ref _sqlConnectionString, value); Database = GetDatabaseFromConnectionString(_sqlConnectionString); } }

        string _database = "";
        public string Database { get { return _database; } set { Set(ref _database, value); } }

        public static ObservableCollection<CString> CSStrings { get; }

        static CString() {
            CSStrings = new ObservableCollection<CString>();
        }

        public static void Add(string cs) {
            CString cString = new CString() { Index = CurrentIndex++, SqlConnectionString = cs };
            CSStrings.Add(cString);
        }

        public static void SetDatabaseFromConnectionString(CString cString) {
            cString.Database = GetDatabaseFromConnectionString(cString.Database);
        }

        public static string GetDatabaseFromConnectionString(string cs) {
            int idx = cs.IndexOf(ICString);
            string database = "";
            for (int i = 0; cs[idx + ICString.Length + i] != ';'; i++)
                database += cs[idx + ICString.Length + i];
            return database;
        }

        public static ObservableCollection<CString> AddInit(bool shouldClearFirst = false) {
            if (shouldClearFirst)
                CSStrings.Clear();
            Add("Data Source=.;Initial Catalog=gfunjoker;Integrated Security=True");
            Add("Data Source=.;Initial Catalog=AdventureWorks2012;Integrated Security=True");
            Add("Data Source=.;Initial Catalog=Digiphoto_Test;Integrated Security=True");
            Add("Data Source=.;Initial Catalog=VideoShare-30-07-20;Integrated Security=True");
            Add("Data Source=.;Initial Catalog=ShoppingCart;Integrated Security=True");
            return CSStrings;
        }

        public static void RemoveAll() {
            CSStrings.Clear();
        }

    }

    public class FileToUpload : NotifyPropertyChanged, IDisposable {

        public enum FileStatus { CreatingZip, Uploading, Uploaded, UploadFailed, DeletingTempFolder, DeletingLocalZip, DeletingGDriveZip }

        public bool IsLocalTempFolderDeleted { get; private set; }

        public bool IsLocalZipDeleted { get; private set; }

        public bool IsGDriveZipDeleted { get; private set; }

        public bool IsZipUploaded { get; private set; }

        public string LocalFilePath { get; private set; }

        public string UploadedFileId { get; private set; }

        public FileStatus eFileStatus { get; private set; }

        public DateTime CreatedOn { get; private set; }

        public DateTime ZipedOn { get; private set; }

        public DateTime UploadedOn { get; private set; }

        public DateTime ErroredOn { get; private set; } = DateTime.MinValue;

        public Exception Error { get; private set; }

        public bool IsFaulted { get; private set; }

        public string DatabaseToBackUp { get; private set; }

        string CurrentGDriveUploadId = "";

        CString _cString = null;

        Dictionary<string, string> _fileAppAttributes = new Dictionary<string, string>();

        public Task<string>? DeleteResultString { get; private set; } = null;

        public const string LastestUploadedSettingFile = "LatestUploadedFiles.json";

        public FileToUpload() {
            eFileStatus = FileStatus.CreatingZip;
            CreatedOn = DateTime.Now;
            CurrentGDriveUploadId = CreatedOn.ToString("dd-HH:mm:ss.fffff");
            _fileAppAttributes.Add("UpId", CurrentGDriveUploadId);
        }

        public static async Task<FileToUpload> UploadFile(string tempBackupFolderPath, CString cString, string createZipFilename)
        {
            FileToUpload fileToUpload = new FileToUpload();
            fileToUpload._cString = cString;
            fileToUpload.DatabaseToBackUp = cString.SqlConnectionString;
            int lastIndexOfPassword = -1;
            if ((lastIndexOfPassword = cString.SqlConnectionString.LastIndexOf("Password")) > 10)
                fileToUpload.DatabaseToBackUp = cString.SqlConnectionString.Substring(0, lastIndexOfPassword);
            await Task.Run(() =>
            {
                try
                {
                    string uploadFilePath = Path.Combine(Path.GetDirectoryName(tempBackupFolderPath), createZipFilename);
                    try
                    {
                        ZipFile.CreateFromDirectory(tempBackupFolderPath, uploadFilePath);
                        fileToUpload.ZipedOn = DateTime.Now;
                    }
                    catch (Exception ex) {
                        throw;
                    }
                    fileToUpload.eFileStatus = FileStatus.Uploading;
                    fileToUpload.LocalFilePath = uploadFilePath;
                    //IUploadProgress uploadProgress = GoogleApis.UploadFile(uploadFilePath, _fileAppAttributes).Result;
                    Google.Apis.Drive.v3.Data.File responseFile = null;
                    IUploadProgress uploadProgress = GoogleApis.UploadFile(uploadFilePath, ref responseFile, fileToUpload._fileAppAttributes);
                    if (uploadProgress.Status == UploadStatus.Failed)
                    {
                        fileToUpload.eFileStatus = FileStatus.UploadFailed;
                        throw uploadProgress.Exception;
                    }
                    Directory.Delete(tempBackupFolderPath, true);
                    //File.Delete(uploadFilePath);
                    fileToUpload.UploadedFileId = responseFile.Id;
                    fileToUpload.eFileStatus = FileStatus.Uploaded;
                    fileToUpload.UploadedOn = DateTime.Now;
                    fileToUpload.IsZipUploaded = true;
                }
                catch (Exception ex) {
                    fileToUpload.SetError(ex);
                }
            });
            return fileToUpload;
        }

        public void SetError(Exception ex) {
            Error = ex;
            IsFaulted = true;
            Logger.LogException(ex);
        }

        //public static bool operator ==(FileToUpload f1, FileToUpload f2) {
        //    return (f1._cString.SqlConnectionString == f2._cString.SqlConnectionString);
        //}

        //public static bool operator !=(FileToUpload f1, FileToUpload f2)
        //{
        //    return (f1._cString.SqlConnectionString != f2._cString.SqlConnectionString);
        //}

        public FileToUpload Find(IEnumerable<FileToUpload> files) {
            //if(files = null)
            FileToUpload fileToUpload1 = files.FirstOrDefault(f => !string.IsNullOrWhiteSpace(DatabaseToBackUp) && f.DatabaseToBackUp == DatabaseToBackUp);
            return fileToUpload1;
        }

        public FileToUpload Find(IEnumerable<Task<FileToUpload>> files)
        {
            Task<FileToUpload> fileToUploadTask = files.FirstOrDefault(f => f.Result.DatabaseToBackUp == DatabaseToBackUp);
            return fileToUploadTask?.Result;
        }

        public string DeleteFromGDriveResult { get; private set; }
        public Task<string> DeleteFromGDriveTask { get; private set; }
        public void DeleteFromGDrive(CString cString)
        {
            eFileStatus = FileStatus.DeletingGDriveZip;
            Task<string> dt = Task.Run<string>(() => {
                try
                {
                    DeleteFromGDriveResult = GoogleApis.DeleteFile(LocalFilePath, UploadedFileId);
                }
                catch (Exception ex) {
                    //cString.HasErrors = true;
                    SetError(ex);
                    throw;
                }
                if (!string.IsNullOrWhiteSpace(DeleteFromGDriveResult))
                {
                    if (cString != null) cString.HasErrors = true;
                    Exception ex1 = new Exception($"{LocalFilePath} could not be deleted from GDrive (Id: {UploadedFileId})", new Exception(DeleteFromGDriveResult));
                    SetError(ex1);
                    throw ex1;
                }
                else
                    IsGDriveZipDeleted = true;
                return DeleteFromGDriveResult;
            });
            DeleteFromGDriveTask = dt;
        }

        public void DeleteFromLocal() {
            if (IsLocalZipDeleted)
                throw new Exception($"{LocalFilePath} file already deleted!");
            File.Delete(LocalFilePath);
            IsLocalZipDeleted = true;
            string deletePath = Path.GetDirectoryName(LocalFilePath);            
        }

        public static void SaveFileToUploadList(IEnumerable<FileToUpload> latestUploadedFiles) {
            string jsonLatestFilesUploaded = JsonConvert.SerializeObject(latestUploadedFiles.ToArray());
            File.WriteAllText("LatestUploadedFiles.json", jsonLatestFilesUploaded);
        }

        public static List<FileToUpload> GetFileToUploadListFromSettings()
        {
            if (!File.Exists(LastestUploadedSettingFile))
                return new List<FileToUpload>();
            string jsonLastestFilesUploaded = File.ReadAllText(LastestUploadedSettingFile);
            if (string.IsNullOrWhiteSpace(jsonLastestFilesUploaded))
                return new List<FileToUpload>();
            JsonConvert.DefaultSettings = () => new JsonSerializerSettings { ContractResolver = new NonPublicPropertiesResolver() };
            List<FileToUpload> filesUploaded = JsonConvert.DeserializeObject<List<FileToUpload>>(jsonLastestFilesUploaded);
            return filesUploaded;
        }

        public async void Dispose()
        {
            string result = "";
            try
            {
                //if (DeleteResultString.HasValue)
                //    result = DeleteResultString.Value.GetResult();
                if (!string.IsNullOrWhiteSpace(DeleteFromGDriveResult))
                    await Logger.LogException(new Exception(DeleteFromGDriveResult));
            }
            catch (Exception ex) {
                await Logger.LogException(ex);
            }
        }
    }

    public class UploadedFiles { 
    
        public string FileId { get; set;}

        public string Filename { get; set; }

        public DateTime GCreatedOn { get; set; }

    }

    public class ViewCoreModule : IModule
    {
        public void OnLoad(IBinder Binder)
        {
            //Binder.BindSingleton<CrashLogsViewModel>();
            //Binder.BindSingleton<FileNameFormatViewModel>();
            //Binder.BindSingleton<LicensesViewModel>();
            //Binder.BindSingleton<ProxySettingsViewModel>();
            //Binder.BindSingleton<SoundsViewModel>();
            //Binder.BindSingleton<RecentViewModel>();
            //Binder.BindSingleton<UpdateCheckerViewModel>();
            //Binder.BindSingleton<ScreenShotViewModel>();
            //Binder.BindSingleton<RecordingViewModel>();
            Binder.BindSingleton<MainViewModel>();
            Binder.Bind<IMainViewModel, MainViewModel>(true);
            
            //Binder.BindSingleton<HotkeysViewModel>();
            //Binder.BindSingleton<FFmpegLogViewModel>();
            //Binder.BindSingleton<FFmpegCodecsViewModel>();
            //Binder.BindSingleton<ViewConditionsModel>();

            //Binder.BindSingleton<VideoSourcesViewModel>();
            //Binder.BindSingleton<VideoWritersViewModel>();

            //Binder.BindSingleton<AudioSourceViewModel>();

            //Binder.BindSingleton<CustomOverlaysViewModel>();
            //Binder.BindSingleton<CustomImageOverlaysViewModel>();
            //Binder.BindSingleton<CensorOverlaysViewModel>();

            //Binder.BindSingleton<FFmpegLog>();
            //Binder.Bind<IFFmpegLogRepository>(ServiceProvider.Get<FFmpegLog>);

            //Binder.Bind<IHotkeyActor, HotkeyActor>();
        }

        public void Dispose() { }
    }

    public class NonPublicPropertiesResolver : DefaultContractResolver
    {
        protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
        {
            var prop = base.CreateProperty(member, memberSerialization);
            if (member is PropertyInfo pi)
            {
                prop.Readable = (pi.GetMethod != null);
                prop.Writable = (pi.SetMethod != null);
            }
            return prop;
        }
    }


}
