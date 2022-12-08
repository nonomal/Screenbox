﻿#nullable enable

using System.Collections.Generic;
using Screenbox.ViewModels;

namespace Screenbox.Factories
{
    internal sealed class ArtistViewModelFactory
    {
        private readonly Dictionary<string, ArtistViewModel> _allArtists;
        private readonly ArtistViewModel _unknownArtist;

        public ArtistViewModelFactory()
        {
            _allArtists = new Dictionary<string, ArtistViewModel>();
            _unknownArtist = new ArtistViewModel(Strings.Resources.UnknownArtist);
        }

        public ArtistViewModel GetArtistFromName(string artistName)
        {
            if (string.IsNullOrEmpty(artistName))
                return _unknownArtist;

            return _allArtists.TryGetValue(artistName, out ArtistViewModel artist) ? artist : _unknownArtist;
        }

        public ArtistViewModel AddSongToArtist(MediaViewModel song, string? artistName = null)
        {
            artistName ??= song.MusicProperties?.Artist ?? string.Empty;
            if (string.IsNullOrEmpty(artistName))
            {
                _unknownArtist.RelatedSongs.Add(song);
                return _unknownArtist;
            }

            ArtistViewModel artist = GetArtistFromName(artistName);
            if (artist != _unknownArtist)
            {
                artist.RelatedSongs.Add(song);
                return artist;
            }

            artist = new ArtistViewModel(artistName);
            artist.RelatedSongs.Add(song);
            return _allArtists[artistName] = artist;
        }
    }
}