// our ideal update looks like this:
//   transport.process_incoming()
//   update_world()
//   transport.process_outgoing()
//
// this way we avoid unnecessary latency for low-ish server tick rates.
// for example, if we were to use this tick:
//   transport.process_incoming/outgoing()
//   update_world()
//
// then anything sent in update_world wouldn't be actually sent out by the
// transport until the next frame. if server runs at 60Hz, then this can add
// 16ms latency for every single packet.
//
// => instead we process incoming, update world, process_outgoing in the same
//    frame. it's more clear (no race conditions) and lower latency.
// => we need to add custom Update functions to the Unity engine:
//      NetworkEarlyUpdate before Update()/FixedUpdate()
//      NetworkLateUpdate after LateUpdate()
//    this way the user can update the world in Update/FixedUpdate/LateUpdate
//    and networking still runs before/after those functions no matter what!
// => see also: https://docs.unity3d.com/Manual/ExecutionOrder.html
// => update order:
//    * we add to the end of EarlyUpdate so it runs after any Unity initializations
//    * we add to the end of PreLateUpdate so it runs after LateUpdate(). adding
//      to the beginning of PostLateUpdate doesn't actually work.
using System;

using UnityEngine;

namespace Mirror
{
    public static class NetworkLoop
    {
        // callbacks for others to hook into if they need Early/LateUpdate.
        public static Action OnEarlyUpdate;
        public static Action OnLateUpdate;

        public static void NetworkEarlyUpdate()
        {
            // loop functions run in edit mode and in play mode.
            // however, we only want to call NetworkServer/Client in play mode.
            if (!Application.isPlaying) return;

            NetworkTime.EarlyUpdate();
            //Debug.Log($"NetworkEarlyUpdate {Time.time}");
            NetworkServer.NetworkEarlyUpdate();
            NetworkClient.NetworkEarlyUpdate();
            // invoke event after mirror has done it's early updating.
            OnEarlyUpdate?.Invoke();
        }

        public static void NetworkLateUpdate()
        {
            // loop functions run in edit mode and in play mode.
            // however, we only want to call NetworkServer/Client in play mode.
            if (!Application.isPlaying) return;

            //Debug.Log($"NetworkLateUpdate {Time.time}");
            // invoke event before mirror does its final late updating.
            OnLateUpdate?.Invoke();
            NetworkServer.NetworkLateUpdate();
            NetworkClient.NetworkLateUpdate();
        }
    }
}
