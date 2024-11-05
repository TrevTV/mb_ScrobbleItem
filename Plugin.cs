using System;
using System.Drawing;
using System.Windows.Forms;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;

namespace MusicBeePlugin
{
    public partial class Plugin
    {
        public static MusicBeeApiInterface mbApiInterface;

        private PluginInfo about = new PluginInfo();
        private MethodInfo CallAPIMethod;
        private static readonly DateTime UnixStartTime = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        private static readonly BindingFlags AllFlags = BindingFlags.Public
            | BindingFlags.NonPublic
            | BindingFlags.Instance
            | BindingFlags.Static
            | BindingFlags.GetField
            | BindingFlags.SetField
            | BindingFlags.GetProperty
            | BindingFlags.SetProperty;

        public PluginInfo Initialise(IntPtr apiInterfacePtr)
        {
            mbApiInterface = new MusicBeeApiInterface();
            mbApiInterface.Initialise(apiInterfacePtr);
            about.PluginInfoVersion = PluginInfoVersion;
            about.Name = "Scrobble Item";
            about.Description = "Allows you to scrobble any album or track in your library without fully playing it.";
            about.Author = "trev";
            about.Type = PluginType.General;
            about.VersionMajor = 1;
            about.VersionMinor = 1;
            about.Revision = 2;
            about.MinInterfaceVersion = 40;
            about.MinApiRevision = 52;
            about.ReceiveNotifications = (ReceiveNotificationFlags.PlayerEvents | ReceiveNotificationFlags.TagEvents);
            about.ConfigurationPanelHeight = 0;
            return about;
        }

        public bool Configure(IntPtr panelHandle)
        {
            string dataPath = mbApiInterface.Setting_GetPersistentStoragePath();

            if (panelHandle != IntPtr.Zero)
            {
                Panel configPanel = (Panel)Panel.FromHandle(panelHandle);
                Label prompt = new Label();
                prompt.AutoSize = true;
                prompt.Location = new Point(0, 0);
                prompt.Text = "prompt:";
                TextBox textBox = new TextBox();
                textBox.Bounds = new Rectangle(60, 0, 100, textBox.Height);
                configPanel.Controls.AddRange(new Control[] { prompt, textBox });
            }
            return false;
        }

        public void SaveSettings()
        {
            string dataPath = mbApiInterface.Setting_GetPersistentStoragePath();
        }

        public void ReceiveNotification(string sourceFileUrl, NotificationType type)
        {
            switch (type)
            {
                case NotificationType.PluginStartup:
                    mbApiInterface.MB_AddMenuItem($"context.Main/Scrobble", "", ScrobbleSelected);
                    mbApiInterface.MB_RegisterCommand("ScrobbleItem: Scrobble Selected", ScrobbleSelected);

                    // An unholy concoction of code to deal with obfuscation and the possibility of type and method names changing in later versions
                    var mbAsm = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.FullName.Contains("MusicBee"));
                    foreach (var refType in mbAsm.GetTypes())
                    {
                        var method = refType.GetMethods(AllFlags).FirstOrDefault(m =>
                        {
                            var parameters = m.GetParameters();
                            return parameters.Length == 3
                                && parameters[0].ParameterType == typeof(string)
                                && parameters[2].ParameterType == typeof(KeyValuePair<string, string>[])
                                && m.ReturnType == typeof(System.Xml.XmlReader);
                        });

                        if (method != null)
                        {
                            CallAPIMethod = method;
                            break;
                        }
                    }

                    break;
            }
        }

        public void ScrobbleSelected(object sender, EventArgs args)
        {
            mbApiInterface.Library_QueryFilesEx("domain=SelectedFiles", out string[] files);
            if (files == null) return;

            List<MBSong> songs = files.Select(f => new MBSong(f)).ToList();
            double totalSongPlayTime = songs.Select(s => s.Duration).Sum();

            // Allows the plugin to do something similar to OpenScrobbler
            // "Scrobbling tracks will scrobble the last track at the current time, backdating the previous ones accordingly (as if you had just finished listening)."
            DateTime startTime = DateTime.UtcNow.AddSeconds(-totalSongPlayTime);
            var apiCallParameters = new List<KeyValuePair<string, string>>();
            for (int i = 0; i < songs.Count; i++)
            {
                MBSong song = songs[i];
                startTime = startTime.AddSeconds(song.Duration);
                long unixTimestamp = (long)startTime.Subtract(UnixStartTime).TotalSeconds;
                apiCallParameters.Add(CreatePair($"track[{i}]", song.Name));
                apiCallParameters.Add(CreatePair($"artist[{i}]", song.Artist));
                apiCallParameters.Add(CreatePair($"albumArtist[{i}]", song.AlbumArtist));
                apiCallParameters.Add(CreatePair($"album[{i}]", song.AlbumName));
                apiCallParameters.Add(CreatePair($"duration[{i}]", song.Duration < 30 ? "31" : song.Duration.ToString())); // Weird but allows for scrobbling tracks under 30 seconds
                apiCallParameters.Add(CreatePair($"timestamp[{i}]", unixTimestamp.ToString()));
            }

            CallAPIMethod.Invoke(null, new object[]
            {
                "track.scrobble",
                5,
                apiCallParameters.ToArray()
            });
        }

        private KeyValuePair<string, string> CreatePair(string key, string value)
            => new KeyValuePair<string, string>(key, value);
    }
}