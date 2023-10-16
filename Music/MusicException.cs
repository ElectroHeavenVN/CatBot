using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CatBot.Music
{
    internal class MusicException : Exception
    {
        internal MusicType MusicType { get; set; }

        public MusicException() { }
        public MusicException(string message) : this(0, message) { }
        public MusicException(MusicType musicType, string message) : base(message) 
        {
            MusicType = musicType;
        }

        internal string GetErrorMessage()
        {
            string content;
            if (MusicType == MusicType.ZingMP3 && Message.StartsWith("-1110"))
                content = $"Bài này bị Zing MP3 chặn ở quốc gia đặt máy chủ của bot!";
            else if (MusicType == MusicType.NhacCuaTui && Message == "not available")
                content = "Bài này bị NhacCuaTui chặn ở quốc gia đặt máy chủ của bot!";
            else if (MusicType == MusicType.YouTube && Message == "video not found")
                content = "Không tìm thấy video này!";
            else if (MusicType == MusicType.YouTube && Message == "channel not found")
                content = "Không tìm thấy kênh này!";
            else if (MusicType == MusicType.SoundCloud && Message == "invalid short link")
                content = "Link SoundCloud không hợp lệ!";
            else if (MusicType == MusicType.Spotify && Message == "music download timeout")
                content = "Hết thời gian chờ để tải nhạc từ Spotify!";
            else if (MusicType == MusicType.Spotify && Message == "not found")
                content = "Không thể phát bài \"{0}\" do bài hát không có sẵn trên các nền tảng khác ngoài Spotify!";
            else if (MusicType == MusicType.Local && Message == "file not found")
                content = "Không tìm thấy bài hát \"{0}\" trong bộ nhớ!";
            else if (Message == "songs not found")
                content = "Không tìm thấy bài \"{0}\"!";
            else if (Message == "not found")
                content = "Không tìm thấy bài này!";
            else if (Message == "playlist not found")
                content = "Không tìm thấy danh sách phát này!";
            else if (Message == "album not found")
                content = "Không tìm thấy album này!";
            else if (Message == "artist not found")
                content = "Không tìm thấy nghệ sĩ này!";
            else
                content = ToString();
            return content;
        }
    }
}
