﻿using Rise.Models;
using Rise.App.Common;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using Windows.Data.Xml.Dom;

namespace Rise.App.ViewModels
{
    public class ArtistViewModel : ViewModel<Artist>
    {
        // private readonly DispatcherQueue dispatcherQueue = DispatcherQueue.GetForCurrentThread();

        /// <summary>
        /// Initializes a new instance of the ArtistViewModel class that wraps an Artist object.
        /// </summary>
        public ArtistViewModel(Artist model = null)
        {
            Model = model ?? new Artist();
            IsNew = true;
        }

        /// <summary>
        /// Gets or sets the artist name.
        /// </summary>
        public string Name
        {
            get
            {
                if (Model.Name == "UnknownArtistResource")
                {
                    return ResourceLoaders.MediaDataLoader.GetString("UnknownArtistResource");
                }

                return Model.Name;
            }
            set
            {
                if (value != Model.Name)
                {
                    Model.Name = value;
                    IsModified = true;
                    OnPropertyChanged(nameof(Name));
                }
            }
        }

        /// <summary>
        /// Gets or sets the artist picture.
        /// </summary>
        public string Picture
        {
            get => Model.Picture;
            set
            {
                if (value != Model.Picture)
                {
                    Model.Picture = value;
                    IsModified = true;
                    OnPropertyChanged(nameof(Picture));
                }
            }
        }

        public async Task<string> GetPictureAsync()
        {
            string name = HttpUtility.UrlEncode(Name);
            string xml;

            try
            {
                xml = await WebHelpers.
                    CreateGETRequestAsync(URLs.MusicBrainz + "artist/?query=artist:" + name);
            }
            catch
            {
                return Resources.MusicThumb;
            }

            if (xml == null)
            {
                return Resources.MusicThumb;
            }

            XmlDocument doc = new XmlDocument();
            doc.LoadXml(xml);

            XmlNodeList nodes = doc.GetElementsByTagName("artist");

            string id = "";
            foreach (IXmlNode node in nodes)
            {
                XmlNamedNodeMap attrs = node.Attributes;
                IXmlNode idAttr = attrs.GetNamedItem("id");

                if (idAttr != null)
                {
                    id = idAttr.InnerText;
                    break;
                }
            }

            if (id != "")
            {
                xml = await WebHelpers.
                    CreateGETRequestAsync(URLs.MusicBrainz + "artist/" + id + "?inc=url-rels");

                doc.LoadXml(xml);

                nodes = doc.GetElementsByTagName("relation");

                string img;
                foreach (IXmlNode node in nodes)
                {
                    XmlNamedNodeMap attrs = node.Attributes;
                    IXmlNode type = attrs.GetNamedItem("type");

                    if (type.InnerText == "image")
                    {
                        img = node.FirstChild.InnerText;

                        string path = await WebHelpers.SaveImageFromURLAsync(img, $@"{name}.png");
                        Debug.WriteLine(path);

                        if (path != "/")
                        {
                            return $@"ms-appdata:///local/{path}.png";
                        }
                    }
                }
            }

            return Resources.MusicThumb;
        }

        /// <summary>
        /// Gets or sets the artist's song count.
        /// </summary>
        public int SongCount => App.MViewModel.Songs.Count(s => s.Model.Artist == Model.Name);
        public string Songs => SongCount.ToString() + " " + ResourceLoaders.MediaDataLoader.GetString("Songs");

        /// <summary>
        /// Gets or sets the artist's album count.
        /// </summary>
        public int AlbumCount => App.MViewModel.Albums.Count(a => a.Model.Artist == Model.Name);
        public string Albums => AlbumCount.ToString() + " " + ResourceLoaders.MediaDataLoader.GetString("Albums");

        /// <summary>
        /// Gets or sets a value that indicates whether the underlying model has been modified. 
        /// </summary>
        /// <remarks>
        /// Used to reduce load and only upsert the models that have changed.
        /// </remarks>
        public bool IsModified { get; set; }

        private bool _isNew;
        /// <summary>
        /// Gets or sets a value that indicates whether this is a new item.
        /// </summary>
        public bool IsNew
        {
            get => _isNew;
            set => Set(ref _isNew, value);
        }

        private bool _isInEdit = false;
        /// <summary>
        /// Gets or sets a value that indicates whether the artist data is being edited.
        /// </summary>
        public bool IsInEdit
        {
            get => _isInEdit;
            set => Set(ref _isInEdit, value);
        }

        /// <summary>
        /// Saves artist data that has been edited.
        /// </summary>
        public async Task SaveAsync()
        {
            IsInEdit = false;
            IsModified = false;

            if (IsNew)
            {
                IsNew = false;
                App.MViewModel.Artists.Add(this);
            }

            if (Picture == null) Picture = Resources.ArtistThumb;
            await App.Repository.Artists.QueueUpsertAsync(Model);
        }

        /// <summary>
        /// Checks whether or not the artist is available. If it's not,
        /// delete it.
        /// </summary>
        public async Task CheckAvailabilityAsync()
        {
            if (SongCount == 0 && AlbumCount == 0)
            {
                await DeleteAsync();
                return;
            }
        }

        /// <summary>
        /// Delete artist from repository and MViewModel.
        /// </summary>
        public async Task DeleteAsync()
        {
            IsModified = true;

            App.MViewModel.Artists.Remove(this);
            await App.Repository.Artists.QueueDeletionAsync(Model);
        }

        /// <summary>
        /// Raised when the user cancels the changes they've made to the artist data.
        /// </summary>
        public event EventHandler AddNewArtistCanceled;

        /// <summary>
        /// Cancels any in progress edits.
        /// </summary>
        public async Task CancelEditsAsync()
        {
            if (IsNew)
            {
                AddNewArtistCanceled?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                await RevertChangesAsync();
            }
        }

        /// <summary>
        /// Discards any edits that have been made, restoring the original values.
        /// </summary>
        public async Task RevertChangesAsync()
        {
            IsInEdit = false;
            if (IsModified)
            {
                await RefreshArtistsAsync();
                IsModified = false;
            }
        }

        /// <summary>
        /// Enables edit mode.
        /// </summary>
        public void StartEdit() => IsInEdit = true;

        /// <summary>
        /// Reloads all of the artist data.
        /// </summary>
        public async Task RefreshArtistsAsync()
        {
            Model = await App.Repository.Artists.GetAsync(Model.Id);
        }
    }
}
