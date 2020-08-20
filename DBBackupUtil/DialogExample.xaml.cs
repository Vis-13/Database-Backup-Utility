using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace DialogReplacement
{
    

    public partial class DialogExample : Window
    {
        public DialogExample()
        {
            InitializeComponent();
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
}
