using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

using PortfolioSync.Views;

namespace PortfolioSync.ViewModels
{
    /// <summary>
    /// View model for the list files dialog
    /// </summary>
    /// <seealso cref="PortfolioSync.ViewModels.BaseViewModel" />
    public class ListViewModel : BaseViewModel
    {
        /// <summary>
        /// Gets or sets the remote path pattern
        /// </summary>
        public string RemotePath
        {
            get => GetProperty<string>("C:\\*.*", nameof(RemotePath));
            set => SetProperty(value.Trim().ToUpper().Replace('/', '\\'));
        }

        /// <summary>
        /// Gets the file collections
        /// </summary>
        public ObservableCollection<string> Files { get; } = new ObservableCollection<string>();

        /// <summary>
        /// Gets or sets the currently selected file.
        /// </summary>
        public string? SelectedFile
        {
            get => GetProperty<string>();
            set => SetProperty(value);
        }

        /// <summary>
        /// Gets a value indicating whether the form is enabled
        /// </summary>
        public bool IsEnabled
        {
            get => GetProperty<bool>(true);
            private set => SetProperty(value);
        }

        /// <summary>
        /// Gets a value indicating whether this instance can retrieve the selected file.
        /// </summary>
        public bool CanRetrieveFile => IsEnabled && SelectedFile != null;

        /// <summary>
        /// The arduino instance
        /// </summary>
        private readonly Arduino arduino;

        /// <summary>
        /// Initializes a new instance of the <see cref="RetrieveViewModel"/> class.
        /// </summary>
        /// <param name="arduino">The arduino.</param>
        public ListViewModel(Arduino arduino)
        {
            this.arduino = arduino;
            this.PropagatePropertyChanged(this, t => t.IsEnabled, t => t.CanRetrieveFile);
            this.PropagatePropertyChanged(this, t => t.SelectedFile!, t => t.CanRetrieveFile);
        }

        /// <summary>
        /// Sends the file.
        /// </summary>
        /// <param name="owner">The owner.</param>
        /// <returns></returns>
        public async Task<bool> RetrieveFileList(Window owner)
        {
            if (string.IsNullOrWhiteSpace(RemotePath))
            {
                MessageBox.Show(owner, "Remote path pattern must be specified.", "List Files", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            try
            {
                IsEnabled = false;
                var files = await arduino.ListFiles(RemotePath);
                Files.Clear();
                foreach (var file in files) Files.Add(file);
                return true;
            }
            finally
            {
                IsEnabled = true;
            }
        }

        /// <summary>
        /// Opens the retrieve file dialog.
        /// </summary>
        /// <param name="owner">The owner.</param>
        /// <param name="fileName">Name of the file.</param>
        public void OpenRetrieveFileDialog(Window owner, string? fileName = null)
        {
            fileName ??= SelectedFile;
            if (string.IsNullOrWhiteSpace(fileName)) return;
            string directoryPath = Path.GetDirectoryName(RemotePath) ?? string.Empty;
            var fullPath = Path.Combine(directoryPath, fileName);
            RetrieveDialog.ShowDialog(owner, arduino, fullPath);
        }
    }
}
