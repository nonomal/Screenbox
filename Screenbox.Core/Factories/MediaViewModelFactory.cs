﻿using LibVLCSharp.Shared;
using Screenbox.Core.Playback;
using Screenbox.Core.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using Windows.Storage;
using MediaViewModel = Screenbox.Core.ViewModels.MediaViewModel;

namespace Screenbox.Core.Factories
{
    public sealed class MediaViewModelFactory
    {
        private readonly IMediaService _mediaService;
        private readonly ArtistViewModelFactory _artistFactory;
        private readonly AlbumViewModelFactory _albumFactory;
        private readonly Dictionary<string, WeakReference<MediaViewModel>> _references = new();
        private int _referencesCleanUpThreshold = 1000;

        public MediaViewModelFactory(IMediaService mediaService,
            ArtistViewModelFactory artistFactory, AlbumViewModelFactory albumFactory)
        {
            _mediaService = mediaService;
            _artistFactory = artistFactory;
            _albumFactory = albumFactory;
        }

        public MediaViewModel GetTransient(StorageFile file)
        {
            return new MediaViewModel(_mediaService, _albumFactory, _artistFactory, file);
        }

        public MediaViewModel GetTransient(Uri uri)
        {
            return new MediaViewModel(_mediaService, _albumFactory, _artistFactory, uri);
        }

        public MediaViewModel GetTransient(Media media)
        {
            if (!Uri.TryCreate(media.Mrl, UriKind.Absolute, out Uri uri))
                return new MediaViewModel(_mediaService, _albumFactory, _artistFactory, media);

            // Prefer URI source for easier clean up
            MediaViewModel vm = new(_mediaService, _albumFactory, _artistFactory, uri)
            {
                Item = new PlaybackItem(media, media)
            };

            if (media.Meta(MetadataType.Title) is { } name)
                vm.Name = name;

            return vm;
        }

        public MediaViewModel GetSingleton(StorageFile file)
        {
            string id = file.Path;
            if (_references.TryGetValue(id, out WeakReference<MediaViewModel> reference) &&
                reference.TryGetTarget(out MediaViewModel instance)) return instance;

            // No existing reference, create new instance
            instance = new MediaViewModel(_mediaService, _albumFactory, _artistFactory, file);
            if (!string.IsNullOrEmpty(id))
            {
                _references[id] = new WeakReference<MediaViewModel>(instance);
                CleanUpStaleReferences();
            }

            return instance;
        }

        public MediaViewModel GetSingleton(Uri uri)
        {
            string id = uri.OriginalString;
            if (_references.TryGetValue(id, out WeakReference<MediaViewModel> reference) &&
                reference.TryGetTarget(out MediaViewModel instance)) return instance;

            // No existing reference, create new instance
            instance = new MediaViewModel(_mediaService, _albumFactory, _artistFactory, uri);
            if (!string.IsNullOrEmpty(id))
            {
                _references[id] = new WeakReference<MediaViewModel>(instance);
                CleanUpStaleReferences();
            }

            return instance;
        }

        private void CleanUpStaleReferences()
        {
            if (_references.Count < _referencesCleanUpThreshold) return;
            string[] keysToRemove = _references
                .Where(pair => !pair.Value.TryGetTarget(out MediaViewModel _))
                .Select(pair => pair.Key).ToArray();
            foreach (string key in keysToRemove)
            {
                _references.Remove(key);
            }

            _referencesCleanUpThreshold = Math.Max(_references.Count * 2, _referencesCleanUpThreshold);
        }
    }
}
