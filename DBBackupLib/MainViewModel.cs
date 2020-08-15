using Google.Apis.Upload;
using Reactive.Bindings;
using Reactive.Bindings.ObjectExtensions;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.SqlClient;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reactive.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;

namespace DBBackupLib
{
    public class MainViewModel : NotifyPropertyChanged, IMainViewModel
    {
        string _cstringDBToBackup = "Data Source=.;Initial Catalog=gfunjoker;Integrated Security=True";
        public string CStringDBToBackup { get { return _cstringDBToBackup; } set { Set(ref _cstringDBToBackup, value); } }

        string _databaseNameToBackup = "gfunjoker";
        public string DatabaseNameToBackup { get { return _databaseNameToBackup; } set { Set(ref _databaseNameToBackup, value); } }

        string _backupFolderPath;
        public string BackupFolderPath { get { return _backupFolderPath; } 
            set { Set(ref _backupFolderPath, value); } }

        string _backupQuery = "BACKUP DATABASE [{0}] TO DISK = '{1}'";

        int _backupIntervalInMins = 10;
        public int BackupIntervalInMins { get { return _backupIntervalInMins; } set { if(Set(ref _backupIntervalInMins, value)) BackUpDatabaseInIntervals(); } }

        public int BackupIntervalInMilliSeconds { get { return (int)TimeSpan.FromMinutes(_backupIntervalInMins).TotalMilliseconds; } }

        public ReactiveProperty<bool> IsProcessing { get; private set; } = new ReactiveProperty<bool>(false);
       
        public ICommand BackupCommand { get; }

        public ICommand SetBackupIntervalCommand { get; }

        public string CurrentDateTimeString { get { return DateTime.Now.ToString("dd-MM-yyyy_HH-mm-ss"); } }

        public ReactiveProperty<bool> HasExceptionOccuredWhileBackingup { get; private set; } = new ReactiveProperty<bool>(false);

        public ReactiveProperty<bool> BackupUploadedSuccessfully { get; private set; } = new ReactiveProperty<bool>(false);

        public ReactiveProperty<string> LastBackupExceptionMsg { get; private set; } = new ReactiveProperty<string>();

        public MainViewModel() {
            _backupFolderPath = ServiceProvider.TempBackupDir;
            BackupCommand = new[] { IsProcessing, HasExceptionOccuredWhileBackingup }.CombineLatest(M => !M[0] && !M[1]).ToReactiveCommand().WithSubscribe(StartDatabaseBackup);
            SetBackupIntervalCommand = new[] { IsProcessing }.CombineLatest(M => !M[0]).ToReactiveCommand<string>().WithSubscribe(SetBackupInervals);
        }

        public void UploadFile(string tempFilePath, string currentTimeString) {
            string uploadFilePath = //Directory.GetFiles(_backupFolderPath).FirstOrDefault(); 
                                    Path.Combine(_backupFolderPath, "backup_" + currentTimeString + ".zip");
            ZipFile.CreateFromDirectory(tempFilePath, uploadFilePath);            
            IUploadProgress uploadProgress = GoogleApis.UploadFile(uploadFilePath).Result;
            if (uploadProgress.Status == UploadStatus.Failed)
                throw uploadProgress.Exception;           
        }

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

        public void BackUpDatabase() {
            HasExceptionOccuredWhileBackingup.Value = false;
            BackupUploadedSuccessfully.Value = false;
            IsProcessing.Value = true;           
            LastBackupExceptionMsg.Value = "";            
            try
            {
                SqlConnection sqlConnection = new SqlConnection(_cstringDBToBackup);
                sqlConnection.Open();
                if (!Directory.Exists(_backupFolderPath))
                    Directory.CreateDirectory(_backupFolderPath);
                string currentTimeString = CurrentDateTimeString;
                DirectoryInfo di = Directory.CreateDirectory(_backupFolderPath + "\\" + _databaseNameToBackup + currentTimeString);

                string tempBackupFilePath = Path.Combine(di.FullName, _databaseNameToBackup + ".bak");
                string backupQuery = string.Format(_backupQuery, DatabaseNameToBackup, tempBackupFilePath);
                using (SqlCommand cmd = new SqlCommand(backupQuery, sqlConnection))
                    cmd.ExecuteNonQuery();
                sqlConnection.Dispose();
                UploadFile(di.FullName, currentTimeString);
                IsProcessing.Value = false;
                BackupUploadedSuccessfully.Value = true;
            }
            catch (Exception ex) {
                SetException(ex);
                //Thread.Sleep(5000);
            }                        
        }

        void SetException(Exception ex) {
            IsProcessing.Value = false;
            HasExceptionOccuredWhileBackingup.Value = true;
            BackupUploadedSuccessfully.Value = false;
            Logger.LogException(ex);
            LastBackupExceptionMsg.Value = "There was an error Backing Up the Database to GDrive. Please check the logs!";
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
            IsProcessing.Value = true;
            BackUpDatabase();
            IsProcessing.Value = false;
            _timer.Change(TimeSpan.FromMilliseconds(BackupIntervalInMilliSeconds), Timeout.InfiniteTimeSpan);
        }

        public void Dispose()
        {
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


}
