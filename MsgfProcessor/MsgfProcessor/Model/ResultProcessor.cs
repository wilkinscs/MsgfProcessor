namespace MsgfProcessor.Model
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;
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
        /// <param name="ionTypeFactory">Factory for generating correct ions and neutral losses.</param>
        /// <param name="tolerance">The peak tolerance.</param>
        public ResultProcessor(IonTypeFactory ionTypeFactory, Tolerance tolerance = null)
        {
            this.tolerance = tolerance ?? new Tolerance(10, ToleranceUnit.Ppm);
            this.ionTypeFactory = ionTypeFactory;
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
            return Task.Run(() => this.Process(rawFilePath, idFilePath, cancellationToken, progress), cancellationToken);
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
            var identifications = mzidReader.Read(idFilePath, cancellationToken);

            // Check to make sure raw and MZID file match.
            var rawFileName = Path.GetFileNameWithoutExtension(rawFilePath);

            var spectrumFileFromId = Path.GetFileNameWithoutExtension(identifications.SpectrumFile);
            var dtaIndex = spectrumFileFromId.LastIndexOf("_dta");
            if (dtaIndex >= 0)
            {
                spectrumFileFromId = spectrumFileFromId.Substring(0, dtaIndex);
            }

            if (rawFileName != spectrumFileFromId)
            {
                throw new ArgumentException($"Mismatch between spectrum file ({rawFileName}) and id file ({spectrumFileFromId}).");
            }

            // Group IDs into a hash by scan number
            var idMap = identifications.Identifications.GroupBy(id => id.ScanNum).ToDictionary(scan => scan.Key, ids => ids);

            var processedResults = new ConcurrentBag<ProcessedResult>();

            // Load raw file
            using (var lcms = MassSpecDataReaderFactory.GetMassSpecDataReader(rawFilePath))
            {
                int count = 0;
                Parallel.ForEach(
                    lcms.ReadAllSpectra(),
                    spectrum =>
                        {
                            if (cancellationToken.IsCancellationRequested)
                            {   // Cancel if necessary
                                return;
                            }

                            // Report completion percentage and current scan number
                            if (count % (int)Math.Max(0.01 * lcms.NumSpectra, 1) == 0)
                            {
                                progressData.Report(count, lcms.NumSpectra, $"{Math.Round(100.0*count / lcms.NumSpectra)}%");
                            }

                            Interlocked.Increment(ref count);

                            // Skip spectrum if it isn't MS2
                            var productSpectrum = spectrum as ProductSpectrum;
                            if (productSpectrum == null || !idMap.ContainsKey(spectrum.ScanNum))
                            {
                                return;
                            }

                            var specResults = idMap[spectrum.ScanNum];

                            var results = from specResult in specResults
                                          let sequence = specResult.Peptide.GetIpSequence()
                                          let coverage = this.CalculateSequenceCoverage(productSpectrum, sequence, specResult.Charge)
                                          select new ProcessedResult
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
                                          };

                            foreach (var result in results) processedResults.Add(result);
                        });
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
            var ionTypes = this.ionTypeFactory.GetAllKnownIonTypes().ToList();

            int found = 0;
            for (int clv = 1; clv < sequence.Count; clv++)
            {
                bool haveFoundClv = false;
                var nTermSeq = sequence.GetRange(0, clv);
                var nTermComp = nTermSeq.Aggregate(Composition.Zero, (l, r) => l + r.Composition);
                var cTermSeq = sequence.GetRange(clv, sequence.Count - clv);
                var cTermComp = cTermSeq.Aggregate(Composition.Zero, (l, r) => l + r.Composition);

                foreach (var ionType in ionTypes)
                {
                    if (haveFoundClv)
                    {
                        break;
                    }

                    if (ionType.Charge >= charge)
                    {
                        continue;
                    }

                    var comp = ionType.IsPrefixIon ? nTermComp : cTermComp;
                    var aa = ionType.IsPrefixIon ? nTermSeq[nTermSeq.Count - 1] : cTermSeq[0];
                    var ions = ionType.GetPossibleIons(comp, aa);
                    foreach (var ion in ions)
                    {
                        if (spectrum.GetCorrScore(ion, this.tolerance) > 0.7)
                        {
                            found++;
                            haveFoundClv = true;
                            break;
                        }
                    }
                }
            }

            return (100.0 * found) / (sequence.Count - 1);
        }
    }
}
