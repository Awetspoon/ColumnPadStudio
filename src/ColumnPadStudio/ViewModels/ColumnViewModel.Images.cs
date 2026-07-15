using System.Collections.Specialized;
using System.ComponentModel;

namespace ColumnPadStudio.ViewModels;

public sealed partial class ColumnViewModel
{
    private readonly HashSet<ColumnImageViewModel> _observedImages = [];

    public void ClearImages()
    {
        if (Images.Count > 0)
            Images.Clear();
    }

    public void SelectImage(ColumnImageViewModel image)
    {
        ArgumentNullException.ThrowIfNull(image);

        foreach (var candidate in Images)
            candidate.IsSelected = ReferenceEquals(candidate, image);
    }

    public void DeselectImages()
    {
        foreach (var image in Images)
            image.IsSelected = false;
    }

    private void Images_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        SynchronizeImageSubscriptions();
        OnPropertyChanged(nameof(Images));
    }

    private void SynchronizeImageSubscriptions()
    {
        foreach (var removedImage in _observedImages.Where(image => !Images.Contains(image)).ToList())
        {
            removedImage.PropertyChanged -= Image_PropertyChanged;
            _observedImages.Remove(removedImage);
        }

        foreach (var image in Images)
        {
            if (!_observedImages.Add(image))
                continue;

            image.PropertyChanged += Image_PropertyChanged;
        }
    }

    private void Image_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(Images));
    }
}
