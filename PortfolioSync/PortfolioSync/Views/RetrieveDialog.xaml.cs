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
        public RetreiveViewModel ViewModel { get; }

        public RetrieveDialog(Window owner)
        {
            InitializeComponent();
            ViewModel = new RetreiveViewModel();
            DataContext = ViewModel;
            Owner = owner;  
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void Retrieve_Click(object sender, RoutedEventArgs e)
        {
            if (!ViewModel.Validate(this)) return;
            DialogResult = true;
        }

        private void SelectFile_Click(object sender, RoutedEventArgs e)
        {
            var saveFileDialog = new SaveFileDialog();
            saveFileDialog.Filter = "All files (*.*)|*.*";
            if (saveFileDialog.ShowDialog() == true)
            {
                ViewModel.DestinationPath = saveFileDialog.FileName; 
            }
        }

        /// <summary>
        /// Shows the dialog.
        /// </summary>
        /// <param name="owner">The owner.</param>
        /// <param name="filePath">The file path.</param>
        /// <returns></returns>
        public static RetreiveViewModel ShowDialog(Window owner)
        {
            var dialog = new RetrieveDialog(owner);
            dialog.ViewModel.Result = dialog.ShowDialog() ?? false;
            return dialog.ViewModel;
        }
    }
}
