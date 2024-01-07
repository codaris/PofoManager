using System;
using System.Collections.Generic;
using System.Linq;
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

using Microsoft.Win32;

using PortfolioSync.ViewModels;

namespace PortfolioSync.Views
{
    /// <summary>
    /// Interaction logic for RetrieveDialog.xaml
    /// </summary>
    public partial class RetrieveDialog : Window
    {
        /// <summary>
        /// Gets the view model.
        /// </summary>
        private readonly RetrieveViewModel viewModel;

        /// <summary>
        /// Initializes a new instance of the <see cref="RetrieveDialog"/> class.
        /// </summary>
        /// <param name="owner">The owner.</param>
        public RetrieveDialog(Window owner, RetrieveViewModel viewModel)
        {
            InitializeComponent();
            this.viewModel = viewModel;
            DataContext = viewModel;
            Owner = owner;  
        }

        /// <summary>
        /// Handles the Click event of the Retrieve control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs"/> instance containing the event data.</param>
        private async void Retrieve_Click(object sender, RoutedEventArgs e)
        {
            if (await viewModel.RetrieveFile(this)) Close();
        }

        /// <summary>
        /// Handles the Click event of the Cancel control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs"/> instance containing the event data.</param>
        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            if (viewModel.IsNotRunning) DialogResult = false;
            else viewModel.Cancel();
        }

        /// <summary>
        /// Handles the Click event of the SelectFile control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs"/> instance containing the event data.</param>
        private void SelectFile_Click(object sender, RoutedEventArgs e)
        {
            var saveFileDialog = new SaveFileDialog();
            saveFileDialog.Filter = "All files (*.*)|*.*";
            if (saveFileDialog.ShowDialog() == true)
            {
                viewModel.DestinationPath = saveFileDialog.FileName; 
            }
        }

        /// <summary>
        /// Shows the dialog.
        /// </summary>
        /// <param name="owner">The owner.</param>
        /// <param name="filePath">The file path.</param>
        /// <returns></returns>
        public static bool ShowDialog(Window owner, Arduino arduino)
        {
            var viewModel = new RetrieveViewModel(arduino);
            var dialog = new RetrieveDialog(owner, viewModel);
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
            e.Cancel = !viewModel.IsNotRunning;
        }
    }
}
