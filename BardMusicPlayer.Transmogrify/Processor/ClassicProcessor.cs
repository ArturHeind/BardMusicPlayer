﻿/*
 * Copyright(c) 2021 MoogleTroupe
 * Licensed under the GPL v3 license. See https://github.com/BardMusicPlayer/BardMusicPlayer/blob/develop/LICENSE for full license information.
 */

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BardMusicPlayer.Quotidian.Structs;
using BardMusicPlayer.Transmogrify.Processor.Utilities;
using BardMusicPlayer.Transmogrify.Song;
using BardMusicPlayer.Transmogrify.Song.Config;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;

namespace BardMusicPlayer.Transmogrify.Processor
{
    internal class ClassicProcessor : BaseProcessor
    {
        public ClassicProcessorConfig ProcessorConfig { get; set; }

        internal ClassicProcessor(ClassicProcessorConfig processorConfig, BmpSong song) : base(song)
        {
            ProcessorConfig = processorConfig;
        }

        public override async Task<List<TrackChunk>> Process()
        {
            var trackChunks = new List<TrackChunk> { Song.TrackContainers[ProcessorConfig.Track].SourceTrackChunk }.Concat(ProcessorConfig.IncludedTracks.Select(track => Song.TrackContainers[track].SourceTrackChunk)).ToList();

            //convert progchanges to lower notes, if it's a guitar
            if (ProcessorConfig.Instrument.InstrumentTone.Index == InstrumentTone.ElectricGuitar.Index)
            {

                foreach (var timedEvent in trackChunks.GetTimedEvents())
                {
                    var programChangeEvent = timedEvent.Event as ProgramChangeEvent;
                    if (programChangeEvent == null)
                        continue;

                    //Skip all except guitar
                    if ((programChangeEvent.ProgramNumber < 27) || (programChangeEvent.ProgramNumber > 31))
                        continue;

                    int number = (int)Instrument.ParseByProgramChange(programChangeEvent.ProgramNumber).InstrumentToneMenuKey;
                    using (NotesManager manager = trackChunks.Merge().ManageNotes())
                    {
                        Note note = new Note((Melanchall.DryWetMidi.Common.SevenBitNumber)number);
                        NotesCollection timedEvents = manager.Notes;
                        note.Time = timedEvent.Time;
                        timedEvents.Add(note);
                    }
                }
            }

            var trackChunk = TimedObjectUtilities.ToTrackChunk(await 
                trackChunks.GetNoteDictionary(Song.SourceTempoMap, ProcessorConfig.Instrument.InstrumentTone,
                        ProcessorConfig.OctaveRange.LowerNote, 
                        ProcessorConfig.OctaveRange.UpperNote, 
                        (int) ProcessorConfig.Instrument.InstrumentToneMenuKey, 
                        true,
                        -ProcessorConfig.OctaveRange.LowerNote)
                .MoveNoteDictionaryToDefaultOctave(ProcessorConfig.OctaveRange)
                .ConcatNoteDictionaryToList());

            var playerNotesDictionary = await trackChunk.GetPlayerNoteDictionary(ProcessorConfig.PlayerCount, OctaveRange.C3toC6.LowerNote, OctaveRange.C3toC6.UpperNote);
            var concurrentPlayerTrackDictionary = new ConcurrentDictionary<long, TrackChunk>(ProcessorConfig.PlayerCount, ProcessorConfig.PlayerCount);

            Parallel.ForEach(playerNotesDictionary.Values, async (notesDictionary, _, iteration) =>
                {
                    concurrentPlayerTrackDictionary[iteration] = TimedObjectUtilities.ToTrackChunk(await notesDictionary.ConcatNoteDictionaryToList().FixChords().OffSet50Ms().FixEndSpacing());
                    concurrentPlayerTrackDictionary[iteration].AddObjects(new List<ITimedObject>{new TimedEvent(new SequenceTrackNameEvent("tone:" + ProcessorConfig.Instrument.InstrumentTone.Name))});
                }
            );
            trackChunks = concurrentPlayerTrackDictionary.Values.ToList();
            return trackChunks;
        }
    }
}
