using System;
using static MusicBeePlugin.Plugin;

namespace MusicBeePlugin
{
    public class MBSong
    {
        public string Name { get; private set; }
        public string AlbumName { get; private set; }
        public string Artist { get; private set; }
        public double Duration { get; private set; }

        public MBSong(string sourceFileUrl)
        {
            Name = mbApiInterface.Library_GetFileTag(sourceFileUrl, MetaDataType.TrackTitle);
            AlbumName = mbApiInterface.Library_GetFileTag(sourceFileUrl, MetaDataType.Album);

            string albArtist = mbApiInterface.Library_GetFileTag(sourceFileUrl, MetaDataType.AlbumArtist);
            string trackArtist = mbApiInterface.Library_GetFileTag(sourceFileUrl, MetaDataType.Artists);
            Artist = trackArtist.Contains(albArtist) ? albArtist : trackArtist;

            // Durations are in MM:SS format and TimeSpan doesn't support parsing something over 59 minutes so this is a janky workaround
            string strDuration = mbApiInterface.Library_GetFileProperty(sourceFileUrl, FilePropertyType.Duration);
            var split = strDuration.Split(':');
            TimeSpan ts = new TimeSpan(0, int.Parse(split[0]), int.Parse(split[1]));
            Duration = ts.TotalSeconds;
        }
    }
}
