using System;
using System.IO;
using System.Reactive;
using System.Reactive.Linq;
using Microsoft.Win32;
using ReactiveUI;

namespace MsgfProcessor
{
    public class FileSelectorViewModel : ReactiveObject
    {
        /// <summary>
        /// Indicates whether this selector should open a save dialog or open dialog.
        /// </summary>
        private readonly bool isSaveDialog;

        /// <summary>
        /// The default extension to be displayed on dialog.
        /// </summary>
        private readonly string defaultExt;

        /// <summary>
        /// The default extension filter to be displayed on dialog.
        /// </summary>
        private readonly string filter;

        /// <summary>
        /// The full path to the file.
        /// </summary>
        private string filePath;

        /// <summary>
        /// A value indicating whether the <see cref="FilePath" /> is not empty and exists.
        /// </summary>
        private readonly ObservableAsPropertyHelper<bool> isValid;

        /// <summary>
        /// Initializes a new instance of the <see cref="FileSelectorViewModel" />. 
        /// </summary>
        /// <param name="label">The label text for the file selector control.</param>
        /// <param name="isSaveDialog">Indicates whether this selector should open a save dialog or open dialog.</param>
        /// <param name="defaultExt">The default extension to be displayed on dialog.</param>
        /// <param name="filter">The default extension filter to be displayed on dialog.</param>
        public FileSelectorViewModel(string label, bool isSaveDialog, string defaultExt, string filter)
        {
            this.Label = label;
            this.isSaveDialog = isSaveDialog;
            this.defaultExt = defaultExt;
            this.filter = filter;

            this.BrowseCommand = ReactiveCommand.Create(this.BrowseImpl);

            this.WhenAnyValue(x => x.FilePath)
                .Select(_ => this.CheckValid())
                .ToProperty(this, x => x.IsValid, out this.isValid);

            this.FilePath = string.Empty;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="FileSelectorViewModel"/>.
        /// Default empty constructor.
        /// </summary>
        public FileSelectorViewModel()
        {
        }

        /// <summary>
        /// Gets the label text for the file selector control.
        /// </summary>
        public string Label { get; }

        /// <summary>
        /// Gets a command that opens a file dialog for the file selection.
        /// </summary>
        public ReactiveCommand<Unit, Unit> BrowseCommand { get; }

        /// <summary>
        /// Gets or sets the full path to the file.
        /// </summary>
        public string FilePath
        {
            get { return this.filePath; }
            set { this.RaiseAndSetIfChanged(ref this.filePath, value); }
        }

        /// <summary>
        /// Gets a value indicating whether the <see cref="FilePath" /> is not empty and exists.
        /// </summary>
        public bool IsValid => this.isValid?.Value ?? false;

        /// <summary>
        /// Checks to see if <see cref="FilePath" /> is not empty and exists.
        /// </summary>
        /// <returns>A value indicating whether the file path is valid.</returns>
        public bool CheckValid()
        {
            return !string.IsNullOrWhiteSpace(this.FilePath) && File.Exists(this.FilePath);
        }

        /// <summary>
        /// Opens a file dialog for the file selection.
        /// </summary>
        private void BrowseImpl()
        {
            if (this.isSaveDialog)
            {
                this.SaveDialog();
            }
            else
            {
                this.OpenDialog();
            }
        }

        /// <summary>
        /// Opens a save dialog and stores the result to <see cref="FilePath" />.
        /// </summary>
        private void SaveDialog()
        {
            var dialog = new SaveFileDialog { DefaultExt = this.defaultExt, Filter = this.filter };

            var result = dialog.ShowDialog();
            if (result == true)
            {
                this.FilePath = dialog.FileName;
            }
        }

        /// <summary>
        /// Opens an open dialog and stores the result to <see cref="FilePath" />.
        /// </summary>
        private void OpenDialog()
        {
            var dialog = new OpenFileDialog { DefaultExt = this.defaultExt, Filter = this.filter };

            var result = dialog.ShowDialog();
            if (result == true)
            {
                this.FilePath = dialog.FileName;
            }
        }
    }
}
