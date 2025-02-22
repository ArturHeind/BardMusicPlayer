﻿/*
 * Copyright(c) 2022 GiR-Zippo
 * Licensed under the GPL v3 license. See https://github.com/GiR-Zippo/LightAmp/blob/main/LICENSE for full license information.
 */

using BardMusicPlayer.Maestro.Events;
using Melanchall.DryWetMidi.Interaction;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace BardMusicPlayer.Maestro
{
    public partial class BmpMaestro
    {
        public EventHandler<CurrentPlayPositionEvent> OnPlaybackTimeChanged;
        public EventHandler<MaxPlayTimeEvent> OnSongMaxTime;
        public EventHandler<SongLoadedEvent> OnSongLoaded;
        public EventHandler<bool> OnPlaybackStarted;
        public EventHandler<bool> OnPlaybackStopped;
        public EventHandler<bool> OnPerformerChanged;
        public EventHandler<TrackNumberChangedEvent> OnTrackNumberChanged;
        public EventHandler<OctaveShiftChangedEvent> OnOctaveShiftChanged;
        public EventHandler<PerformerUpdate> OnPerformerUpdate;
        private ConcurrentQueue<MaestroEvent> _eventQueue;
        private bool _eventQueueOpen;

        private async Task RunEventsHandler(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                while (_eventQueue.TryDequeue(out var meastroEvent))
                {
                    if (token.IsCancellationRequested)
                        break;

                    try
                    {
                        switch (meastroEvent)
                        {
                            case CurrentPlayPositionEvent currentPlayPosition:
                                OnPlaybackTimeChanged(this, currentPlayPosition);
                                break;
                            case MaxPlayTimeEvent maxPlayTime:
                                OnSongMaxTime(this, maxPlayTime);
                                break;
                            case SongLoadedEvent songloaded:
                                if (OnSongLoaded == null)
                                    break;
                                OnSongLoaded(this, songloaded);
                                break;
                            case PlaybackStartedEvent playbackStarted:
                                if (OnPlaybackStarted == null)
                                    break;
                                OnPlaybackStarted(this, playbackStarted.Started);
                                break;
                            case PlaybackStoppedEvent playbackStopped:
                                if (OnPlaybackStopped == null)
                                    break;
                                OnPlaybackStopped(this, playbackStopped.Stopped);
                                break;
                            case PerformersChangedEvent performerChanged:
                                if (OnPerformerChanged == null)
                                    break;
                                OnPerformerChanged(this, performerChanged.Changed);
                                break;
                            case TrackNumberChangedEvent trackNumberChanged:
                                if (OnTrackNumberChanged == null)
                                    break;
                                OnTrackNumberChanged(this, trackNumberChanged);
                                break;
                            case OctaveShiftChangedEvent octaveShiftChanged:
                                if (OnOctaveShiftChanged == null)
                                    break;
                                OnOctaveShiftChanged(this, octaveShiftChanged);
                                break;
                            case PerformerUpdate performerUpdate:
                                if (OnPerformerUpdate == null)
                                    break;
                                OnPerformerUpdate(this, performerUpdate);
                                break;

                        };
                    }
                    catch
                    { }
                }
                await Task.Delay(25, token).ContinueWith(tsk=> { });
            }
        }

        private CancellationTokenSource _eventsTokenSource;

        private void StartEventsHandler()
        {
            _eventQueue = new ConcurrentQueue<MaestroEvent>();
            _eventsTokenSource = new CancellationTokenSource();
            Task.Factory.StartNew(() => RunEventsHandler(_eventsTokenSource.Token), TaskCreationOptions.LongRunning);
            _eventQueueOpen = true;
        }

        private void StopEventsHandler()
        {
            _eventQueueOpen = false;
            _eventsTokenSource.Cancel();
            while (_eventQueue.TryDequeue(out _))
            {
            }
        }

        internal void PublishEvent(MaestroEvent meastroEvent)
        {
            if (!_eventQueueOpen)
                return;

            _eventQueue.Enqueue(meastroEvent);
        }
    }
}
