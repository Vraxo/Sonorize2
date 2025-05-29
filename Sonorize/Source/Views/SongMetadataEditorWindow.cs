using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using Sonorize.Models; // For ThemeColors
using Sonorize.ViewModels;
using System; // Required for EventArgs

namespace Sonorize.Views;

public class SongMetadataEditorWindow : Window
{
    private readonly ThemeColors _theme;
    private SongMetadataEditorViewModel? _viewModel;

    public SongMetadataEditorWindow(ThemeColors theme)
    {
        _theme = theme;
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        Title = "Edit Song Metadata"; // Will be updated by ViewModel
        Width = 450; MinWidth = 400;
        Height = 420; MinHeight = 380;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = _theme.B_SlightlyLighterBackground;
        CanResize = true;
        Icon = null; // Or set an icon

        var mainGrid = new Grid
        {
            Margin = new Thickness(15),
            RowDefinitions = new RowDefinitions("Auto,*,Auto") // Title, Form, Buttons
        };

        // Title will be bound from ViewModel
        var titleLabel = new TextBlock
        {
            FontSize = 18,
            FontWeight = FontWeight.SemiBold,
            Foreground = _theme.B_TextColor,
            Margin = new Thickness(0, 0, 0, 15),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        titleLabel.Bind(TextBlock.TextProperty, new Binding("WindowTitle"));
        Grid.SetRow(titleLabel, 0);
        mainGrid.Children.Add(titleLabel);

        // Metadata Form
        var formGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*")
            // Removed RowSpacing = 8, Grid doesn't have this property.
            // Spacing is managed by RowDefinition heights or control Margins.
        };

        formGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto)); // Title
        formGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto)); // Artist
        formGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto)); // Album
        formGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto)); // Genre
        formGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto)); // Track #
        formGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto)); // Year

        // Title
        formGrid.Children.Add(CreateLabel("Title:", 0));
        formGrid.Children.Add(CreateTextBox("EditableTitle", 0, 1));

        // Artist
        formGrid.Children.Add(CreateLabel("Artist:", 1));
        formGrid.Children.Add(CreateTextBox("EditableArtist", 1, 1));

        // Album
        formGrid.Children.Add(CreateLabel("Album:", 2));
        formGrid.Children.Add(CreateTextBox("EditableAlbum", 2, 1));

        // Genre
        formGrid.Children.Add(CreateLabel("Genre:", 3));
        formGrid.Children.Add(CreateTextBox("EditableGenre", 3, 1));

        // Track Number
        formGrid.Children.Add(CreateLabel("Track #:", 4));
        formGrid.Children.Add(CreateTextBox("EditableTrackNumber", 4, 1, isNumeric: true));

        // Year
        formGrid.Children.Add(CreateLabel("Year:", 5));
        formGrid.Children.Add(CreateTextBox("EditableYear", 5, 1, isNumeric: true));

        Grid.SetRow(formGrid, 1);
        mainGrid.Children.Add(formGrid);

        // Buttons
        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 10,
            Margin = new Thickness(0, 15, 0, 0)
        };

        var saveButton = new Button
        {
            Content = "Save",
            Background = _theme.B_AccentColor,
            Foreground = _theme.B_AccentForeground,
            Padding = new Thickness(15, 8),
            CornerRadius = new CornerRadius(3)
        };
        saveButton.Bind(Button.CommandProperty, new Binding("SaveCommand"));

        var cancelButton = new Button
        {
            Content = "Cancel",
            Background = _theme.B_ControlBackgroundColor,
            Foreground = _theme.B_TextColor,
            Padding = new Thickness(15, 8),
            CornerRadius = new CornerRadius(3)
        };
        cancelButton.Bind(Button.CommandProperty, new Binding("CancelCommand"));

        buttonPanel.Children.Add(saveButton);
        buttonPanel.Children.Add(cancelButton);
        Grid.SetRow(buttonPanel, 2);
        mainGrid.Children.Add(buttonPanel);

        Content = mainGrid;

        DataContextChanged += OnDataContextChanged;
    }

    private TextBlock CreateLabel(string text, int row)
    {
        var label = new TextBlock
        {
            Text = text,
            Foreground = _theme.B_TextColor,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 0, 10, 8) // Added bottom margin for spacing
        };
        Grid.SetRow(label, row);
        Grid.SetColumn(label, 0);
        return label;
    }

    private TextBox CreateTextBox(string bindingPath, int row, int column, bool isNumeric = false)
    {
        var textBox = new TextBox
        {
            Background = _theme.B_ControlBackgroundColor,
            Foreground = _theme.B_TextColor,
            CaretBrush = _theme.B_TextColor,
            BorderBrush = _theme.B_SecondaryTextColor,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(5),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = new Thickness(0, 0, 0, 8) // Added bottom margin for spacing
        };
        textBox.Bind(TextBox.TextProperty, new Binding(bindingPath, BindingMode.TwoWay));
        Grid.SetRow(textBox, row);
        Grid.SetColumn(textBox, column);
        return textBox;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_viewModel != null)
        {
            _viewModel.CloseAction = null;
            _viewModel.UnsubscribeFromPlaybackServiceEvents();
        }

        _viewModel = DataContext as SongMetadataEditorViewModel;

        if (_viewModel != null)
        {
            _viewModel.CloseAction = success =>
            {
                // For ShowDialog<T>, call Close(T result) to return the result
                this.Close(success);
            };
            _viewModel.SubscribeToPlaybackServiceEvents(); // Subscribe when VM is ready
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        if (_viewModel != null)
        {
            _viewModel.UnsubscribeFromPlaybackServiceEvents(); // Ensure unsubscription
            _viewModel.CloseAction = null;
        }
        base.OnClosed(e);
    }
}