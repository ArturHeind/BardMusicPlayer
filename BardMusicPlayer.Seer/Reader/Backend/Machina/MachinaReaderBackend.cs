/*
 * Copyright(c) 2022 MoogleTroupe, GiR-Zippo
 * Licensed under the GPL v3 license. See https://github.com/BardMusicPlayer/BardMusicPlayer/blob/develop/LICENSE for full license information.
 */

using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using BardMusicPlayer.Seer.Events;
using BardMusicPlayer.Seer.Utilities;

namespace BardMusicPlayer.Seer.Reader.Backend.Machina
{
    internal class MachinaReaderBackend : IReaderBackend
    {
        public EventSource ReaderBackendType { get; }

        public ReaderHandler ReaderHandler { get; set; }

        public int SleepTimeInMs { get; set; }

        private ConcurrentQueue<byte[]> _messageQueue;
        private bool _messageQueueOpen;
        private Packet _packet;

        public MachinaReaderBackend(int sleepTimeInMs)
        {
            ReaderBackendType = EventSource.Machina;
            SleepTimeInMs     = sleepTimeInMs;
        }

        public async Task Loop(CancellationToken token)
        {
            _messageQueue     = new ConcurrentQueue<byte[]>();
            _messageQueueOpen = true;
            _packet           = new Packet(this);

            MachinaManager.Instance.MessageReceived += OnMessageReceived;
            MachinaManager.Instance.AddGame(ReaderHandler.Game.Pid);

            while (!token.IsCancellationRequested)
            {
                while (_messageQueue.TryDequeue(out var message))
                {
                    try
                    {
                        var otherActorId = BitConverter.ToUInt32(message, 4);
                        var myActorId = BitConverter.ToUInt32(message, 8);
                        long timeStamp = BitConverter.ToUInt32(message, 24);
                        timeStamp *= 1000;

                        //string hexString = BitConverter.ToString(message);
                        //System.Diagnostics.Debug.WriteLine(hexString + " " + message.Length.ToString());

                        if (!(ActorIdTools.RangeOkay(myActorId) && ActorIdTools.RangeOkay(otherActorId))) continue;

                        if (myActorId == otherActorId)
                            ReaderHandler.Game.PublishEvent(new ActorIdChanged(EventSource.Machina, myActorId));

                        UInt16 Opcode = BitConverter.ToUInt16(message, 18); //implement if needed
                        switch (message.Length)
                        {
                            case 48:
                                _packet.Size48(timeStamp, otherActorId, myActorId, message);        //Handles Ensemble Stop
                                break;
                            case 56:
                                _packet.Size56(timeStamp, otherActorId, myActorId, message);        //Handles Ensemble Request, Ensemble Reject, and Instrument Equip/De-Equip.
                                break;
                            case 88:
                                _packet.Size88(timeStamp, otherActorId, myActorId, message);        //Handles EnsembleStart
                                break;
                            case 656:
                                _packet.Size656(timeStamp, otherActorId, myActorId, message);
                                break;
                            case 664:
                                _packet.Size664(timeStamp, otherActorId, myActorId, message);
                                break;
                            case 928:
                                _packet.Size928(timeStamp, otherActorId, myActorId, message);
                                break;
                            case 3576:
                                _packet.Size3576(timeStamp, otherActorId, myActorId, message);
                                break;
                            default:
                                ReaderHandler.Game.PublishEvent(new BackendExceptionEvent(EventSource.Machina,
                                    new BmpSeerMachinaException("Unknown packet size: " + message.Length)));
                                break;
                        }
                        break;
                    }
                    catch (Exception ex)
                    {
                        ReaderHandler.Game.PublishEvent(new BackendExceptionEvent(EventSource.Machina, ex));
                    }
                }
                await Task.Delay(SleepTimeInMs, token);
            }
        }

        private void OnMessageReceived(int processId, byte[] message)
        {
            if (!_messageQueueOpen || ReaderHandler.Game.Pid != processId) return;
            _messageQueue.Enqueue(message);
        }

        public void Dispose()
        {
            _messageQueueOpen                        =  false;
            MachinaManager.Instance.MessageReceived -= OnMessageReceived;
            MachinaManager.Instance.RemoveGame(ReaderHandler.Game.Pid);
            while (_messageQueue.TryDequeue(out _))
            {
            }

            _packet?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}