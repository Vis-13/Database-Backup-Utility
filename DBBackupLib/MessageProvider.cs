using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;



namespace DBBackupLib
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public class MessageProvider : IMessageProvider
    {
        public MessageProvider()
        {
            
        }

        public void ShowError(string Message, string Header = null)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                //var dialog = new ModernDialog
                //{
                //    Title = _loc.ErrorOccurred,
                //    Content = new StackPanel
                //    {
                //        Children =
                //        {
                //            new TextBlock
                //            {
                //                Text = Header,
                //                Margin = new Thickness(0, 0, 0, 10),
                //                FontSize = 15
                //            },

                //            new ScrollViewer
                //            {
                //                Content = Message,
                //                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                //                Padding = new Thickness(0, 0, 0, 10)
                //            }
                //        }
                //    }
                //};

                //dialog.OkButton.Content = _loc.Ok;
                //dialog.Buttons = new[] { dialog.OkButton };

                //dialog.BackgroundContent = new Grid
                //{
                //    Background = new SolidColorBrush(Color.FromArgb(255, 244, 67, 54)),
                //    VerticalAlignment = VerticalAlignment.Top,
                //    Height = 10
                //};

                //_audioPlayer.Play(SoundKind.Error);

                //dialog.ShowDialog();
            });
        }

        public void ShowException(Exception Exception, string Message, bool Blocking = false)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                //var win = new ErrorWindow(Exception, Message);

                //_audioPlayer.Play(SoundKind.Error);

                //if (Blocking)
                //{
                //    win.ShowDialog();
                //}
                //else win.ShowAndFocus();
            });
        }

        public bool ShowYesNo(string Message, string Title)
        {
            return Application.Current.Dispatcher.Invoke(() =>
            {
                //var dialog = new ModernDialog
                //{
                //    Title = Title,
                //    Content = new ScrollViewer
                //    {
                //        Content = Message,
                //        HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                //        Padding = new Thickness(0, 0, 0, 10)
                //    }
                //};

                var result = false;

                //dialog.YesButton.Content = _loc.Yes;
                //dialog.YesButton.Click += (S, E) => result = true;

                //dialog.NoButton.Content = _loc.No;

                //dialog.Buttons = new[] { dialog.YesButton, dialog.NoButton };

                //dialog.ShowDialog();

                return result;
            });
        }

        public System.Windows.Window CurrentDialog { get; private set; } = null;

        public void ShowMessage(string Message, string Title, TimeSpan? timeToClose, bool Blocking)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                //var dialog = new ModernDialog
                //{
                //    Title = Title,
                //    Content = new ScrollViewer
                //    {
                //        Content = Message,
                //        HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                //        Padding = new Thickness(0, 0, 0, 10)
                //    }
                //};

                //dialog.YesButton.Visibility = Visibility.Hidden;//.Content = _loc.Yes;
                ////dialog.YesButton.Click += (S, E) => result = true;

                //dialog.NoButton.Visibility = Visibility.Hidden;//.Content = _loc.No;
                //dialog.CloseButton.Visibility = Visibility.Hidden;
                ////dialog.Buttons = new[] { dialog.YesButton, dialog.NoButton };
                
                //if (CurrentDialog != null && CurrentDialog.IsActive)
                //    CurrentDialog.Close();
                //CurrentDialog = dialog;
                //if (timeToClose.HasValue && timeToClose.Value != TimeSpan.MaxValue && timeToClose != TimeSpan.MaxValue)
                //   Task.Run(() => CloseCurrentDialog(timeToClose));
                //if (Blocking)
                //{
                //    dialog.ShowDialog();
                //}
                //else dialog.ShowAndFocus();
            });
        }

        public void CloseCurrentDialog(TimeSpan? timeSpan = null) {
            if(timeSpan.HasValue)
                Thread.Sleep(timeSpan.Value);
            Application.Current.Dispatcher.Invoke(() =>
            {
                //CurrentDialog.Close();
            });
        }

    }

    public interface IMessageProvider
    {
        System.Windows.Window CurrentDialog { get; }

        void ShowError(string Message, string Header = null);

        bool ShowYesNo(string Message, string Title);

        void ShowException(Exception Exception, string Message, bool Blocking = false);
        void ShowMessage(string Message, string Title, TimeSpan? timeToClose = null, bool Blocking = false);

        void CloseCurrentDialog(TimeSpan? timeToClose = null);
    }


    public interface IMessageProviderNoPresen
    {
        void ShowError(string Message, string Header = null);

        bool ShowYesNo(string Message, string Title);

        void ShowException(Exception Exception, string Message, bool Blocking = false);

    }
    

}
