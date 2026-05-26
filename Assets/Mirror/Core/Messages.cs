using System;
using UnityEngine;

namespace Mirror
{
    // need to send time every sendInterval.
    // batching automatically includes remoteTimestamp.
    // all we need to do is ensure that an empty message is sent.
    // and react to it.
    // => we don't want to insert a snapshot on every batch.
    // => do it exactly every sendInterval on every TimeSnapshotMessage.
    public struct TimeSnapshotMessage : NetworkMessage {}

    public struct ReadyMessage : NetworkMessage {}

    public struct NotReadyMessage : NetworkMessage {}

    public struct AddPlayerMessage : NetworkMessage {}

    // whoever wants to measure rtt, sends this to the other end.
    public struct NetworkPingMessage : NetworkMessage
    {
        // local time is used to calculate round trip time,
        // and to calculate the predicted time offset.
        public double localTime;

        // predicted time is sent to compare the final error, for debugging only
        public double predictedTimeAdjusted;

        public NetworkPingMessage(double localTime, double predictedTimeAdjusted)
        {
            this.localTime = localTime;
            this.predictedTimeAdjusted = predictedTimeAdjusted;
        }
    }

    // the other end responds with this message.
    // we can use this to calculate rtt.
    public struct NetworkPongMessage : NetworkMessage
    {
        // local time is used to calculate round trip time.
        public double localTime;

        // predicted error is used to adjust the predicted timeline.
        public double predictionErrorUnadjusted;
        public double predictionErrorAdjusted; // for debug purposes

        public NetworkPongMessage(double localTime, double predictionErrorUnadjusted, double predictionErrorAdjusted)
        {
            this.localTime = localTime;
            this.predictionErrorUnadjusted = predictionErrorUnadjusted;
            this.predictionErrorAdjusted = predictionErrorAdjusted;
        }
    }
}
