﻿using BardMusicPlayer.Coffer;
using BardMusicPlayer.Transmogrify.Song;
using BardMusicPlayer.Ui.Functions;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text;

namespace BardMusicPlayer.Ui.Functions
{
    public class SongContainer
    {
        public string Name { get; set; } = "";
        public byte[] Data { get; set; } = null;
    }

    public static class JsonPlaylist
    {
        public static List<SongContainer> Load(string filename)
        {
            List<SongContainer> songs = new List<SongContainer>();

            MemoryStream memoryStream = new MemoryStream();
            FileStream compressedFileStream = File.Open(filename, FileMode.Open);
            var compressor = new GZipStream(compressedFileStream, CompressionMode.Decompress);
            compressor.CopyTo(memoryStream);
            compressor.Close();
            compressedFileStream.Close();

            var data = memoryStream.ToArray();
            songs = JsonConvert.DeserializeObject<List<SongContainer>>(new UTF8Encoding(true).GetString(data));

            return songs;
        }

        public static void Save(string filename, List<SongContainer> sc)
        {
            var t = JsonConvert.SerializeObject(sc);
            byte[] content = new UTF8Encoding(true).GetBytes(t);

            FileStream compressedFileStream = File.Create(filename);
            var compressor = new GZipStream(compressedFileStream, CompressionMode.Compress);
            compressor.Write(content, 0, content.Length);
            compressor.Close();
        }
    }

    public static class PlaylistImporter
    {
        static List<KeyValuePair<string, string>> songs_url = new List<KeyValuePair<string, string>>();
        public static void ImportPlaylist()
        {
            var httpClient = new HttpClient();
            var publicFolderId = "1M1mU2ZxMgxz0_S274gf1oKLrea3AX_4Q";
            var nextPageToken = "";
            do
            {
                var folderContentsUri = $"https://drive.google.com/drive/folders/"+publicFolderId+ "?usp=sharing";
                Console.WriteLine(folderContentsUri);
                if (!String.IsNullOrEmpty(nextPageToken))
                {
                    folderContentsUri += $"&pageToken={nextPageToken}";
                }
                var contentsJson = httpClient.GetStringAsync(folderContentsUri).Result;
                var content = contentsJson.Split( new string[] { "aria-label=" }, StringSplitOptions.RemoveEmptyEntries);


                foreach (var data in content)
                {
                    if (data.Contains("_._DumpException(e)"))
                        continue;
                    if (data.Contains("<path fill=\"none\""))
                        continue;

                    if (data.Contains("jsdata"))
                    {
                        var name = data.Split('"')[1];
                        var path = data.Split(new string[] { "jsdata" }, StringSplitOptions.RemoveEmptyEntries)[1].Split(';')[3];
                        songs_url.Add(new KeyValuePair<string, string>(name, path));
                        Console.WriteLine(data);
                    }
                }
                Console.WriteLine(songs_url.ToString());
            } while (!String.IsNullOrEmpty(nextPageToken));
            httpClient.Dispose();
        }

        static public byte[] DownloadFile(string name)
        {
            var foundSong = songs_url.Find(x => x.Key.ToLower().Contains(name.ToLower()));

            var httpClient = new HttpClient();
            var file = httpClient.GetByteArrayAsync("https://drive.google.com/uc?export=download&id=" + foundSong.Value).Result;
            using (var stream = File.Open("output.mid", FileMode.Create))
            {
                foreach(var data in file)
                stream.WriteByte(data);
            }

            return file;
        }
    }
}
