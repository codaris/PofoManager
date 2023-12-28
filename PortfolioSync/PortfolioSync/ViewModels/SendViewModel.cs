using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace PortfolioSync.ViewModels
{
    public class SendViewModel : BaseViewModel
    {
        /// <summary>
        /// Gets or sets the selected serial port.
        /// </summary>
        public string FilePath { get; }

        /// <summary>
        /// Gets or sets the destination path.
        /// </summary>
        public string DestinationPath
        {
            get => GetProperty<string>();
            set => SetProperty(value.Trim().ToUpper().Replace('/', '\\'));
        }

        /// <summary>
        /// Gets or sets a value indicating whether this <see cref="SendViewModel"/> is overwrite.
        /// </summary>
        public bool OverwriteFile
        {
            get => GetProperty<bool>(true);
            set => SetProperty(value);
        }

        /// <summary>
        /// Gets or sets a value indicating whether this <see cref="SendViewModel"/> is result.
        /// </summary>
        public bool Result { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="SendViewModel"/> class.
        /// </summary>
        /// <param name="filePath">The file path.</param>
        public SendViewModel(string filePath)
        {
            this.FilePath = filePath;
            this.DestinationPath = "C:\\" + Path.GetFileNameWithoutExtension(filePath).ToUpper().Truncate(8) + Path.GetExtension(filePath).ToUpper().Truncate(4);
        }

        /// <summary>
        /// Validates the dialog
        /// </summary>
        /// <param name="owner">The owner.</param>
        /// <returns></returns>
        public bool Validate(Window owner)
        {
            if (string.IsNullOrWhiteSpace(DestinationPath))
            {
                MessageBox.Show(owner, "Destination file name must be specified.", "Send File", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
            if (!DestinationPath.IsValidFileName())
            {
                MessageBox.Show(owner, "Destination file name is not valid.", "Send File", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
            return true;
        }
    }
}
