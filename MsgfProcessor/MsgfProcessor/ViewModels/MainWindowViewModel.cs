namespace MsgfProcessor.ViewModels
{
    using System;
    using System.IO;
    using System.Reactive;
    using System.Reactive.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Windows;

    using InformedProteomics.Backend.MassSpecData;
    using InformedProteomics.Backend.Utils;

    using MsgfProcessor.Model;

    using ReactiveUI;

    public class MainWindowViewModel : ReactiveObject
    {
        /// <summary>
        /// Cancellation token for cancelling processing.
        /// </summary>
        private CancellationTokenSource cancellationToken;
        
        /// <summary>
        /// The percentage complete on processing the spectra.
        /// </summary>
        private double progressPercent;

        /// <summary>
        /// The current status on processing the spectra.
        /// </summary>
        private string progressStatus;

        /// <summary>
        /// A value indicating whether spectra are currently being processed.
        /// </summary>
        private readonly ObservableAsPropertyHelper<bool> isRunning;

        /// <summary>
        /// A value that indicates whether the user should be able to edit file paths.
        /// </summary>
        private readonly ObservableAsPropertyHelper<bool> areFileEditorsEnabled;

        /// <summary>
        /// A value that indicates whether or not the progress bar should be visible.
        /// </summary>
        private readonly ObservableAsPropertyHelper<Visibility> showProgressBar;

        /// <summary>
        /// The progress reporter used to report the percentage and status of the processing.
        /// </summary>
        private readonly IProgress<ProgressData> progressReporter;

        /// <summary>
        /// Initializes a new instance of the <see cref="MainWindowViewModel" /> class. 
        /// </summary>
        public MainWindowViewModel()
        {
            this.cancellationToken = new CancellationTokenSource();

            // Initialize child view models.
            this.RawFileSelector = new FileSelectorViewModel(
                                    "Raw File (*.raw, *.mzml)",
                                    false, 
                                    ".mzML", MassSpecDataReaderFactory.MassSpecDataTypeFilterString);

            this.MzIdFileSelector = new FileSelectorViewModel(
                                    "Identification File (*.mzid)",
                                    false, 
                                    ".mzid", @"Supported Files|*.mzId;*.mzId.gz|MzId Files (*.mzId[.gz])|*.mzId;*.mzId.gz");

            this.OutputFileSelector = new FileSelectorViewModel(
                                    "Output File (*.tsv)",
                                    true,
                                    ".tsv", @"TSV Files (*.tsv)|*.tsv");

            this.IonTypeFactoryViewModel = new IonTypeFactoryViewModel { MaxChargeState = 10 };

            // Set up progress reporter.
            this.progressReporter = new Progress<ProgressData>(
                pd =>
                    {
                        this.ProgressPercent = pd.Percent;
                        this.ProgressStatus = pd.Status;
                    });

            // Set up RunCommand
            // RunCommand can run when a raw file, mzId file, and output file are all selected and valid.
            var canRun = this.WhenAnyValue(
                                x => x.RawFileSelector.IsValid,
                                x => x.MzIdFileSelector.IsValid,
                                x => x.OutputFileSelector.FilePath)
                             .Select(x => x.Item1 && x.Item2 && !string.IsNullOrWhiteSpace(x.Item3));
            this.RunCommand = ReactiveCommand.CreateFromTask(async () => await this.RunImpl(), canRun);

            // Set up CancelCommand
            // Cancel command can run when we are running and not currently cancelling.
            var canCancel = this.WhenAnyValue(x => x.IsRunning, x => x.ProgressStatus)
                                .Select(x => x.Item1 && x.Item2 != "Cancelling...");
            this.CancelCommand = ReactiveCommand.Create(this.CancelImpl, canCancel);

            // When progress is zero, turn off the IsRunning flag.
            this.WhenAnyValue(x => x.ProgressPercent).Select(x => x > 0).ToProperty(this, x => x.IsRunning, out this.isRunning);

            // When processing is running, the file path text boxes should be disabled.
            this.WhenAnyValue(x => x.IsRunning)
                .Select(isRunning => !isRunning)
                .ToProperty(this, x => x.AreFileEditorsEnabled, out this.areFileEditorsEnabled);

            // When processing isn't running, hide progress bar
            this.WhenAnyValue(x => x.IsRunning)
                .Select(isRunning => isRunning ? Visibility.Visible : Visibility.Collapsed)
                .ToProperty(this, x => x.ShowProgressBar, out this.showProgressBar);

            // When mzId file is selected and output file path has not been selected yet, automatically set the output file path
            // based on the mzId file path.
            this.WhenAnyValue(x => x.MzIdFileSelector.IsValid)
                .Where(x => x)
                .Where(_ => !this.OutputFileSelector.IsValid)
                .Select(_ => this.GetOutputPathFromIdFile(this.MzIdFileSelector.FilePath))
                .Subscribe(fp => this.OutputFileSelector.FilePath = fp);
        }

        /// <summary>
        /// Gets the view model for selecting and editing the raw file path.
        /// </summary>
        public FileSelectorViewModel RawFileSelector { get; }

        /// <summary>
        /// Gets the view model for selecting and editing the mzId file path.
        /// </summary>
        public FileSelectorViewModel MzIdFileSelector { get; }

        /// <summary>
        /// Gets the view model for selecting and editing the output file path.
        /// </summary>
        public FileSelectorViewModel OutputFileSelector { get; }

        /// <summary>
        /// Gets the view model for selecting ion types and neutral losses.
        /// </summary>
        public IonTypeFactoryViewModel IonTypeFactoryViewModel { get; }

        /// <summary>
        /// Gets a command that runs the spectrum processing.
        /// </summary>
        public ReactiveCommand<Unit, Unit> RunCommand { get; }

        /// <summary>
        /// Gets a command that cancels the spectrum processing.
        /// </summary>
        public ReactiveCommand<Unit, Unit> CancelCommand { get; }

        /// <summary>
        /// Gets a value indicating whether spectra are currently being processed.
        /// </summary>
        public bool IsRunning => this.isRunning?.Value ?? false;

        /// <summary>
        /// Gets a value that indicates whether the user should be able to edit file paths.
        /// </summary>
        public bool AreFileEditorsEnabled => this.areFileEditorsEnabled?.Value ?? true;

        /// <summary>
        /// Gets a value that indicates whether or not the progress bar should be visible.
        /// </summary>
        public Visibility ShowProgressBar => this.showProgressBar?.Value ?? Visibility.Collapsed; 

        /// <summary>
        /// Gets the percentage complete on processing the spectra.
        /// </summary>
        public double ProgressPercent
        {
            get { return this.progressPercent; }
            private set { this.RaiseAndSetIfChanged(ref this.progressPercent, value); }
        }

        /// <summary>
        /// Gets the current status on processing the spectra.
        /// </summary>
        public string ProgressStatus
        {
            get { return this.progressStatus; }
            private set { this.RaiseAndSetIfChanged(ref this.progressStatus, value); }
        }

        /// <summary>
        /// Runs the sequence coverage calculation.
        /// </summary>
        private async Task RunImpl()
        {
            var processor = new ResultProcessor(this.IonTypeFactoryViewModel.IonTypeFactory);

            var results = await processor.ProcessAsync(
                this.RawFileSelector.FilePath, 
                this.MzIdFileSelector.FilePath, 
                this.cancellationToken.Token, 
                this.progressReporter);

            if (results.Count > 0)
            {
                await ProcessedResult.WriteToFile(results, this.OutputFileSelector.FilePath);
            }

            this.progressReporter.Report(new ProgressData());
        }

        /// <summary>
        /// Cancels the spectrum processing.
        /// </summary>
        private void CancelImpl()
        {
            // Show cancel status message.
            var progressData = new ProgressData(this.progressReporter);
            progressData.Report(this.ProgressPercent, "Cancelling...");

            this.cancellationToken.Cancel();

            this.cancellationToken.Dispose();
            this.cancellationToken = new CancellationTokenSource();
        }

        /// <summary>
        /// Determine the default output file path from the selected identification file path.
        /// </summary>
        /// <param name="idFilePath">The identification file path to determine output file path.</param>
        /// <returns>The default output file path.</returns>
        private string GetOutputPathFromIdFile(string idFilePath)
        {
            var directory = Path.GetDirectoryName(idFilePath);
            if (directory == null)
            {
                return null;
            }

            var fileName = Path.GetFileNameWithoutExtension(idFilePath);
            if (fileName.EndsWith(".mzid")) // File was gzipped (.mzid.gz)
            {
                fileName = Path.GetFileNameWithoutExtension(fileName);
            }

            return Path.Combine(directory, $"{fileName}.tsv");
        }
    }
}
