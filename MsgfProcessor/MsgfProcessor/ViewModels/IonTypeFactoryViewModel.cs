namespace MsgfProcessor.ViewModels
{
    using System.Linq;

    using InformedProteomics.Backend.Data.Spectrometry;

    using ReactiveUI;

    public class IonTypeFactoryViewModel : ReactiveObject
    {
        /// <summary>
        /// The maximum charge state to create ion objects for.
        /// </summary>
        private int maxChargeState;

        /// <summary>
        /// Initializes a new instance of the <see cref="IonTypeFactoryViewModel" />. 
        /// </summary>
        public IonTypeFactoryViewModel()
        {
            this.IonTypes =
                new ReactiveList<SelectableItemViewModel<BaseIonType>>(
                    BaseIonType.AllBaseIonTypes.Select(bit => new SelectableItemViewModel<BaseIonType>(bit)));

            this.NeutralLosses =
                new ReactiveList<SelectableItemViewModel<NeutralLoss>>(
                    NeutralLoss.CommonNeutralLosses.Select(nl => new SelectableItemViewModel<NeutralLoss>(nl)));

            // Select default ion types
            var defaultIonTypes = this.IonTypes.Where(ionType => ionType.Item.Symbol == "a" || 
                                                                 ionType.Item.Symbol == "b" ||
                                                                 ionType.Item.Symbol == "c" ||
                                                                 ionType.Item.Symbol == "x" ||
                                                                 ionType.Item.Symbol == "y" ||
                                                                 ionType.Item.Symbol == "z");
            foreach (var ionTypeVm in defaultIonTypes)
            {
                ionTypeVm.IsSelected = true;
            }

            // Select default neutral losses
            var defaultNeutralLosses = this.NeutralLosses.Where(nl => nl.Item.Symbol == "NoLoss");
            foreach (var neutralLoss in defaultNeutralLosses)
            {
                neutralLoss.IsSelected = true;
            }
        }

        /// <summary>
        /// Gets the list of selectable base ion types.
        /// </summary>
        public ReactiveList<SelectableItemViewModel<BaseIonType>> IonTypes { get; private set; }

        /// <summary>
        /// Gets the list of selectable neutral losses.
        /// </summary>
        public ReactiveList<SelectableItemViewModel<NeutralLoss>> NeutralLosses { get; private set; }

        /// <summary>
        /// Gets or sets the maximum charge state to create ion objects for.
        /// </summary>
        public int MaxChargeState
        {
            get { return this.maxChargeState; }
            set { this.RaiseAndSetIfChanged(ref this.maxChargeState, value); }
        }

        /// <summary>
        /// Gets the <see cref="IonTypeFactory" /> based on the selected settings.
        /// </summary>
        public IonTypeFactory IonTypeFactory
        {
            get
            {
                return new IonTypeFactory(
                                this.IonTypes.Where(bit => bit.IsSelected).Select(bit => bit.Item),
                                this.NeutralLosses.Where(nl => nl.IsSelected).Select(nl => nl.Item),
                                this.MaxChargeState);
            }
        }
    }
}
