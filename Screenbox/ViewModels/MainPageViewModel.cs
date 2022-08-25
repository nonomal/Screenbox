﻿using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.UI.Xaml.Controls;
using Screenbox.Core.Messages;

namespace Screenbox.ViewModels
{
    internal partial class MainPageViewModel : ObservableRecipient,
        IRecipient<PlayerVisibilityChangedMessage>,
        IRecipient<NavigationViewDisplayModeRequestMessage>
    {
        [ObservableProperty] private bool _playerVisible;
        [ObservableProperty] private NavigationViewDisplayMode _navigationViewDisplayMode;

        public MainPageViewModel()
        {
            IsActive = true;
        }

        public void Receive(PlayerVisibilityChangedMessage message)
        {
            PlayerVisible = message.Value;
        }

        public void Receive(NavigationViewDisplayModeRequestMessage message)
        {
            message.Reply(NavigationViewDisplayMode);
        }

        partial void OnNavigationViewDisplayModeChanged(NavigationViewDisplayMode value)
        {
            Messenger.Send(new NavigationViewDisplayModeChangedMessage(value));
        }
    }
}