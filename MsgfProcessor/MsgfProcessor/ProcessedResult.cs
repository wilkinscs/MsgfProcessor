using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using System.IO;
using InformedProteomics.Backend.Data.Sequence;
using InformedProteomics.Backend.Data.Spectrometry;

namespace MsgfProcessor
{
    public class ProcessedResult
    {
        /// <summary>
        /// Gets or sets the scan number.
        /// </summary>
        public int ScanNum { get; set; }

        /// <summary>
        /// Gets or sets the parsed sequence.
        /// </summary>
        public Sequence Sequence { get; set; }

        /// <summary>
        /// Gets or sets the protein accession and description.
        /// </summary>
        public string Protein { get; set; }

        /// <summary>
        /// Gets or sets the precursor charge state.
        /// </summary>
        public int Charge { get; set; }

        /// <summary>
        /// Gets or sets the MS/MS fragmentation method used.
        /// </summary>
        public ActivationMethod FragMethod { get; set; }

        /// <summary>
        /// Gets or sets the precursor mass-to-charge.
        /// </summary>
        public double PrecursorMz { get; set; }

        /// <summary>
        /// Gets or sets the difference in score between this result
        /// and the sequence found through denovo sequencing.
        /// </summary>
        public double DeNovoScore { get; set; }

        /// <summary>
        /// Gets or sets the MSGF+ score.
        /// </summary>
        public double MsgfScore { get; set; }

        /// <summary>
        /// Gets or sets the spectra E value.
        /// </summary>
        public double SpecEValue { get; set; }

        /// <summary>
        /// Gets or sets the e value.
        /// </summary>
        public double EValue { get; set; }

        /// <summary>
        /// Gets or sets the FDR.
        /// </summary>
        public double QValue { get; set; }

        /// <summary>
        /// Gets or sets the peptide-level FDR.
        /// </summary>
        public double PepQValue { get; set; }

        /// <summary>
        /// Gets or sets the number of isotopes difference for precursor ion.
        /// </summary>
        public double IsotopeError { get; set; }

        /// <summary>
        /// Gets or sets the sequence coverage calculated for this sequence-spectrum match.
        /// </summary>
        public double SequenceCoverage { get; set; }
    
        /// <summary>
        /// Asynchronously write a set of results to a file in tab-separated value format.
        /// </summary>
        /// <param name="processedResults">The results to write.</param>
        /// <param name="filePath">The path of the file to write the results to.</param>
        /// <returns>Awaitable asynchronous task.</returns>
        public static async Task WriteToFile(IEnumerable<ProcessedResult> processedResults, string filePath)
        {
            using (var streamWriter = new StreamWriter(filePath))
            {
                await streamWriter.WriteLineAsync(
                      "ResultID\tScan\tFragMethod\tCharge\tPrecursorMZ\tPeptide\tProtein\tDeNovoScore\tMSGFScore\tSpecEValue\tEValue\tQValue\tPepQValue\tIsotopeError\tSequenceCoverage");

                int count = 1;
                foreach (var result in processedResults)
                {
                    if (result.QValue > 0.01) continue;

                    var line = string.Format(
                        "{0}\t{1}\t{2}\t{3}\t{4}\t{5}\t{6}\t{7}\t{8}\t{9}\t{10}\t{11}\t{12}\t{13}\t{14}",
                        count++,
                        result.ScanNum,
                        result.FragMethod,
                        result.Charge,
                        result.PrecursorMz,
                        WriteSequence(result.Sequence),
                        result.Protein,
                        result.DeNovoScore,
                        result.MsgfScore,
                        result.SpecEValue,
                        result.EValue,
                        result.QValue,
                        result.PepQValue,
                        result.IsotopeError,
                        result.SequenceCoverage);
                    await streamWriter.WriteLineAsync(line);
                }
            }
        }

        /// <summary>
        /// Convert an informed proteomics <see cref="Sequence" /> into a human readable format.
        /// </summary>
        /// <param name="sequence">The sequence to convert.</param>
        /// <returns>String containing formatted sequence.</returns>
        public static string WriteSequence(Sequence sequence)
        {
            string strSequence = string.Empty;
            foreach (var aa in sequence)
            {
                strSequence += aa.Residue;
                var modAa = aa as ModifiedAminoAcid;
                if (modAa != null)
                {
                    var modMass = Math.Round(modAa.Modification.Mass, 3);
                    var sign = modMass > 0 ? "+" : modMass < 0 ? "-" : string.Empty;
                    strSequence += $"{sign}{modMass}";
                }
            }

            return strSequence;
        }
    }
}
