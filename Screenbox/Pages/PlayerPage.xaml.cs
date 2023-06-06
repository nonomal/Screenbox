﻿#nullable enable

using CommunityToolkit.Diagnostics;
using CommunityToolkit.Mvvm.DependencyInjection;
using Microsoft.Toolkit.Uwp.UI;
using Screenbox.Controls;
using Screenbox.Core.Enums;
using Screenbox.Core.ViewModels;
using System;
using System.ComponentModel;
using System.Threading;
using Windows.ApplicationModel.Core;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;
using Windows.UI;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace Screenbox.Pages
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class PlayerPage : Page
    {
        internal PlayerPageViewModel ViewModel => (PlayerPageViewModel)DataContext;

        private const VirtualKey PlusKey = (VirtualKey)0xBB;
        private const VirtualKey MinusKey = (VirtualKey)0xBD;
        private const VirtualKey AddKey = (VirtualKey)0x6B;
        private const VirtualKey SubtractKey = (VirtualKey)0x6D;

        private readonly DispatcherQueueTimer _delayFlyoutOpenTimer;
        private CancellationTokenSource? _animationCancellationTokenSource;

        public PlayerPage()
        {
            this.InitializeComponent();
            DataContext = Ioc.Default.GetRequiredService<PlayerPageViewModel>();
            _delayFlyoutOpenTimer = DispatcherQueue.GetForCurrentThread().CreateTimer();

            RegisterSeekBarPointerHandlers();
            UpdatePreviewType();
            UpdateBackgroundAcrylicOpacity(LayoutRoot.ActualTheme);

            CoreApplicationViewTitleBar coreTitleBar = CoreApplication.GetCurrentView().TitleBar;
            LeftPaddingColumn.Width = new GridLength(coreTitleBar.SystemOverlayLeftInset);
            RightPaddingColumn.Width = new GridLength(coreTitleBar.SystemOverlayRightInset);
            coreTitleBar.LayoutMetricsChanged += CoreTitleBar_LayoutMetricsChanged;

            ViewModel.PropertyChanged += ViewModelOnPropertyChanged;
            AlbumArtImage.RegisterPropertyChangedCallback(Image.SourceProperty, AlbumArtImageOnSourceChanged);
            LayoutRoot.ActualThemeChanged += OnActualThemeChanged;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            if (e.Parameter is true)
            {
                LayoutRoot.Transitions.Clear();
                ViewModel.PlayerVisibility = PlayerVisibilityState.Visible;
            }
        }

        private bool GetControlsIsMinimal(PlayerVisibilityState visibility) =>
            visibility != PlayerVisibilityState.Visible;

        private void FocusVideoView()
        {
            VideoView.Focus(FocusState.Programmatic);
        }

        private void SetTitleBar()
        {
            Window.Current.SetTitleBar(TitleBarElement);
            UpdateSystemCaptionButtonForeground();
        }

        private void AlbumArtImageOnSourceChanged(DependencyObject sender, DependencyProperty dp)
        {
            PlayBackgroundArtChangeCrossFadeAnimation();
        }

        private void OnLoading(FrameworkElement sender, object args)
        {
            if (ViewModel.PlayerVisibility == PlayerVisibilityState.Hidden)
                VisualStateManager.GoToState(this, "Hidden", false);
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (LayoutRoot.Transitions.Count != 0) return;
            PaneThemeTransition transition = new()
            {
                Edge = EdgeTransitionLocation.Bottom
            };

            LayoutRoot.Transitions.Add(transition);

            if (ViewModel.PlayerVisibility == PlayerVisibilityState.Visible)
            {
                FocusVideoView();
            }
        }

        private void CoreTitleBar_LayoutMetricsChanged(CoreApplicationViewTitleBar sender, object args)
        {
            // Get the size of the caption controls and set padding.
            LeftPaddingColumn.Width = new GridLength(sender.SystemOverlayLeftInset);
            RightPaddingColumn.Width = new GridLength(sender.SystemOverlayRightInset);
        }

        private void BackgroundElementOnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            double edgeLength = Math.Max(e.NewSize.Width, e.NewSize.Height);
            BackgroundArt.Width = edgeLength;
            BackgroundArt.Height = edgeLength;
        }

        private void RegisterSeekBarPointerHandlers()
        {
            SeekBar? seekBar = PlayerControls.FindDescendant<SeekBar>();
            Guard.IsNotNull(seekBar, nameof(seekBar));
            seekBar.AddHandler(PointerPressedEvent, (PointerEventHandler)SeekBarPointerPressedOrEnteredEventHandler, true);
            seekBar.AddHandler(PointerReleasedEvent, (PointerEventHandler)SeekBarPointerReleasedEventHandler, true);
            seekBar.AddHandler(PointerCanceledEvent, (PointerEventHandler)SeekBarPointerReleasedEventHandler, true);
            seekBar.AddHandler(PointerEnteredEvent, (PointerEventHandler)SeekBarPointerPressedOrEnteredEventHandler, false);
            seekBar.AddHandler(PointerExitedEvent, (PointerEventHandler)SeekBarPointerExitedEventHandler, false);
        }

        private void SeekBarPointerPressedOrEnteredEventHandler(object s, PointerRoutedEventArgs e)
        {
            ViewModel.SeekBarPointerInteracting = true;
        }

        private void SeekBarPointerReleasedEventHandler(object s, PointerRoutedEventArgs e)
        {
            ViewModel.SeekBarPointerInteracting = false;
            FocusVideoView();
        }

        private void SeekBarPointerExitedEventHandler(object s, PointerRoutedEventArgs e)
        {
            ViewModel.SeekBarPointerInteracting = false;
        }

        private void OnLayoutVisualStateChanged(object _, VisualStateChangedEventArgs args)
        {
            if (args.OldState?.Name == "MiniPlayer" || args.OldState?.Name == "Hidden" &&
                (args.NewState == null || args.NewState.Name == "Normal"))
                FocusVideoView();
        }

        private void ViewModelOnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(PlayerPageViewModel.ControlsHidden):
                    VisualStateManager.GoToState(this, ViewModel.ControlsHidden ? "ControlsHidden" : "ControlsVisible", true);
                    break;
                case nameof(PlayerPageViewModel.ViewMode):
                    switch (ViewModel.ViewMode)
                    {
                        case WindowViewMode.Default:
                            VisualStateManager.GoToState(this, "Normal", true);
                            break;
                        case WindowViewMode.Compact:
                            ViewModel.PlayerVisibility = PlayerVisibilityState.Visible;
                            VisualStateManager.GoToState(this, "CompactOverlay", true);
                            SetTitleBar();
                            break;
                        case WindowViewMode.FullScreen:
                            ViewModel.PlayerVisibility = PlayerVisibilityState.Visible;
                            VisualStateManager.GoToState(this, "Fullscreen", true);
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }

                    break;
                case nameof(PlayerPageViewModel.AudioOnly):
                    VisualStateManager.GoToState(this, ViewModel.AudioOnly ?? false ? "AudioOnly" : "Video", true);
                    UpdateSystemCaptionButtonForeground();
                    UpdatePreviewType();
                    break;
                case nameof(PlayerPageViewModel.PlayerVisibility):
                    switch (ViewModel.PlayerVisibility)
                    {
                        case PlayerVisibilityState.Visible:
                            VisualStateManager.GoToState(this, "NoPreview", true);
                            VisualStateManager.GoToState(this, "Normal", true);
                            SetTitleBar();
                            break;
                        case PlayerVisibilityState.Minimal:
                            VisualStateManager.GoToState(this, "MiniPlayer", true);
                            break;
                        case PlayerVisibilityState.Hidden:
                            VisualStateManager.GoToState(this, "Hidden", true);
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }

                    UpdatePreviewType();
                    UpdateMiniPlayerMargin();
                    break;
                case nameof(PlayerPageViewModel.NavigationViewDisplayMode) when ViewModel.ViewMode == WindowViewMode.Default:
                    UpdateMiniPlayerMargin();
                    break;
            }
        }

        private async void PlayBackgroundArtChangeCrossFadeAnimation()
        {
            // AnimationSet does not throw exception on cancellation
            _animationCancellationTokenSource?.Cancel();
            if (BackgroundElement.Visibility == Visibility.Collapsed ||
            BackgroundArt.Visibility == Visibility.Collapsed)
            {
                BackgroundImage.Source = AlbumArtImage.Source;
                return;
            }

            using CancellationTokenSource cts = _animationCancellationTokenSource = new CancellationTokenSource();
            if (ViewModel.Media == null)
            {
                await BackgroundArtFadeOutAnimation.StartAsync(cts.Token);
                BackgroundImage.Source = null;
            }
            else if (BackgroundImage.Source == null)
            {
                BackgroundImageNext.Visibility = Visibility.Collapsed;
                BackgroundImage.GetVisual().Opacity = 0;
                BackgroundImage.Source = AlbumArtImage.Source;
                await BackgroundArtFadeInAnimation.StartAsync(cts.Token);
            }
            else
            {
                BackgroundImageNext.Visibility = Visibility.Visible;
                await BackgroundArtFadeOutAnimation.StartAsync(cts.Token);
                BackgroundImage.Source = AlbumArtImage.Source;
                await BackgroundArtFadeInAnimation.StartAsync(cts.Token);
                BackgroundImageNext.Visibility = Visibility.Collapsed;
            }

            if (cts == _animationCancellationTokenSource)
                _animationCancellationTokenSource = null;
        }

        private void UpdateSystemCaptionButtonForeground()
        {
            if (ApplicationView.GetForCurrentView()?.TitleBar is { } titleBar)
            {
                titleBar.ButtonForegroundColor = ViewModel.AudioOnly ?? false ? null : Colors.White;
            }
        }

        private void UpdatePreviewType()
        {
            if (ViewModel.PlayerVisibility == PlayerVisibilityState.Visible || ViewModel.ViewMode == WindowViewMode.Compact)
            {
                VisualStateManager.GoToState(this, "NoPreview", true);
            }
            else
            {
                VisualStateManager.GoToState(this, ViewModel.AudioOnly ?? false ? "AudioPreview" : "VideoPreview", true);
            }
        }

        private void UpdateMiniPlayerMargin()
        {
            if (ViewModel.PlayerVisibility == PlayerVisibilityState.Visible || ViewModel.ViewMode == WindowViewMode.Compact)
            {
                VisualStateManager.GoToState(this, "NoMargin", false);
            }
            else
            {
                switch (ViewModel.NavigationViewDisplayMode)
                {
                    case NavigationViewDisplayMode.Minimal when ViewModel.PlayerVisibility == PlayerVisibilityState.Hidden:
                        VisualStateManager.GoToState(this, "HiddenMinimalMargin", false);
                        break;
                    case NavigationViewDisplayMode.Minimal:
                        VisualStateManager.GoToState(this, "MinimalMargin", false);
                        break;
                    case NavigationViewDisplayMode.Compact when ViewModel.PlayerVisibility == PlayerVisibilityState.Hidden:
                    case NavigationViewDisplayMode.Expanded when ViewModel.PlayerVisibility == PlayerVisibilityState.Hidden:
                        VisualStateManager.GoToState(this, "HiddenNormalMargin", false);
                        break;
                    case NavigationViewDisplayMode.Compact:
                    case NavigationViewDisplayMode.Expanded:
                        VisualStateManager.GoToState(this, "NormalMargin", false);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }

        private async void PlayQueueFlyout_OnOpened(object sender, object e)
        {
            if (PlaylistView == null) return;
            await PlaylistView.SmoothScrollActiveItemIntoViewAsync();
        }

        private void OnActualThemeChanged(FrameworkElement sender, object args)
        {
            UpdateBackgroundAcrylicOpacity(ActualTheme);
        }

        private void UpdateBackgroundAcrylicOpacity(ElementTheme theme)
        {
            // Set in code due to XAML compiler not setting it in Release
            BackgroundAcrylicBrush.TintLuminosityOpacity = theme == ElementTheme.Light ? 0.5 : 0.4;
        }

        private void OnGamepadKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.GamepadY &&
                ViewModel.PlayerVisibility == PlayerVisibilityState.Visible &&
                ViewModel.ViewMode != WindowViewMode.Compact)
            {
                e.Handled = true;
                PlayQueueFlyout.ShowAt(TitleBarArea, new FlyoutShowOptions { Placement = FlyoutPlacementMode.Bottom });
            }
        }

        private void PlayQueueButton_OnDragEnter(object sender, DragEventArgs e)
        {
            if (!e.DataView.Contains(StandardDataFormats.StorageItems)) return;
            FindName(nameof(PlayQueueFlyout));  // Trigger element lazy load
            _delayFlyoutOpenTimer.Debounce(() => PlayQueueFlyout.ShowAt(PlayQueueButton), TimeSpan.FromMilliseconds(500));
        }

        private void PlayQueueButton_OnDragLeave(object sender, DragEventArgs e)
        {
            _delayFlyoutOpenTimer.Stop();
        }
    }
}
