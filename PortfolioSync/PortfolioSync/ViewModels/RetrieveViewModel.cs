using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace PortfolioSync.ViewModels
{
    /// <summary>
    /// The view model for the retrieve dialog
    /// </summary>
    /// <seealso cref="PortfolioSync.ViewModels.BaseViewModel" />
    /// <seealso cref="PortfolioSync.IFileProgress" />
    public class RetrieveViewModel : TransferViewModel
    {
        /// <summary>
        /// Gets or sets the source file path.
        /// </summary>
        public string SourcePath
        {
            get => GetProperty<string>();
            set => SetProperty(value.Trim().ToUpper().Replace('/', '\\'));
        }

        /// <summary>
        /// Gets or sets the local destination path.
        /// </summary>
        public string DestinationPath
        {
            get => GetProperty<string>();
            set
            {
                SetProperty(value);
                OnPropertyChanged(nameof(DestinationPathVisibility));
            }
        }

        /// <summary>
        /// Gets the destination path visibility.
        /// </summary>
        public Visibility DestinationPathVisibility => string.IsNullOrWhiteSpace(DestinationPath) ? Visibility.Collapsed : Visibility.Visible;

        /// <summary>
        /// Gets a value indicating whether the form is enabled
        /// </summary>
        public bool IsEnabled
        {
            get => GetProperty<bool>(true);
            private set => SetProperty(value);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="RetrieveViewModel"/> class.
        /// </summary>
        /// <param name="arduino">The arduino.</param>
        public RetrieveViewModel(Arduino arduino) : base(arduino)
        {
        }

        /// <summary>
        /// Sends the file.
        /// </summary>
        /// <param name="owner">The owner.</param>
        /// <returns></returns>
        public async Task<bool> RetrieveFile(Window owner)
        {
            if (string.IsNullOrWhiteSpace(SourcePath))
            {
                MessageBox.Show(owner, "Source file path must be specified.", "Retrieve File", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
            if (!SourcePath.IsValidFileName())
            {
                MessageBox.Show(owner, "Source file path is not valid.", "Retrieve File", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
            if (string.IsNullOrWhiteSpace(DestinationPath))
            {
                MessageBox.Show(owner, "Destination file path must be selected.", "Retrieve File", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            try
            {
                IsEnabled = false;
                await arduino.RetreiveFile(SourcePath, DestinationPath, this).ConfigureAwait(false);
                return true;
            }
            finally
            {
                IsEnabled = true;
            }
        }
    }
}
