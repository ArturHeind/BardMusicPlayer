﻿/*
 * Copyright(c) 2022 MoogleTroupe, isaki, GiR-Zippo
 * Licensed under the GPL v3 license. See https://github.com/GiR-Zippo/LightAmp/blob/main/LICENSE for full license information.
 */

using System.Collections.Generic;
using BardMusicPlayer.Transmogrify.Song;
using LiteDB;

namespace BardMusicPlayer.Coffer
{
    public sealed class BmpPlaylist
    {
        [BsonId]
        public ObjectId Id { get; set; } = null;

        public string Name { get; set; }

        [BsonRef(Constants.SONG_COL_NAME)]
        public List<BmpSong> Songs { get; set; }
    }
}
