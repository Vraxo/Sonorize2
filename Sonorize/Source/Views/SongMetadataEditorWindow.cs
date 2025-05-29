using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging; // Required for BitmapInterpolationMode
using Sonorize.Models; // For ThemeColors
using Sonorize.ViewModels; // For SongMetadataEditorViewModel

namespace Sonorize.Views;

public class SongMetadataEditorWindow : Window
{
    private readonly ThemeColors _theme;

    public SongMetadataEditorWindow(ThemeColors theme)
    {
        _theme = theme;
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        Title = "Edit Song Metadata";
        Width = 450; MinWidth = 400;
        Height = 430; MinHeight = 380; // Increased height for thumbnail
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        CanResize = true;
        Background = _theme.B_SlightlyLighterBackground;
        Foreground = _theme.B_TextColor;

        this.DataContextChanged += (sender, args) =>
        {
            if (DataContext is SongMetadataEditorViewModel vm)
            {
                vm.CloseWindowAction = this.Close;
            }
        };

        var mainPanel = new StackPanel { Margin = new Thickness(15), Spacing = 10 };

        // Thumbnail Display and Change Button
        var thumbnailImage = new Image
        {
            Width = 100,
            Height = 100,
            Stretch = Stretch.UniformToFill
            // Removed Background property from Image
        };
        thumbnailImage.Bind(Image.SourceProperty, new Binding("CurrentDisplayThumbnail"));
        RenderOptions.SetBitmapInterpolationMode(thumbnailImage, BitmapInterpolationMode.HighQuality);

        var thumbnailBorder = new Border // Wrap Image in a Border
        {
            Width = 100,
            Height = 100,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 5),
            Background = _theme.B_ControlBackgroundColor, // Set Background on the Border
            Child = thumbnailImage
        };
        mainPanel.Children.Add(thumbnailBorder);


        var changeCoverButton = new Button
        {
            Content = "Change Cover",
            HorizontalAlignment = HorizontalAlignment.Center,
            Background = _theme.B_ControlBackgroundColor,
            Foreground = _theme.B_TextColor,
            Padding = new Thickness(10, 5),
            Margin = new Thickness(0, 0, 0, 10)
        };
        changeCoverButton.Bind(Button.CommandProperty, new Binding("ChangeThumbnailCommand"));
        // Pass the window itself as the command parameter for StorageProvider access
        changeCoverButton.CommandParameter = this;
        mainPanel.Children.Add(changeCoverButton);


        // Title
        mainPanel.Children.Add(new TextBlock { Text = "Title:", Foreground = _theme.B_TextColor });
        var titleBox = new TextBox
        {
            Background = _theme.B_ControlBackgroundColor,
            Foreground = _theme.B_TextColor,
            BorderBrush = _theme.B_SecondaryTextColor,
            Watermark = "Song Title"
        };
        titleBox.Bind(TextBox.TextProperty, new Binding("Title", BindingMode.TwoWay));
        mainPanel.Children.Add(titleBox);

        // Artist
        mainPanel.Children.Add(new TextBlock { Text = "Artist:", Foreground = _theme.B_TextColor });
        var artistBox = new TextBox
        {
            Background = _theme.B_ControlBackgroundColor,
            Foreground = _theme.B_TextColor,
            BorderBrush = _theme.B_SecondaryTextColor,
            Watermark = "Artist Name"
        };
        artistBox.Bind(TextBox.TextProperty, new Binding("Artist", BindingMode.TwoWay));
        mainPanel.Children.Add(artistBox);

        // Album
        mainPanel.Children.Add(new TextBlock { Text = "Album:", Foreground = _theme.B_TextColor });
        var albumBox = new TextBox
        {
            Background = _theme.B_ControlBackgroundColor,
            Foreground = _theme.B_TextColor,
            BorderBrush = _theme.B_SecondaryTextColor,
            Watermark = "Album Name"
        };
        albumBox.Bind(TextBox.TextProperty, new Binding("Album", BindingMode.TwoWay));
        mainPanel.Children.Add(albumBox);

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
        mainPanel.Children.Add(buttonPanel);

        Content = mainPanel;
    }
}