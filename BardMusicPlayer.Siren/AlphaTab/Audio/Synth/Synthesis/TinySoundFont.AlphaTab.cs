/*
 * Copyright(c) 2021 Daniel Kuschny
 * Licensed under the MPL-2.0 license. See https://github.com/CoderLine/alphaTab/blob/develop/LICENSE for full license information.
 */

// This file contains alphaTab specific extensions to the TinySoundFont audio synthesis

using BardMusicPlayer.Siren.AlphaTab.Audio.Synth.Ds;
using BardMusicPlayer.Siren.AlphaTab.Audio.Synth.Midi.Event;
using BardMusicPlayer.Siren.AlphaTab.Audio.Synth.Util;
using BardMusicPlayer.Siren.AlphaTab.Collections;
using BardMusicPlayer.Siren.AlphaTab.Util;

namespace BardMusicPlayer.Siren.AlphaTab.Audio.Synth.Synthesis
{
    internal partial class TinySoundFont
    {
        public const int MicroBufferCount = 32; // 4069 samples in total
        public const int MicroBufferSize = 64; // 64 stereo samples

        private readonly LinkedList<SynthEvent> _midiEventQueue = new LinkedList<SynthEvent>();
        private readonly int[] _midiEventCounts = new int[MicroBufferCount];
        private FastDictionary<int, bool> _mutedChannels = new FastDictionary<int, bool>();
        private FastDictionary<int, bool> _soloChannels = new FastDictionary<int, bool>();
        private bool _isAnySolo;

        public float[] Synthesize()
        {
            return FillWorkingBuffer(false);
        }

        public void SynthesizeSilent()
        {
            FillWorkingBuffer(true);
        }

        public float ChannelGetMixVolume(int channel)
        {
            return (_channels != null && channel < _channels.ChannelList.Count
                ? _channels.ChannelList[channel].MixVolume
                : 1.0f);
        }

        public void ChannelSetMixVolume(int channel, float volume)
        {
            var c = ChannelInit(channel);
            foreach (var v in _voices)
            {
                if (v.PlayingChannel == channel && v.PlayingPreset != -1)
                {
                    v.MixVolume = volume;
                }
            }

            c.MixVolume = volume;
        }

        public void ChannelSetMute(int channel, bool mute)
        {
            if (mute)
            {
                _mutedChannels[channel] = true;
            }
            else
            {
                _mutedChannels.Remove(channel);
            }
        }

        public void ChannelSetSolo(int channel, bool solo)
        {
            if (solo)
            {
                _soloChannels[channel] = true;
            }
            else
            {
                _soloChannels.Remove(channel);
            }

            _isAnySolo = _soloChannels.Count > 0;
        }

        public void ResetChannelStates()
        {
            _mutedChannels = new FastDictionary<int, bool>();
            _soloChannels = new FastDictionary<int, bool>();
            _isAnySolo = false;
        }

        public void DispatchEvent(int i, SynthEvent synthEvent)
        {
            _midiEventQueue.AddFirst(synthEvent);
            _midiEventCounts[i]++;
        }

        private float[] FillWorkingBuffer(bool silent)
        {
            /*Break the process loop into sections representing the smallest timeframe before the midi controls need to be updated
            the bigger the timeframe the more efficent the process is, but playback quality will be reduced.*/
            var buffer = new float[MicroBufferSize * MicroBufferCount * SynthConstants.AudioChannels];
            var bufferPos = 0;
            var anySolo = _isAnySolo;

            // process in micro-buffers
            for (var x = 0; x < MicroBufferCount; x++)
            {
                // process events for first microbuffer
                if (_midiEventQueue.Length > 0)
                {
                    for (var i = 0; i < _midiEventCounts[x]; i++)
                    {
                        var m = _midiEventQueue.RemoveLast();
                        if (m == null)
                            continue;
                        ProcessMidiMessage(m.Event);
                    }
                }

                // voice processing loop
                foreach (var voice in _voices)
                {
                    if (voice.PlayingPreset != -1)
                    {
                        var channel = voice.PlayingChannel;
                        // channel is muted if it is either explicitley muted, or another channel is set to solo but not this one.
                        var isChannelMuted = _mutedChannels.ContainsKey(channel) ||
                                             anySolo && !_soloChannels.ContainsKey(channel);

                        if (silent)
                        {
                            voice.Kill();
                        }
                        else
                        {
                            voice.Render(this, buffer, bufferPos, MicroBufferSize, isChannelMuted);
                        }
                    }
                }

                bufferPos += MicroBufferSize * SynthConstants.AudioChannels;
            }

            Platform.ClearIntArray(_midiEventCounts);
            return buffer;
        }

        private void ProcessMidiMessage(MidiEvent e)
        {
            Logger.Debug("Midi", "Processing midi " + e.Command);
            var command = e.Command;
            var channel = e.Channel;
            var data1 = e.Data1;
            var data2 = e.Data2;
            switch (command)
            {
                case MidiEventType.NoteOff:
                    ChannelNoteOff(channel, data1);
                    break;
                case MidiEventType.NoteOn:
                    ChannelNoteOn(channel, data1, data2 / 127f);
                    break;
                case MidiEventType.NoteAftertouch:
                    break;
                case MidiEventType.Controller:
                    ChannelMidiControl(channel, data1, data2);
                    break;
                case MidiEventType.ProgramChange:
                    ChannelSetPresetNumber(channel, data1, channel == 9);
                    break;
                case MidiEventType.ChannelAftertouch:
                    break;
                case MidiEventType.PitchBend:
                    ChannelSetPitchWheel(channel, (short)(data1 | (data2 << 8)));
                    break;
            }
        }

        /// <summary>
        /// Stop all playing notes immediatly and reset all channel parameters but keeps user
        /// defined settings
        /// </summary>
        public void ResetSoft()
        {
            _noteCounter = 0;
            foreach (var v in _voices)
            {
                if (v.PlayingPreset != -1 &&
                    (v.AmpEnv.Segment < VoiceEnvelopeSegment.Release || v.AmpEnv.Parameters.Release != 0))
                {
                    v.EndQuick(OutSampleRate);
                }
            }

            if (_channels != null)
            {
                foreach (var c in _channels.ChannelList)
                {
                    c.PresetIndex = c.Bank = 0;
                    c.PitchWheel = c.MidiPan = 8192;
                    c.MidiVolume = c.MidiExpression = 16383;
                    c.MidiRpn = 0xFFFF;
                    c.MidiData = 0;
                    c.PanOffset = 0.0f;
                    c.GainDb = 0.0f;
                    c.PitchRange = 2.0f;
                    c.Tuning = 0.0f;
                }
            }
        }
    }
}
