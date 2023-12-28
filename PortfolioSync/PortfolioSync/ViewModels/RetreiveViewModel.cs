using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace PortfolioSync.ViewModels
{
    public class RetreiveViewModel : BaseViewModel
    {
        /// <summary>
        /// Gets or sets the destination path.
        /// </summary>
        public string SourcePath
        {
            get => GetProperty<string>();
            set => SetProperty(value.Trim().ToUpper().Replace('/', '\\'));
        }

        /// <summary>
        /// Gets or sets the destination path.
        /// </summary>
        public string DestinationPath
        {
            get => GetProperty<string>();
            set => SetProperty(value);
        }

        /// <summary>
        /// Gets or sets a value indicating whether this <see cref="RetreiveViewModel"/> is result.
        /// </summary>
        public bool Result { get; set; }

        /// <summary>
        /// Validates the dialog
        /// </summary>
        /// <param name="owner">The owner.</param>
        /// <returns></returns>
        public bool Validate(Window owner)
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
            return true;
        }
    }
}
