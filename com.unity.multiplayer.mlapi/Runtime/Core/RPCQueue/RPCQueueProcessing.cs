using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Profiling;
using MLAPI.Configuration;
using MLAPI.Messaging;
using MLAPI.Profiling;
using MLAPI.Serialization.Pooled;


namespace MLAPI
{
    /// <summary>
    /// RPCQueueProcessing
    /// Handles processing of RPCQueues
    /// Inbound to invocation
    /// Outbound to send
    /// </summary>
    internal class RPCQueueProcessing
    {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
        static ProfilerMarker s_MLAPIRPCQueueProcess = new ProfilerMarker("MLAPIRPCQueueProcess");
        static ProfilerMarker s_MLAPIRPCQueueSend = new ProfilerMarker("MLAPIRPCQueueSend");
#endif

        //NSS-TODO: Need to determine how we want to handle all other MLAPI send types
        //Temporary place to keep internal MLAPI messages
        private readonly List<FrameQueueItem> internalMLAPISendQueue = new List<FrameQueueItem>();

        /// <summary>
        /// ProcessReceiveQueue
        /// Public facing interface method to start processing all RPCs in the current inbound frame
        /// </summary>
        public static void ProcessReceiveQueue(NetworkUpdateManager.NetworkUpdateStages currentStage)
        {
            bool AdvanceFrameHistory = false;
            var rpcQueueManager = NetworkingManager.Singleton.RpcQueueManager;
            if (rpcQueueManager != null)
            {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                s_MLAPIRPCQueueProcess.Begin();
#endif
                var CurrentFrame = rpcQueueManager.GetCurrentFrame(QueueHistoryFrame.QueueFrameType.Inbound,currentStage);
                if (CurrentFrame != null)
                {
                    var currentQueueItem = CurrentFrame.GetFirstQueueItem();
                    while (currentQueueItem.QueueItemType != RPCQueueManager.QueueItemType.None)
                    {
                        AdvanceFrameHistory = true;
                        if (rpcQueueManager.IsLoopBack())
                        {
                            currentQueueItem.ItemStream.Position = 1;
                        }
                        if(rpcQueueManager.IsTesting())
                        {
                            Debug.Log("RPC invoked during the " + currentStage.ToString() + " update stage.");
                        }
                        NetworkingManager.InvokeRpc(currentQueueItem);
                        ProfilerStatManager.rpcsQueueProc.Record();
                        currentQueueItem = CurrentFrame.GetNextQueueItem();
                    }

                    //We call this to dispose of the shared stream writer and stream
                    CurrentFrame.CloseQueue();
                }

                if (AdvanceFrameHistory)
                {
                    rpcQueueManager.AdvanceFrameHistory(QueueHistoryFrame.QueueFrameType.Inbound);
                }
            }
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            s_MLAPIRPCQueueProcess.End();
#endif
        }

        /// <summary>
        /// ProcessSendQueue
        /// Called to send both performance RPC and internal messages and then flush the outbound queue
        /// </summary>
        public void ProcessSendQueue()
        {
            InternalMessagesSendAndFlush();

#if DEVELOPMENT_BUILD || UNITY_EDITOR
            s_MLAPIRPCQueueSend.Begin();
#endif
            RPCQueueSendAndFlush();

#if DEVELOPMENT_BUILD || UNITY_EDITOR
            s_MLAPIRPCQueueSend.End();
#endif

        }

        /// <summary>
        ///  QueueInternalMLAPICommand
        ///  Added this as an example of how to add internal messages to the outbound send queue
        /// </summary>
        /// <param name="queueItem">message queue item to add<</param>
        public void QueueInternalMLAPICommand(FrameQueueItem queueItem)
        {
            internalMLAPISendQueue.Add(queueItem);
        }

        /// <summary>
        /// Generic Sending Method for Internal Messages
        /// TODO: Will need to open this up for discussion, but we will want to determine if this is how we want internal MLAPI command
        /// messages to be sent.  We might want specific commands to occur during specific network update regions (see NetworkUpdate
        /// </summary>
        public void InternalMessagesSendAndFlush()
        {
            foreach (FrameQueueItem queueItem in internalMLAPISendQueue)
            {
                var PoolStream = queueItem.ItemStream;
                switch (queueItem.QueueItemType)
                {
                    case RPCQueueManager.QueueItemType.CreateObject:
                    {
                        foreach (ulong clientId in queueItem.ClientIds)
                        {
                            InternalMessageSender.Send(clientId, MLAPIConstants.MLAPI_ADD_OBJECT, queueItem.Channel, PoolStream, queueItem.SendFlags);
                        }

                        ProfilerStatManager.rpcsSent.Record(queueItem.ClientIds.Length);
                        break;
                    }
                    case RPCQueueManager.QueueItemType.DestroyObject:
                    {
                        foreach (ulong clientId in queueItem.ClientIds)
                        {
                            InternalMessageSender.Send(clientId, MLAPIConstants.MLAPI_DESTROY_OBJECT, queueItem.Channel, PoolStream, queueItem.SendFlags);
                        }

                        ProfilerStatManager.rpcsSent.Record(queueItem.ClientIds.Length);
                        break;
                    }
                }

                PoolStream.Dispose();
            }

            internalMLAPISendQueue.Clear();
        }

        /// <summary>
        /// RPCQueueSendAndFlush
        /// Sends all RPC queue items in the current outbound frame
        /// </summary>
        private void RPCQueueSendAndFlush()
        {
            bool AdvanceFrameHistory = false;
            var rpcQueueManager = NetworkingManager.Singleton.RpcQueueManager;
            if (rpcQueueManager != null)
            {
                var CurrentFrame = rpcQueueManager.GetCurrentFrame(QueueHistoryFrame.QueueFrameType.Outbound,NetworkUpdateManager.NetworkUpdateStages.LATEUPDATE);
                //If loopback is enabled
                if (rpcQueueManager.IsLoopBack())
                {
                    //Migrate the outbound buffer to the inbound buffer
                    rpcQueueManager.LoopbackSendFrame();
                    AdvanceFrameHistory = true;
                }
                else
                {
                    if (CurrentFrame != null)
                    {
                        var currentQueueItem = CurrentFrame.GetFirstQueueItem();
                        while (currentQueueItem.QueueItemType != RPCQueueManager.QueueItemType.None)
                        {
                            AdvanceFrameHistory = true;
                            SendFrameQueueItem(currentQueueItem);
                            currentQueueItem = CurrentFrame.GetNextQueueItem();
                        }
                    }
                }

                if (AdvanceFrameHistory)
                {
                    rpcQueueManager.AdvanceFrameHistory(QueueHistoryFrame.QueueFrameType.Outbound);
                }
            }
        }

        /// <summary>
        /// SendFrameQueueItem
        /// Sends the RPC Queue Item to the specified destination
        /// </summary>
        /// <param name="queueItem">Information on what to send</param>
        private void SendFrameQueueItem(FrameQueueItem queueItem)
        {
            switch (queueItem.QueueItemType)
            {
                case RPCQueueManager.QueueItemType.ServerRpc:
                {
                    NetworkingManager.Singleton.NetworkConfig.NetworkTransport.Send(queueItem.NetworkId, queueItem.MessageData,
                        string.IsNullOrEmpty(queueItem.Channel) ? "MLAPI_DEFAULT_MESSAGE" : queueItem.Channel);

                    //For each packet sent, we want to record how much data we have sent
                    ProfilerStatManager.bytesSent.Record((int)queueItem.StreamSize);
                    ProfilerStatManager.rpcsSent.Record();
                    break;
                }
                case RPCQueueManager.QueueItemType.ClientRpc:
                {
                    foreach (ulong clientid in queueItem.ClientIds)
                    {
                        NetworkingManager.Singleton.NetworkConfig.NetworkTransport.Send(clientid, queueItem.MessageData, string.IsNullOrEmpty(queueItem.Channel) ? "MLAPI_DEFAULT_MESSAGE" : queueItem.Channel);

                        //For each packet sent, we want to record how much data we have sent
                        ProfilerStatManager.bytesSent.Record((int)queueItem.StreamSize);
                    }

                    //For each client we send to, we want to record how many RPCs we have sent
                    ProfilerStatManager.rpcsSent.Record(queueItem.ClientIds.Length);

                    break;
                }
            }
        }
    }
}
