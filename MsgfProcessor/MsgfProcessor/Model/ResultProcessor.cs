namespace MsgfProcessor.Model
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using InformedProteomics.Backend.Data.Composition;
    using InformedProteomics.Backend.Data.Sequence;
    using InformedProteomics.Backend.Data.Spectrometry;
    using InformedProteomics.Backend.MassSpecData;
    using InformedProteomics.Backend.Results;
    using InformedProteomics.Backend.Utils;

    using PSI_Interface.IdentData;

    public class ResultProcessor
    {
        /// <summary>
        /// The peak tolerance.
        /// </summary>
        private readonly Tolerance tolerance;

        /// <summary>
        /// For generating fly weight ion types.
        /// </summary>
        private readonly IonTypeFactory ionTypeFactory;

        /// <summary>
        /// Initializes a new instance of the <see cref="ResultProcessor" /> class. 
        /// </summary>
        /// <param name="tolerance">The peak tolerance.</param>
        /// <param name="maxCharge">Maximum charge possible in dataset. For preloading ion types.</param>
        public ResultProcessor(Tolerance tolerance = null, int maxCharge = 10)
        {
            this.tolerance = tolerance ?? new Tolerance(10, ToleranceUnit.Ppm);
            this.ionTypeFactory = new IonTypeFactory(maxCharge);
        }

        /// <summary>
        /// Process spectra and identifications asynchronously.
        /// </summary>
        /// <param name="rawFilePath">Full file path to raw file.</param>
        /// <param name="idFilePath">Full file path to identification file.</param>
        /// <param name="cancellationToken">For notification of cancellation.</param>
        /// <param name="progress">Progress reporter.</param>
        /// <returns>Awaitable task which returns a list of processed IDs on completion.</returns>
        public Task<List<ProcessedResult>> ProcessAsync(string rawFilePath, string idFilePath, CancellationToken cancellationToken, IProgress<ProgressData> progress = null)
        {
            return Task.Run(() => this.Process(rawFilePath, idFilePath, cancellationToken, progress));
        }

        /// <summary>
        /// Process spectra and identifications.
        /// </summary>
        /// <param name="rawFilePath">Full file path to raw file.</param>
        /// <param name="idFilePath">Full file path to identification file.</param>
        /// <param name="cancellationToken">For notification of cancellation.</param>
        /// <param name="progress">Progress reporter.</param>
        /// <returns>List of processed IDs.</returns>
        public List<ProcessedResult> Process(string rawFilePath, string idFilePath, CancellationToken cancellationToken, IProgress<ProgressData> progress = null)
        {
            // Set up progress reporter
            progress = progress ?? new Progress<ProgressData>();

            var progressData = new ProgressData(progress);

            // Show initial loading message
            progressData.Report(0.1, "Loading...");

            // Read mzid file
            var mzidReader = new SimpleMZIdentMLReader();
            var results = mzidReader.Read(idFilePath, cancellationToken);

            // Group IDs into a hash by scan number
            var idMap = results.Identifications.GroupBy(id => id.ScanNum).ToDictionary(scan => scan.Key, ids => ids);

            var processedResults = new List<ProcessedResult>();

            // Load raw file
            using (var lcms = MassSpecDataReaderFactory.GetMassSpecDataReader(rawFilePath))
            {
                int count = 0;
                foreach (var spectrum in lcms.ReadAllSpectra())
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return new List<ProcessedResult>();
                    }

                    progressData.Report(count++, lcms.NumSpectra, $"Scan: {spectrum?.ScanNum ?? '-'}");
                    var productSpectrum = spectrum as ProductSpectrum;
                    if (productSpectrum == null || !idMap.ContainsKey(spectrum.ScanNum))
                    {
                        continue;
                    }

                    var specResults = idMap[spectrum.ScanNum];

                    foreach (var specResult in specResults)
                    {
                        var sequence = specResult.Peptide.GetIpSequence();  // Parse text sequence

                        // Calculate sequence coverage.
                        var coverage = this.CalculateSequenceCoverage(productSpectrum, sequence, specResult.Charge);

                        processedResults.Add(new ProcessedResult
                        {
                            ScanNum = spectrum.ScanNum,
                            Sequence = sequence,
                            Charge = specResult.Charge,
                            PrecursorMz = specResult.CalMz,
                            DeNovoScore = specResult.DeNovoScore,
                            SpecEValue = specResult.SpecEv,
                            EValue = specResult.EValue,
                            QValue = specResult.QValue,
                            PepQValue = specResult.PepQValue,
                            FragMethod = productSpectrum.ActivationMethod,
                            IsotopeError = specResult.IsoError,
                            SequenceCoverage = Math.Round(coverage),
                        });
                    }
                }
            }

            // Sort spectra by SpecEValue
            return processedResults.OrderBy(pr => pr.SpecEValue).ToList();
        }

        /// <summary>
        /// Calculate the sequence coverage for the given sequence and spectrum.
        /// </summary>
        /// <param name="spectrum">The spectrum to calculate sequence coverage within.</param>
        /// <param name="sequence">The sequence to calculate coverage for.</param>
        /// <param name="charge">The parent charge state of this spectrum.</param>
        /// <returns>The sequence coverage.</returns>
        private double CalculateSequenceCoverage(ProductSpectrum spectrum, Sequence sequence, int charge)
        {
            var ions = this.ionTypeFactory.GetAllKnownIonTypes().ToList();

            int found = 0;
            for (int clv = 1; clv < sequence.Count; clv++)
            {
                bool haveFoundClv = false;
                var nTermSeq = sequence.GetRange(0, clv).Aggregate(Composition.Zero, (l, r) => l + r.Composition);
                var cTermSeq = sequence.GetRange(clv, sequence.Count - clv).Aggregate(Composition.Zero, (l, r) => l + r.Composition);

                foreach (var ionType in ions)
                {
                    if (haveFoundClv)
                    {
                        break;
                    }

                    if (ionType.Charge >= charge)
                    {
                        continue;
                    }

                    var comp = ionType.IsPrefixIon ? nTermSeq : cTermSeq;
                    var ion = ionType.GetIon(comp);
                    if (spectrum.GetCorrScore(ion, this.tolerance) > 0.7)
                    {
                        found++;
                        haveFoundClv = true;
                    }
                }
            }

            return (100.0 * found) / (sequence.Count - 1);
        }
    }
}
