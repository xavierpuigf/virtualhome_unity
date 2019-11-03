using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using UnityEngine;
using WebSocketSharp;
using WebSocketSharp.Net;
using WebSocketSharp.Server;

namespace StoryGenerator.Communication
{
    class CommunicationServer 
    {
        private WebSocketServer wsServer;
        private WebSocketControl wsControl;
        private TestDriver driver;

        public TestDriver Driver
        {
            get { return driver; }
            set {
                driver = value;
                if (wsControl != null)
                    wsControl.Driver = driver;
            }
        }

        public CommunicationServer(int port)
        {
            wsServer = new WebSocketServer(System.Net.IPAddress.Parse("127.0.0.1"), port);
            wsServer.AddWebSocketService("/", () => ObtainNewWebSocketControl());
            wsServer.Start();
        }

        private WebSocketControl ObtainNewWebSocketControl()
        {
            if (wsControl != null)
                wsControl.Driver = null;
            wsControl = new WebSocketControl() { Driver = driver };
            return wsControl;
        }

        public void Broadcast(string message)
        {
            wsServer.WebSocketServices.Broadcast(message);
        }

        public void Stop()
        {
            wsServer.Stop();
        }

    }

    public class WebSocketControl : WebSocketBehavior
    {
        public TestDriver Driver { get; set; }

        protected override void OnMessage(MessageEventArgs msg)
        {
            Driver?.ProcessRequest(msg.Data);
        }
    }

    public class NetworkRequest
    {
        public string id;
        public string action;
        public IList<int> intParams;
        public IList<string> stringParams;

    }

    public class NetworkResponse
    {
        public string id;
        public bool success;
        public string message;
        public int value;
        public IList<string> message_list;

        public NetworkResponse()
        {
        }

        public NetworkResponse(string id, bool success, string message)
        {
            this.id = id;
            this.success = success;
            this.message = message;
        }
    }

    public class HttpCommunicationServer
    {
        private HttpServer httpServer;
        private object networkResponse;

        private static ManualResetEvent processingNotBlocked = new ManualResetEvent(false);

        public TestDriver Driver { get; internal set; }
        public int Timeout { get; internal set; }   // Timeout in milliseconds

        public HttpCommunicationServer(int port)
        {
            httpServer = new HttpServer(port);
            httpServer.OnPost += PostEventHandler;
            httpServer.Start();
        }

        internal void Stop()
        {
            httpServer.Stop();
        }

        internal void Broadcast(string v)
        {
            throw new NotImplementedException();
        }

        public void PostEventHandler(object sender, HttpRequestEventArgs eventArgs)
        {
            string reqBody = GetRequestBody(eventArgs.Request);
            NetworkRequest networkRequest = null;
            int respStatus; // Not all statuses are covered by HttpStatusCode enum, so we use int
            object respBody;

            if (string.IsNullOrEmpty(reqBody)) {
                respBody = new NetworkResponse("", false, "Request body is null or empty");
                respStatus = (int)HttpStatusCode.BadRequest;
            } else {
                try {
                    Debug.Log(string.Format("Received request: {0}", reqBody));
                    networkRequest = JsonConvert.DeserializeObject<NetworkRequest>(reqBody);

                    if (Driver == null) {
                        respBody = new NetworkResponse(networkRequest.id, false, string.Format("Driver is null"));
                        respStatus = (int)HttpStatusCode.InternalServerError;
                    } else {
                        ProcessRequestAndWait(networkRequest, out respStatus, out respBody);
                    }
                } catch (JsonException e) {
                    respBody = new NetworkResponse("", false, string.Format("Invalid request format ({0})", e.Message));
                    respStatus = (int)HttpStatusCode.BadRequest;
                } catch (Exception e) {
                    respBody = new NetworkResponse(networkRequest == null ? "" : networkRequest.id, false, string.Format("Unknown error ({0})", e.Message));
                    respStatus = (int)HttpStatusCode.InternalServerError;
                }
            }
            CreateResponse(eventArgs.Response, respStatus, respBody);
        }

        internal void UnlockProcessing(object response = null)
        {
            networkResponse = response;
            processingNotBlocked.Set();
        }

        public void ProcessRequestAndWait(NetworkRequest req, out int respStatus, out object respBody)
        {
            if (!processingNotBlocked.WaitOne()) {
                respBody = new NetworkResponse(req.id, false, "Busy");
                respStatus = 429; // TOO MANY REQUESTS;
            } else {
                networkResponse = null;
                processingNotBlocked.Reset();
                Driver.SetRequest(req);
                processingNotBlocked.WaitOne(Timeout);
                if (networkResponse != null) {
                    respBody = networkResponse;
                    respStatus = (int)HttpStatusCode.OK;
                } else {
                    respBody = new NetworkResponse(req.id, false, "Timeout");
                    respStatus = (int)HttpStatusCode.RequestTimeout;
                }
            }
        }

        private void CreateResponse(HttpListenerResponse res, int statusCode, object bodyObject)
        {
            string body = "";

            try {
                body = JsonConvert.SerializeObject(bodyObject);
            } catch (JsonSerializationException e) {
                // Should never happen...
                Debug.Log(e.StackTrace);
                body = JsonConvert.SerializeObject(new NetworkResponse("", false, string.Format("Error serializing response: ({0})", e.Message)));
                statusCode = (int)HttpStatusCode.InternalServerError;
            }
            res.StatusCode = statusCode;
            res.ContentType = "application/json";
            res.ContentEncoding = Encoding.UTF8;
            res.WriteContent(Encoding.UTF8.GetBytes(body));
        }


        private string GetRequestBody(HttpListenerRequest req)
        {
            using (StreamReader reader = new StreamReader(req.InputStream)) {
                return reader.ReadToEnd();
            }
        }
    }



}
