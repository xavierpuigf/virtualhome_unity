using Unity.RenderStreaming;
using Unity.WebRTC;
using UnityEngine;
using StoryGenerator;
using System.Collections.Generic;

public class WebBrowserInputData : WebBrowserInputChannelReceiver {
    RTCDataChannel channel;
    private TestDriver td;
    private int char_id;
    public override void SetChannel(string connectionId, RTCDataChannel channel)
    {
        this.channel = channel;
        base.SetChannel(connectionId, channel);
    }
    public void SendData(string msg){
        channel.Send(msg);
    }
    public void SetDriver(TestDriver td, int char_id)
    {
        this.td = td;
        this.char_id = char_id;
    }
    public override void OnButtonClick(int elementId)
    {
        Debug.Log("button click");
        string action = td.action_button[char_id][elementId];
        
        td.scriptLines[char_id] = action;
        StartCoroutine(td.ExecuteScript(char_id));
    }
}