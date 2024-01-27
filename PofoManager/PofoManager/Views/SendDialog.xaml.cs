using System;
using System.Collections.Generic;
using System.Linq;
using System.Printing;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

using PofoManager.ViewModels;

namespace PofoManager.Views
{
    /// <summary>
    /// Interaction logic for SendDialog.xaml
    /// </summary>
    public partial class SendDialog : Window
    {
        /// <summary>
        /// The view model
        /// </summary>
        private readonly SendViewModel viewModel;

        /// <summary>
        /// Initializes a new instance of the <see cref="SendDialog"/> class.
        /// </summary>
        /// <param name="owner">The owner.</param>
        /// <param name="viewModel">The view model.</param>
        public SendDialog(Window owner, SendViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            this.viewModel = viewModel; 
            Owner = owner;  
        }

        /// <summary>
        /// Handles the Click event of the Send control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs"/> instance containing the event data.</param>
        private async void Send_Click(object sender, RoutedEventArgs e)
        {
            if (await viewModel.SendFile(this)) Close();
        }

        /// <summary>
        /// Handles the Click event of the Cancel control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs"/> instance containing the event data.</param>
        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            if (viewModel.IsEnabled) DialogResult = false;
            else viewModel.Cancel();
        }

        /// <summary>
        /// Shows the dialog.
        /// </summary>
        /// <param name="owner">The owner.</param>
        /// <param name="filePath">The file path.</param>
        /// <returns></returns>
        public static bool ShowDialog(Window owner, Arduino arduino, string filePath)
        {
            var viewModel = new SendViewModel(arduino, filePath);
            var dialog = new SendDialog(owner, viewModel);
            return dialog.ShowDialog() ?? false;
        }

        /// <summary>
        /// Handles the Closing event of the Window control. 
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.ComponentModel.CancelEventArgs"/> instance containing the event data.</param>
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // Prevent closing the window if task is running
            e.Cancel = !viewModel.IsEnabled;
        }
    }
}
