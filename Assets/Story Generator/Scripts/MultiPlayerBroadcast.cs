using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Unity.RenderStreaming
{
    public class MultiPlayerBroadcast : SignalingHandlerBase,
        IOfferHandler, IAddChannelHandler, IDisconnectHandler, IDeletedConnectionHandler
    {
        [SerializeField]
        private List<Component> streams = new List<Component>();

        //private List<string> connectionIds = new List<string>();

        private Dictionary<string, List<Component> > connectionIds = new Dictionary<string, List<Component>>();


        public void AddComponent(Component component)
        {
            streams.Add(component);
        }

        public void RemoveComponent(Component component)
        {
            streams.Remove(component);
        }

        public void OnDeletedConnection(SignalingEventData eventData)
        {
            Disconnect(eventData.connectionId);
        }

        public void OnDisconnect(SignalingEventData eventData)
        {
            Disconnect(eventData.connectionId);
        }

        private void Disconnect(string connectionId)
        {
            if (!connectionIds.ContainsKey(connectionId))
                return;

            foreach (var source in connectionIds[connectionId].OfType<IStreamSource>())
            {
                source.SetSender(connectionId, null);
            }
            foreach (var receiver in connectionIds[connectionId].OfType<IStreamReceiver>())
            {
                receiver.SetReceiver(connectionId, null);
            }
            foreach (var channel in connectionIds[connectionId].OfType< IDataChannel>())
            {
                channel.SetChannel(connectionId, null);
            }

            connectionIds.Remove(connectionId);
        }

        public void OnOffer(SignalingEventData data)
        {
            if (connectionIds.ContainsKey(data.connectionId))
            {
                Debug.Log($"Already answered this connectionId : {data.connectionId}");
                return;
            }

            if (connectionIds.Count >= streams.OfType<WebBrowserInputData>().Count())
                return;

            connectionIds.Add(data.connectionId, new List<Component>());

            if (streams.OfType<VideoStreamBase>().Count() >= connectionIds.Count)
            {
                VideoStreamBase vidSrc = streams.OfType<VideoStreamBase>().ElementAt(connectionIds.Count - 1);
                var transceiver = AddTrack(data.connectionId, vidSrc.Track);
                vidSrc.SetSender(data.connectionId, transceiver.Sender);
                connectionIds[data.connectionId].Add(vidSrc);

            }



            //foreach (var source in streams.OfType<IStreamSource>())
            //{
            //    var transceiver = AddTrack(data.connectionId, source.Track);
            //    source.SetSender(data.connectionId, transceiver.Sender);
            //}
            foreach (var channel in streams.OfType<DataChannelBase>().Where(c => c.IsLocal))
            {
                var _channel = CreateChannel(data.connectionId, channel.Label);
                channel.SetChannel(data.connectionId, _channel);
                connectionIds[data.connectionId].Add(channel);
            }
            SendAnswer(data.connectionId);
        }

        public void OnAddChannel(SignalingEventData data)
        {
            var channel = streams.OfType<DataChannelBase>().
                FirstOrDefault(r => r.Channel == null && !r.IsLocal);
            channel?.SetChannel(data.connectionId, data.channel);
            if (channel != null)
            {
                connectionIds[data.connectionId].Add(channel);

            }
        }
    }
}
