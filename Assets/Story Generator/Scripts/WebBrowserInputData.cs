using Unity.RenderStreaming;
using Unity.WebRTC;
using UnityEngine;

public class WebBrowserInputData : WebBrowserInputChannelReceiver {
    RTCDataChannel channel;
    
    public override void SetChannel(string connectionId, RTCDataChannel channel)
    {
        this.channel = channel;
        base.SetChannel(connectionId, channel);
    }
    public void SendData(string msg){
        channel.Send(msg);
    }

    public override void OnButtonClick(int elementId)
    {
       Debug.Log(elementId);
    }
}