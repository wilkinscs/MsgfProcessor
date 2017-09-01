namespace MsgfProcessor.ViewModels
{
    using System.Collections.Generic;
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
            var selectedIonTypes = new HashSet<BaseIonType> { BaseIonType.A, BaseIonType.B, BaseIonType.C, BaseIonType.X, BaseIonType.Y, BaseIonType.Z };
            foreach (var ionTypeVm in this.IonTypes.Where(ionType => selectedIonTypes.Contains(ionType.Item)))
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
