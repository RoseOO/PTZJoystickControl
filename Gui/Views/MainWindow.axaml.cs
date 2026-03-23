using Avalonia.Controls;
using PtzJoystickControl.Gui.ViewModels;
using System.ComponentModel;

namespace PtzJoystickControl.Gui.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(object? sender, System.EventArgs e)
        {
            if (DataContext is MainWindowViewModel vm)
            {
                vm.PropertyChanged += OnViewModelPropertyChanged;
                UpdateColumnWidths(vm);
            }
        }

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender is MainWindowViewModel vm &&
                e.PropertyName is nameof(MainWindowViewModel.ShowInputPane)
                    or nameof(MainWindowViewModel.ShowCamerasPane)
                    or nameof(MainWindowViewModel.ShowControlsPane))
            {
                UpdateColumnWidths(vm);
            }
        }

        private void UpdateColumnWidths(MainWindowViewModel vm)
        {
            var mainGrid = this.FindControl<Grid>("MainGrid");
            if (mainGrid == null || mainGrid.ColumnDefinitions.Count < 5) return;

            var inputCol = mainGrid.ColumnDefinitions[0];
            var splitter1Col = mainGrid.ColumnDefinitions[1];
            var camerasCol = mainGrid.ColumnDefinitions[2];
            var splitter2Col = mainGrid.ColumnDefinitions[3];
            var controlsCol = mainGrid.ColumnDefinitions[4];

            inputCol.Width = vm.ShowInputPane ? new GridLength(4, GridUnitType.Star) : new GridLength(0);
            inputCol.MinWidth = vm.ShowInputPane ? 200 : 0;
            splitter1Col.Width = vm.ShowInputPane ? GridLength.Auto : new GridLength(0);

            camerasCol.Width = vm.ShowCamerasPane ? new GridLength(3, GridUnitType.Star) : new GridLength(0);
            camerasCol.MinWidth = vm.ShowCamerasPane ? 200 : 0;
            splitter2Col.Width = vm.ShowCamerasPane ? GridLength.Auto : new GridLength(0);

            controlsCol.Width = vm.ShowControlsPane ? new GridLength(3, GridUnitType.Star) : new GridLength(0);
            controlsCol.MinWidth = vm.ShowControlsPane ? 380 : 0;
        }
    }
}
