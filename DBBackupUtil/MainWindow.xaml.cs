﻿//using Microsoft.VisualStudio.Data;
//using Microsoft.VisualStudio.Data.AdoDotNet;
using com.rusanu.dataconnectiondialog;
using DBBackupLib;
using System;
using System.Data.SqlClient;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interactivity;
//using System.Windows.Forms;
using System.Windows.Interop;

namespace DBBackupUtil
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        SqlConnectionStringBuilder _scsb = null;

        public static ItemsControl ItemsControl { get; set; }

        public static NativeWindow Win32Parent = new NativeWindow();

        MainViewModel MainViewModel = null;

        public MainWindow()
        {
            InitializeComponent();
            StateChanged += MainWindowStateChangeRaised;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
            Win32Parent.AssignHandle(new WindowInteropHelper(this).Handle);
            MainViewModel = ServiceProvider.Get<MainViewModel>();
            DataContext = MainViewModel;
            ItemsControl = icCS;
            Closed += (Sender, Args) =>
            {
                MainViewModel.Dispose();                
            };
            Loaded += (sender, args) => {
                string logChar = string.Format(MainViewModel.LogChars, DateTime.Now + " : " + "{0}");
                File.AppendAllText(MainViewModel.BackupLogFile, string.Format(logChar, $"\n" + "Backup Application Started!" + "\n\n"));
            };
            ContentRendered += (sender, args) => { 
            
            };
            
        }

        private void TaskScheduler_UnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            if (!e.Observed)
            {
                string logChar = string.Format(MainViewModel.LogChars, DateTime.Now + " : " + "{0}");
                Logger.LogException(e.Exception == null ? new Exception("Unhandled Exception....") : e.Exception).Wait(1);
                File.AppendAllText(MainViewModel.BackupLogFile, string.Format(logChar, e.Exception + "\n"));
            }
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            string logChar = string.Format(MainViewModel.LogChars, DateTime.Now + " : " + "{0}");
            Logger.LogException(new Exception(e.ExceptionObject == null ? "Unhandled Exception...." : e.ExceptionObject.ToString())).Wait(1);
            File.AppendAllText(MainViewModel.BackupLogFile, string.Format(logChar, e.ExceptionObject +"\n"));
        }

        private void btnSelectFolder_Click(object sender, RoutedEventArgs e)
        {
            FolderBrowserDialog dialog = new FolderBrowserDialog();
            dialog.RootFolder = Environment.SpecialFolder.MyDocuments;// = MainViewModel.BackupFolderPath;
            dialog.ShowNewFolderButton = true;
            
            if (dialog.ShowDialog(Win32Parent) == System.Windows.Forms.DialogResult.OK) {
                MainViewModel.BackupFolderPath = dialog.SelectedPath;
                //RaisePropertyChanged("StatusText");
            }
            
        }

        private void btnDatabase_Click(object sender, RoutedEventArgs e)
        {
            string connectionString = SelectDatabase();
            CString cString = ((System.Windows.Controls.Button)sender).Tag as CString;
            if(!string.IsNullOrWhiteSpace(connectionString))
                cString.SqlConnectionString = connectionString;
        }

        string SelectDatabase() {
            //string cstring = cs.ConnectionString;
            //cs.ConnectionString = "Data Source=.;Initial Catalog=LuckyDrawRecordingWeb;User Id=sa;Password=Sherlock007;";
            _scsb = new SqlConnectionStringBuilder("Data Source=.;Initial Catalog=master;Integrated Security=true;");
            // Display the connection dialog
            DataConnectionDialog dlg = new DataConnectionDialog(_scsb);
            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                _scsb = dlg.ConnectionStringBuilder;
                MainViewModel.CStringDBToBackup = _scsb.ConnectionString;
                MainViewModel.DatabaseNameToBackup = _scsb.InitialCatalog;
                return _scsb.ConnectionString;
            }
            return string.Empty;
        }

        private void btnGDriveList_Click(object sender, RoutedEventArgs e)
        {
            //Task t = Task.Run(() => 
                MainViewModel.StartDatabaseBackup();
            //);
            //while (!t.Wait(10))
            //    System.Windows.Forms.Application.DoEvents();
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            if (e != null)
                e.Handled = true;
            MessageBoxResult msgResult = MessageBoxResult.None;
            if (MainViewModel.IsProcessing.Value)
            {
                msgResult = System.Windows.MessageBox.Show("Taking a Backup. Do you want to close abruptly!", "Warning", MessageBoxButton.YesNo);
                if (msgResult == MessageBoxResult.No)
                {
                    return;
                }
            }
            SystemCommands.CloseWindow(this);
            //Close();
        }

        //public void WaitForRecordingToFinish()
        //{
        //    Task waitTask = Task.Run(() => { while (_recordingViewModel.RecorderState.Value == RecorderState.Recording || string.IsNullOrWhiteSpace(_recordingViewModel.FilenameAfterRecordingStoped)) Thread.Sleep(TimeSpan.FromMilliseconds(20)); });
        //    while (!waitTask.Wait(TimeSpan.FromMilliseconds(1)))
        //        System.Windows.Forms.Application.DoEvents();
        //    Settings.Duration = -1;
        //}

        //void Grid_PreviewMouseLeftButtonDown(object Sender, MouseButtonEventArgs Args)
        //{
        //    DragMove();
        //    Args.Handled = true;
        //}

        // Can execute
        private void CommandBinding_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
        }

        // Minimize
        private void CommandBinding_Executed_Minimize(object sender, ExecutedRoutedEventArgs e)
        {
            SystemCommands.MinimizeWindow(this);
        }

        // Maximize
        private void CommandBinding_Executed_Maximize(object sender, ExecutedRoutedEventArgs e)
        {
            SystemCommands.MaximizeWindow(this);
        }

        // Restore
        private void CommandBinding_Executed_Restore(object sender, ExecutedRoutedEventArgs e)
        {
            SystemCommands.RestoreWindow(this);
        }

        // Close
        private void CommandBinding_Executed_Close(object sender, ExecutedRoutedEventArgs e)
        {
            e.Handled = true;
            btnCancel_Click(sender, null);
        }

        // State change
        private void MainWindowStateChangeRaised(object sender, EventArgs e)
        {
            if (WindowState == WindowState.Maximized)
            {
                MainWindowBorder.BorderThickness = new Thickness(8);
                //RestoreButton.Visibility = Visibility.Visible;
                //MaximizeButton.Visibility = Visibility.Collapsed;
            }
            else
            {
                MainWindowBorder.BorderThickness = new Thickness(0);
                RestoreButton.Visibility = Visibility.Collapsed;
                //MaximizeButton.Visibility = Visibility.Visible;
            }
        }

        private void txtBackUpInterval_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {

        }

        private void btnAddConnectionString_Click(object sender, RoutedEventArgs e)
        {
            string cString = SelectDatabase();
            if(!string.IsNullOrWhiteSpace(cString))
                CString.Add(cString);   
        }

        private void btnSettings_Click(object sender, RoutedEventArgs e)
        {
            Settings settingsWindow = new Settings();
            settingsWindow.ShowDialog();
        }

        private void btnCleanLocalAndGDrive_Click(object sender, RoutedEventArgs e)
        {
            //DialogExample dialogExample = new DialogExample();
            //dialogExample.Show();
            OpenMbox_Clicked(sender, e);
        }


        private void OpenMbox_Clicked(object sender, RoutedEventArgs e)
        {
            DialogReplacement.Visibility = System.Windows.Visibility.Visible;
        }


        private void mbox_ok(object sender, RoutedEventArgs e)
        {
            DialogReplacement.Visibility = System.Windows.Visibility.Collapsed;
        }


        private void mbox_cancel(object sender, RoutedEventArgs e)
        {
            DialogReplacement.Visibility = System.Windows.Visibility.Collapsed;
        }
    }

    public class ScrollToBehavior : Behavior<StackPanel>
    {
        public static readonly DependencyProperty IsProcessingProperty = DependencyProperty.Register("IsProcessing", typeof(bool), typeof(ScrollToBehavior));

        public bool IsProcessing
        {
            get { return (bool)GetValue(IsProcessingProperty); }
            set { SetValue(IsProcessingProperty, value); }
        }

        protected override void OnAttached()
        {
            Changed += ScrollToBehavior_Changed;
        }

        private void ScrollToBehavior_Changed(object sender, EventArgs e)
        {
            if (IsProcessing)
                ScrollIntoView();
        }

        private void BorderBrush_Changed(object sender, EventArgs e)
        {
            ScrollIntoView();
        }

        protected override void OnDetaching()
        {
            this.AssociatedObject.TargetUpdated -= this.AssociatedObject_TargetUpdated;
        }

        private void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
        {
            
        }

        private void AssociatedObject_TargetUpdated(object sender, System.Windows.Data.DataTransferEventArgs e)
        {
            //e.Handled = tr
            if(IsProcessing)
                ScrollIntoView();
        }

        bool _wasScrolledIntoView = false;
        public void ScrollIntoView()
        {
            if (_wasScrolledIntoView)
                return;
            ItemsControl control = MainWindow.ItemsControl;
            FrameworkElement framework = control.ItemContainerGenerator.ContainerFromItem(this.AssociatedObject.DataContext) as FrameworkElement;
            if (framework == null) { return; }
            framework.BringIntoView();
        }

    }


}
