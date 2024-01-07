using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace PortfolioSync.ViewModels
{
    public class ListViewModel : BaseViewModel
    {
        /// <summary>
        /// Gets or sets the destination path.
        /// </summary>
        public string RemotePath
        {
            get => GetProperty<string>("C:\\*.*", nameof(RemotePath));
            set => SetProperty(value.Trim().ToUpper().Replace('/', '\\'));
        }

        /// <summary>
        /// Gets the files.
        /// </summary>
        public ObservableCollection<string> Files { get; } = new ObservableCollection<string>();

        /// <summary>
        /// Gets or sets the selected file.
        /// </summary>
        public string? SelectedFile
        {
            get => GetProperty<string>();
            set => SetProperty(value);
        }

        /// <summary>
        /// Gets a value indicating whether the send is not running.
        /// </summary>
        public bool IsNotRunning
        {
            get => GetProperty<bool>(true);
            private set => SetProperty(value);
        }

        /// <summary>
        /// Gets a value indicating whether this instance can retrieve file.
        /// </summary>
        public bool CanRetrieveFile => IsNotRunning && SelectedFile != null;

        /// <summary>
        /// Gets the full path for a file name
        /// </summary>
        /// <param name="fileName">Name of the file.</param>
        /// <returns></returns>
        public string GetFullPath(string fileName)
        {
            string directoryPath = Path.GetDirectoryName(RemotePath) ?? string.Empty;
            return Path.Combine(directoryPath, fileName);
        }

        /// <summary>
        /// The arduino instance
        /// </summary>
        public Arduino Arduino { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="RetrieveViewModel"/> class.
        /// </summary>
        /// <param name="arduino">The arduino.</param>
        public ListViewModel(Arduino arduino)
        {
            this.Arduino = arduino;
            this.PropagatePropertyChanged(this, t => t.IsNotRunning, t => t.CanRetrieveFile);
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
                MessageBox.Show(owner, "Remote path must be specified.", "Retrieve File", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            try
            {
                IsNotRunning = false;
                var files = await Arduino.ListFiles(RemotePath);
                Files.Clear();
                foreach (var file in files) Files.Add(file);
                return true;
            }
            finally
            {
                IsNotRunning = true;
            }
        }

    }
}
