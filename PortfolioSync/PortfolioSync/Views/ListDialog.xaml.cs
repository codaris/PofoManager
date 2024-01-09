using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using PortfolioSync.ViewModels;

namespace PortfolioSync.Views
{
    /// <summary>
    /// Interaction logic for ListDialog.xaml
    /// </summary>
    public partial class ListDialog : Window
    {
        /// <summary>
        /// Gets the view model.
        /// </summary>
        private readonly ListViewModel viewModel;

        /// <summary>
        /// Initializes a new instance of the <see cref="ListDialog"/> class.
        /// </summary>
        /// <param name="owner">The owner.</param>
        /// <param name="viewModel">The view model.</param>
        public ListDialog(Window owner, ListViewModel viewModel)
        {
            InitializeComponent();
            this.viewModel = viewModel;
            DataContext = viewModel;
            Owner = owner;  
        }

        /// <summary>
        /// Handles the Click event of the List control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs"/> instance containing the event data.</param>
        private async void List_Click(object sender, RoutedEventArgs e)
        {
            await viewModel.RetrieveFileList(this);
        }

        /// <summary>
        /// Handles the Click event of the Retrieve control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs"/> instance containing the event data.</param>
        private void Retrieve_Click(object sender, RoutedEventArgs e)
        {
            viewModel.OpenRetrieveFileDialog(this);
        }

        /// <summary>
        /// Handles the Click event of the Cancel control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs"/> instance containing the event data.</param>
        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        /// <summary>
        /// Shows the dialog.
        /// </summary>
        /// <param name="owner">The owner.</param>
        /// <param name="filePath">The file path.</param>
        /// <returns></returns>
        public static bool ShowDialog(Window owner, Arduino arduino)
        {
            var viewModel = new ListViewModel(arduino);
            var dialog = new ListDialog(owner, viewModel);
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

        /// <summary>
        /// Handles the MouseDoubleClick event of the ListView control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="MouseButtonEventArgs"/> instance containing the event data.</param>
        private void ListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // Open the dialog on double click of item
            if (((FrameworkElement)e.OriginalSource).DataContext is string item)
            {
                viewModel.OpenRetrieveFileDialog(this, item);
            }
        }
    }
}
