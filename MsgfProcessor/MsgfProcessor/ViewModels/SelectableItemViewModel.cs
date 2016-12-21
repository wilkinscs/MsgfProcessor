namespace MsgfProcessor.ViewModels
{
    using ReactiveUI;

    public class SelectableItemViewModel<T> : ReactiveObject
    {
        /// <summary>
        /// A value that indicates whether this item has been selected.
        /// </summary>
        private bool isSelected;

        /// <summary>
        /// Initializes a new instance of the <see cref="SelectableItemViewModel{T}" /> class.
        /// </summary>
        /// <param name="item">The selectable item.</param>
        public SelectableItemViewModel(T item)
        {
            this.Item = item;
        }

        /// <summary>
        /// Gets the selectable item.
        /// </summary>
        public T Item { get; }

        /// <summary>
        /// Gets or sets a value that indicates whether this item has been selected.
        /// </summary>
        public bool IsSelected
        {
            get { return this.isSelected; }
            set { this.RaiseAndSetIfChanged(ref this.isSelected, value); }
        }
    }
}
